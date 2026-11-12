using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEventRow>
{
    public void Configure(EntityTypeBuilder<AuditEventRow> builder)
    {
        builder.ToTable("audit_event");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityAlwaysColumn();
        builder.Property(e => e.UserName).HasMaxLength(200);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.EntityType).HasMaxLength(100);
        builder.Property(e => e.EntityKey).HasMaxLength(200);
        builder.Property(e => e.RootType).HasMaxLength(100);
        builder.Property(e => e.RootKey).HasMaxLength(200);
        builder.Property(e => e.Action).HasMaxLength(10);
        builder.HasIndex(e => new { e.EntityType, e.EntityKey });
        builder.HasIndex(e => new { e.RootType, e.RootKey });
        builder.HasMany(e => e.Changes).WithOne().HasForeignKey(c => c.AuditEventId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditChangeConfiguration : IEntityTypeConfiguration<AuditChangeRow>
{
    public void Configure(EntityTypeBuilder<AuditChangeRow> builder)
    {
        builder.ToTable("audit_change");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();
        builder.Property(c => c.Property).HasMaxLength(100);
        builder.Property(c => c.TypeCodeOf).HasMaxLength(100);
        builder.HasIndex(c => new { c.AuditEventId, c.Property });
    }
}

internal sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEventRow>
{
    public void Configure(EntityTypeBuilder<SecurityEventRow> builder)
    {
        builder.ToTable("security_event");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityAlwaysColumn();
        builder.Property(e => e.UserName).HasMaxLength(200);
        builder.Property(e => e.Detail).HasMaxLength(2000);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.HasIndex(e => e.OccurredAt);
    }
}
