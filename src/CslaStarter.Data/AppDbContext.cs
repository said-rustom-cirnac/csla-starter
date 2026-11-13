using CslaStarter.Core.Security;
using CslaStarter.Dal.Projects;
using CslaStarter.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CslaStarter.Data;

/// <summary>
/// The application's PostgreSQL database. Also stores the Data Protection keys shared by every instance.
/// <para>
/// Record-level access: [RecordScoped] entities get the named query filter <see cref="RecordAccessFilter"/>, built from
/// the <see cref="IDataAccessContext"/> this context was created for. Without one, scoped rows are invisible.
/// </para>
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IDataAccessContext? access = null)
    : DbContext(options), IDataProtectionKeyContext
{
    public const string RecordAccessFilter = "RecordAccess";

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserRoleTypeRow> UserRoleTypes => Set<UserRoleTypeRow>();

    public DbSet<ProjectRow> Projects => Set<ProjectRow>();

    public DbSet<ProjectTaskRow> ProjectTasks => Set<ProjectTaskRow>();

    public DbSet<ProjectStatusTypeRow> ProjectStatusTypes => Set<ProjectStatusTypeRow>();

    public DbSet<AuditEventRow> AuditEvents => Set<AuditEventRow>();

    public DbSet<AuditChangeRow> AuditChanges => Set<AuditChangeRow>();

    public DbSet<SecurityEventRow> SecurityEvents => Set<SecurityEventRow>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<DraftRow> Drafts => Set<DraftRow>();

    // Read by the query filters (EF turns them into query parameters for each query).
    private bool SeesAllProjects => access is not null && access.RoleCodes.Overlaps(ProjectAccess.SeesAllRoles);

    private Guid AccessUserId => access?.UserId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<ProjectRow>().HasQueryFilter(
            RecordAccessFilter, p => SeesAllProjects || p.Members.Any(m => m.UserId == AccessUserId));
        modelBuilder.Entity<ProjectTaskRow>().HasQueryFilter(
            RecordAccessFilter, t => SeesAllProjects || t.Project!.Members.Any(m => m.UserId == AccessUserId));

        // Membership rows carry no filter of their own: the project filter reads them, and a filter on both sides would
        // recurse forever. They are only reachable through a (filtered) project; there is deliberately no DbSet for them.
    }
}
