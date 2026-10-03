using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class TemplateLayoutConfiguration : IEntityTypeConfiguration<TemplateLayout>
{
    public void Configure(EntityTypeBuilder<TemplateLayout> builder)
    {
        builder.ToTable("TemplateLayout");

        builder.ConfigureEntityBase();

        builder.Property(x => x.LayoutJson).IsRequired();

        builder.HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(x => x.TemplateVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TemplateVersionId, x.VersionNo })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TemplateLayout_Version");
    }
}
