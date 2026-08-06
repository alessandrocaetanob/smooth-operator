using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Infrastructure.Tests.Services
{
    public class AuditRetentionServiceTests
    {
        [Fact]
        public async Task PurgeOnceAsync_WhenRetentionIsZero_ShouldNotDeleteLogs()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var provider = BuildProvider(connection);

            await SeedAsync(provider, db =>
            {
                db.SystemSettings.Add(new SystemSettings { AuditLogRetentionDays = 0 });
                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Timestamp = DateTime.UtcNow.AddDays(-90),
                    Action = "connection.started",
                });
            });

            var logger = new TestLogger<AuditRetentionService>();
            var sut = new AuditRetentionService(provider.GetRequiredService<IServiceScopeFactory>(), logger);

            await InvokeNonPublicAsync(sut, "PurgeOnceAsync");

            await AssertDbAsync(provider, db =>
            {
                db.AuditLogs.Should().HaveCount(1);
                db.AuditLogs.Should().NotContain(a => a.Action == "system.audit_purged");
            });
        }

        [Fact]
        public async Task PurgeOnceAsync_WhenExpiredLogsExist_ShouldDeleteAndAudit()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var provider = BuildProvider(connection);

            await SeedAsync(provider, db =>
            {
                db.SystemSettings.Add(new SystemSettings { AuditLogRetentionDays = 30 });
                db.AuditLogs.AddRange(
                    new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        Timestamp = DateTime.UtcNow.AddDays(-45),
                        Action = "old",
                    },
                    new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        Timestamp = DateTime.UtcNow.AddDays(-5),
                        Action = "recent",
                    });
            });

            var logger = new TestLogger<AuditRetentionService>();
            var sut = new AuditRetentionService(provider.GetRequiredService<IServiceScopeFactory>(), logger);

            await InvokeNonPublicAsync(sut, "PurgeOnceAsync");

            await AssertDbAsync(provider, db =>
            {
                db.AuditLogs.Should().HaveCount(2);
                db.AuditLogs.Should().ContainSingle(a => a.Action == "recent");

                var purgeLog = db.AuditLogs.Should().ContainSingle(a => a.Action == "system.audit_purged").Subject;
                using var doc = JsonDocument.Parse(purgeLog.Details);
                doc.RootElement.GetProperty("deleted").GetInt32().Should().Be(1);
                doc.RootElement.GetProperty("retentionDays").GetInt32().Should().Be(30);
            });
        }

        [Fact]
        public async Task PurgeExpiredSsoStatesAsync_WhenExpiredStatesExist_ShouldDeleteAndLog()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var provider = BuildProvider(connection);

            await SeedAsync(provider, db =>
            {
                db.SsoAuthStates.AddRange(
                    new SsoAuthState
                    {
                        Id = Guid.NewGuid(),
                        State = "expired",
                        ReturnUrl = "/",
                        ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
                    },
                    new SsoAuthState
                    {
                        Id = Guid.NewGuid(),
                        State = "active",
                        ReturnUrl = "/",
                        ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    });
            });

            var logger = new TestLogger<AuditRetentionService>();
            var sut = new AuditRetentionService(provider.GetRequiredService<IServiceScopeFactory>(), logger);

            await InvokeNonPublicAsync(sut, "PurgeExpiredSsoStatesAsync");

            await AssertDbAsync(provider, db => db.SsoAuthStates.Should().ContainSingle(s => s.State == "active"));
            logger.Messages.Should().Contain(m => m.Contains("SSO auth-state sweep removed"));
        }

        [Fact]
        public async Task PurgeExpiredSsoStatesAsync_WhenNoExpiredStates_ShouldNotLogRemoval()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var provider = BuildProvider(connection);

            await SeedAsync(provider, db =>
            {
                db.SsoAuthStates.Add(new SsoAuthState
                {
                    Id = Guid.NewGuid(),
                    State = "active",
                    ReturnUrl = "/",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                });
            });

            var logger = new TestLogger<AuditRetentionService>();
            var sut = new AuditRetentionService(provider.GetRequiredService<IServiceScopeFactory>(), logger);

            await InvokeNonPublicAsync(sut, "PurgeExpiredSsoStatesAsync");

            await AssertDbAsync(provider, db => db.SsoAuthStates.Should().HaveCount(1));
            logger.Messages.Should().NotContain(m => m.Contains("SSO auth-state sweep removed"));
        }

        private static ServiceProvider BuildProvider(SqliteConnection connection)
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            return provider;
        }

        private static async Task SeedAsync(ServiceProvider provider, Action<AppDbContext> seed)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            seed(db);
            await db.SaveChangesAsync();
        }

        private static Task AssertDbAsync(ServiceProvider provider, Action<AppDbContext> assert)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            assert(db);
            return Task.CompletedTask;
        }

        private static async Task InvokeNonPublicAsync(AuditRetentionService sut, string methodName)
        {
            var method = typeof(AuditRetentionService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var task = method.Invoke(sut, new object[] { CancellationToken.None }) as Task;
            task.Should().NotBeNull();
            await task;
        }

        private sealed class TestLogger<T> : ILogger<T>
        {
            private readonly List<string> _messages = [];

            public IReadOnlyList<string> Messages => _messages;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }
}
