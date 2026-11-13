using CslaStarter.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CslaStarter.Integration.Tests.PostgresFixture))]

namespace CslaStarter.Integration.Tests;

/// <summary>
/// One PostgreSQL container per test run, with the app's roles and a migrated template database. Each test
/// gets its own copy of the template (fast, isolated).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string OwnerPassword = "owner-test-password";
    public const string RuntimePassword = "runtime-test-password";
    private const string TemplateDatabase = "cslastarter_template";

    // Every test database has its own connection pools (one per role), and idle pooled connections stay open for a few
    // minutes, so a run holds far more than PostgreSQL's default 100 connections.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithCommand("-c", "max_connections=1000")
        .Build();
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public string AdminConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var script = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "sql", "create-roles.sql"));
        await _container.CopyAsync(script, "/tmp/create-roles.sql");
        var result = await _container.ExecAsync(
            ["psql", "-U", "postgres", "-v", $"owner_password={OwnerPassword}", "-v", $"runtime_password={RuntimePassword}", "-f", "/tmp/create-roles.sql"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("create-roles.sql failed: " + result.Stderr);
        }

        await ExecuteAdminAsync($"CREATE DATABASE {TemplateDatabase} OWNER app_owner");
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(ConnectionString(TemplateDatabase, "app_owner", OwnerPassword));
        await using (var context = new AppDbContext(options.Options))
        {
            await context.Database.MigrateAsync();
        }

        // A template database can only be copied while nobody is connected to it.
        NpgsqlConnection.ClearAllPools();
    }

    public string ConnectionString(string database, string user, string password) =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = database, Username = user, Password = password }.ConnectionString;

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "test_" + Guid.NewGuid().ToString("N")[..16];
        await _createLock.WaitAsync();
        try
        {
            await ExecuteAdminAsync($"CREATE DATABASE {name} TEMPLATE {TemplateDatabase} OWNER app_owner");
        }
        finally
        {
            _createLock.Release();
        }

        return name;
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        _createLock.Dispose();
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
