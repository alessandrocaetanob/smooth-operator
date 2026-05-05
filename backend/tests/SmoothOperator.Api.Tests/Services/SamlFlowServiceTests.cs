using System.Security.Cryptography.X509Certificates;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using SmoothOperator.Infrastructure.Services.Sso;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

public class SamlFlowServiceTests
{
    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Mock certificate for testing
    private static string GetTestCert()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest("cn=test", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        return Convert.ToBase64String(cert.Export(X509ContentType.Cert));
    }

    private static (string cert, string key) GetTestCertWithKey()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest("cn=test-sp", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        
        var certPem = cert.ExportCertificatePem();
        var keyPem = rsa.ExportPkcs8PrivateKeyPem();
        
        return (certPem, keyPem);
    }

    [Fact]
    public async Task BuildAuthnRequestUrlAsync_GeneratesCorrectUrl()
    {
        await using var db = NewContext();
        var providers = new Mock<ISsoProviderService>();
        
        var (spCert, spKey) = GetTestCertWithKey();
        var idpCert = GetTestCert();

        providers.Setup(p => p.GetDecryptedSamlAsync()).ReturnsAsync(new SamlConfig
        {
            SpEntityId = "https://smooth-operator",
            IdpEntityId = "https://idp",
            IdpSsoUrl = "https://idp/sso",
            SpCertificate = spCert,
            SpPrivateKey = spKey,
            IdpCertificate = idpCert,
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"
        });

        var svc = new SamlFlowService(db, providers.Object);

        var url = await svc.BuildAuthnRequestUrlAsync("https://app.com/acs", "/dashboard");

        Assert.Contains("https://idp/sso", url);
        Assert.Contains("SAMLRequest=", url);
        Assert.Contains("RelayState=", url);

        var state = await db.SsoAuthStates.FirstOrDefaultAsync();
        Assert.NotNull(state);
        Assert.Equal("/dashboard", state.ReturnUrl);
    }

    [Fact]
    public async Task GetSpMetadataAsync_ReturnsValidXml()
    {
        await using var db = NewContext();
        var providers = new Mock<ISsoProviderService>();
        
        var (spCert, spKey) = GetTestCertWithKey();
        var idpCert = GetTestCert();

        providers.Setup(p => p.GetDecryptedSamlAsync()).ReturnsAsync(new SamlConfig
        {
            SpEntityId = "https://smooth-operator",
            IdpEntityId = "https://idp",
            IdpSsoUrl = "https://idp/sso",
            SpCertificate = spCert,
            SpPrivateKey = spKey,
            IdpCertificate = idpCert,
            NameIdFormat = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"
        });

        var svc = new SamlFlowService(db, providers.Object);

        var xml = await svc.GetSpMetadataAsync("https://app.com/acs", "https://app.com/metadata");

        Assert.Contains("EntityDescriptor", xml);
        Assert.Contains("entityID=\"https://smooth-operator\"", xml);
        Assert.Contains("AssertionConsumerService", xml);
    }
}
