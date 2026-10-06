namespace Borc.DataMapper.Web.ViewModels;

/// <summary>
/// تنظیمات partial «_FileDrop» (ناحیهٔ کشیدن و رها کردن فایل).
/// Accept: پسوندهای مجاز مثل ".xlsx,.csv" — همان مقدار ویژگی accept؛ MaxBytes: سقف حجم برای بررسی سمت کاربر.
/// </summary>
public sealed record FileDropModel(string Name, string Accept, long MaxBytes, bool Required = true);
