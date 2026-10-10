namespace Borc.DataMapper.Domain.Imports;

/// <summary>
/// فایل اصلیِ آپلودشدهٔ یک ایمپورت (بایت‌به‌بایت)، جدا از ImportBatch تا خواندن فهرست و جزئیات، محتوای فایل را بار نکند.
/// با حذف منطقی ایمپورت می‌ماند. دسترسی فقط از راه ایمپورتی که کاربر می‌بیند.
/// </summary>
public sealed class ImportBatchFile
{
    private ImportBatchFile()
    {
    }

    public long ImportBatchId { get; private set; }

    public string ContentType { get; private set; } = null!;

    public byte[] Content { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public static ImportBatchFile Create(long importBatchId, string contentType, byte[] content)
    {
        return new ImportBatchFile
        {
            ImportBatchId = importBatchId,
            ContentType = contentType,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
    }
}
