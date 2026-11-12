using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CslaStarter.Data;

/// <summary>Used by dotnet-ef only. Set CSLASTARTER_DESIGN_CONNECTION to point at a real database when needed.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CSLASTARTER_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=cslastarter;Username=app_owner";
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseAppDatabase(connectionString);
        return new AppDbContext(builder.Options);
    }
}
