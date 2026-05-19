using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Application.Tests.Options;

public sealed class GuacdOptionsValidationTests
{
    private static List<ValidationResult> Validate(GuacdOptions opts)
    {
        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(opts, ctx, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void DefaultValues_PassValidation()
    {
        var opts = new GuacdOptions();

        var results = Validate(opts);

        Assert.Empty(results);
    }

    [Fact]
    public void ValidPort_PassesValidation()
    {
        var opts = new GuacdOptions { Host = "guacd", Port = 4822 };

        var results = Validate(opts);

        Assert.Empty(results);
    }

    [Fact]
    public void PortZero_FailsRangeValidation()
    {
        var opts = new GuacdOptions { Host = "guacd", Port = 0 };

        var results = Validate(opts);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(GuacdOptions.Port)));
    }

    [Fact]
    public void PortAboveMax_FailsRangeValidation()
    {
        var opts = new GuacdOptions { Host = "guacd", Port = 65536 };

        var results = Validate(opts);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(GuacdOptions.Port)));
    }

    [Fact]
    public void EmptyHost_FailsRequired()
    {
        var opts = new GuacdOptions { Host = string.Empty, Port = 4822 };

        var results = Validate(opts);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(GuacdOptions.Host)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(4822)]
    [InlineData(65535)]
    public void BoundaryPorts_PassValidation(int port)
    {
        var opts = new GuacdOptions { Host = "guacd", Port = port };

        var results = Validate(opts);

        Assert.Empty(results);
    }
}
