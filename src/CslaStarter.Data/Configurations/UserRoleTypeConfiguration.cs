using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Configurations;

internal sealed class UserRoleTypeConfiguration : IEntityTypeConfiguration<UserRoleTypeRow>
{
    public void Configure(EntityTypeBuilder<UserRoleTypeRow> builder)
    {
        builder.ToTable("user_role_type");
        builder.HasKey(t => t.Code);
        builder.Property(t => t.Code).ValueGeneratedNever();
        builder.Property(t => t.DescriptionEn).HasMaxLength(100);
        builder.Property(t => t.DescriptionFr).HasMaxLength(100);
        builder.HasData(
            new UserRoleTypeRow { Code = 1, DescriptionEn = "Administrator", DescriptionFr = "Administrateur", IsActive = true, SortOrder = 1 },
            new UserRoleTypeRow { Code = 2, DescriptionEn = "Client", DescriptionFr = "Client", IsActive = true, SortOrder = 2 },
            new UserRoleTypeRow { Code = 3, DescriptionEn = "Technical", DescriptionFr = "Technique", IsActive = true, SortOrder = 3 });
    }
}
