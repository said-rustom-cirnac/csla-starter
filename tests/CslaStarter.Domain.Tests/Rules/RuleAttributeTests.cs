using CslaStarter.Domain.Platform.Rules;

namespace CslaStarter.Domain.Tests.Rules;

public class RuleAttributeTests
{
    [Fact]
    public void RequiredField_Message_ComesFromErrorMessageType()
    { using var _ = Culture.Use("en-CA"); Assert.Equal("Display name is required.", new RequiredFieldAttribute().FormatErrorMessage("Display name")); }

    [Fact]
    public void MaxLengthOf_Message_NamesTheLimit()
    { using var _ = Culture.Use("en-CA"); Assert.Equal("Display name must be 200 characters or fewer.", new MaxLengthOfAttribute(200).FormatErrorMessage("Display name")); }
}
