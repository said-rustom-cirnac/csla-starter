using CslaStarter.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data;

/// <summary>The application's PostgreSQL database. Also stores the Data Protection keys shared by every instance.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserRoleTypeRow> UserRoleTypes => Set<UserRoleTypeRow>();

    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();

    public DbSet<AuditChangeRow> AuditChanges => Set<AuditChangeRow>();

    public DbSet<SecurityEventRow> SecurityEvents => Set<SecurityEventRow>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
