using Borc.DataMapper.Domain.Common;

namespace Borc.DataMapper.Domain.Imports;

public sealed class ImportRow : EntityBase
{
    private ImportRow()
    {
    }

    public long ImportBatchId { get; private set; }

    public int RowNumber { get; private set; }

    public string RawDataJson { get; private set; } = null!;

    public string? MappedDataJson { get; private set; }

    public ImportRowStatus Status { get; private set; }

    public int ErrorCount { get; private set; }

    public string? ErrorJson { get; private set; }

    public static ImportRow Create(long importBatchId, int rowNumber, string rawDataJson)
    {
        return new ImportRow
        {
            ImportBatchId = importBatchId,
            RowNumber = rowNumber,
            RawDataJson = rawDataJson,
            Status = ImportRowStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetMapped(string mappedDataJson)
    {
        MappedDataJson = mappedDataJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkValid()
    {
        Status = ImportRowStatus.Valid;
        ErrorCount = 0;
        ErrorJson = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkInvalid(int errorCount, string? errorJson)
    {
        Status = ImportRowStatus.Invalid;
        ErrorCount = errorCount;
        ErrorJson = errorJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkImported()
    {
        if (Status != ImportRowStatus.Valid)
            throw new InvalidOperationException("فقط سطر معتبر قابل ثبت نهایی است.");

        Status = ImportRowStatus.Imported;
        UpdatedAt = DateTime.UtcNow;
    }
}