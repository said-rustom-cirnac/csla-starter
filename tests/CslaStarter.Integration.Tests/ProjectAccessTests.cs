using CslaStarter.Data;
using CslaStarter.Data.Access;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Integration.Tests;

/// <summary>Record-level access: Admin and Technical see every project; a Client sees only projects they belong to.</summary>
public sealed class ProjectAccessTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly Guid ClientId = Guid.Parse("0193a000-0000-7000-8000-0000000000c1");
    private static readonly FakeDataAccessContext Admin = new(Guid.Parse("0193a000-0000-7000-8000-0000000000a1"), "Admin", Roles: [1]);
    private static readonly FakeDataAccessContext Technical = new(Guid.Parse("0193a000-0000-7000-8000-0000000000b1"), "Tech", Roles: [3]);
    private static readonly FakeDataAccessContext Client = new(ClientId, "Client", Roles: [2]);

    private TestDatabase _db = null!;
    private Guid _mine;
    private Guid _theirs;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        await _db.AddUserAsync("Client", ClientId.ToString(), true, 2);
        var clientUserId = await IdOfAsync(ClientId.ToString());
        await using var context = _db.CreateContext(Admin);
        var mine = NewProject("P-MINE");
        mine.Members.Add(new ProjectMemberRow { UserId = clientUserId });
        mine.Tasks.Add(new ProjectTaskRow { Title = "Mine task" });
        var theirs = NewProject("P-THEIRS");
        theirs.Tasks.Add(new ProjectTaskRow { Title = "Their task" });
        context.Projects.AddRange(mine, theirs);
        await context.SaveChangesAsync(Ct);
        (_mine, _theirs) = (mine.Id, theirs.Id);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RecordAccess_Fetch_HiddenProject_IsNotFound()
    {
        await using var context = _db.CreateContext(await AsClientAsync());

        Assert.Null(await context.Projects.SingleOrDefaultAsync(p => p.Id == _theirs, Ct));
        Assert.NotNull(await context.Projects.SingleOrDefaultAsync(p => p.Id == _mine, Ct));
    }

    [Fact]
    public async Task RecordAccess_List_CountsOnlyVisibleProjects()
    {
        await using var context = _db.CreateContext(await AsClientAsync());

        Assert.Equal(1, await context.Projects.CountAsync(Ct));
        Assert.Equal(["P-MINE"], await context.Projects.Select(p => p.Code).ToListAsync(Ct));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("technical")]
    public async Task RecordAccess_AdminAndTechnical_SeeAllProjects(string who)
    {
        await using var context = _db.CreateContext(who == "admin" ? Admin : Technical);

        Assert.Equal(2, await context.Projects.CountAsync(Ct));
    }

    [Fact]
    public async Task RecordAccess_NoAccessContext_SeesNothing()
    {
        await using var context = new AppDatabase(Npgsql.NpgsqlDataSource.Create(_db.RuntimeConnectionString)).CreateContext();

        Assert.Equal(0, await context.Projects.CountAsync(Ct));
    }

    [Fact]
    public async Task RecordAccess_ChildRows_AreFilteredToo()
    {
        await using var context = _db.CreateContext(await AsClientAsync());

        Assert.Equal(["Mine task"], await context.ProjectTasks.Select(t => t.Title).ToListAsync(Ct));
    }

    [Fact]
    public async Task RecordScopedEntities_HaveTheFilter()
    {
        await using var context = _db.CreateContext(Admin);
        var scoped = context.Model.GetEntityTypes().Where(t => t.ClrType.GetCustomAttributes(typeof(RecordScopedAttribute), false).Length > 0).ToList();

        Assert.NotEmpty(scoped);
        Assert.All(scoped, t => Assert.Contains(t.GetDeclaredQueryFilters(), f => f.Key == AppDbContext.RecordAccessFilter));
    }

    [Fact]
    public async Task Concurrency_ChildChangedByOtherUser_RootSaveFails()
    {
        await using var mine = _db.CreateContext(Admin);
        var loaded = await mine.Projects.SingleAsync(p => p.Id == _mine, Ct);
        await using (var other = _db.CreateContext(Technical))
        {
            var project = await other.Projects.Include(p => p.Tasks).SingleAsync(p => p.Id == _mine, Ct);
            project.Tasks.Add(new ProjectTaskRow { Title = "Added by someone else" });
            other.Entry(project).State = EntityState.Modified;
            await other.SaveChangesAsync(Ct);
        }

        loaded.Name = "My rename";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => mine.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Audit_ChildOnlyChange_TouchesRootVersion()
    {
        uint before;
        await using (var context = _db.CreateContext(Admin))
        {
            var project = await context.Projects.Include(p => p.Tasks).SingleAsync(p => p.Id == _mine, Ct);
            before = project.Version;
            project.Tasks.Single().IsDone = true;
            context.Entry(project).State = EntityState.Modified;
            await context.SaveChangesAsync(Ct);
        }

        await using var check = _db.CreateContext(Admin);
        Assert.NotEqual(before, (await check.Projects.SingleAsync(p => p.Id == _mine, Ct)).Version);
        var taskChange = await check.AuditEvents.Include(e => e.Changes).SingleAsync(e => e.EntityType == "ProjectTask" && e.Action == "Update", Ct);
        Assert.Equal(("Project", _mine.ToString()), (taskChange.RootType, taskChange.RootKey));
        Assert.Equal("IsDone", Assert.Single(taskChange.Changes).Property);
    }

    private static ProjectRow NewProject(string code) => new() { Code = code, Name = code, StatusCode = 1 };

    private async Task<FakeDataAccessContext> AsClientAsync() => Client with { UserId = await IdOfAsync(ClientId.ToString()) };

    private async Task<Guid> IdOfAsync(string objectId)
    {
        await using var context = _db.CreateContext();
        return (await context.Users.SingleAsync(u => u.ObjectId == objectId, Ct)).Id;
    }
}
