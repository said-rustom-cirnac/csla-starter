using System.Globalization;
using System.Text.RegularExpressions;
using CslaStarter.Data.Platform;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CslaStarter.Integration.Tests;

/// <summary>
/// A fresh copy of the migrated template database for one test, with the app's data services over it and plain SQL
/// helpers for arranging and checking rows. Create it in the test class's <c>InitializeAsync</c> (xUnit makes a new
/// class instance per test) and dispose it in <c>DisposeAsync</c>, which drops the database.
/// </summary>
public sealed partial class TestDatabase : IAsyncDisposable
{
    private readonly PostgresFixture _postgres;
    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    private TestDatabase(PostgresFixture postgres, string name)
    {
        _postgres = postgres;
        Name = name;
        ConnectionString = postgres.ConnectionString(name);
        _root = new ServiceCollection().AddAppData(ConnectionString).BuildServiceProvider(validateScopes: true);
        _scope = _root.CreateAsyncScope();
    }

    public string Name { get; }

    public string ConnectionString { get; }

    /// <summary>The services <c>AddAppData</c> registers, resolved inside one scope that lasts as long as the test.</summary>
    public IServiceProvider Services => _scope.ServiceProvider;

    public static async Task<TestDatabase> CreateAsync(PostgresFixture postgres) =>
        new(postgres, await postgres.CreateDatabaseAsync());

    /// <summary>The names of the tables in the public schema.</summary>
    public async Task<HashSet<string>> TableNamesAsync()
    {
        await using var connection = await OpenAsync();
        await using var command = Command(connection, "select table_name from information_schema.tables where table_schema = 'public'", []);
        await using var reader = await command.ExecuteReaderAsync(Token);
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(Token))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>How many rows <paramref name="table"/> holds, or how many match <paramref name="where"/> (plain SQL).</summary>
    public async Task<int> CountAsync(string table, string? where = null) =>
        await ScalarAsync<int>($"select count(*)::int from {table}" + (where is null ? "" : $" where {where}"));

    /// <summary>
    /// Runs <paramref name="sql"/>. Each distinct <c>@name</c> in the SQL takes the next of <paramref name="args"/>, in the
    /// order the names first appear.
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, params object?[] args)
    {
        await using var connection = await OpenAsync();
        await using var command = Command(connection, sql, args);
        return await command.ExecuteNonQueryAsync(Token);
    }

    /// <summary>The first column of the first row, or <c>default</c> when there is none or it is null. Parameters as in <see cref="ExecuteAsync"/>.</summary>
    public async Task<T?> ScalarAsync<T>(string sql, params object?[] args)
    {
        await using var connection = await OpenAsync();
        await using var command = Command(connection, sql, args);
        var value = await command.ExecuteScalarAsync(Token);
        return value switch
        {
            null or DBNull => default,
            T typed => typed,
            _ => (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T), CultureInfo.InvariantCulture),
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
        await using (var pooled = new NpgsqlConnection(ConnectionString))
        {
            NpgsqlConnection.ClearPool(pooled);
        }

        await _postgres.DropDatabaseAsync(Name);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(Token);
        return connection;
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, string sql, object?[] args)
    {
        var names = ParameterName().Matches(sql).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
        if (names.Count != args.Length)
        {
            throw new ArgumentException($"The SQL names {names.Count} parameters but {args.Length} values were given.", nameof(args));
        }

        var command = new NpgsqlCommand(sql, connection);
        for (var i = 0; i < names.Count; i++)
        {
            command.Parameters.AddWithValue(names[i], args[i] ?? DBNull.Value);
        }

        return command;
    }

    [GeneratedRegex(@"(?<![@\w])@(\w+)")]
    private static partial Regex ParameterName();
}
