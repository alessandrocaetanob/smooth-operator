using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SmoothOperator.Application.Options;

namespace SmoothOperator.Application.Tests.Options;

public sealed class EncryptionOptionsValidationTests
{
    private static IList<ValidationResult> Validate(EncryptionOptions opts)
    {
        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(opts, ctx, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_64CharHex_PassesValidation()
    {
        var opts = new EncryptionOptions
        {
            Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        };

        var results = Validate(opts);

        Assert.Empty(results);
    }

    [Fact]
    public void EmptyKey_FailsRequired()
    {
        var opts = new EncryptionOptions { Key = string.Empty };

        var results = Validate(opts);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EncryptionOptions.Key)));
    }

    [Fact]
    public void TooShortKey_FailsIValidatable()
    {
        var opts = new EncryptionOptions { Key = "abc123" }; // only 6 chars

        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>();
        // IValidatableObject is called by TryValidateObject only when DataAnnotations pass first
        // — for non-empty strings we need to call Validate() directly.
        results.AddRange(opts.Validate(ctx));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EncryptionOptions.Key)));
    }

    [Fact]
    public void NonHexKey_FailsIValidatable()
    {
        var opts = new EncryptionOptions
        {
            Key = "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ" // 64 chars but not hex
        };

        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>(opts.Validate(ctx));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(EncryptionOptions.Key)));
    }

    [Fact]
    public void UppercaseHexKey_PassesValidation()
    {
        var opts = new EncryptionOptions
        {
            Key = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"
        };

        var ctx = new ValidationContext(opts);
        var results = new List<ValidationResult>(opts.Validate(ctx));

        Assert.Empty(results);
    }
}
