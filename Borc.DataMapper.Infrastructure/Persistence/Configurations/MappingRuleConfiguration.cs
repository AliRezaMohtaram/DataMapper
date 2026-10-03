using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class MappingRuleConfiguration : IEntityTypeConfiguration<MappingRule>
{
    public void Configure(EntityTypeBuilder<MappingRule> builder)
    {
        builder.ToTable("MappingRule");

        builder.ConfigureEntityBase();

        builder.Property(x => x.SourceColumn)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.NormalizedSourceColumn)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.MappingMethod)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasPrecision(5, 2);

        builder.HasOne<MappingProfile>()
            .WithMany()
            .HasForeignKey(x => x.MappingProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TemplateField>()
            .WithMany()
            .HasForeignKey(x => x.TargetFieldId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MappingProfileId, x.SourceColumn })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_MappingRule_Profile_Source");

        builder.HasIndex(x => x.MappingProfileId)
            .HasDatabaseName("IX_MappingRule_Profile");

        builder.HasIndex(x => x.TargetFieldId)
            .HasDatabaseName("IX_MappingRule_TargetField");
    }
}
