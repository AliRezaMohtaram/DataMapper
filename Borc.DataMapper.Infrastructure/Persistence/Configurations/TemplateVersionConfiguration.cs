using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("TemplateVersion");

        builder.ConfigureEntityBase();

        builder.Property(x => x.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.PublishedAt).HasPrecision(3);

        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TemplateId, x.VersionNo })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TemplateVersion_Template_Version");
    }
}
