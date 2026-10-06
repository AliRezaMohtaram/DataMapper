namespace Borc.DataMapper.Application.DataRecords.Common;

/// <summary>
/// نتیجهٔ ثبت/ویرایش رکورد: یا ذخیره شده (Id)، یا خطاهای اعتبارسنجی به‌تفکیک کلید فیلد دارد.
/// خطاهای غیرفیلدی با Result.Failure برمی‌گردند.
/// </summary>
public sealed record SaveRecordOutcome(long? Id, IReadOnlyDictionary<string, string> FieldErrors)
{
    public bool Saved => Id.HasValue && FieldErrors.Count == 0;
}
