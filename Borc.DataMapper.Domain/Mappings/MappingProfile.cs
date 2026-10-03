using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Mappings;

public sealed class MappingProfile : EntityBase
{
    private MappingProfile()
    {
    }

    public string Name { get; private set; } = null!;

    public long TemplateVersionId { get; private set; }

    public ImportSourceType SourceType { get; private set; }

    public MappingProfileStatus Status { get; private set; }

    public static MappingProfile Create(
        string name,
        long templateVersionId,
        ImportSourceType sourceType = ImportSourceType.Excel)
    {
        return new MappingProfile
        {
            Name = name.Trim(),
            TemplateVersionId = templateVersionId,
            SourceType = sourceType,
            Status = MappingProfileStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, ImportSourceType sourceType)
    {
        Name = name.Trim();
        SourceType = sourceType;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = MappingProfileStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = MappingProfileStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }
}