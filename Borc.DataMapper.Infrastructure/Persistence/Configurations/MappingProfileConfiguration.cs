using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class MappingProfileConfiguration : IEntityTypeConfiguration<MappingProfile>
{
    public void Configure(EntityTypeBuilder<MappingProfile> builder)
    {
        builder.ToTable("MappingProfile");

        builder.ConfigureEntityBase();

        builder.Property(x => x.Name)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.SourceType)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.HasOne<TemplateVersion>()
            .WithMany()
            .HasForeignKey(x => x.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TemplateVersionId, x.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_MappingProfile_TemplateVersion_Name");

        builder.HasIndex(x => x.TemplateVersionId)
            .HasDatabaseName("IX_MappingProfile_TemplateVersion");
    }
}
