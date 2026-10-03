using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class TemplateFieldConfiguration : IEntityTypeConfiguration<TemplateField>
{
    public void Configure(EntityTypeBuilder<TemplateField> builder)
    {
        builder.ToTable("TemplateField");

        builder.ConfigureEntityBase();

        builder.Property(x => x.FieldKey)
            .HasMaxLength(200)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Label)
            .HasMaxLength(250)
            .IsRequired();

        // در دیتابیس متن حروف کوچک است (CHECK: text, number, date, datetime, any)
        builder.Property(x => x.DataType)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<FieldDataType>(v, true))
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.DbType)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Regex).HasMaxLength(1000);
        builder.Property(x => x.DefaultValue).HasMaxLength(2000);

        builder.HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(x => x.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(x => x.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TemplateVersionId, x.FieldKey })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TemplateField_Version_Key");

        builder.HasIndex(x => new { x.TemplateVersionId, x.SortOrder })
            .HasDatabaseName("IX_TemplateField_Version_Sort");

        builder.HasIndex(x => x.DataSourceId)
            .HasFilter("[DataSourceId] IS NOT NULL")
            .HasDatabaseName("IX_TemplateField_DataSource");
    }
}
