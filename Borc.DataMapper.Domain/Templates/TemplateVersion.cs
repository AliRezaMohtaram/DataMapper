using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Templates;

public sealed class TemplateVersion : EntityBase
{
    private TemplateVersion()
    {
    }

    public long TemplateId { get; private set; }

    public int VersionNo { get; private set; }

    public TemplateVersionStatus Status { get; private set; }

    public string? SchemaJson { get; private set; }

    /// <summary>همواره برابر (Status == Published) نگه داشته می‌شود.</summary>
    public bool IsPublished { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public long? PublishedBy { get; private set; }

    public static TemplateVersion Create(
        long templateId,
        int versionNo,
        string? schemaJson = null)
    {
        return new TemplateVersion
        {
            TemplateId = templateId,
            VersionNo = versionNo,
            SchemaJson = schemaJson,
            Status = TemplateVersionStatus.Draft,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>نسخه منتشرشده تغییرناپذیر است؛ فقط Draft ویرایش می‌شود.</summary>
    public bool IsEditable => Status == TemplateVersionStatus.Draft;

    public void UpdateSchema(string? schemaJson)
    {
        EnsureEditable();

        SchemaJson = schemaJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish(long? userId = null)
    {
        if (Status != TemplateVersionStatus.Draft)
            throw new InvalidOperationException("فقط نسخه پیش‌نویس قابل انتشار است.");

        Status = TemplateVersionStatus.Published;
        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
        PublishedBy = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (Status == TemplateVersionStatus.Archived)
            throw new InvalidOperationException("این نسخه قبلاً بایگانی شده است.");

        Status = TemplateVersionStatus.Archived;
        IsPublished = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
            throw new InvalidOperationException("فقط نسخه پیش‌نویس قابل ویرایش است.");
    }
}