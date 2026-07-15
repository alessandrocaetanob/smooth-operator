using SmoothOperator.Application.Features.Connections;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using Xunit;

namespace SmoothOperator.Application.Tests.Features.Connections;

/// <summary>
/// Truth table for <see cref="FileTransferPolicyResolver.Resolve"/>: per-connection
/// override wins when set, otherwise the parent vault's default applies, and a
/// connection with no vault at all defaults to Disabled.
/// </summary>
public class FileTransferPolicyResolverTests
{
    private static Connection With(FileTransferPolicy? vaultPolicy, FileTransferPolicy? connectionOverride)
    {
        var vault = vaultPolicy == null ? null : new ConnectionGroup { FileTransferPolicy = vaultPolicy.Value };
        return new Connection { ConnectionGroup = vault, FileTransferPolicyOverride = connectionOverride };
    }

    [Fact]
    public void NoVault_NoOverride_DefaultsDisabled()
    {
        Assert.Equal(FileTransferPolicy.Disabled, FileTransferPolicyResolver.Resolve(With(null, null)));
    }

    [Fact]
    public void VaultDisabled_NoOverride_IsDisabled()
    {
        Assert.Equal(FileTransferPolicy.Disabled, FileTransferPolicyResolver.Resolve(With(FileTransferPolicy.Disabled, null)));
    }

    [Theory]
    [InlineData(FileTransferPolicy.DownloadOnly)]
    [InlineData(FileTransferPolicy.UploadOnly)]
    [InlineData(FileTransferPolicy.Both)]
    public void VaultEnabled_NoOverride_InheritsVaultPolicy(FileTransferPolicy vaultPolicy)
    {
        Assert.Equal(vaultPolicy, FileTransferPolicyResolver.Resolve(With(vaultPolicy, null)));
    }

    [Fact]
    public void VaultBoth_ConnectionOverrideDisabled_OverrideWins()
    {
        var resolved = FileTransferPolicyResolver.Resolve(With(FileTransferPolicy.Both, FileTransferPolicy.Disabled));
        Assert.Equal(FileTransferPolicy.Disabled, resolved);
    }

    [Fact]
    public void VaultDisabled_ConnectionOverrideBoth_OverrideWins()
    {
        var resolved = FileTransferPolicyResolver.Resolve(With(FileTransferPolicy.Disabled, FileTransferPolicy.Both));
        Assert.Equal(FileTransferPolicy.Both, resolved);
    }

    [Fact]
    public void NoVault_ConnectionOverrideUploadOnly_OverrideApplies()
    {
        var resolved = FileTransferPolicyResolver.Resolve(With(null, FileTransferPolicy.UploadOnly));
        Assert.Equal(FileTransferPolicy.UploadOnly, resolved);
    }
}
