using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.TenantId).HasMaxLength(64);
        builder.Property(u => u.ObjectId).HasMaxLength(64);
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.PreferredLanguage).HasMaxLength(5);
        builder.Property(u => u.EmployeeNumber).HasMaxLength(20);
        builder.Property(u => u.CreatedBy).HasMaxLength(200);
        builder.Property(u => u.ModifiedBy).HasMaxLength(200);
        builder.Property(u => u.Version).IsRowVersion();
        builder.HasIndex(u => new { u.TenantId, u.ObjectId }).IsUnique();
        // ClientCascade: EF deletes role rows itself (and audits them); the database never cascades behind its back.
        builder.HasMany(u => u.Roles).WithOne(r => r.User).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.ClientCascade);
    }
}
