using System.Globalization;

namespace CslaStarter.Architecture.Tests;

/// <summary>The app is bilingual; a host without ICU would silently lose French formatting.</summary>
public class EnvironmentTests
{
    [Fact]
    public void Environment_SupportsCanadianCultures()
    {
        var fr = CultureInfo.GetCultureInfo("fr-CA");

        Assert.Equal(",", fr.NumberFormat.NumberDecimalSeparator);
        Assert.Contains("novembre", new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Unspecified).ToString("D", fr), StringComparison.Ordinal);
        Assert.Equal(".", CultureInfo.GetCultureInfo("en-CA").NumberFormat.NumberDecimalSeparator);
    }
}
