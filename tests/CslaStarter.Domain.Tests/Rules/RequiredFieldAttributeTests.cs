using System.ComponentModel.DataAnnotations;
using CslaStarter.Domain.Platform.Rules;
using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Tests.Rules;

// Outcomes of [RequiredField], one theory row each:
// | Input | Outcome | Message code              |
// |-------|---------|---------------------------|
// | "a"   | valid   | none                      |
// | ""    | invalid | ErrorMessageType.Required |
// | "   " | invalid | ErrorMessageType.Required |
// | null  | invalid | ErrorMessageType.Required |
public class RequiredFieldAttributeTests
{
    [Theory]
    [InlineData("a", null)]
    [InlineData("", ErrorMessageType.Required)]
    [InlineData("   ", ErrorMessageType.Required)]
    [InlineData(null, ErrorMessageType.Required)]
    public void Outcome(string? input, int? messageCode)
    {
        using var _ = Culture.Use("en-CA");
        var attribute = new RequiredFieldAttribute();

        Assert.Equal(messageCode is null, attribute.IsValid(input));
        var expected = messageCode is { } code ? Text.Error(code, "Display name") : null;
        Assert.Equal(expected, attribute.GetValidationResult(input, DisplayName())?.ErrorMessage);
    }

    [Fact]
    public void Message_ComesFromErrorMessageType()
    { using var _ = Culture.Use("en-CA"); Assert.Equal("Display name is required.", new RequiredFieldAttribute().FormatErrorMessage("Display name")); }

    private static ValidationContext DisplayName() => new(new object()) { DisplayName = "Display name" };
}
