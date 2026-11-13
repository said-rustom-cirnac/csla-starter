using CslaStarter.Core.Realtime;
using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Auditing;
using CslaStarter.Data.Entities;
using CslaStarter.Data.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CslaStarter.Integration.Tests;

/// <summary>
/// A fresh, migrated database for one test, reachable as app_owner and as app_runtime. Dispose it when the test ends:
/// the copies would otherwise fill the container's disk.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly PostgresFixture _postgres;

    private TestDatabase(PostgresFixture postgres, string name)
    {
        _postgres = postgres;
        Name = name;
        OwnerConnectionString = postgres.ConnectionString(name, "app_owner", PostgresFixture.OwnerPassword);
        RuntimeConnectionString = postgres.ConnectionString(name, "app_runtime", PostgresFixture.RuntimePassword);
    }

    public string Name { get; }

    public string OwnerConnectionString { get; }

    public string RuntimeConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync(PostgresFixture postgres) =>
        new(postgres, await postgres.CreateDatabaseAsync());

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearPool(new NpgsqlConnection(OwnerConnectionString));
        NpgsqlConnection.ClearPool(new NpgsqlConnection(RuntimeConnectionString));
        await _postgres.DropDatabaseAsync(Name);
    }

    /// <summary>
    /// A context as the app sees it: connected as app_runtime unless <paramref name="asOwner"/>, publishing committed
    /// changes to <paramref name="notifier"/> when one is given.
    /// </summary>
    public AppDbContext CreateContext(IDataAccessContext? access = null, TimeProvider? time = null, bool asOwner = false, IEntityChangeNotifier? notifier = null)
    {
        access ??= FakeDataAccessContext.Tester;
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(asOwner ? OwnerConnectionString : RuntimeConnectionString)
            .AddInterceptors(
                new AuditSaveChangesInterceptor(access, time ?? TimeProvider.System),
                new ChangePublishingInterceptor(access, NullLogger<ChangePublishingInterceptor>.Instance, notifier));
        return new AppDbContext(options.Options, access);
    }

    public Task<Guid> AddUserAsync(string displayName, params int[] roleCodes) =>
        AddUserAsync(displayName, Guid.NewGuid().ToString(), true, roleCodes);

    public async Task<Guid> AddUserAsync(string displayName, string objectId, bool isActive, params int[] roleCodes)
    {
        await using var context = CreateContext();
        var user = new AppUser { TenantId = "tenant-1", ObjectId = objectId, DisplayName = displayName, IsActive = isActive };
        foreach (var code in roleCodes)
        {
            user.Roles.Add(new UserRole { RoleTypeCode = code });
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }
}
