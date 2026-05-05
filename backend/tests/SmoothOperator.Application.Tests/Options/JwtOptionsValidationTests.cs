using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Application.Tests.Options;

public sealed class JwtOptionsValidationTests
{
    private static IList<ValidationResult> ValidateAnnotations(JwtOptions opts)
    {
        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(opts, ctx, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_LongKey_PassesValidation()
    {
        var opts = new JwtOptions
        {
            Key = "this-is-a-sufficiently-long-jwt-signing-key-for-hs256-at-least-32b"
        };

        var results = ValidateAnnotations(opts);
        var ivalidatableResults = new List<ValidationResult>(opts.Validate(new ValidationContext(opts)));

        Assert.Empty(results);
        Assert.Empty(ivalidatableResults);
    }

    [Fact]
    public void EmptyKey_FailsRequired()
    {
        var opts = new JwtOptions { Key = string.Empty };

        var results = ValidateAnnotations(opts);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(JwtOptions.Key)));
    }

    [Fact]
    public void TooShortKey_FailsIValidatable()
    {
        var opts = new JwtOptions { Key = "short" }; // < 32 bytes

        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>(opts.Validate(ctx));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(JwtOptions.Key)));
    }

    [Fact]
    public void ExactlyMinLength_PassesIValidatable()
    {
        // Exactly 32 ASCII bytes (32 * 1 byte per char = 32 bytes UTF-8).
        var opts = new JwtOptions { Key = new string('x', 32) };

        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>(opts.Validate(ctx));

        Assert.Empty(results);
    }
}
