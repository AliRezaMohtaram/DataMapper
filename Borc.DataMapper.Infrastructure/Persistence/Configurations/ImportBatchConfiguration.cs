using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatch");

        builder.ConfigureEntityBase();

        builder.Property(x => x.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.FileHash)
            .HasMaxLength(128)
            .IsUnicode(false);

        builder.Property(x => x.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.StartedAt).HasPrecision(3);
        builder.Property(x => x.CompletedAt).HasPrecision(3);

        builder.HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(x => x.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MappingProfile>()
            .WithMany()
            .HasForeignKey(x => x.MappingProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TemplateVersionId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ImportBatch_TemplateVersion_Created");
    }
}
