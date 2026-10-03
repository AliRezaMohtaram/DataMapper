using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Templates;

public sealed class TemplateFieldAlias : EntityBase
{
    private TemplateFieldAlias()
    {
    }

    public long TemplateFieldId { get; private set; }

    public string Alias { get; private set; } = null!;

    /// <summary>خروجی TextNormalizer.NormalizeHeader؛ مبنای تطبیق سرستون‌ها.</summary>
    public string NormalizedAlias { get; private set; } = null!;

    public static TemplateFieldAlias Create(long templateFieldId, string alias)
    {
        return new TemplateFieldAlias
        {
            TemplateFieldId = templateFieldId,
            Alias = alias.Trim(),
            NormalizedAlias = TextNormalizer.NormalizeHeader(alias),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string alias)
    {
        Alias = alias.Trim();
        NormalizedAlias = TextNormalizer.NormalizeHeader(alias);
        UpdatedAt = DateTime.UtcNow;
    }
}