using System.Text.RegularExpressions;

namespace CslaStarter.Architecture.Tests;

/// <summary>The GitHub workflows run pinned actions only, and the release attaches what spec §17 promises.</summary>
public partial class WorkflowTests
{
    private static IEnumerable<string> Workflows => Directory.EnumerateFiles(Path.Combine(RepoPaths.Root, ".github", "workflows"), "*.yml");

    [Fact]
    public void Workflows_PinEveryActionBySha()
    {
        var unpinned = Workflows
            .SelectMany(file => File.ReadLines(file).Select(line => (File: Path.GetFileName(file), Line: line.Trim())))
            .Where(l => l.Line.StartsWith("- uses:", StringComparison.Ordinal) || l.Line.StartsWith("uses:", StringComparison.Ordinal))
            .Where(l => !PinnedAction().IsMatch(l.Line))
            .Select(l => $"{l.File}: {l.Line}")
            .ToList();

        Assert.True(unpinned.Count == 0, "Pin these actions to a full commit SHA: " + string.Join(", ", unpinned));
    }

    [Fact]
    public void Release_BuildsTestsAndAttachesTheAppSbomAndMigrationBundle()
    {
        var release = File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "workflows", "release.yml"));

        Assert.Contains("tags: ['v*']", release, StringComparison.Ordinal);
        Assert.Contains("dotnet test", release, StringComparison.Ordinal);
        Assert.Contains("dotnet publish src/CslaStarter.Web", release, StringComparison.Ordinal);
        Assert.Contains("out/*.zip", release, StringComparison.Ordinal);
        Assert.Contains("dotnet CycloneDX", release, StringComparison.Ordinal);
        Assert.Contains("dotnet ef migrations bundle", release, StringComparison.Ordinal);
        Assert.Contains("gh release create", release, StringComparison.Ordinal);
    }

    // The end-to-end project is outside the solution, so CI compiles and format-checks it on every pull request.
    [Fact]
    public void Ci_CompilesAndFormatChecksTheEndToEndTests()
    {
        var ci = File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "workflows", "ci.yml"));

        Assert.Contains("dotnet build tests/CslaStarter.E2E.Tests", ci, StringComparison.Ordinal);
        Assert.Contains("dotnet format tests/CslaStarter.E2E.Tests", ci, StringComparison.Ordinal);
    }

    // Spec §16: Business and Components keep at least 80% line coverage.
    [Fact]
    public void Ci_GatesBusinessAndComponentsCoverage()
    {
        var ci = File.ReadAllText(Path.Combine(RepoPaths.Root, ".github", "workflows", "ci.yml"));

        Assert.Contains("--coverage-output-format cobertura", ci, StringComparison.Ordinal);
        Assert.Contains("bash scripts/check-coverage.sh 80 CslaStarter.Business", ci, StringComparison.Ordinal);
        Assert.Contains("CslaStarter.Components TestResults/coverage/Components.cobertura.xml", ci, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"uses:\s+[\w.-]+/[\w./-]+@[0-9a-f]{40}(\s+#.*)?$")]
    private static partial Regex PinnedAction();
}
