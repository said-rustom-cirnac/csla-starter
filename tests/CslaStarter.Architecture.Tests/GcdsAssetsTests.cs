using System.Text.RegularExpressions;

namespace CslaStarter.Architecture.Tests;

/// <summary>
/// The GC Design System is self-hosted at the version the spec pins. scripts/update-gcds.sh writes it and its output is
/// committed: the app, the template and the E2E tests all use the committed copy.
/// </summary>
public partial class GcdsAssetsTests
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
    public void Assets_MatchThePin()
    {
        Assert.Equal(PinnedVersion, File.ReadAllText(Path.Combine(Assets, "VERSION.txt")).Trim());
        Assert.True(File.Exists(Path.Combine(Assets, "loader", "index.js")), "the Stencil loader is missing");
        Assert.True(File.Exists(Path.Combine(Assets, "fonts.css")), "the self-hosted fonts are missing");
        Assert.DoesNotContain("fonts.googleapis.com", File.ReadAllText(Path.Combine(Assets, "dist", "gcds", "gcds.css")), StringComparison.Ordinal);
    }

    [Fact]
    public void Assets_LoadOnlyLocalFilesThatExist()
    {
        var broken = new List<string>();
        foreach (var css in Directory.EnumerateFiles(Assets, "*.css", SearchOption.AllDirectories))
        {
            foreach (Match match in CssUrl().Matches(File.ReadAllText(css)))
            {
                var url = match.Groups["url"].Value;
                if (url.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(css)!, url.Split('?', '#')[0]));
                if (url.Contains("//", StringComparison.Ordinal) || !File.Exists(path))
                {
                    broken.Add($"{Path.GetRelativePath(Assets, css)}: {url}");
                }
            }
        }

        Assert.True(broken.Count == 0, "These stylesheet urls don't load a local file:\n" + string.Join('\n', broken));
    }

    [Fact]
    public void Assets_ShipOnlyTheBuildsTheAppLoads()
    {
        string[] unused = ["cjs", "collection", "components", "types", "tests"];
        Assert.DoesNotContain(unused, dir => Directory.Exists(Path.Combine(Assets, "dist", dir)));
        Assert.Empty(Directory.EnumerateFiles(Assets, "*.d.ts", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(Assets, "dist", "esm", "loader.js")), "the ESM build the loader imports is missing");
    }

    [GeneratedRegex("""url\(\s*["']?(?<url>[^"')]+)["']?\s*\)""")]
    private static partial Regex CssUrl();
}
