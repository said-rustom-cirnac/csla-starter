using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Tests.Texts;

public class RuleAttributeTests
{
    [Fact]
    public void Label_FollowsTheCulture()
    {
        var label = new LabelAttribute("Display name");
        using (Culture.Use("en-CA")) Assert.Equal("Display name", label.DisplayName);
        using (Culture.Use("fr-CA")) Assert.Equal("fr-Display name", label.DisplayName);
    }

    [Fact]
    public void RequiredField_Message_ComesFromErrorMessageType()
    { using var _ = Culture.Use("en-CA"); Assert.Equal("Display name is required.", new RequiredFieldAttribute().FormatErrorMessage("Display name")); }

    [Fact]
    public void MaxLengthOf_Message_NamesTheLimit()
    { using var _ = Culture.Use("en-CA"); Assert.Equal("Display name must be 200 characters or fewer.", new MaxLengthOfAttribute(200).FormatErrorMessage("Display name")); }
}
