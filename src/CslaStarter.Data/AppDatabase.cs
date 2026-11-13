using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CslaStarter.Data;

/// <summary>
/// The application's database: one shared <see cref="NpgsqlDataSource"/> (the connection pool, and on Azure the Entra
/// token authentication). Everything that needs its own short-lived context creates it here.
/// </summary>
public sealed class AppDatabase(NpgsqlDataSource dataSource)
{
    public NpgsqlDataSource DataSource { get; } = dataSource;

    public static AppDatabase Create(string connectionString, Action<NpgsqlDataSourceBuilder>? configure = null)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        configure?.Invoke(builder);
        return new AppDatabase(builder.Build());
    }

    public AppDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(DataSource).AddInterceptors(interceptors);
        return new AppDbContext(options.Options);
    }
}
