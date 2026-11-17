namespace CslaStarter.Architecture.Tests;

public sealed class ProjectGraphTests
{
    [Theory]
    [InlineData("CslaStarter.Domain", new string[0])]
    [InlineData("CslaStarter.Data", new[] { "CslaStarter.Domain" })]
    [InlineData("CslaStarter.Web", new[] { "CslaStarter.Data", "CslaStarter.Domain" })]
    public void Project_References_OnlyItsAllowedProjects(string project, string[] allowed) =>
        Assert.Equal(allowed, ProjectFile.Load(project).ProjectReferences.Order());

    [Fact]
    public void Solution_Lists_OnlyRebuildProjects()
    {
        var projects = ProjectFile.SolutionProjects().ToHashSet();
        Assert.Superset(new HashSet<string> { "CslaStarter.Domain", "CslaStarter.Data", "CslaStarter.Web", "CslaStarter.Architecture.Tests" }, projects);
        Assert.Subset(new HashSet<string> { "CslaStarter.Domain", "CslaStarter.Data", "CslaStarter.Web", "CslaStarter.Architecture.Tests",
            "CslaStarter.Domain.Tests", "CslaStarter.Integration.Tests", "CslaStarter.Web.Tests" }, projects);
    }
}
