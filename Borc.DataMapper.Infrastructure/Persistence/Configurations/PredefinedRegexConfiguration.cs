using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Borc.DataMapper.Infrastructure.Persistence.Configurations;

public sealed class PredefinedRegexConfiguration : IEntityTypeConfiguration<PredefinedRegex>
{
    public void Configure(EntityTypeBuilder<PredefinedRegex> builder)
    {
        builder.ToTable("PredefinedRegex");

        // بدون EntityBase: کلید INT و بدون audit / حذف منطقی
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Code)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Pattern)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UQ_PredefinedRegex_Code");
    }
}
