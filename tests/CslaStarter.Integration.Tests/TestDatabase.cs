using CslaStarter.Core.Security;
using CslaStarter.Data;
using CslaStarter.Data.Auditing;
using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Integration.Tests;

/// <summary>A fresh, migrated database for one test, reachable as app_owner and as app_runtime.</summary>
public sealed class TestDatabase
{
    private TestDatabase(PostgresFixture postgres, string name)
    {
        Name = name;
        OwnerConnectionString = postgres.ConnectionString(name, "app_owner", PostgresFixture.OwnerPassword);
        RuntimeConnectionString = postgres.ConnectionString(name, "app_runtime", PostgresFixture.RuntimePassword);
    }

    public string Name { get; }

    public string OwnerConnectionString { get; }

    public string RuntimeConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync(PostgresFixture postgres) =>
        new(postgres, await postgres.CreateDatabaseAsync());

    /// <summary>A context as the app sees it: connected as app_runtime unless <paramref name="asOwner"/>.</summary>
    public AppDbContext CreateContext(IDataAccessContext? access = null, TimeProvider? time = null, bool asOwner = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(asOwner ? OwnerConnectionString : RuntimeConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(access ?? FakeDataAccessContext.Tester, time ?? TimeProvider.System));
        return new AppDbContext(options.Options);
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
