using CslaStarter.Data.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CslaStarter.Integration.Tests.PostgresFixture))]

namespace CslaStarter.Integration.Tests;

/// <summary>
/// One PostgreSQL container per test run, holding a template database migrated to the current model.
/// <see cref="TestDatabase.CreateAsync"/> copies the template, so every test starts from an empty, migrated database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "cslastarter_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    // PostgreSQL refuses to copy a template while another session is connected to it, and each copy connects to it.
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteAdminAsync($"create database {TemplateDatabase}");

        await using (var services = new ServiceCollection().AddAppData(ConnectionString(TemplateDatabase)).BuildServiceProvider())
        {
            await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await context.Database.MigrateAsync();
        }

        // Close the pooled connections to the template, or the first copy fails.
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>The container's connection string, pointed at <paramref name="database"/>.</summary>
    public string ConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    /// <summary>Creates a new database as a copy of the migrated template and returns its name.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = "test_" + Guid.NewGuid().ToString("N")[..16];
        await _createLock.WaitAsync();
        try
        {
            await ExecuteAdminAsync($"create database {name} template {TemplateDatabase}");
        }
        finally
        {
            _createLock.Release();
        }

        return name;
    }

    /// <summary>Drops a test database, closing whatever connections still use it.</summary>
    public Task DropDatabaseAsync(string name) => ExecuteAdminAsync($"drop database if exists {name} with (force)");

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        _createLock.Dispose();
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
