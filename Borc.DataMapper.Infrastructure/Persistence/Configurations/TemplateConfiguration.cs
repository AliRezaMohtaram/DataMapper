using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration
    : IEntityTypeConfiguration<Template>
{
    public void Configure(
        EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("Template");

        builder.ConfigureEntityBase();

        builder.Property(x => x.Code)
            .HasMaxLength(50)
            .IsUnicode(false)   // ستون در دیتابیس VARCHAR است
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<short>()   // ستون SMALLINT است
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_Template_Code");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Template_Status");
    }
}