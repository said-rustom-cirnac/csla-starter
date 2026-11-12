using System.Globalization;
using CslaStarter.Core.Localization;

namespace CslaStarter.Business.Tests;

public class LocalizationTests
{
    [Theory]
    [InlineData("fr-CA", "Enregistrer")]
    [InlineData("en-CA", "Save")]
    public void LocalizedDisplayName_FollowsCurrentUICulture(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal(expected, new LocalizedDisplayNameAttribute("Common_Save").DisplayName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void LocalizedDisplayName_UnknownKey_ReturnsKey() =>
        Assert.Equal("No_Such_Key", new LocalizedDisplayNameAttribute("No_Such_Key").DisplayName);
}
