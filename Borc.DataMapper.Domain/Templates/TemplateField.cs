using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Templates;

public sealed class TemplateField : EntityBase
{
    private TemplateField()
    {
    }

    public long TemplateVersionId { get; private set; }

    public string FieldKey { get; private set; } = null!;

    public string Label { get; private set; } = null!;

    public FieldDataType DataType { get; private set; }

    /// <summary>نوع ستون در سیستم مقصد (مثلاً VARCHAR2).</summary>
    public string DbType { get; private set; } = null!;

    public int? Length { get; private set; }

    public byte? Precision { get; private set; }

    public byte? Scale { get; private set; }

    public bool IsRequired { get; private set; }

    public string? Regex { get; private set; }

    public string? DefaultValue { get; private set; }

    public long? DataSourceId { get; private set; }

    public int SortOrder { get; private set; }

    public string? ConfigJson { get; private set; }

    public static TemplateField Create(
        long templateVersionId,
        string fieldKey,
        string label,
        FieldDataType dataType,
        string dbType,
        bool isRequired = false,
        int sortOrder = 0,
        int? length = null,
        byte? precision = null,
        byte? scale = null,
        string? regex = null,
        string? defaultValue = null,
        long? dataSourceId = null,
        string? configJson = null)
    {
        return new TemplateField
        {
            TemplateVersionId = templateVersionId,
            FieldKey = fieldKey.Trim(),
            Label = label.Trim(),
            DataType = dataType,
            DbType = dbType.Trim(),
            IsRequired = isRequired,
            SortOrder = sortOrder,
            Length = length,
            Precision = precision,
            Scale = scale,
            Regex = regex,
            DefaultValue = defaultValue,
            DataSourceId = dataSourceId,
            ConfigJson = configJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>FieldKey پس از ایجاد تغییر نمی‌کند.</summary>
    public void Update(
        string label,
        FieldDataType dataType,
        string dbType,
        bool isRequired,
        int sortOrder,
        int? length,
        byte? precision,
        byte? scale,
        string? regex,
        string? defaultValue,
        long? dataSourceId,
        string? configJson)
    {
        Label = label.Trim();
        DataType = dataType;
        DbType = dbType.Trim();
        IsRequired = isRequired;
        SortOrder = sortOrder;
        Length = length;
        Precision = precision;
        Scale = scale;
        Regex = regex;
        DefaultValue = defaultValue;
        DataSourceId = dataSourceId;
        ConfigJson = configJson;
        UpdatedAt = DateTime.UtcNow;
    }
}