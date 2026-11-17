using System.Text.Json;

namespace CslaStarter.Architecture.Tests;

/// <summary>
/// Checks of the template repository itself. They read files that apps made from the template don't have
/// (.template.config, template/), so the template leaves this file out.
/// </summary>
public class TemplateRepositoryTests
{
    // Everything kept off main must also be left out of apps made with --claude false.
    [Fact]
    public void ClaudeOnlyPaths_AreExcludedByTheTemplate()
    {
        using var template = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, ".template.config", "template.json")));
        var excluded = template.RootElement.GetProperty("sources")[0].GetProperty("modifiers").EnumerateArray()
            .Where(m => m.TryGetProperty("exclude", out _))
            .SelectMany(m => m.GetProperty("exclude").EnumerateArray().Select(e => e.GetString()!))
            .ToHashSet(StringComparer.Ordinal);
        var missing = File.ReadLines(Path.Combine(RepoPaths.Root, ".github", "claude-only-paths.txt"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(pattern => pattern.EndsWith('/') ? pattern + "**" : pattern)
            .Where(pattern => !excluded.Contains(pattern))
            .ToList();

        Assert.True(missing.Count == 0, "Add to the template's excludes: " + string.Join(", ", missing));
    }

    [Fact]
    public void Release_AlsoAttachesTheTemplatePackage()
    {
        var release = File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "workflows", "release.yml"));

        Assert.Contains("dotnet pack template/CslaStarter.Templates.csproj", release, StringComparison.Ordinal);
    }
}
