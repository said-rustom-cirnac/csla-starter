using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CslaStarter.Data;

/// <summary>The one place the provider and naming are configured, shared by the app, tests and design-time tools.</summary>
public static class AppDbContextOptions
{
    public static DbContextOptionsBuilder UseAppDatabase(this DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

    public static DbContextOptionsBuilder UseAppDatabase(this DbContextOptionsBuilder builder, NpgsqlDataSource dataSource) =>
        builder.UseNpgsql(dataSource).UseSnakeCaseNamingConvention();
}
