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

    /// <summary>Column mapping the background validation applies: JSON object { "source header": "FIELD_KEY" }.</summary>
    public string? MappingJson { get; private set; }

    /// <summary>Rows processed by the running background step (validation or commit), for the progress bar.</summary>
    public int ProcessedRows { get; private set; }

    /// <summary>Why the last background step failed (shown on the import page); cleared when a step starts.</summary>
    public string? LastError { get; private set; }

    /// <summary>A background step is queued or running.</summary>
    public bool IsBusy => Status is ImportBatchStatus.Validating or ImportBatchStatus.Committing;

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

    /// <summary>Queues the validation of every row with this column mapping (runs in the background).</summary>
    public void StartValidation(string mappingJson)
    {
        Require(Status == ImportBatchStatus.Uploaded, "اعتبارسنجی فقط برای ایمپورت آپلودشده شروع می‌شود.");

        MappingJson = mappingJson;
        Status = ImportBatchStatus.Validating;
        ProcessedRows = 0;
        LastError = null;
        StartedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReportProgress(int processedRows)
    {
        ProcessedRows = processedRows;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Validation could not finish: back to "uploaded" so the user can try again.</summary>
    public void FailValidation(string error)
    {
        Require(Status == ImportBatchStatus.Validating, "اعتبارسنجی در جریان نیست.");

        Status = ImportBatchStatus.Uploaded;
        LastError = Trim(error);
        ProcessedRows = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Queues the commit of the valid rows (runs in the background).</summary>
    public void StartCommit()
    {
        Require(Status == ImportBatchStatus.Validated, "فقط ایمپورت اعتبارسنجی‌شده قابل ثبت نهایی است.");

        Status = ImportBatchStatus.Committing;
        ProcessedRows = 0;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The commit stopped: back to "validated". Rows already turned into records stay (each chunk is saved with its
    /// records); committing again continues with the rows left.
    /// </summary>
    public void FailCommit(string error)
    {
        Require(Status == ImportBatchStatus.Committing, "ثبت نهایی در جریان نیست.");

        Status = ImportBatchStatus.Validated;
        LastError = Trim(error);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string Trim(string error) => error.Length <= 1000 ? error : error[..1000];

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
        Require(Status == ImportBatchStatus.Committing, "ثبت نهایی در جریان نیست.");

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
        ProcessedRows = 0;
        LastError = null;
        StartedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}