using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Tests.Texts;

public class LabelAttributeTests
{
    [Fact]
    public void Label_FollowsTheCulture()
    {
        var label = new LabelAttribute("Display name");
        using (Culture.Use("en-CA")) Assert.Equal("Display name", label.DisplayName);
        using (Culture.Use("fr-CA")) Assert.Equal("fr-Display name", label.DisplayName);
    }
}
