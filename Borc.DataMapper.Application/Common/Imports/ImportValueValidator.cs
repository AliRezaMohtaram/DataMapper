using System.Globalization;
using System.Text.RegularExpressions;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;

namespace Borc.DataMapper.Application.Imports.Common;

public sealed record TargetField(
    long Id,
    string Key,
    string Label,
    FieldDataType DataType,
    bool IsRequired,
    int? Length,
    byte? Precision,
    byte? Scale,
    string? Regex,
    string? DefaultValue);

/// <summary>Value: null | decimal (عدد) | string. Error: پیام فارسی یا null.</summary>
public readonly record struct FieldValue(object? Value, string? Error);

/// <summary>تبدیل و اعتبارسنجی مقدار خام یک سلول بر اساس تعریف فیلد.</summary>
public static class ImportValueValidator
{
    private static readonly Regex DateRx = new(
        @"^(\d{4})[/\-](\d{1,2})[/\-](\d{1,2})(?:[ T](\d{1,2}):(\d{2})(?::(\d{2}))?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static FieldValue Validate(TargetField f, string? raw)
    {
        var text = raw?.Trim();

        if (string.IsNullOrEmpty(text))
            text = f.DefaultValue?.Trim();

        if (string.IsNullOrEmpty(text))
            return f.IsRequired
                ? new FieldValue(null, $"«{f.Label}»: مقدار الزامی است.")
                : new FieldValue(null, null);

        object value;
        string representation;

        switch (f.DataType)
        {
            case FieldDataType.Number:
                {
                    var cleaned = TextNormalizer.NormalizeDigits(text)
                        .Replace("\u066C", string.Empty)
                        .Replace(",", string.Empty)
                        .Replace("\u066B", ".")
                        .Replace(" ", string.Empty);

                    if (!decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture, out var number))
                        return Fail(f, $"«{text}» عدد معتبر نیست.");

                    var plain = number.ToString("0.############################", CultureInfo.InvariantCulture);
                    var parts = plain.TrimStart('-').Split('.');
                    var integerDigits = parts[0].TrimStart('0').Length;
                    var scaleDigits = parts.Length > 1 ? parts[1].Length : 0;

                    if (f.Precision.HasValue)
                    {
                        var scale = f.Scale ?? 0;

                        if (scaleDigits > scale)
                            return Fail(f, $"حداکثر {scale} رقم اعشار مجاز است.");

                        if (integerDigits > f.Precision.Value - scale)
                            return Fail(f, $"حداکثر {f.Precision.Value - scale} رقم صحیح مجاز است.");
                    }
                    else if (f.Scale.HasValue && scaleDigits > f.Scale.Value)
                    {
                        return Fail(f, $"حداکثر {f.Scale.Value} رقم اعشار مجاز است.");
                    }

                    value = number;
                    representation = plain;
                    break;
                }

            case FieldDataType.Date:
            case FieldDataType.DateTime:
                {
                    if (!TryParseDate(text, out var date))
                        return Fail(f, $"«{text}» تاریخ معتبر نیست (نمونه: 2026-10-05 یا 1405/07/13).");

                    representation = f.DataType == FieldDataType.Date
                        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

                    value = representation;
                    break;
                }

            default:
                {
                    if (f.Length.HasValue && text.Length > f.Length.Value)
                        return Fail(f, $"طول مقدار ({text.Length}) از حداکثر {f.Length.Value} بیشتر است.");

                    value = text;
                    representation = text;
                    break;
                }
        }

        if (!string.IsNullOrWhiteSpace(f.Regex))
        {
            try
            {
                if (!Regex.IsMatch(representation, f.Regex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                    return Fail(f, "مقدار با الگوی مجاز مطابقت ندارد.");
            }
            catch (RegexMatchTimeoutException)
            {
                return Fail(f, "بررسی الگوی مجاز بیش از حد طول کشید.");
            }
            catch (ArgumentException)
            {
                return Fail(f, "الگوی Regex فیلد معتبر نیست.");
            }
        }

        return new FieldValue(value, null);
    }

    private static FieldValue Fail(TargetField f, string message)
        => new(null, $"«{f.Label}»: {message}");

    /// <summary>تاریخ میلادی یا شمسی (سال کمتر از 1700 = شمسی)، با ساعت اختیاری.</summary>
    private static bool TryParseDate(string text, out DateTime result)
    {
        result = default;

        var m = DateRx.Match(TextNormalizer.NormalizeDigits(text).Trim());

        if (!m.Success)
            return false;

        var y = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var mo = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var d = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        var h = m.Groups[4].Success ? int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        var mi = m.Groups[5].Success ? int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture) : 0;
        var s = m.Groups[6].Success ? int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture) : 0;

        try
        {
            result = y < 1700
                ? new PersianCalendar().ToDateTime(y, mo, d, h, mi, s, 0)
                : new DateTime(y, mo, d, h, mi, s);

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}