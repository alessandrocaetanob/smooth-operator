using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SmoothOperator.Application.Options;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Application.Interfaces.Sso;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Sso;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SmoothOperator.Api.Tests.Services;

public class SsoConnectionTesterTests
{
    private const string TestKey = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EncryptionService NewEncryption() =>
        new EncryptionService(Options.Create(new EncryptionOptions { Key = TestKey }));

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static string CreateSelfSignedPem()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=test-sp", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var bytes = cert.Export(X509ContentType.Cert);
        return "-----BEGIN CERTIFICATE-----\n"
            + Convert.ToBase64String(bytes, Base64FormattingOptions.InsertLineBreaks)
            + "\n-----END CERTIFICATE-----";
    }

    [Fact]
    public async Task TestAsync_ReturnsFailure_WhenNoProviderConfigured()
    {
        await using var ctx = NewContext();
        var providers = new SsoProviderService(ctx, NewEncryption());
        var tester = new SsoConnectionTester(providers, new FakeHttpClientFactory());

        var result = await tester.TestAsync();

        Assert.False(result.Success);
        Assert.Contains("No SSO provider", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestAsync_Saml_Success_WithValidUrlAndCert()
    {
        await using var ctx = NewContext();
        var providers = new SsoProviderService(ctx, NewEncryption());
        await providers.UpsertSamlAsync("Corp SAML", new SamlConfig
        {
            SpEntityId = "sp",
            IdpEntityId = "idp",
            IdpSsoUrl = "https://idp.example.com/sso",
            IdpCertificate = CreateSelfSignedPem(),
            SpCertificate = CreateSelfSignedPem(),
            SpPrivateKey = "irrelevant"
        });

        var tester = new SsoConnectionTester(providers, new FakeHttpClientFactory());
        var result = await tester.TestAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Details);
    }

    [Fact]
    public async Task TestAsync_Saml_Fails_WhenUrlIsNotAbsolute()
    {
        await using var ctx = NewContext();
        var providers = new SsoProviderService(ctx, NewEncryption());
        await providers.UpsertSamlAsync("Corp SAML", new SamlConfig
        {
            SpEntityId = "sp",
            IdpEntityId = "idp",
            IdpSsoUrl = "/relative/url",
            IdpCertificate = CreateSelfSignedPem(),
            SpCertificate = CreateSelfSignedPem(),
            SpPrivateKey = "irrelevant"
        });

        var tester = new SsoConnectionTester(providers, new FakeHttpClientFactory());
        var result = await tester.TestAsync();

        Assert.False(result.Success);
        Assert.Contains("IdpSsoUrl", result.Message);
    }

    [Fact]
    public async Task TestAsync_Saml_Fails_WhenCertCannotBeParsed()
    {
        await using var ctx = NewContext();
        var providers = new SsoProviderService(ctx, NewEncryption());
        await providers.UpsertSamlAsync("Corp SAML", new SamlConfig
        {
            SpEntityId = "sp",
            IdpEntityId = "idp",
            IdpSsoUrl = "https://idp.example.com/sso",
            IdpCertificate = "not a real PEM",
            SpCertificate = CreateSelfSignedPem(),
            SpPrivateKey = "irrelevant"
        });

        var tester = new SsoConnectionTester(providers, new FakeHttpClientFactory());
        var result = await tester.TestAsync();

        Assert.False(result.Success);
        Assert.Contains("IdpCertificate", result.Message);
    }
}
