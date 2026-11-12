using CslaStarter.Dal;
using CslaStarter.Dal.Projects;
using CslaStarter.Data.Dal;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Integration.Tests;

public sealed class ProjectDalTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly FakeDataAccessContext Admin = new(Guid.Parse("0193a000-0000-7000-8000-0000000000a2"), "Admin", Roles: [1]);

    private TestDatabase _db = null!;
    private Guid _clientUser;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _clientUser = await _db.AddUserAsync("Cleo", 2);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ProjectDal_Insert_WithTasksAndMembers()
    {
        var saved = await InsertAsync("P-100", [Task("First"), Task("Second")], [_clientUser]);

        Assert.Equal(["First", "Second"], saved.Tasks.Select(t => t.Title));
        Assert.Equal("Cleo", Assert.Single(saved.Members).DisplayName);
        Assert.True(saved.Version > 0);
    }

    [Fact]
    public async Task ProjectDal_Update_AddsEditsAndRemovesChildrenByKey()
    {
        var saved = await InsertAsync("P-101", [Task("Keep"), Task("Remove")], []);
        var keep = saved.Tasks.Single(t => t.Title == "Keep") with { IsDone = true };

        var updated = await UseDal(dal => dal.UpdateAsync(SaveDto(saved, [keep, Task("Added")], [_clientUser]), Ct));

        Assert.Equal([("Keep", true), ("Added", false)], updated.Tasks.Select(t => (t.Title, t.IsDone)));
        Assert.Single(updated.Members);
        Assert.NotEqual(saved.Version, updated.Version);
    }

    [Fact]
    public async Task ProjectDal_Fetch_HiddenProject_ThrowsNotFound()
    {
        var saved = await InsertAsync("P-102", [], []);
        await using var context = _db.CreateContext(new FakeDataAccessContext(_clientUser, "Cleo", Roles: [2]));

        await Assert.ThrowsAsync<NotFoundException>(() => new ProjectDal(context).FetchAsync(saved.Id, Ct));
    }

    [Fact]
    public async Task ProjectDal_ChildChangedByOtherUser_RootSaveFails()
    {
        var loaded = await InsertAsync("P-103", [Task("Original")], []);
        await UseDal(dal => dal.UpdateAsync(SaveDto(loaded, [.. loaded.Tasks, Task("By someone else")], []), Ct));

        await Assert.ThrowsAsync<ConcurrencyException>(() => UseDal(dal => dal.UpdateAsync(SaveDto(loaded, loaded.Tasks, []) with { Name = "Mine" }, Ct)));
    }

    [Fact]
    public async Task ProjectDal_List_SortsFiltersAndPages()
    {
        await InsertAsync("P-B", [Task("a")], []);
        await InsertAsync("P-A", [Task("a"), Task("b", done: true)], []);
        await InsertAsync("X-C", [], []);

        var page = await UseDal(dal => dal.FetchListAsync(new ProjectListCriteria("P-", null, "Code", false, 1, 1), Ct));
        var descending = await UseDal(dal => dal.FetchListAsync(new ProjectListCriteria(null, null, "Code", true, 1, 10), Ct));

        Assert.Equal(2, page.TotalCount);
        var first = Assert.Single(page.Items);
        Assert.Equal(("P-A", 1, 2), (first.Code, first.OpenTaskCount, first.TaskCount));
        Assert.Equal(["X-C", "P-B", "P-A"], descending.Items.Select(i => i.Code));
    }

    [Fact]
    public async Task ProjectDal_IsCodeUnique_IgnoresSelf_AndSeesHiddenProjects()
    {
        var saved = await InsertAsync("P-104", [], []);
        await using var context = _db.CreateContext(new FakeDataAccessContext(_clientUser, "Cleo", Roles: [2]));
        var dal = new ProjectDal(context);

        Assert.False(await dal.IsCodeUniqueAsync("P-104", Guid.CreateVersion7(), Ct));
        Assert.True(await dal.IsCodeUniqueAsync("P-104", saved.Id, Ct));
        Assert.True(await dal.IsCodeUniqueAsync("P-999", Guid.CreateVersion7(), Ct));
    }

    [Fact]
    public async Task ProjectDal_Delete_RemovesChildrenWithAudit()
    {
        var saved = await InsertAsync("P-105", [Task("t")], [_clientUser]);

        await UseDal(async dal =>
        {
            await dal.DeleteAsync(saved.Id, saved.Version, Ct);
            return 0;
        });

        await using var context = _db.CreateContext(Admin);
        Assert.False(await context.Projects.AnyAsync(p => p.Id == saved.Id, Ct));
        Assert.Equal(3, await context.AuditEvents.CountAsync(e => e.RootKey == saved.Id.ToString() && e.Action == "Delete", Ct));
    }

    private static int _sortOrder;

    // Tasks get increasing sort orders, as the business layer assigns them; ids alone don't order reliably.
    private static ProjectTaskDto Task(string title, bool done = false) => new(Guid.CreateVersion7(), title, done, null, Interlocked.Increment(ref _sortOrder));

    private static ProjectSaveDto SaveDto(ProjectDto project, IReadOnlyList<ProjectTaskDto> tasks, IReadOnlyList<Guid> members) =>
        new(project.Id, project.Version, project.Code, project.Name, project.Description, project.StatusCode, project.Budget, project.StartDate, project.EndDate, tasks, members);

    private Task<ProjectDto> InsertAsync(string code, IReadOnlyList<ProjectTaskDto> tasks, IReadOnlyList<Guid> members) =>
        UseDal(dal => dal.InsertAsync(new ProjectSaveDto(Guid.CreateVersion7(), 0, code, code, null, 1, 100m, null, null, tasks, members), Ct));

    private async Task<T> UseDal<T>(Func<ProjectDal, Task<T>> action)
    {
        await using var context = _db.CreateContext(Admin);
        return await action(new ProjectDal(context));
    }
}
