using Borc.Users.Services;

namespace Borc.DataMapper.Web.Modules;

/// <summary>نام نمایشی ثبت‌کنندهٔ رکوردها و ایمپورت‌ها (یک پرس‌وجو برای همهٔ ردیف‌های یک صفحه).</summary>
public sealed class UserNames(IUserLookup users)
{
    public async Task<IReadOnlyDictionary<long, string>> GetAsync(IEnumerable<long?> ids, CancellationToken cancellationToken = default)
    {
        long[] distinct = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return new Dictionary<long, string>();
        }

        IReadOnlyDictionary<long, UserSummary> found = await users.GetAsync(distinct, cancellationToken);
        return found.ToDictionary(p => p.Key, p => p.Value.DisplayName);
    }
}
