using System;
using System.Security.Cryptography;
using System.Text;
using SmoothOperator.Application.Features.Webhooks;

namespace SmoothOperator.Application.Tests.Features.Webhooks;

public sealed class WebhookSignatureTests
{
    private const string Secret = "whsec_testsecret";
    private const string Timestamp = "2026-05-22T20:00:00.0000000Z";
    private const string Body = "{\"event\":\"webhook.ping\"}";

    [Fact]
    public void Compute_MatchesIndependentHmacOf_TimestampDotBody()
    {
        var expectedHash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret),
            Encoding.UTF8.GetBytes($"{Timestamp}.{Body}"));
        var expected = "sha256=" + Convert.ToHexStringLower(expectedHash);

        Assert.Equal(expected, WebhookSignature.Compute(Secret, Timestamp, Body));
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        Assert.Equal(
            WebhookSignature.Compute(Secret, Timestamp, Body),
            WebhookSignature.Compute(Secret, Timestamp, Body));
    }

    [Fact]
    public void Compute_DiffersWhenSecretDiffers()
    {
        Assert.NotEqual(
            WebhookSignature.Compute(Secret, Timestamp, Body),
            WebhookSignature.Compute("whsec_other", Timestamp, Body));
    }

    [Fact]
    public void Compute_BindsTimestamp_SoReplayWithNewTimestampFails()
    {
        Assert.NotEqual(
            WebhookSignature.Compute(Secret, Timestamp, Body),
            WebhookSignature.Compute(Secret, "2026-05-22T20:05:00.0000000Z", Body));
    }

    [Fact]
    public void Compute_ReturnsSha256PrefixedLowercaseHex()
    {
        var signature = WebhookSignature.Compute(Secret, Timestamp, Body);

        Assert.StartsWith("sha256=", signature);
        var hex = signature["sha256=".Length..];
        Assert.Equal(64, hex.Length);
        Assert.Equal(hex.ToLowerInvariant(), hex);
    }
}
