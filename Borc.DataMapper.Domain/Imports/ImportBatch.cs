using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Imports;

public sealed class ImportBatch : EntityBase, IOrgUnitOwned
{
    /// <summary>واحد سازمانی مالک (کلید چارت)؛ null = عمومی.</summary>
    public string? OrgUnitKey { get; private set; }

    public void AssignOrgUnit(string? orgUnitKey) =>
        OrgUnitKey = string.IsNullOrWhiteSpace(orgUnitKey) ? null : orgUnitKey.Trim();

    private ImportBatch()
    {
    }

    public long TemplateVersionId { get; private set; }

    public long? MappingProfileId { get; private set; }

    public string FileName { get; private set; } = null!;

    public string? FileHash { get; private set; }

    public long? FileSize { get; private set; }

    public int TotalRows { get; private set; }

    public int ValidRows { get; private set; }

    public int InvalidRows { get; private set; }

    public ImportBatchStatus Status { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public static ImportBatch Create(
        long templateVersionId,
        string fileName,
        string? fileHash = null,
        long? fileSize = null,
        long? mappingProfileId = null)
    {
        return new ImportBatch
        {
            TemplateVersionId = templateVersionId,
            MappingProfileId = mappingProfileId,
            FileName = fileName.Trim(),
            FileHash = fileHash,
            FileSize = fileSize,
            Status = ImportBatchStatus.Uploaded,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AssignProfile(long? mappingProfileId)
    {
        Require(Status == ImportBatchStatus.Uploaded, "پروفایل نگاشت فقط پیش از شروع اعتبارسنجی قابل تغییر است.");

        MappingProfileId = mappingProfileId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartValidation()
    {
        Require(Status == ImportBatchStatus.Uploaded, "اعتبارسنجی فقط برای ایمپورت آپلودشده شروع می‌شود.");

        Status = ImportBatchStatus.Validating;
        StartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteValidation(int totalRows, int validRows, int invalidRows)
    {
        Require(Status == ImportBatchStatus.Validating, "اعتبارسنجی در جریان نیست.");

        TotalRows = totalRows;
        ValidRows = validRows;
        InvalidRows = invalidRows;
        Status = ImportBatchStatus.Validated;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkImported()
    {
        Require(Status == ImportBatchStatus.Validated, "فقط ایمپورت اعتبارسنجی‌شده قابل ثبت نهایی است.");

        Status = ImportBatchStatus.Imported;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Require(
            Status is not ImportBatchStatus.Imported and not ImportBatchStatus.Failed,
            "ایمپورت پایان‌یافته را نمی‌توان ناموفق کرد.");

        Status = ImportBatchStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRowCount(int totalRows)
    {
        Require(Status == ImportBatchStatus.Uploaded, "تعداد سطرها فقط پیش از اعتبارسنجی قابل تنظیم است.");

        TotalRows = totalRows;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>بازگشت از «اعتبارسنجی‌شده» به «آپلودشده» برای تغییر تطبیق ستون‌ها.</summary>
    public void ReopenForMapping()
    {
        Require(Status == ImportBatchStatus.Validated, "فقط ایمپورت اعتبارسنجی‌شده را می‌توان دوباره تطبیق داد.");

        Status = ImportBatchStatus.Uploaded;
        ValidRows = 0;
        InvalidRows = 0;
        StartedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}