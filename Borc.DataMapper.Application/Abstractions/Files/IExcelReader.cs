namespace Borc.DataMapper.Application.Abstractions.Files;

/// <summary>خواندن اولین برگه قابل‌مشاهدهٔ یک فایل Excel (xlsx). سطر اول = سرستون‌ها.</summary>
public interface IExcelReader
{
    /// <exception cref="ExcelReadException">فایل نامعتبر یا بیش از حد بزرگ.</exception>
    ExcelSheetData Read(Stream stream, int maxRows, int maxColumns);
}

public sealed record ExcelSheetData(
    IReadOnlyList<string> Headers,
    IReadOnlyList<ExcelRow> Rows);

/// <summary>RowNumber = شمارهٔ سطر در خود Excel (برای پیدا کردن خطا توسط کاربر).</summary>
public sealed record ExcelRow(int RowNumber, IReadOnlyList<string?> Values);

public sealed class ExcelReadException : Exception
{
    public ExcelReadException(string message) : base(message)
    {
    }
}