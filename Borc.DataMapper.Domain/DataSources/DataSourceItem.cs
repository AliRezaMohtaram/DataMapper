using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.DataSources;

/// <summary>یک گزینهٔ منبع داده (برای نوع StaticList و File). Value = مقدار واقعی ذخیره‌شده، Label = عنوان نمایشی.</summary>
public sealed class DataSourceItem : EntityBase
{
    private DataSourceItem()
    {
    }

    public long DataSourceId { get; private set; }

    public string Value { get; private set; } = null!;

    public string Label { get; private set; } = null!;

    public int SortOrder { get; private set; }

    public static DataSourceItem Create(long dataSourceId, string value, string? label, int sortOrder)
    {
        var v = value.Trim();

        return new DataSourceItem
        {
            DataSourceId = dataSourceId,
            Value = v,
            Label = string.IsNullOrWhiteSpace(label) ? v : label.Trim(),
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }
}
