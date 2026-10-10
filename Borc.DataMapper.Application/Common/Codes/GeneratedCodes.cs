using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Common.Codes;

/// <summary>
/// کدهای خودکار (مثل TPL-0001): پیشوند + یکی بیشتر از بزرگ‌ترین شمارهٔ به‌کاررفته پس از آن پیشوند.
/// کاربر هیچ کدی را دستی وارد نمی‌کند؛ کدهای قدیمیِ دستی دست نمی‌خورند و فقط در شمارش حساب می‌شوند.
/// </summary>
public static class GeneratedCodes
{
    public const string TemplatePrefix = "TPL-";
    public const string DataSourcePrefix = "DS-";
    public const string FieldPrefix = "F";

    /// <param name="existing">همهٔ کدهای موجود، حذف‌شده‌ها هم (IgnoreQueryFilters) تا کدی دوباره استفاده نشود.</param>
    public static async Task<string> NextAsync(
        IQueryable<string> existing,
        string prefix,
        int digits,
        CancellationToken cancellationToken)
    {
        var used = await existing
            .Where(c => c.StartsWith(prefix))
            .ToListAsync(cancellationToken);

        var highest = used
            .Select(c => int.TryParse(c.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty()
            .Max();

        return prefix + (highest + 1).ToString(new string('0', digits), CultureInfo.InvariantCulture);
    }
}
