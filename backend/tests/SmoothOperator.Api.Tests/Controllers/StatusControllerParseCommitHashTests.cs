using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;
using SmoothOperator.Api.Controllers;

namespace SmoothOperator.Api.Tests.Controllers;

/// <summary>
/// Coverage for the static commit-hash parser used by /status.json. The runtime
/// supplies an <see cref="AssemblyInformationalVersionAttribute"/> whose value
/// looks like <c>1.0.0+&lt;sha&gt;</c> when SourceRevisionId is wired up. We
/// validate every defensive branch without touching the controller's HTTP path.
/// </summary>
public class StatusControllerParseCommitHashTests
{
    private static AssemblyBuilder AssemblyWithInformationalVersion(string? value)
    {
        // Build an in-memory assembly definition carrying the requested attribute.
        // System.Reflection.Emit is overkill here — we just create a dynamic assembly
        // and stamp the attribute via AssemblyBuilder.
        var asmName = new AssemblyName($"InformationalVersionTest-{Guid.NewGuid():N}");
        var asm = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
        if (value is not null)
        {
            var ctor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            asm.SetCustomAttribute(new CustomAttributeBuilder(ctor, [value]));
        }
        return asm;
    }

    [Fact]
    public void ReturnsUnknown_WhenAttributeMissing()
    {
        var sha = StatusController.ParseCommitHash(AssemblyWithInformationalVersion(null));
        Assert.Equal("unknown", sha);
    }

    [Fact]
    public void ReturnsUnknown_WhenValueHasNoPlus()
    {
        var sha = StatusController.ParseCommitHash(AssemblyWithInformationalVersion("1.0.0"));
        Assert.Equal("unknown", sha);
    }

    [Fact]
    public void ReturnsUnknown_WhenPlusIsAtEnd()
    {
        var sha = StatusController.ParseCommitHash(AssemblyWithInformationalVersion("1.0.0+"));
        Assert.Equal("unknown", sha);
    }

    [Fact]
    public void ReturnsUnknown_WhenShaHasNonHexCharacters()
    {
        var sha = StatusController.ParseCommitHash(AssemblyWithInformationalVersion("1.0.0+not-a-sha!"));
        Assert.Equal("unknown", sha);
    }

    [Fact]
    public void ReturnsSha_WhenValueIsWellFormed()
    {
        var hash = "a1b2c3d4e5f67890abcdef1234567890abcdef12";
        var sha = StatusController.ParseCommitHash(AssemblyWithInformationalVersion($"1.0.0+{hash}"));
        Assert.Equal(hash, sha);
    }

    [Fact]
    public void AcceptsUppercaseHex()
    {
        var hash = "ABCDEF1234567890";
        var sha = StatusController.ParseCommitHash(AssemblyWithInformationalVersion($"1.0.0+{hash}"));
        Assert.Equal(hash, sha);
    }
}
