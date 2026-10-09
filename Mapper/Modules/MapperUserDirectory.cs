using System.Globalization;
using Borc.Users.Services;

namespace Borc.DataMapper.Web.Modules;

/// <summary>
/// کاربران ماژول Borc.Users برای چارت سازمانی و Acl (شناسهٔ long به‌صورت رشته).
/// One user directory for both modules, over the Users module (ids are the long account ids as strings).
/// </summary>
public sealed class MapperUserDirectory(IUserLookup users) : OrgChart.Core.Abstractions.IUserDirectory, Acl.Core.Abstractions.IUserDirectory
{
    async Task<IReadOnlyList<OrgChart.Core.Abstractions.UserInfo>> OrgChart.Core.Abstractions.IUserDirectory.SearchAsync(string text, int maxResults, CancellationToken cancellationToken) =>
        (await users.SearchAsync(text, maxResults, cancellationToken)).Select(u => new OrgChart.Core.Abstractions.UserInfo(Id(u.Id), u.DisplayName, Detail(u))).ToList();

    async Task<IReadOnlyDictionary<string, OrgChart.Core.Abstractions.UserInfo>> OrgChart.Core.Abstractions.IUserDirectory.GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken) =>
        (await GetAsync(userIds, cancellationToken)).ToDictionary(u => Id(u.Id), u => new OrgChart.Core.Abstractions.UserInfo(Id(u.Id), u.DisplayName, Detail(u)));

    async Task<IReadOnlyList<Acl.Core.Abstractions.UserInfo>> Acl.Core.Abstractions.IUserDirectory.SearchAsync(string text, int maxResults, CancellationToken cancellationToken) =>
        (await users.SearchAsync(text, maxResults, cancellationToken)).Select(u => new Acl.Core.Abstractions.UserInfo(Id(u.Id), u.DisplayName, Detail(u))).ToList();

    async Task<IReadOnlyDictionary<string, Acl.Core.Abstractions.UserInfo>> Acl.Core.Abstractions.IUserDirectory.GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken) =>
        (await GetAsync(userIds, cancellationToken)).ToDictionary(u => Id(u.Id), u => new Acl.Core.Abstractions.UserInfo(Id(u.Id), u.DisplayName, Detail(u)));

    private async Task<IEnumerable<UserSummary>> GetAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        long[] ids = userIds.Select(id => long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long v) ? v : (long?)null)
            .OfType<long>().ToArray();
        return (await users.GetAsync(ids, cancellationToken)).Values;
    }

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static string Detail(UserSummary u) => u.IsActive ? u.UserName : u.UserName + " (غیرفعال)";
}
