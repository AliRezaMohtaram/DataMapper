using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

/// <summary>جدول در Scripts/011_TemplateUnits.sql ساخته می‌شود.</summary>
public sealed class TemplateOrgUnitConfiguration : IEntityTypeConfiguration<TemplateOrgUnit>
{
    public void Configure(EntityTypeBuilder<TemplateOrgUnit> builder)
    {
        builder.ToTable("TemplateOrgUnit");
        builder.HasKey(x => new { x.TemplateId, x.OrgUnitKey });
        builder.Property(x => x.OrgUnitKey).HasMaxLength(Borc.DataMapper.Domain.Common.IOrgUnitOwned.KeyMaxLength);
        builder.HasIndex(x => x.OrgUnitKey);
        builder.HasOne<Template>()
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
