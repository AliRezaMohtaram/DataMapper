using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class DataRecordConfiguration : IEntityTypeConfiguration<DataRecord>
{
    public void Configure(EntityTypeBuilder<DataRecord> builder)
    {
        builder.ToTable("DataRecord");

        builder.ConfigureEntityBase();

        builder.Property(x => x.DataJson).IsRequired();

        builder.Property(x => x.SourceType)
            .HasConversion<short>()
            .IsRequired();

        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(x => x.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ImportRow>()
            .WithMany()
            .HasForeignKey(x => x.ImportRowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TemplateVersionId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_DataRecord_TemplateVersion_Created");

        builder.HasIndex(x => x.ImportBatchId)
            .HasFilter("[ImportBatchId] IS NOT NULL")
            .HasDatabaseName("IX_DataRecord_ImportBatch");
    }
}
