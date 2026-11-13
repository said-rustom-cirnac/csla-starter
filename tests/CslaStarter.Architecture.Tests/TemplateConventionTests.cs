using System.Text.Json;

namespace CslaStarter.Architecture.Tests;

/// <summary>Rules that keep the starter safe to generate apps from.</summary>
public class TemplateConventionTests
{
    // Raw HTML from user data is an XSS hole; nothing in the starter needs MarkupString.
    [Fact]
    public void NoMarkupString_InSource()
    {
        var offenders = SourceFiles.CSharp(RepoPaths.Src).Concat(SourceFiles.Razor(RepoPaths.Src))
            .Where(file => File.ReadLines(file).Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && line.Contains("MarkupString", StringComparison.Ordinal)))
            .Select(RepoPaths.Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "MarkupString renders raw HTML; render text instead: " + string.Join(", ", offenders));
    }

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
}
