using Borc.DataMapper.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("ImportRow");

        builder.ConfigureEntityBase();

        builder.Property(x => x.RawDataJson).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ImportBatchId, x.RowNumber })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ImportRow_Batch_Row");

        builder.HasIndex(x => new { x.ImportBatchId, x.Status })
            .HasDatabaseName("IX_ImportRow_Batch_Status");
    }
}
