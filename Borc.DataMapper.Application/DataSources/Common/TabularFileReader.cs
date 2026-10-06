using System.Text;
using Borc.DataMapper.Application.Abstractions.Files;

namespace Borc.DataMapper.Application.DataSources.Common;

/// <summary>خواندن فایل xlsx (با IExcelReader) یا CSV/TSV و برگرداندن همان ExcelSheetData؛ سطر اول = سرستون‌ها.</summary>
public static class TabularFileReader
{
    public static bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".csv", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".tsv", StringComparison.OrdinalIgnoreCase);
    }

    public static ExcelSheetData Read(
        string fileName,
        byte[] content,
        IExcelReader excel,
        int maxRows,
        int maxColumns)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".xlsx")
        {
            using var stream = new MemoryStream(content);
            return excel.Read(stream, maxRows, maxColumns);
        }

        return ReadDelimited(content, maxRows, maxColumns);
    }

    private static ExcelSheetData ReadDelimited(byte[] content, int maxRows, int maxColumns)
    {
        var text = Decode(content);

        if (string.IsNullOrWhiteSpace(text))
            throw new ExcelReadException("فایل خالی است.");

        var delimiter = DetectDelimiter(text);
        var records = Parse(text, delimiter);

        if (records.Count == 0)
            throw new ExcelReadException("فایل خالی است.");

        var width = records[0].Count;

        if (width > maxColumns)
            throw new ExcelReadException($"تعداد ستون‌ها بیش از {maxColumns} است.");

        var headers = new List<string>(width);

        for (var i = 0; i < width; i++)
        {
            var h = records[0][i].Trim();
            headers.Add(h.Length == 0 ? $"ستون {i + 1}" : h);
        }

        // سرستون‌های تکراری را یکتا می‌کنیم
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            if (seen.TryGetValue(headers[i], out var n))
            {
                seen[headers[i]] = n + 1;
                headers[i] = $"{headers[i]} ({n + 1})";
            }
            else
            {
                seen[headers[i]] = 1;
            }
        }

        if (records.Count - 1 > maxRows)
            throw new ExcelReadException($"تعداد سطرها بیش از {maxRows} است.");

        var rows = new List<ExcelRow>();

        for (var r = 1; r < records.Count; r++)
        {
            var values = new string?[width];
            var any = false;

            for (var c = 0; c < width; c++)
            {
                var v = c < records[r].Count ? records[r][c].Trim() : string.Empty;
                values[c] = v.Length == 0 ? null : v;
                any |= values[c] is not null;
            }

            if (any)
                rows.Add(new ExcelRow(r + 1, values));
        }

        return new ExcelSheetData(headers, rows);
    }

    /// <summary>BOM → UTF-8 سخت‌گیرانه → در صورت خطا Windows-1256 (CSVهای فارسی Excel قدیمی).</summary>
    private static string Decode(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);

        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
            return Encoding.Unicode.GetString(content, 2, content.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1256).GetString(content);
        }
    }

    private static char DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];

        var candidates = new[] { ',', ';', '\t' };

        return candidates
            .OrderByDescending(c => firstLine.Count(x => x == c))
            .First();
    }

    /// <summary>تجزیهٔ RFC 4180: فیلدهای داخل گیومه، گیومهٔ دوتایی و خط جدید داخل فیلد.</summary>
    private static List<List<string>> Parse(string text, char delimiter)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                row.Add(field.ToString());
                field.Clear();

                if (row.Count > 1 || row[0].Length > 0)
                    records.Add(row);

                row = new List<string>();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            if (row.Count > 1 || row[0].Length > 0)
                records.Add(row);
        }

        return records;
    }
}
