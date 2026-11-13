using System.Globalization;
using System.Security.Claims;
using CslaStarter.Core.Realtime;
using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Entities;
using CslaStarter.Data.Security;
using CslaStarter.Infrastructure.Identity;
using CslaStarter.Infrastructure.Realtime;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CslaStarter.Integration.Tests.Realtime;

/// <summary>An access-changed notice reaches every instance: caches forget the user and open circuits revalidate.</summary>
public sealed class UserAccessTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;
    private AppDatabase _database = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDatabase.CreateAsync(postgres);
        _database = AppDatabase.Create(_db.RuntimeConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _database.DataSource.DisposeAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task UserAccess_Notice_ClearsDirectoryCacheOnEveryInstance()
    {
        var aHub = new EntityChangeHub(NullLogger<EntityChangeHub>.Instance);
        var bHub = new EntityChangeHub(NullLogger<EntityChangeHub>.Instance);
        var a = Backplane(aHub);
        var b = Backplane(bHub);
        await a.StartAsync(Ct);
        await b.StartAsync(Ct);
        using var bDirectory = Directory(bHub);
        try
        {
            await Eventually.True(() => a.IsListening && b.IsListening);
            var before = await bDirectory.GetOrProvisionAsync("tenant-1", "oid-ada", "Ada", null, Ct);
            Assert.Empty(before.RoleCodes);

            await using (var context = _db.CreateContext())
            {
                context.UserRoles.Add(new UserRole { UserId = before.Id, RoleTypeCode = 1 });
                await context.SaveChangesAsync(Ct);
            }

            await a.PublishAsync([EntityChange.UserAccess(before.Id, null, "Admin", null)], Ct);

            await Eventually.True(() => bDirectory.GetOrProvisionAsync("tenant-1", "oid-ada", "Ada", null, Ct).Result.RoleCodes.Contains(1));
        }
        finally
        {
            await a.StopAsync(CancellationToken.None);
            await b.StopAsync(CancellationToken.None);
            a.Dispose();
            b.Dispose();
        }
    }

    [Fact]
    public async Task UserAccess_Notice_RevalidatesTheCircuitAtOnce()
    {
        var hub = new EntityChangeHub(NullLogger<EntityChangeHub>.Instance);
        var userId = await _db.AddUserAsync("Ada", "oid-ada", true, 2);
        await using var services = new ServiceCollection()
            .AddSingleton<IUserDirectory>(Directory(hub))
            .BuildServiceProvider();
        using var provider = new UserRevalidatingAuthenticationStateProvider(NullLoggerFactory.Instance, services.GetRequiredService<IServiceScopeFactory>(), hub);
        provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(Principal(userId, 2))));
        await Eventually.True(() => provider.IsWatchingAccess);

        await hub.DispatchAsync(EntityChange.UserAccess(Guid.CreateVersion7(), null, "Admin", null));
        Assert.True((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);

        await using (var context = _db.CreateContext())
        {
            context.UserRoles.Add(new UserRole { UserId = userId, RoleTypeCode = 1 });
            await context.SaveChangesAsync(Ct);
        }

        await hub.DispatchAsync(EntityChange.UserAccess(userId, null, "Admin", null));

        await Eventually.True(() => provider.GetAuthenticationStateAsync().Result.User.Identity?.IsAuthenticated != true);
    }

    private PostgresChangeBackplane Backplane(EntityChangeHub hub) =>
        new(_database.DataSource, hub, TimeProvider.System, NullLogger<PostgresChangeBackplane>.Instance);

    private UserDirectory Directory(IEntityChangeHub hub) => new(
        _database,
        new MemoryCache(new MemoryCacheOptions()),
        TimeProvider.System,
        new SecurityEventLog(_database, TimeProvider.System, NullLogger<SecurityEventLog>.Instance),
        hub);

    private static ClaimsPrincipal Principal(Guid userId, params int[] roles)
    {
        var claims = new List<Claim> { new(AppClaimTypes.UserId, userId.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString(CultureInfo.InvariantCulture))));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
