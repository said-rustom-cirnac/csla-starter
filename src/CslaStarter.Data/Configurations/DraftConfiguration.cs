using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Configurations;

internal sealed class DraftConfiguration : IEntityTypeConfiguration<DraftRow>
{
    public void Configure(EntityTypeBuilder<DraftRow> builder)
    {
        builder.ToTable("draft");
        builder.HasKey(d => new { d.UserId, d.EntityType, d.EntityKey });
        builder.Property(d => d.EntityType).HasMaxLength(100);
        builder.Property(d => d.EntityKey).HasMaxLength(200);
        builder.HasIndex(d => d.ExpiresAt);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
