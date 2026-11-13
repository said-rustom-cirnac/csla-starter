using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CslaStarter.Integration.Tests;

public sealed class BootstrapAdminTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task BootstrapAdmin_CreatesFirstAdmin_AndLogsSecurityEvent()
    {
        var exit = await Bootstrapper().RunAsync("tenant-1", "oid-first", "First Admin", Ct);

        Assert.Equal(0, exit);
        await using var context = _db.CreateContext();
        var user = await context.Users.Include(u => u.Roles).SingleAsync(u => u.ObjectId == "oid-first", Ct);
        Assert.Contains(user.Roles, r => r.RoleTypeCode == SystemRoles.Admin);
        Assert.True(await context.SecurityEvents.AnyAsync(e => e.Kind == (int)SecurityEventKind.AdminBootstrapped, Ct));
    }

    [Fact]
    public async Task BootstrapAdmin_RefusesWhenAnAdminExists()
    {
        await Bootstrapper().RunAsync("tenant-1", "oid-first", "First Admin", Ct);
        await using var before = _db.CreateContext();
        var counts = (await before.Users.CountAsync(Ct), await before.UserRoles.CountAsync(Ct), await before.SecurityEvents.CountAsync(Ct));

        var exit = await Bootstrapper().RunAsync("tenant-1", "oid-second", "Second Admin", Ct);

        Assert.Equal(2, exit);
        await using var after = _db.CreateContext();
        Assert.Equal(counts, (await after.Users.CountAsync(Ct), await after.UserRoles.CountAsync(Ct), await after.SecurityEvents.CountAsync(Ct)));
    }

    private AdminBootstrapper Bootstrapper() =>
        new(AppDatabase.Create(_db.OwnerConnectionString), TimeProvider.System, NullLoggerFactory.Instance);
}
