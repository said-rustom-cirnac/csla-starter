using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Platform.Users;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever(); // Guid v7, set in code
        builder.Property(u => u.TenantId).HasMaxLength(100);
        builder.Property(u => u.ObjectId).HasMaxLength(100);
        builder.Property(u => u.DisplayName).HasMaxLength(200);
        builder.Property(u => u.Version).IsRowVersion(); // a uint row version is PostgreSQL's xmin
        builder.HasIndex(u => new { u.TenantId, u.ObjectId }).IsUnique();
    }
}
