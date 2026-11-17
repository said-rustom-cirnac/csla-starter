using System.ComponentModel.DataAnnotations;
using CslaStarter.Domain.Platform.Rules;
using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Tests.Rules;

// Outcomes of [MaxLengthOf(200)], one theory row each (the row gives the input's length; null is no string):
// | Input          | Outcome | Message code               |
// |----------------|---------|----------------------------|
// | 200 characters | valid   | none                       |
// | 201 characters | invalid | ErrorMessageType.MaxLength |
// | null           | valid   | none                       |
// | ""             | valid   | none                       |
public class MaxLengthOfAttributeTests
{
    [Theory]
    [InlineData(200, null)]
    [InlineData(201, ErrorMessageType.MaxLength)]
    [InlineData(null, null)]
    [InlineData(0, null)]
    public void Outcome(int? length, int? messageCode)
    {
        using var _ = Culture.Use("en-CA");
        var input = length is { } count ? new string('a', count) : null;
        var attribute = new MaxLengthOfAttribute(200);

        Assert.Equal(messageCode is null, attribute.IsValid(input));
        var expected = messageCode is { } code ? Text.Error(code, "Display name", 200) : null;
        Assert.Equal(expected, attribute.GetValidationResult(input, DisplayName())?.ErrorMessage);
    }

    [Fact]
    public void Message_NamesTheLimit()
    { using var _ = Culture.Use("en-CA"); Assert.Equal("Display name must be 200 characters or fewer.", new MaxLengthOfAttribute(200).FormatErrorMessage("Display name")); }

    private static ValidationContext DisplayName() => new(new object()) { DisplayName = "Display name" };
}
