using System.Reflection;
using CslaStarter.Core.Auditing;
using CslaStarter.Data;
using CslaStarter.Data.Auditing;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace CslaStarter.Integration.Tests;

public sealed class AuditTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly string[] TechnicalColumns = ["Version", "CreatedOn", "CreatedBy", "ModifiedOn", "ModifiedBy"];

    private readonly FakeTimeProvider _time = new(Now);
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Audit_Insert_RecordsNewValues()
    {
        var id = await InsertUserAsync("Ada");

        var audit = Assert.Single(await EventsAsync(id));
        Assert.Equal("Insert", audit.Action);
        Assert.Equal("User", audit.EntityType);
        var change = audit.Changes.Single(c => c.Property == "DisplayName");
        Assert.Null(change.OldValue);
        Assert.Equal("Ada", change.NewValue);
    }

    [Fact]
    public async Task Audit_Update_RecordsOnlyChangedProperties()
    {
        var id = await InsertUserAsync("Ada");
        await using (var context = Context())
        {
            (await context.Users.SingleAsync(u => u.Id == id, Ct)).DisplayName = "Ada L.";
            await context.SaveChangesAsync(Ct);
        }

        var update = (await EventsAsync(id)).Single(e => e.Action == "Update");
        var change = Assert.Single(update.Changes);
        Assert.Equal(("DisplayName", "Ada", "Ada L."), (change.Property, change.OldValue, change.NewValue));
    }

    [Fact]
    public async Task Audit_UpdateWithNoRealChange_WritesNothing()
    {
        var id = await InsertUserAsync("Ada");
        await using (var context = Context())
        {
            var detached = await context.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
            context.Users.Update(detached);
            await context.SaveChangesAsync(Ct);
        }

        Assert.Single(await EventsAsync(id));
    }

    [Fact]
    public async Task Audit_TouchedRoot_IssuesUpdateAndBumpsVersion()
    {
        var id = await InsertUserAsync("Ada");
        uint before;
        await using (var context = Context())
        {
            var user = await context.Users.SingleAsync(u => u.Id == id, Ct);
            before = user.Version;
            context.Entry(user).State = EntityState.Modified;
            await context.SaveChangesAsync(Ct);
        }

        await using var check = Context();
        Assert.NotEqual(before, (await check.Users.SingleAsync(u => u.Id == id, Ct)).Version);
        Assert.Single(await EventsAsync(id));
    }

    [Fact]
    public async Task Audit_FailedSave_LeavesNoPhantomAuditRows()
    {
        var id = await InsertUserAsync("Ada");
        await using var stale = Context();
        var mine = await stale.Users.SingleAsync(u => u.Id == id, Ct);
        await using (var other = Context())
        {
            (await other.Users.SingleAsync(u => u.Id == id, Ct)).DisplayName = "Other";
            await other.SaveChangesAsync(Ct);
        }

        mine.DisplayName = "Mine";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(Ct));
        stale.Entry(mine).State = EntityState.Unchanged;
        stale.SecurityEvents.Add(new SecurityEventRow { Kind = 1, OccurredAt = Now, Detail = "unrelated" });
        await stale.SaveChangesAsync(Ct);

        var changes = (await EventsAsync(id)).SelectMany(e => e.Changes).Select(c => c.NewValue);
        Assert.DoesNotContain("Mine", changes);
    }

    [Fact]
    public async Task Audit_Delete_RecordsOldValues()
    {
        var id = await InsertUserAsync("Ada");
        await using (var context = Context())
        {
            context.Users.Remove(await context.Users.SingleAsync(u => u.Id == id, Ct));
            await context.SaveChangesAsync(Ct);
        }

        var delete = (await EventsAsync(id)).Single(e => e.Action == "Delete");
        Assert.All(delete.Changes, c => Assert.Null(c.NewValue));
        Assert.Equal("Ada", delete.Changes.Single(c => c.Property == "DisplayName").OldValue);
    }

    [Fact]
    public async Task Audit_MaskedProperty_StoresNoValues()
    {
        var id = await InsertUserAsync("Ada", employeeNumber: "E-12345");

        var change = (await EventsAsync(id)).Single().Changes.Single(c => c.Property == "EmployeeNumber");
        Assert.True(change.IsMasked);
        Assert.Null(change.OldValue);
        Assert.Null(change.NewValue);
    }

    [Fact]
    public async Task Audit_StampsCreatedAndModified()
    {
        var id = await InsertUserAsync("Ada");
        _time.Advance(TimeSpan.FromHours(1));
        await using (var context = Context())
        {
            (await context.Users.SingleAsync(u => u.Id == id, Ct)).DisplayName = "Ada L.";
            await context.SaveChangesAsync(Ct);
        }

        await using var check = Context();
        var user = await check.Users.SingleAsync(u => u.Id == id, Ct);
        Assert.Equal(Now, user.CreatedOn);
        Assert.Equal(Now.AddHours(1), user.ModifiedOn);
        Assert.Equal("tester", user.CreatedBy);
        Assert.Equal("tester", user.ModifiedBy);
    }

    [Fact]
    public async Task Audit_TechnicalColumnsAreNeverAudited()
    {
        var id = await InsertUserAsync("Ada");
        await using (var context = Context())
        {
            (await context.Users.SingleAsync(u => u.Id == id, Ct)).DisplayName = "Ada L.";
            await context.SaveChangesAsync(Ct);
        }

        var properties = (await EventsAsync(id)).SelectMany(e => e.Changes).Select(c => c.Property);
        Assert.DoesNotContain(properties, TechnicalColumns.Contains);
    }

    [Fact]
    public async Task Audit_ChildRow_IsRecordedUnderItsRoot()
    {
        var id = await InsertUserAsync("Ada");
        await using (var context = Context())
        {
            context.UserRoles.Add(new UserRole { UserId = id, RoleTypeCode = 1 });
            await context.SaveChangesAsync(Ct);
        }

        await using var check = Context();
        var child = await check.AuditEvents.Include(e => e.Changes).SingleAsync(e => e.EntityType == "UserRole", Ct);
        Assert.Equal(("User", id.ToString()), (child.RootType, child.RootKey));
        var role = Assert.Single(child.Changes);
        Assert.Equal(("RoleTypeCode", "1", "UserRoleType"), (role.Property, role.NewValue, role.TypeCodeOf));
    }

    [Fact]
    public async Task Audit_RolledBackTransaction_LeavesNoAuditRows()
    {
        var user = NewUser("Ada");
        await using (var context = Context())
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            context.Users.Add(user);
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        Assert.Empty(await EventsAsync(user.Id));
    }

    [Fact]
    public async Task AuditTables_RejectUpdateAndDelete_EvenForOwner()
    {
        await InsertUserAsync("Ada");

        foreach (var sql in new[] { "update audit_event set user_name = 'x'", "delete from audit_change", "delete from audit_event" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(_db.OwnerConnectionString, sql));
            Assert.Contains("append-only", error.MessageText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RuntimeRole_CannotUpdateAuditRows()
    {
        await InsertUserAsync("Ada");

        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(_db.RuntimeConnectionString, "update audit_event set user_name = 'x'"));
        Assert.Equal("42501", error.SqlState);
    }

    [Theory]
    [InlineData("audit_event")]
    [InlineData("audit_change")]
    [InlineData("security_event")]
    public async Task RuntimeRole_HasInsertAndSelectOnly_OnAppendOnlyTables(string table)
    {
        foreach (var (privilege, expected) in new[] { ("INSERT", true), ("SELECT", true), ("UPDATE", false), ("DELETE", false), ("TRUNCATE", false) })
        {
            await using var connection = new NpgsqlConnection(_db.OwnerConnectionString);
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand($"select has_table_privilege('app_runtime', '{table}', '{privilege}')", connection);
            Assert.True(expected == (bool)(await command.ExecuteScalarAsync(Ct))!, $"app_runtime {privilege} on {table} should be {expected}");
        }
    }

    [Fact]
    public async Task AuditHistoryReader_ReturnsNewestFirst_AndFiltersByProperty()
    {
        var id = await InsertUserAsync("Ada");
        foreach (var change in new Action<AppUser>[] { u => u.DisplayName = "Ada L.", u => u.Email = "ada@example.ca" })
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            await using var context = Context();
            change(await context.Users.SingleAsync(u => u.Id == id, Ct));
            await context.SaveChangesAsync(Ct);
        }

        await using var readerContext = Context();
        var reader = new AuditHistoryReader(readerContext);
        var all = await reader.GetAsync(new AuditQuery("User", id.ToString()), Ct);
        var displayName = await reader.GetAsync(new AuditQuery("User", id.ToString(), Property: "DisplayName"), Ct);

        Assert.Equal(3, all.TotalCount);
        Assert.Equal("Email", Assert.Single(all.Entries[0].Changes).Property);
        Assert.Equal(2, displayName.TotalCount);
        Assert.All(displayName.Entries, e => Assert.Equal("DisplayName", Assert.Single(e.Changes).Property));
    }

    [Fact]
    public async Task AuditHistoryReader_LeavesOutHiddenFields_AndEntriesWithNothingElse()
    {
        var id = await InsertUserAsync("Ada", employeeNumber: "E-1");
        foreach (var change in new Action<AppUser>[] { u => u.EmployeeNumber = "E-2", u => u.DisplayName = "Ada L." })
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            await using var context = Context();
            change(await context.Users.SingleAsync(u => u.Id == id, Ct));
            await context.SaveChangesAsync(Ct);
        }

        await using var readerContext = Context();
        var page = await new AuditHistoryReader(readerContext).GetAsync(new AuditQuery("User", id.ToString(), HiddenFields: ["User.EmployeeNumber"]), Ct);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Entries.Count);
        Assert.DoesNotContain(page.Entries.SelectMany(e => e.Changes), c => c.Property == "EmployeeNumber");
    }

    [Fact]
    public void EveryAuditedEntity_HasABusinessTypeThatGuardsItsHistory()
    {
        var audited = typeof(AppDbContext).Assembly.GetTypes()
            .Select(t => t.GetCustomAttribute<AuditEntityAttribute>()?.Name).OfType<string>().Order(StringComparer.Ordinal);

        Assert.Equal(audited, CslaStarter.Business.Auditing.AuditedTypes.EntityTypes.Order(StringComparer.Ordinal));
    }

    private AppDbContext Context() => _db.CreateContext(FakeDataAccessContext.Tester, _time);

    private static AppUser NewUser(string name, string? employeeNumber = null) =>
        new() { TenantId = "tenant-1", ObjectId = Guid.NewGuid().ToString(), DisplayName = name, EmployeeNumber = employeeNumber };

    private async Task<Guid> InsertUserAsync(string name, string? employeeNumber = null)
    {
        var user = NewUser(name, employeeNumber);
        await using var context = Context();
        context.Users.Add(user);
        await context.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<List<AuditEventRow>> EventsAsync(Guid id)
    {
        await using var context = Context();
        var key = id.ToString();
        return await context.AuditEvents.Include(e => e.Changes).Where(e => e.EntityKey == key).OrderBy(e => e.Id).ToListAsync(Ct);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
