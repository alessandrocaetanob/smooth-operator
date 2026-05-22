using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Options;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Infrastructure.Tests.Services
{
    public class WebhookDeliveryProcessorTests
    {
        private static AppDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"webhook-processor-{Guid.NewGuid():N}")
                .Options;
            return new AppDbContext(options);
        }

        private static (
            WebhookDeliveryProcessor processor,
            FakeHandler handler,
            Mock<IEncryptionService> encryption)
            Build(
                Func<HttpRequestMessage, HttpResponseMessage>? responder = null,
                int maxAttempts = 3,
                int batchSize = 50,
                int retryBaseSeconds = 60,
                int maxConcurrency = 4)
        {
            var encryption = new Mock<IEncryptionService>();
            encryption.Setup(e => e.Decrypt(It.IsAny<string>()))
                .Returns<string>(s => "decrypted:" + s);

            var handler = new FakeHandler
            {
                Responder = (req, _) =>
                    Task.FromResult(responder?.Invoke(req) ?? new HttpResponseMessage(HttpStatusCode.OK)),
            };

            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(WebhookDeliveryProcessor.HttpClientName))
                .Returns(() => new HttpClient(handler, disposeHandler: false));

            var options = Options.Create(new WebhookOptions
            {
                PollIntervalSeconds = 10,
                BatchSize = batchSize,
                MaxAttempts = maxAttempts,
                RetryBaseSeconds = retryBaseSeconds,
                HttpTimeoutSeconds = 15,
                MaxConcurrency = maxConcurrency,
                DeliveryRetentionDays = 30,
            });

            var processor = new WebhookDeliveryProcessor(
                factory.Object, encryption.Object, options,
                NullLogger<WebhookDeliveryProcessor>.Instance);

            return (processor, handler, encryption);
        }

        private static WebhookEndpoint AddEndpoint(AppDbContext db, string url = "https://example.com/hook")
        {
            var endpoint = new WebhookEndpoint
            {
                Id = Guid.NewGuid(),
                Name = "ep-" + Guid.NewGuid().ToString("N")[..6],
                Url = url,
                EncryptedSecret = "enc-" + Guid.NewGuid(),
                EventTypes = "*",
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.WebhookEndpoints.Add(endpoint);
            return endpoint;
        }

        private static WebhookDelivery AddDueDelivery(
            AppDbContext db, WebhookEndpoint endpoint, string eventType = "user.login")
        {
            var delivery = new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookEndpointId = endpoint.Id,
                Endpoint = endpoint,
                EventType = eventType,
                Payload = "{\"event\":\"" + eventType + "\"}",
                Status = WebhookDeliveryStatus.Pending,
                NextAttemptAt = DateTime.UtcNow.AddSeconds(-1),
                CreatedAt = DateTime.UtcNow,
            };
            db.WebhookDeliveries.Add(delivery);
            return delivery;
        }

        [Fact]
        public async Task ProcessBatchAsync_EmptyQueue_ReturnsZero()
        {
            var (processor, _, _) = Build();
            using var db = NewDb();

            var processed = await processor.ProcessBatchAsync(db, CancellationToken.None);

            processed.Should().Be(0);
        }

        [Fact]
        public async Task ProcessBatchAsync_SuccessResponse_MarksDeliveredAndUpdatesEndpoint()
        {
            var (processor, handler, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            var delivery = AddDueDelivery(db, endpoint);
            await db.SaveChangesAsync();

            await processor.ProcessBatchAsync(db, CancellationToken.None);

            handler.CallCount.Should().Be(1);
            var refreshed = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
            refreshed.Status.Should().Be(WebhookDeliveryStatus.Delivered);
            refreshed.DeliveredAt.Should().NotBeNull();
            refreshed.LastResponseCode.Should().Be(202);
            refreshed.LastError.Should().BeNull();
            refreshed.AttemptCount.Should().Be(1);

            var refreshedEndpoint = await db.WebhookEndpoints.SingleAsync(w => w.Id == endpoint.Id);
            refreshedEndpoint.LastDeliveryStatus.Should().Be("success");
            refreshedEndpoint.LastResponseCode.Should().Be(202);
            refreshedEndpoint.ConsecutiveFailures.Should().Be(0);
        }

        [Fact]
        public async Task ProcessBatchAsync_NonSuccessResponse_SchedulesRetryAndIncrementsFailureCounts()
        {
            var (processor, _, _) = Build(
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { ReasonPhrase = "Server Error" },
                retryBaseSeconds: 60);
            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            var delivery = AddDueDelivery(db, endpoint);
            await db.SaveChangesAsync();

            var before = DateTime.UtcNow;
            await processor.ProcessBatchAsync(db, CancellationToken.None);

            var refreshed = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
            refreshed.Status.Should().Be(WebhookDeliveryStatus.Pending);
            refreshed.AttemptCount.Should().Be(1);
            refreshed.LastResponseCode.Should().Be(500);
            refreshed.LastError.Should().Contain("HTTP 500");
            refreshed.NextAttemptAt.Should().BeAfter(before.AddSeconds(50));

            var refreshedEndpoint = await db.WebhookEndpoints.SingleAsync(w => w.Id == endpoint.Id);
            refreshedEndpoint.LastDeliveryStatus.Should().Be("failed");
            refreshedEndpoint.ConsecutiveFailures.Should().Be(1);
        }

        [Fact]
        public async Task ProcessBatchAsync_OnFinalAttempt_MarksDead()
        {
            var (processor, _, _) = Build(
                _ => new HttpResponseMessage(HttpStatusCode.BadGateway), maxAttempts: 3);
            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            var delivery = AddDueDelivery(db, endpoint);
            delivery.AttemptCount = 2; // this run will be attempt #3 → Dead
            await db.SaveChangesAsync();

            await processor.ProcessBatchAsync(db, CancellationToken.None);

            var refreshed = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
            refreshed.Status.Should().Be(WebhookDeliveryStatus.Dead);
            refreshed.AttemptCount.Should().Be(3);
        }

        [Fact]
        public async Task ProcessBatchAsync_SecretDecryptionFailure_MarksFailedWithoutSending()
        {
            var encryption = new Mock<IEncryptionService>();
            encryption.Setup(e => e.Decrypt(It.IsAny<string>()))
                .Throws(new InvalidOperationException("bad key"));
            var handler = new FakeHandler();
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler, disposeHandler: false));
            var options = Options.Create(new WebhookOptions { MaxAttempts = 3, MaxConcurrency = 2 });
            var processor = new WebhookDeliveryProcessor(
                factory.Object, encryption.Object, options,
                NullLogger<WebhookDeliveryProcessor>.Instance);

            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            var delivery = AddDueDelivery(db, endpoint);
            await db.SaveChangesAsync();

            await processor.ProcessBatchAsync(db, CancellationToken.None);

            handler.CallCount.Should().Be(0);
            var refreshed = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
            refreshed.Status.Should().Be(WebhookDeliveryStatus.Pending); // not last attempt
            refreshed.LastError.Should().Contain("Secret decryption failed");
            refreshed.AttemptCount.Should().Be(1);
        }

        [Fact]
        public async Task ProcessBatchAsync_TransportException_MarksFailedWithExceptionMessage()
        {
            var handler = new FakeHandler
            {
                Responder = (_, _) => throw new HttpRequestException("connection refused"),
            };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(() => new HttpClient(handler, disposeHandler: false));
            var encryption = new Mock<IEncryptionService>();
            encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns("s");
            var processor = new WebhookDeliveryProcessor(
                factory.Object, encryption.Object,
                Options.Create(new WebhookOptions { MaxAttempts = 3, MaxConcurrency = 1 }),
                NullLogger<WebhookDeliveryProcessor>.Instance);

            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            var delivery = AddDueDelivery(db, endpoint);
            await db.SaveChangesAsync();

            await processor.ProcessBatchAsync(db, CancellationToken.None);

            var refreshed = await db.WebhookDeliveries.SingleAsync(d => d.Id == delivery.Id);
            refreshed.Status.Should().Be(WebhookDeliveryStatus.Pending);
            refreshed.LastResponseCode.Should().BeNull();
            refreshed.LastError.Should().Contain("connection refused");
        }

        [Fact]
        public async Task ProcessBatchAsync_DecryptsEndpointSecretOnceAcrossDeliveriesInTheBatch()
        {
            var (processor, _, encryption) = Build();
            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            AddDueDelivery(db, endpoint, "user.a");
            AddDueDelivery(db, endpoint, "user.b");
            AddDueDelivery(db, endpoint, "user.c");
            await db.SaveChangesAsync();

            await processor.ProcessBatchAsync(db, CancellationToken.None);

            encryption.Verify(e => e.Decrypt(endpoint.EncryptedSecret), Times.Once);
        }

        [Fact]
        public async Task ProcessBatchAsync_SendsSignedRequest_WithExpectedHeadersAndBody()
        {
            var (processor, handler, _) = Build();
            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            var delivery = AddDueDelivery(db, endpoint, "connection.started");
            await db.SaveChangesAsync();

            await processor.ProcessBatchAsync(db, CancellationToken.None);

            handler.Requests.Should().ContainSingle();
            var req = handler.Requests[0];
            req.Method.Should().Be(HttpMethod.Post);
            req.Uri.Should().Be(new Uri(endpoint.Url));
            req.Headers["X-SmoothOperator-Event"].Should().Be("connection.started");
            req.Headers["X-SmoothOperator-Delivery"].Should().Be(delivery.Id.ToString());
            req.Headers["X-SmoothOperator-Timestamp"].Should().NotBeNullOrWhiteSpace();
            var sig = req.Headers["X-SmoothOperator-Signature"];
            sig.Should().StartWith("sha256=");
            sig.Length.Should().Be("sha256=".Length + 64);
            req.Body.Should().Be(delivery.Payload);
        }

        [Fact]
        public async Task ProcessBatchAsync_SkipsDeliveriesNotYetDue()
        {
            var (processor, handler, _) = Build();
            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            var future = new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookEndpointId = endpoint.Id,
                Endpoint = endpoint,
                EventType = "later",
                Payload = "{}",
                Status = WebhookDeliveryStatus.Pending,
                NextAttemptAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
            };
            db.WebhookDeliveries.Add(future);
            await db.SaveChangesAsync();

            var processed = await processor.ProcessBatchAsync(db, CancellationToken.None);

            processed.Should().Be(0);
            handler.CallCount.Should().Be(0);
        }

        [Fact]
        public async Task ProcessBatchAsync_RespectsBatchSizeLimit()
        {
            var (processor, handler, _) = Build(batchSize: 2);
            using var db = NewDb();
            var endpoint = AddEndpoint(db);
            for (var i = 0; i < 5; i++)
                AddDueDelivery(db, endpoint, $"user.{i}");
            await db.SaveChangesAsync();

            var processed = await processor.ProcessBatchAsync(db, CancellationToken.None);

            processed.Should().Be(2);
            handler.CallCount.Should().Be(2);
        }

        [Fact]
        public async Task PurgeOldAsync_RemovesOldDeliveredAndDead_KeepsPendingAndRecent()
        {
            // ExecuteDeleteAsync is unsupported on the InMemory provider, so this
            // test uses an in-memory Sqlite connection (kept open by the test).
            var (processor, _, _) = Build();
            using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var endpoint = AddEndpoint(db);
            await db.SaveChangesAsync();

            var oldCutoff = DateTime.UtcNow.AddDays(-60);
            var recent = DateTime.UtcNow.AddDays(-1);

            var oldDelivered = new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookEndpointId = endpoint.Id,
                EventType = "x",
                Payload = "{}",
                Status = WebhookDeliveryStatus.Delivered,
                CreatedAt = oldCutoff,
                NextAttemptAt = oldCutoff,
            };
            var oldDead = new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookEndpointId = endpoint.Id,
                EventType = "x",
                Payload = "{}",
                Status = WebhookDeliveryStatus.Dead,
                CreatedAt = oldCutoff,
                NextAttemptAt = oldCutoff,
            };
            var oldPending = new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookEndpointId = endpoint.Id,
                EventType = "x",
                Payload = "{}",
                Status = WebhookDeliveryStatus.Pending,
                CreatedAt = oldCutoff,
                NextAttemptAt = DateTime.UtcNow,
            };
            var recentDelivered = new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookEndpointId = endpoint.Id,
                EventType = "x",
                Payload = "{}",
                Status = WebhookDeliveryStatus.Delivered,
                CreatedAt = recent,
                NextAttemptAt = recent,
            };
            db.WebhookDeliveries.AddRange(oldDelivered, oldDead, oldPending, recentDelivered);
            await db.SaveChangesAsync();

            var deleted = await processor.PurgeOldAsync(db, CancellationToken.None);

            deleted.Should().Be(2);
            var remaining = await db.WebhookDeliveries.Select(d => d.Id).ToListAsync();
            remaining.Should().BeEquivalentTo(new[] { oldPending.Id, recentDelivered.Id });
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Responder { get; set; }
                = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

            private int _callCount;
            public int CallCount => _callCount;

            private readonly object _gate = new();
            public List<CapturedRequest> Requests { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _callCount);
                // Snapshot what we need before the caller disposes the request.
                var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                var headers = request.Headers
                    .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
                lock (_gate)
                {
                    Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, headers, body));
                }
                return await Responder(request, cancellationToken);
            }
        }

        private sealed record CapturedRequest(
            HttpMethod Method,
            Uri Uri,
            IReadOnlyDictionary<string, string> Headers,
            string Body);
    }
}
