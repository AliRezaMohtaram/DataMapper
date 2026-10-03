using System.Text;

namespace Borc.DataMapper.Domain.Common;

public static class TextNormalizer
{
    /// <summary>
    /// نرمال‌سازی سرستون‌ها و aliasها برای تطبیق: ی/ك عربی به فارسی، حذف اعراب و کشیده،
    /// نیم‌فاصله و _ و - به فاصله، ارقام فارسی/عربی به لاتین، حروف کوچک، حذف فاصله‌های اضافه.
    /// </summary>
    public static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\u064A':
                case '\u0649':
                    sb.Append('\u06CC');
                    break;

                case '\u0643':
                    sb.Append('\u06A9');
                    break;

                case '\u200C':
                case '\u00A0':
                case '_':
                case '-':
                    sb.Append(' ');
                    break;

                case '\u200B':
                case '\u200D':
                case '\u200E':
                case '\u200F':
                case '\uFEFF':
                case '\u0640':
                    break;

                default:
                    if ((ch >= '\u064B' && ch <= '\u065F') || ch == '\u0670')
                        break;

                    if (ch >= '\u06F0' && ch <= '\u06F9')
                        sb.Append((char)('0' + (ch - '\u06F0')));
                    else if (ch >= '\u0660' && ch <= '\u0669')
                        sb.Append((char)('0' + (ch - '\u0660')));
                    else
                        sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}