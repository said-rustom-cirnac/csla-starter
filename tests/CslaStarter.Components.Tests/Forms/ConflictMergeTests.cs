using Csla;
using Csla.Core;
using CslaStarter.Business.Projects;
using CslaStarter.Business.Tests;
using CslaStarter.Components.Forms;
using CslaStarter.Dal.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Components.Tests.Forms;

public sealed class ConflictMergeTests : IDisposable
{
    private static readonly Guid A = Guid.CreateVersion7();
    private static readonly Guid B = Guid.CreateVersion7();
    private readonly ComponentTestContext _context = new(TestPrincipals.Admin);
    private readonly Guid _projectId = Guid.CreateVersion7();

    public ConflictMergeTests() =>
        _context.Projects.Add(_projectId, "P-1", tasks: [new ProjectTaskDto(A, "Task A", false, null, 1), new ProjectTaskDto(B, "Task B", false, null, 2)]);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task FieldDiff_RootAndChildrenByKey()
    {
        var mine = await FetchAsync();
        mine.Name = "Mine";
        mine.Tasks.Single(t => t.Id == A).Title = "A by me";
        TheyChange(p => p with { Description = "Theirs" });
        var theirs = await FetchAsync();

        var differences = FieldDiff.Between(theirs, mine);

        Assert.Equal(
            [("", nameof(ProjectEdit.Name)), ("", nameof(ProjectEdit.Description)), ($"Tasks[{A}]", nameof(ProjectTask.Title))],
            differences.Select(d => (d.Path, d.Property)));
        Assert.Equal(("P-1", "Mine"), (differences[0].Theirs, differences[0].Mine));
    }

    [Fact]
    public async Task ConflictMerge_AppliesOnlyMyChanges_ChildrenByKey()
    {
        var original = await FetchAsync();
        var mine = Clone(original);
        mine.Name = "Mine";
        mine.Tasks.Single(t => t.Id == A).Title = "A by me";
        mine.Tasks.Remove(mine.Tasks.Single(t => t.Id == B));
        mine.AddTask("C by me");
        var d = Guid.CreateVersion7();
        TheyChange(p => p with
        {
            Description = "Theirs",
            Version = 9,
            Tasks = [p.Tasks[0] with { IsDone = true }, p.Tasks[1], new ProjectTaskDto(d, "D by them", false, null, 3)],
        });
        var theirs = await FetchAsync();

        var result = ConflictMerge.Apply(original, mine, theirs);
        var merged = (ProjectEdit)result.Merged;

        Assert.Empty(result.BothChanged);
        Assert.Equal(("Mine", "Theirs", 9u), (merged.Name, merged.Description, merged.Version));
        var a = merged.Tasks.Single(t => t.Id == A);
        Assert.Equal(("A by me", true), (a.Title, a.IsDone));
        Assert.DoesNotContain(merged.Tasks, t => t.Id == B);
        Assert.Contains(merged.Tasks, t => t.Title == "C by me" && t.IsNew);
        Assert.Contains(merged.Tasks, t => t.Id == d);
        Assert.True(merged.IsDirty);
    }

    [Fact]
    public async Task ConflictMerge_BothChanged_IsReported_AndCanKeepTheirs()
    {
        var original = await FetchAsync();
        var mine = Clone(original);
        mine.Name = "Mine";
        mine.Description = "My description";
        TheyChange(p => p with { Name = "Theirs", Version = 9 });
        var theirs = await FetchAsync();

        var both = ConflictMerge.Apply(original, mine, theirs).BothChanged;
        var keepTheirs = (ProjectEdit)ConflictMerge.Apply(original, mine, await FetchAsync(), new HashSet<string> { both.Single().Key }).Merged;

        Assert.Equal(nameof(ProjectEdit.Name), Assert.Single(both).Property);
        Assert.Equal(("Theirs", "My description"), (keepTheirs.Name, keepTheirs.Description));
    }

    private void TheyChange(Func<ProjectDto, ProjectDto> change) => _context.Projects.Projects[_projectId] = change(_context.Projects.Projects[_projectId]);

    private ProjectEdit Clone(ProjectEdit project) => (ProjectEdit)ObjectCloner.GetInstance(_context.Services.GetRequiredService<ApplicationContext>()).Clone(project)!;

    private Task<ProjectEdit> FetchAsync() => _context.Services.GetRequiredService<IDataPortal<ProjectEdit>>().FetchAsync(_projectId);
}
