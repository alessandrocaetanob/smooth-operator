using System.Reflection;
using SmoothOperator.Domain.Enums;
using SmoothOperator.Domain.Models;
using SmoothOperator.Infrastructure.Services;
using Xunit;

namespace SmoothOperator.Api.Tests.Services;

/// <summary>
/// Pure-decision tests for the vault-default + per-connection-override precedence rules.
/// Invokes the private static <c>ResolveRecordingEffectiveFlags</c> via reflection so we
/// don't need to stand up guacd or a DbContext to assert the truth table.
/// </summary>
public class RecordingEffectiveFlagsTests
{
    private static (bool Enabled, bool IncludeKeys) Resolve(Connection c)
    {
        var m = typeof(GuacamoleProxyService).GetMethod(
            "ResolveRecordingEffectiveFlags",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var t = ((bool, bool))m.Invoke(null, new object[] { c })!;
        return (t.Item1, t.Item2);
    }

    private static Connection With(bool? vaultRecording, bool? vaultIncludeKeys,
        RecordingOverride connOverride, bool? connIncludeKeys)
    {
        ConnectionGroup? vault = vaultRecording == null && vaultIncludeKeys == null
            ? null
            : new ConnectionGroup
            {
                RecordingEnabled = vaultRecording ?? false,
                RecordingIncludeKeys = vaultIncludeKeys ?? false,
            };
        return new Connection
        {
            ConnectionGroup = vault,
            RecordingOverride = connOverride,
            RecordingIncludeKeys = connIncludeKeys,
        };
    }

    [Fact]
    public void NoVault_NoOverride_DefaultsOff()
    {
        var (enabled, includeKeys) = Resolve(With(null, null, RecordingOverride.Inherit, null));
        Assert.False(enabled);
        Assert.False(includeKeys);
    }

    [Fact]
    public void VaultOn_InheritOverride_Records()
    {
        var (enabled, _) = Resolve(With(true, false, RecordingOverride.Inherit, null));
        Assert.True(enabled);
    }

    [Fact]
    public void VaultOff_ForceOnOverride_Records()
    {
        var (enabled, _) = Resolve(With(false, false, RecordingOverride.ForceOn, null));
        Assert.True(enabled);
    }

    [Fact]
    public void VaultOn_ForceOffOverride_DoesNotRecord()
    {
        var (enabled, _) = Resolve(With(true, true, RecordingOverride.ForceOff, null));
        Assert.False(enabled);
    }

    [Fact]
    public void ConnectionIncludeKeys_OverridesVault()
    {
        var (_, includeKeys) = Resolve(With(true, false, RecordingOverride.Inherit, true));
        Assert.True(includeKeys);
    }

    [Fact]
    public void VaultIncludeKeys_AppliesWhenConnectionIsNull()
    {
        var (_, includeKeys) = Resolve(With(true, true, RecordingOverride.Inherit, null));
        Assert.True(includeKeys);
    }
}
