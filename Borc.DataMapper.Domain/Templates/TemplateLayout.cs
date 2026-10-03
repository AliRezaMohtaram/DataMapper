using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Templates;

public sealed class TemplateLayout : EntityBase
{
    private TemplateLayout()
    {
    }

    public long TemplateVersionId { get; private set; }

    public int VersionNo { get; private set; }

    public string LayoutJson { get; private set; } = null!;

    public bool IsPublished { get; private set; }

    public static TemplateLayout Create(
        long templateVersionId,
        string layoutJson,
        int versionNo = 1)
    {
        return new TemplateLayout
        {
            TemplateVersionId = templateVersionId,
            VersionNo = versionNo,
            LayoutJson = layoutJson,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateLayout(string layoutJson)
    {
        LayoutJson = layoutJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        IsPublished = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        IsPublished = false;
        UpdatedAt = DateTime.UtcNow;
    }
}