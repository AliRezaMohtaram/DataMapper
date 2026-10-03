namespace Borc.DataMapper.Domain.Imports;

/// <summary>چرخه عمر ایمپورت. پیشنهادی: نیاز به تأیید.</summary>
public enum ImportBatchStatus : short
{
    Uploaded = 1,
    Validating = 2,
    Validated = 3,
    Imported = 4,
    Failed = 5
}