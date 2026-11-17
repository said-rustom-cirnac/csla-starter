using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CslaStarter.Data.Platform.Texts;

internal sealed class TextConfiguration : IEntityTypeConfiguration<TranslationRow>, IEntityTypeConfiguration<ErrorMessageRow>
{
    private const int SourceStringLength = 500;
    private const int DescriptionLength = 2000;

    public void Configure(EntityTypeBuilder<TranslationRow> builder)
    {
        builder.ToTable("translation_type");
        builder.HasKey(t => t.Code);
        builder.Property(t => t.Code).UseIdentityByDefaultColumn();
        builder.Property(t => t.SourceString).HasMaxLength(SourceStringLength);
        builder.Property(t => t.EnDesc).HasMaxLength(DescriptionLength);
        builder.Property(t => t.FrDesc).HasMaxLength(DescriptionLength);
        builder.Property(t => t.AddedDate).HasDefaultValueSql("now()");
        builder.HasIndex(t => t.SourceString).IsUnique();
    }

    public void Configure(EntityTypeBuilder<ErrorMessageRow> builder)
    {
        builder.ToTable("error_message_type");
        builder.HasKey(e => e.Code);
        builder.Property(e => e.Code).ValueGeneratedNever();
        builder.Property(e => e.EnDesc).HasMaxLength(DescriptionLength);
        builder.Property(e => e.FrDesc).HasMaxLength(DescriptionLength);
        builder.Property(e => e.AddedDate).HasDefaultValueSql("now()");
    }
}
