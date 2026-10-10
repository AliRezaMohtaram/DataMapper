using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Mappings;

public sealed class MappingProfile : EntityBase, IOrgUnitOwned
{
    /// <summary>واحد سازمانی مالک (کلید چارت)؛ null = عمومی.</summary>
    public string? OrgUnitKey { get; private set; }

    public void AssignOrgUnit(string? orgUnitKey) =>
        OrgUnitKey = string.IsNullOrWhiteSpace(orgUnitKey) ? null : orgUnitKey.Trim();

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