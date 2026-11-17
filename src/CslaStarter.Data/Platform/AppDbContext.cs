using CslaStarter.Data.Platform.Texts;
using CslaStarter.Data.Platform.Users;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data.Platform;

/// <summary>The application's PostgreSQL database. Each entity's shape is in its <c>IEntityTypeConfiguration</c>, next to it.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<TranslationRow> Translations => Set<TranslationRow>();

    public DbSet<ErrorMessageRow> ErrorMessages => Set<ErrorMessageRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
