using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.DataSources;

public sealed class DataSource : EntityBase
{
    private DataSource()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public DataSourceType SourceType { get; private set; }

    public string? ConfigJson { get; private set; }

    public bool IsActive { get; private set; }

    public static DataSource Create(
        string code,
        string name,
        DataSourceType sourceType,
        string? configJson = null)
    {
        return new DataSource
        {
            Code = code.Trim(),
            Name = name.Trim(),
            SourceType = sourceType,
            ConfigJson = configJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Code پس از ایجاد تغییر نمی‌کند.</summary>
    public void Update(string name, DataSourceType sourceType, string? configJson)
    {
        Name = name.Trim();
        SourceType = sourceType;
        ConfigJson = configJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}