using CslaStarter.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<ProjectRow>
{
    public void Configure(EntityTypeBuilder<ProjectRow> builder)
    {
        builder.ToTable("project");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever(); // Guid v7 from the app
        builder.Property(p => p.Code).HasMaxLength(20);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Budget).HasPrecision(14, 2);
        builder.Property(p => p.CreatedBy).HasMaxLength(200);
        builder.Property(p => p.ModifiedBy).HasMaxLength(200);
        builder.Property(p => p.Version).IsRowVersion();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasOne<ProjectStatusTypeRow>().WithMany().HasForeignKey(p => p.StatusCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Tasks).WithOne(t => t.Project).HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.ClientCascade);
        builder.HasMany(p => p.Members).WithOne(m => m.Project).HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.ClientCascade);
    }
}

internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTaskRow>
{
    public void Configure(EntityTypeBuilder<ProjectTaskRow> builder)
    {
        builder.ToTable("project_task");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever(); // Guid v7 from the app: tasks added to a loaded project are inserts
        builder.Property(t => t.Title).HasMaxLength(200);
        builder.HasIndex(t => new { t.ProjectId, t.SortOrder });
    }
}

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMemberRow>
{
    public void Configure(EntityTypeBuilder<ProjectMemberRow> builder)
    {
        builder.ToTable("project_member");
        builder.HasKey(m => new { m.ProjectId, m.UserId });
        builder.HasIndex(m => m.UserId);
        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProjectStatusTypeConfiguration : IEntityTypeConfiguration<ProjectStatusTypeRow>
{
    public void Configure(EntityTypeBuilder<ProjectStatusTypeRow> builder)
    {
        builder.ToTable("project_status_type");
        builder.HasKey(t => t.Code);
        builder.Property(t => t.Code).ValueGeneratedNever();
        builder.Property(t => t.DescriptionEn).HasMaxLength(100);
        builder.Property(t => t.DescriptionFr).HasMaxLength(100);
        builder.HasData(
            new ProjectStatusTypeRow { Code = 1, DescriptionEn = "Planned", DescriptionFr = "Planifié", IsActive = true, SortOrder = 1 },
            new ProjectStatusTypeRow { Code = 2, DescriptionEn = "Active", DescriptionFr = "Actif", IsActive = true, SortOrder = 2 },
            new ProjectStatusTypeRow { Code = 3, DescriptionEn = "On hold", DescriptionFr = "En suspens", IsActive = true, SortOrder = 3 },
            new ProjectStatusTypeRow { Code = 4, DescriptionEn = "Closed", DescriptionFr = "Fermé", IsActive = true, SortOrder = 4 });
    }
}
