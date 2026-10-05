using System.Globalization;
using Borc.DataMapper.Application.Abstractions.Files;
using ClosedXML.Excel;

namespace Borc.DataMapper.Infrastructure.Files;

/// <summary>پیاده‌سازی IExcelReader با ClosedXML (NuGet: ClosedXML).</summary>
public sealed class ClosedXmlExcelReader : IExcelReader
{
    public ExcelSheetData Read(Stream stream, int maxRows, int maxColumns)
    {
        XLWorkbook workbook;

        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            throw new ExcelReadException("فایل Excel معتبر نیست یا آسیب دیده است (فقط xlsx پشتیبانی می‌شود).");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets
                            .FirstOrDefault(w => w.Visibility == XLWorksheetVisibility.Visible)
                        ?? workbook.Worksheets.FirstOrDefault();

            var used = sheet?.RangeUsed();

            if (sheet is null || used is null)
                throw new ExcelReadException("برگهٔ Excel خالی است.");

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var firstCol = used.FirstColumn().ColumnNumber();
            var lastCol = used.LastColumn().ColumnNumber();

            if (lastCol - firstCol + 1 > maxColumns)
                throw new ExcelReadException($"تعداد ستون‌ها بیش از {maxColumns} است.");

            if (lastRow - firstRow > maxRows)
                throw new ExcelReadException($"تعداد سطرها بیش از {maxRows} است.");

            var headers = ReadHeaders(sheet, firstRow, firstCol, lastCol);
            var rows = new List<ExcelRow>();

            for (var r = firstRow + 1; r <= lastRow; r++)
            {
                var values = new string?[headers.Count];
                var any = false;

                for (var c = firstCol; c <= lastCol; c++)
                {
                    var value = ReadCell(sheet.Cell(r, c));
                    values[c - firstCol] = value;
                    any |= value is not null;
                }

                if (any)
                    rows.Add(new ExcelRow(r, values));
            }

            return new ExcelSheetData(headers, rows);
        }
    }

    private static List<string> ReadHeaders(IXLWorksheet sheet, int row, int firstCol, int lastCol)
    {
        var headers = new List<string>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var c = firstCol; c <= lastCol; c++)
        {
            var text = ReadCell(sheet.Cell(row, c));

            if (string.IsNullOrWhiteSpace(text))
                text = $"ستون {c - firstCol + 1}";

            // سرستون تکراری یکتا می‌شود تا کلید JSON سطرها تداخل نکند
            if (seen.TryGetValue(text, out var count))
            {
                seen[text] = count + 1;
                text = $"{text} ({count + 1})";
            }
            else
            {
                seen[text] = 1;
            }

            headers.Add(text);
        }

        return headers;
    }

    private static string? ReadCell(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        string? text;

        switch (cell.DataType)
        {
            case XLDataType.DateTime:
                var dt = cell.GetDateTime();
                text = dt.TimeOfDay == TimeSpan.Zero
                    ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                break;

            case XLDataType.Number:
                var number = Convert.ToDecimal(cell.GetDouble());
                text = number.ToString("0.############################", CultureInfo.InvariantCulture);
                break;

            case XLDataType.Boolean:
                text = cell.GetBoolean() ? "true" : "false";
                break;

            case XLDataType.TimeSpan:
                text = cell.GetTimeSpan().ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                break;

            default:
                text = cell.GetString();
                break;
        }

        text = text?.Trim();

        return string.IsNullOrEmpty(text) ? null : text;
    }
}