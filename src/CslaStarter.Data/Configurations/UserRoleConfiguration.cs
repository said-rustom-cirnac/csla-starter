using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_role");
        builder.HasKey(r => new { r.UserId, r.RoleTypeCode });
        builder.HasOne<UserRoleTypeRow>().WithMany().HasForeignKey(r => r.RoleTypeCode).OnDelete(DeleteBehavior.Restrict);
    }
}
