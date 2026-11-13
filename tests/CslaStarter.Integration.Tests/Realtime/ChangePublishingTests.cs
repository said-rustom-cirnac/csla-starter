using CslaStarter.Core.Realtime;
using CslaStarter.Dal;
using CslaStarter.Dal.Projects;
using CslaStarter.Data.Dal;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Integration.Tests.Realtime;

public sealed class ChangePublishingTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly FakeDataAccessContext Admin =
        new(Guid.Parse("0193a000-0000-7000-8000-0000000000a2"), "Admin", "corr-9", "circuit-9", [1]);

    private readonly RecordingNotifier _notifier = new();
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Publish_Insert_PublishesInserted()
    {
        var id = await AddUserAsync("Ada");

        var change = Assert.Single(_notifier.Published, c => c.EntityType == "User");
        Assert.Equal((id.ToString(), ChangeKind.Inserted), (change.EntityKey, change.Kind));
        Assert.Equal(await UserVersionAsync(id), change.Version);
    }

    [Fact]
    public async Task Publish_ChildOnlyChange_NotifiesRootOnce()
    {
        var project = await UseProjectDal(dal => dal.InsertAsync(NewProject("P-1", [Task("a"), Task("b")], []), Ct));
        _notifier.Published.Clear();

        var renamed = project.Tasks.Select(t => t with { Title = t.Title + "!" }).ToList();
        var updated = await UseProjectDal(dal => dal.UpdateAsync(SaveDto(project, renamed, []), Ct));

        var change = Assert.Single(_notifier.Published);
        Assert.Equal(("Project", project.Id.ToString(), ChangeKind.Updated), (change.EntityType, change.EntityKey, change.Kind));
        Assert.Equal(updated.Version, change.Version);
        Assert.NotEqual(project.Version, change.Version);
    }

    [Fact]
    public async Task Publish_InsertWithChildren_PublishesOneInserted()
    {
        var member = await _db.AddUserAsync("Cleo", 2);
        _notifier.Published.Clear();

        var project = await UseProjectDal(dal => dal.InsertAsync(NewProject("P-0", [Task("a"), Task("b")], [member]), Ct));

        var change = Assert.Single(_notifier.Published, c => c.EntityType == "Project");
        Assert.Equal((ChangeKind.Inserted, project.Version), (change.Kind, change.Version));
    }

    [Fact]
    public async Task Publish_DeleteWithChildren_PublishesOneDeleted()
    {
        var project = await UseProjectDal(dal => dal.InsertAsync(NewProject("P-9", [Task("a"), Task("b")], []), Ct));
        _notifier.Published.Clear();

        await UseProjectDal(async dal =>
        {
            await dal.DeleteAsync(project.Id, project.Version, Ct);
            return 0;
        });

        var change = Assert.Single(_notifier.Published);
        Assert.Equal(("Project", ChangeKind.Deleted), (change.EntityType, change.Kind));
    }

    [Fact]
    public async Task Publish_Delete_PublishesDeleted()
    {
        var id = await AddUserAsync("Ada", 2);
        _notifier.Published.Clear();

        await using (var context = Context())
        {
            context.Users.Remove(await context.Users.Include(u => u.Roles).SingleAsync(u => u.Id == id, Ct));
            await context.SaveChangesAsync(Ct);
        }

        var change = Assert.Single(_notifier.Published, c => c.EntityType == "User");
        Assert.Equal((ChangeKind.Deleted, (uint?)null), (change.Kind, change.Version));
    }

    [Fact]
    public async Task Publish_RolledBack_PublishesNothing()
    {
        await using var context = Context();
        await using (var transaction = await context.Database.BeginTransactionAsync(Ct))
        {
            context.Users.Add(NewUser("Ada"));
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        Assert.Empty(_notifier.Published);
    }

    [Fact]
    public async Task Publish_DisposedWithoutCommit_PublishesNothing()
    {
        await using var context = Context();
        await using (await context.Database.BeginTransactionAsync(Ct))
        {
            context.Users.Add(NewUser("Ada"));
            await context.SaveChangesAsync(Ct);
        }

        await context.Database.BeginTransactionAsync(Ct);
        await context.Database.CommitTransactionAsync(Ct);

        Assert.Empty(_notifier.Published);
    }

    [Fact]
    public async Task Publish_CommittedTransaction_PublishesAfterCommit()
    {
        await using var context = Context();
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);
        var user = NewUser("Ada");
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        user.DisplayName = "Ada L.";
        await context.SaveChangesAsync(Ct);

        Assert.Empty(_notifier.Published);
        await transaction.CommitAsync(Ct);

        var change = Assert.Single(_notifier.Published, c => c.EntityType == "User");
        Assert.Equal((ChangeKind.Inserted, user.Version), (change.Kind, change.Version));
    }

    [Fact]
    public async Task Publish_FailedSave_IsNotPublishedByTheNextSave()
    {
        var project = await UseProjectDal(dal => dal.InsertAsync(NewProject("P-2", [], []), Ct));
        await UseProjectDal(dal => dal.UpdateAsync(SaveDto(project, [], []) with { Name = "Theirs" }, Ct));
        _notifier.Published.Clear();

        await using var context = Context();
        var dal = new ProjectDal(context);
        await Assert.ThrowsAsync<ConcurrencyException>(() => dal.UpdateAsync(SaveDto(project, [], []) with { Name = "Mine" }, Ct));
        context.ChangeTracker.Clear();
        var otherId = await AddUserAsync(context, "Bo");

        Assert.DoesNotContain(_notifier.Published, c => c.EntityType == "Project");
        Assert.Contains(_notifier.Published, c => c.EntityKey == otherId.ToString());
    }

    [Fact]
    public async Task Publish_MemberRemoved_PublishesUserAccessForThatUser()
    {
        var member = await _db.AddUserAsync("Cleo", 2);
        var project = await UseProjectDal(dal => dal.InsertAsync(NewProject("P-3", [], [member]), Ct));
        _notifier.Published.Clear();

        await UseProjectDal(dal => dal.UpdateAsync(SaveDto(project, [], []), Ct));

        var access = Assert.Single(_notifier.Published, c => c.Kind == ChangeKind.AccessChanged);
        Assert.Equal((EntityChange.UserAccessType, member.ToString()), (access.EntityType, access.EntityKey));
        Assert.Contains(_notifier.Published, c => c.EntityType == "Project" && c.Kind == ChangeKind.Updated);
    }

    [Fact]
    public async Task Publish_RoleChanged_PublishesUserAccess()
    {
        var id = await AddUserAsync("Ada", 2);
        _notifier.Published.Clear();

        await using (var context = Context())
        {
            var user = await context.Users.Include(u => u.Roles).SingleAsync(u => u.Id == id, Ct);
            user.Roles.Add(new UserRole { RoleTypeCode = 3 });
            context.Entry(user).State = EntityState.Modified;
            await context.SaveChangesAsync(Ct);
        }

        var access = Assert.Single(_notifier.Published, c => c.Kind == ChangeKind.AccessChanged);
        Assert.Equal(id.ToString(), access.EntityKey);
    }

    [Fact]
    public async Task Publish_Deactivated_PublishesUserAccess_ButARenameDoesNot()
    {
        var id = await AddUserAsync("Ada");
        _notifier.Published.Clear();

        await using (var context = Context())
        {
            (await context.Users.SingleAsync(u => u.Id == id, Ct)).DisplayName = "Ada L.";
            await context.SaveChangesAsync(Ct);
        }

        Assert.DoesNotContain(_notifier.Published, c => c.Kind == ChangeKind.AccessChanged);

        await using (var context = Context())
        {
            (await context.Users.SingleAsync(u => u.Id == id, Ct)).IsActive = false;
            await context.SaveChangesAsync(Ct);
        }

        Assert.Single(_notifier.Published, c => c.Kind == ChangeKind.AccessChanged);
    }

    [Fact]
    public async Task Publish_CarriesChangerAndOrigin()
    {
        await AddUserAsync("Ada");

        Assert.All(_notifier.Published, c => Assert.Equal((Admin.UserId, "Admin", "circuit-9"), (c.ChangedByUserId, c.ChangedByName, c.OriginId)));
    }

    [Fact]
    public async Task Publish_NotifierFailure_DoesNotFailSave()
    {
        _notifier.Failure = new InvalidOperationException("backplane down");

        var id = await AddUserAsync("Ada");

        await using var context = Context();
        Assert.True(await context.Users.AnyAsync(u => u.Id == id, Ct));
    }

    private Data.AppDbContext Context() => _db.CreateContext(Admin, notifier: _notifier);

    private async Task<Guid> AddUserAsync(string name, params int[] roles)
    {
        await using var context = Context();
        return await AddUserAsync(context, name, roles);
    }

    private static async Task<Guid> AddUserAsync(Data.AppDbContext context, string name, params int[] roles)
    {
        var user = NewUser(name);
        foreach (var role in roles)
        {
            user.Roles.Add(new UserRole { RoleTypeCode = role });
        }

        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<uint> UserVersionAsync(Guid id)
    {
        await using var context = Context();
        return (await context.Users.SingleAsync(u => u.Id == id, Ct)).Version;
    }

    private async Task<T> UseProjectDal<T>(Func<ProjectDal, Task<T>> action)
    {
        await using var context = Context();
        return await action(new ProjectDal(context));
    }

    private static AppUser NewUser(string name) =>
        new() { TenantId = "tenant-1", ObjectId = Guid.NewGuid().ToString(), DisplayName = name };

    private static int _sortOrder;

    private static ProjectTaskDto Task(string title) => new(Guid.CreateVersion7(), title, false, null, Interlocked.Increment(ref _sortOrder));

    private static ProjectSaveDto NewProject(string code, IReadOnlyList<ProjectTaskDto> tasks, IReadOnlyList<Guid> members) =>
        new(Guid.CreateVersion7(), 0, code, code, null, 1, null, null, null, tasks, members);

    private static ProjectSaveDto SaveDto(ProjectDto project, IReadOnlyList<ProjectTaskDto> tasks, IReadOnlyList<Guid> members) =>
        new(project.Id, project.Version, project.Code, project.Name, project.Description, project.StatusCode, project.Budget, project.StartDate, project.EndDate, tasks, members);
}

internal sealed class RecordingNotifier : IEntityChangeNotifier
{
    public List<EntityChange> Published { get; } = [];

    public Exception? Failure { get; set; }

    public Task PublishAsync(IReadOnlyList<EntityChange> changes, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        lock (Published)
        {
            Published.AddRange(changes);
        }

        return Task.CompletedTask;
    }
}
