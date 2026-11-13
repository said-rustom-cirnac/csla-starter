using CslaStarter.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CslaStarter.Integration.Tests;

public sealed class SchemaTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestDatabase _db = null!;

    public async ValueTask InitializeAsync() => _db = await TestDatabase.CreateAsync(postgres);

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Migrations_CreateCoreTables()
    {
        await using var connection = new NpgsqlConnection(_db.OwnerConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("select table_name from information_schema.tables where table_schema = 'public'", connection);
        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (var expected in new[] { "app_user", "user_role", "user_role_type", "audit_event", "audit_change", "security_event", "data_protection_keys" })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task UserRoleTypes_AreSeeded()
    {
        await using var context = _db.CreateContext();
        var types = await context.UserRoleTypes.OrderBy(t => t.Code).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], types.Select(t => t.Code));
        Assert.Equal("Administrateur", types[0].DescriptionFr);
    }

    [Fact]
    public async Task Concurrency_StaleVersion_Throws()
    {
        var id = await _db.AddUserAsync("Ada");
        var ct = TestContext.Current.CancellationToken;
        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var a = await first.Users.SingleAsync(u => u.Id == id, ct);
        var b = await second.Users.SingleAsync(u => u.Id == id, ct);

        a.DisplayName = "Ada L.";
        await first.SaveChangesAsync(ct);
        b.DisplayName = "Ada Lovelace";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task RuntimeRole_CannotAlterSchema()
    {
        await using var connection = new NpgsqlConnection(_db.RuntimeConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("create table sneaky(i int)", connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal("42501", error.SqlState);
    }

}
