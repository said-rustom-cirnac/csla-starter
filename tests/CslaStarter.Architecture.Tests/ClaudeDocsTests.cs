using System.Text.RegularExpressions;

namespace CslaStarter.Architecture.Tests;

/// <summary>
/// Where the Claude guides are present (the claude branch, or an app made with --claude), every project has a
/// CLAUDE.md and every link in the guides resolves. Skipped on main and in apps made with --claude false.
/// </summary>
public partial class ClaudeDocsTests
{
    private static readonly string[] IgnoredSegments = ["bin", "obj", ".git", ".superpowers", ".remember", "node_modules"];

    private static bool ClaudeFilesPresent => Directory.Exists(Path.Combine(RepoPaths.Root, ".claude"));

    [Fact]
    public void EveryProjectFolder_HasClaudeMd()
    {
        Assert.SkipUnless(ClaudeFilesPresent, "No .claude folder, so the Claude guides are not part of this checkout.");

        var missing = new[] { RepoPaths.Src, RepoPaths.Tests }
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Where(folder => Directory.EnumerateFiles(folder, "*.csproj").Any())
            .Where(folder => !File.Exists(Path.Combine(folder, "CLAUDE.md")))
            .Select(RepoPaths.Relative)
            .ToList();

        Assert.True(missing.Count == 0, "Add a CLAUDE.md to: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("new-business-object")]
    [InlineData("new-type")]
    [InlineData("new-component")]
    public void Skills_ExistWithFrontMatter(string skill)
    {
        Assert.SkipUnless(ClaudeFilesPresent, "No .claude folder, so the Claude guides are not part of this checkout.");

        var path = Path.Combine(RepoPaths.Root, ".claude", "skills", skill, "SKILL.md");
        Assert.True(File.Exists(path), $"Missing skill {skill}");
        var text = File.ReadAllText(path);
        Assert.StartsWith("---", text, StringComparison.Ordinal);
        Assert.Contains($"name: {skill}", text, StringComparison.Ordinal);
        Assert.Contains("description:", text, StringComparison.Ordinal);
        Assert.Contains("dotnet test", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ClaudeMdLinks_PointToExistingFiles()
    {
        Assert.SkipUnless(ClaudeFilesPresent, "No .claude folder, so the Claude guides are not part of this checkout.");

        var broken = ClaudeDocuments()
            .SelectMany(doc => MarkdownLink().Matches(File.ReadAllText(doc))
                .Select(match => match.Groups["target"].Value)
                .Where(target => !IsExternalOrAnchor(target))
                .Where(target => !Exists(Path.Combine(Path.GetDirectoryName(doc)!, target.Split('#')[0])))
                .Select(target => $"{RepoPaths.Relative(doc)}: {target}"))
            .ToList();

        Assert.True(broken.Count == 0, "Broken links: " + string.Join(", ", broken));
    }

    private static IEnumerable<string> ClaudeDocuments()
    {
        var claudeMds = Directory.EnumerateFiles(RepoPaths.Root, "CLAUDE.md", SearchOption.AllDirectories);
        var guidesFolder = Path.Combine(RepoPaths.Root, "docs", "claude");
        var guides = Directory.Exists(guidesFolder)
            ? Directory.EnumerateFiles(guidesFolder, "*.md", SearchOption.AllDirectories)
            : [];

        return claudeMds.Concat(guides)
            .Where(path => !RepoPaths.Relative(path).Split('/').Intersect(IgnoredSegments).Any());
    }

    private static bool IsExternalOrAnchor(string target) =>
        target.StartsWith('#') || target.Contains("://", StringComparison.Ordinal) || target.StartsWith("mailto:", StringComparison.Ordinal);

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    [GeneratedRegex(@"\]\((?<target>[^)\s]+)\)")]
    private static partial Regex MarkdownLink();
}
