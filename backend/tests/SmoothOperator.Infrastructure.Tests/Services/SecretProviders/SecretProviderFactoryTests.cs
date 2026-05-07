using System;
using FluentAssertions;
using Moq;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services.SecretProviders;

namespace SmoothOperator.Infrastructure.Tests.Services.SecretProviders
{
    public class SecretProviderFactoryTests
    {
        [Fact]
        public void Create_WhenProviderDisabled_ShouldThrowInvalidOperationException()
        {
            var encryption = new Mock<IEncryptionService>(MockBehavior.Strict);
            var sut = new SecretProviderFactory(encryption.Object);
            var provider = new SecretProvider
            {
                Name = "disabled-provider",
                IsEnabled = false,
                Type = SecretProviderType.AzureKeyVault,
            };

            var act = () => sut.Create(provider);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*disabled*");
            encryption.Verify(e => e.Decrypt(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Create_WhenTypeIsUnsupported_ShouldThrowNotSupportedException()
        {
            var encryption = new Mock<IEncryptionService>();
            encryption.Setup(e => e.Decrypt("encrypted")).Returns("{}");
            var sut = new SecretProviderFactory(encryption.Object);
            var provider = new SecretProvider
            {
                Name = "custom-provider",
                EncryptedConfigJson = "encrypted",
                IsEnabled = true,
                Type = (SecretProviderType)999,
            };

            var act = () => sut.Create(provider);

            act.Should().Throw<NotSupportedException>()
                .WithMessage("*not supported*");
            encryption.Verify(e => e.Decrypt("encrypted"), Times.Once);
        }

        [Fact]
        public void Create_WhenAzureKeyVaultConfigIsValid_ShouldReturnAzureKeyVaultProvider()
        {
            var encryption = new Mock<IEncryptionService>();
            encryption
                .Setup(e => e.Decrypt("encrypted"))
                .Returns("""
                    {"tenantId":"tenant","clientId":"client","clientSecret":"secret","vaultUri":"https://example.vault.azure.net/"}
                    """);

            var sut = new SecretProviderFactory(encryption.Object);
            var provider = new SecretProvider
            {
                Name = "akv",
                EncryptedConfigJson = "encrypted",
                IsEnabled = true,
                Type = SecretProviderType.AzureKeyVault,
            };

            var result = sut.Create(provider);

            result.Should().BeOfType<AzureKeyVaultSecretProvider>();
            encryption.Verify(e => e.Decrypt("encrypted"), Times.Once);
        }

        [Fact]
        public void Create_WhenAzureKeyVaultConfigIsInvalidJson_ShouldThrow()
        {
            var encryption = new Mock<IEncryptionService>();
            encryption.Setup(e => e.Decrypt("encrypted")).Returns("{invalid-json");
            var sut = new SecretProviderFactory(encryption.Object);
            var provider = new SecretProvider
            {
                Name = "akv",
                EncryptedConfigJson = "encrypted",
                IsEnabled = true,
                Type = SecretProviderType.AzureKeyVault,
            };

            var act = () => sut.Create(provider);

            act.Should().Throw<System.Text.Json.JsonException>();
            encryption.Verify(e => e.Decrypt("encrypted"), Times.Once);
        }
    }
}
