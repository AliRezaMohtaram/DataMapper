using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class TemplateFieldAliasConfiguration : IEntityTypeConfiguration<TemplateFieldAlias>
{
    public void Configure(EntityTypeBuilder<TemplateFieldAlias> builder)
    {
        builder.ToTable("TemplateFieldAlias");

        builder.ConfigureEntityBase();

        builder.Property(x => x.Alias)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.NormalizedAlias)
            .HasMaxLength(250)
            .IsRequired();

        builder.HasOne<TemplateField>()
            .WithMany()
            .HasForeignKey(x => x.TemplateFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TemplateFieldId, x.NormalizedAlias })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TemplateFieldAlias_Field_Alias");

        builder.HasIndex(x => x.NormalizedAlias)
            .HasDatabaseName("IX_TemplateFieldAlias_Normalized");
    }
}
