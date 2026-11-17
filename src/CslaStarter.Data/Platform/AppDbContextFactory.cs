using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CslaStarter.Data.Platform;

/// <summary>
/// Used only by <c>dotnet ef</c>. The connection string comes from <c>CSLASTARTER_DESIGN_CONNECTION</c>; adding a
/// migration never connects, so any valid connection string will do for that.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ConnectionVariable = "CSLASTARTER_DESIGN_CONNECTION";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionVariable} to a PostgreSQL connection string, for example \"Host=localhost;Database=cslastarter\".");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAppDatabase(connectionString);
        return new AppDbContext(options.Options);
    }
}
