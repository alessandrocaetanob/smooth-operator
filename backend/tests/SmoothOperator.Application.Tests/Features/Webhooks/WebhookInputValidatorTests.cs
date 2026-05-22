using FluentAssertions;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.Webhooks;

namespace SmoothOperator.Application.Tests.Features.Webhooks;

public sealed class WebhookInputValidatorTests
{
    private const string ValidUrl = "https://example.com/hook";

    [Fact]
    public void Normalize_TrimsInputs_AndKeepsValidValues()
    {
        var (name, url, eventTypes) = WebhookInputValidator.Normalize(
            "  siem  ", "  " + ValidUrl + "  ", "  user.* , connection.*  ");

        name.Should().Be("siem");
        url.Should().Be(ValidUrl);
        eventTypes.Should().Be("user.*,connection.*");
    }

    [Fact]
    public void Normalize_CollapsesStarToWildcard_WhenMixedWithCategories()
    {
        var (_, _, eventTypes) = WebhookInputValidator.Normalize("n", ValidUrl, "*,user.*,connection.*");
        eventTypes.Should().Be("*");
    }

    [Fact]
    public void Normalize_DedupesCategoriesCaseInsensitively()
    {
        var (_, _, eventTypes) = WebhookInputValidator.Normalize("n", ValidUrl, "user.*,USER.*,connection.*");
        eventTypes.Should().Be("user.*,connection.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyName_Throws(string? name)
    {
        var act = () => WebhookInputValidator.Normalize(name, ValidUrl, "*");
        act.Should().Throw<BadRequestException>().WithMessage("*name is required*");
    }

    [Fact]
    public void Normalize_NameLongerThan100Chars_Throws()
    {
        var longName = new string('n', 101);
        var act = () => WebhookInputValidator.Normalize(longName, ValidUrl, "*");
        act.Should().Throw<BadRequestException>().WithMessage("*100 characters*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/")]
    [InlineData("http://example.com/")] // http is not allowed; HTTPS required
    public void Normalize_NonHttpsUrl_Throws(string url)
    {
        var act = () => WebhookInputValidator.Normalize("n", url, "*");
        act.Should().Throw<BadRequestException>().WithMessage("*HTTPS*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    public void Normalize_EmptyEventTypes_Throws(string? eventTypes)
    {
        var act = () => WebhookInputValidator.Normalize("n", ValidUrl, eventTypes);
        act.Should().Throw<BadRequestException>().WithMessage("*at least one event type*");
    }
}
