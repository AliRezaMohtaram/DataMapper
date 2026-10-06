using Borc.DataMapper.Domain.DataSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class DataSourceItemConfiguration : IEntityTypeConfiguration<DataSourceItem>
{
    public void Configure(EntityTypeBuilder<DataSourceItem> builder)
    {
        builder.ToTable("DataSourceItem");

        builder.ConfigureEntityBase();

        builder.Property(x => x.Value)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Label)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(x => x.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.DataSourceId, x.Value })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_DataSourceItem_Source_Value");

        builder.HasIndex(x => new { x.DataSourceId, x.SortOrder })
            .HasDatabaseName("IX_DataSourceItem_Source_Sort");
    }
}
