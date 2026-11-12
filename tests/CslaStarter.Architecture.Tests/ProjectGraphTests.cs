namespace CslaStarter.Architecture.Tests;

/// <summary>Locks in the allowed references between the src projects (spec section 5).</summary>
public class ProjectGraphTests
{
    private static readonly Dictionary<string, string[]> AllowedGraph = new()
    {
        ["CslaStarter.Core"] = [],
        ["CslaStarter.Dal"] = ["CslaStarter.Core"],
        ["CslaStarter.Business"] = ["CslaStarter.Core", "CslaStarter.Dal"],
        ["CslaStarter.Data"] = ["CslaStarter.Core", "CslaStarter.Dal"],
        ["CslaStarter.Infrastructure"] = ["CslaStarter.Core", "CslaStarter.Data"],
        ["CslaStarter.Components"] = ["CslaStarter.Business", "CslaStarter.Core"],
        ["CslaStarter.Web"] =
        [
            "CslaStarter.Business", "CslaStarter.Components", "CslaStarter.Core",
            "CslaStarter.Dal", "CslaStarter.Data", "CslaStarter.Infrastructure",
        ],
    };

    public static TheoryData<string, string[]> Allowed()
    {
        var data = new TheoryData<string, string[]>();
        foreach (var (project, references) in AllowedGraph)
        {
            data.Add(project, references);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Allowed))]
    public void ProjectReferences_MatchAllowedGraph(string project, string[] expected) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal), ProjectFile.Load(project).ProjectReferences.Order(StringComparer.Ordinal));

    [Fact]
    public void EverySrcProject_IsCoveredByTheGraph() =>
        Assert.Equal(AllowedGraph.Keys.Order(StringComparer.Ordinal), ProjectFile.AllSrcProjectNames().Order(StringComparer.Ordinal));

    [Theory]
    [InlineData("CslaStarter.Core")]
    [InlineData("CslaStarter.Dal")]
    [InlineData("CslaStarter.Business")]
    [InlineData("CslaStarter.Components")]
    public void UpperLayers_DoNotReferenceDataAccessPackages(string project) =>
        Assert.DoesNotContain(
            ProjectFile.Load(project).PackageReferences,
            p => p.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || p.StartsWith("Npgsql", StringComparison.Ordinal));

    [Fact]
    public void Core_ReferencesOnlyExtensionAbstractions() =>
        Assert.All(
            ProjectFile.Load("CslaStarter.Core").PackageReferences,
            p => Assert.Matches(@"^Microsoft\.Extensions\.[A-Za-z.]*Abstractions$", p));
}
