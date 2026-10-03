using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Mappings;

public sealed class MappingRule : EntityBase
{
    private MappingRule()
    {
    }

    public long MappingProfileId { get; private set; }

    public string SourceColumn { get; private set; } = null!;

    public string NormalizedSourceColumn { get; private set; } = null!;

    public long TargetFieldId { get; private set; }

    public MappingMethod MappingMethod { get; private set; }

    /// <summary>0 تا 100؛ برای نگاشت‌های خودکار.</summary>
    public decimal? Confidence { get; private set; }

    public string? TransformJson { get; private set; }

    public string? ValidationJson { get; private set; }

    public int SortOrder { get; private set; }

    public static MappingRule Create(
        long mappingProfileId,
        string sourceColumn,
        long targetFieldId,
        MappingMethod mappingMethod,
        decimal? confidence = null,
        string? transformJson = null,
        string? validationJson = null,
        int sortOrder = 0)
    {
        return new MappingRule
        {
            MappingProfileId = mappingProfileId,
            SourceColumn = sourceColumn.Trim(),
            NormalizedSourceColumn =    TextNormalizer.NormalizeHeader(sourceColumn),
            TargetFieldId = targetFieldId,
            MappingMethod = mappingMethod,
            Confidence = confidence,
            TransformJson = transformJson,
            ValidationJson = validationJson,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        long targetFieldId,
        MappingMethod mappingMethod,
        decimal? confidence,
        string? transformJson,
        string? validationJson,
        int sortOrder)
    {
        TargetFieldId = targetFieldId;
        MappingMethod = mappingMethod;
        Confidence = confidence;
        TransformJson = transformJson;
        ValidationJson = validationJson;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}