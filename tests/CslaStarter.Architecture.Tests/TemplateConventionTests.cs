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
}
