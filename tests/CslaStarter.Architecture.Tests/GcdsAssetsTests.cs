namespace CslaStarter.Architecture.Tests;

/// <summary>The GC Design System is self-hosted at the version the spec pins, fetched by scripts/update-gcds.sh.</summary>
public class GcdsAssetsTests
{
    private const string PinnedVersion = "1.7.0";
    private const string FontsVersion = "1.1.1";

    private static string Script => Path.Combine(RepoPaths.Root, "scripts", "update-gcds.sh");

    private static string Assets => Path.Combine(RepoPaths.Src, "CslaStarter.Components", "wwwroot", "gcds");

    [Fact]
    public void UpdateScript_PinsTheSpecPackages_AndVerifiesIntegrity()
    {
        var script = File.ReadAllText(Script);

        Assert.Contains("\"@gcds-core/components\"", script, StringComparison.Ordinal);
        Assert.Contains($"COMPONENTS_VERSION=\"{PinnedVersion}\"", script, StringComparison.Ordinal);
        Assert.Contains("\"@gcds-core/fonts\"", script, StringComparison.Ordinal);
        Assert.Contains($"FONTS_VERSION=\"{FontsVersion}\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("@cdssnc", script, StringComparison.Ordinal);
        Assert.Contains("integrity", script, StringComparison.Ordinal);
    }

    [Fact]
    public void App_LinksTheSelfHostedFonts()
    {
        var app = File.ReadAllText(Path.Combine(RepoPaths.Src, "CslaStarter.Web", "Components", "App.razor"));

        Assert.Contains("_content/CslaStarter.Components/gcds/fonts.css", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Assets_WhenPresent_MatchThePin()
    {
        Assert.SkipUnless(Directory.Exists(Assets), "GCDS assets not fetched yet: run scripts/update-gcds.sh.");

        Assert.Equal(PinnedVersion, File.ReadAllText(Path.Combine(Assets, "VERSION")).Trim());
        Assert.True(File.Exists(Path.Combine(Assets, "loader", "index.js")), "the Stencil loader is missing");
        Assert.True(File.Exists(Path.Combine(Assets, "fonts.css")), "the self-hosted fonts are missing");
        Assert.DoesNotContain("fonts.googleapis.com", File.ReadAllText(Path.Combine(Assets, "dist", "gcds", "gcds.css")), StringComparison.Ordinal);
    }
}
