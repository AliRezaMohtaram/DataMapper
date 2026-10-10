using Borc.DataMapper.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

/// <summary>جدول در Scripts/010_ImportFiles.sql ساخته می‌شود.</summary>
public sealed class ImportBatchFileConfiguration : IEntityTypeConfiguration<ImportBatchFile>
{
    public void Configure(EntityTypeBuilder<ImportBatchFile> builder)
    {
        builder.ToTable("ImportBatchFile");

        builder.HasKey(x => x.ImportBatchId);
        builder.Property(x => x.ImportBatchId).ValueGeneratedNever();

        builder.Property(x => x.ContentType)
            .HasMaxLength(200)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Content).IsRequired();

        builder.Property(x => x.CreatedAt).HasPrecision(3);

        builder.HasOne<ImportBatch>()
            .WithOne()
            .HasForeignKey<ImportBatchFile>(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
