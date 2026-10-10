using System.Globalization;
using Acl.Core.Abstractions;
using Borc.DataMapper.Application.Abstractions.Identity;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace Borc.DataMapper.Web.Modules;

/// <summary>
/// محدودهٔ دادهٔ Acl برای فیلتر جدول‌های Mapper. Mapper's row filter over Acl's data scope rules (per resource):
/// All, Own (rows the user created) or org units (sub-units included where the rule says so). Outside an HTTP request
/// (startup, tools) everything is visible; inside one without loaded access only public rows are.
/// </summary>
public sealed class AclDataScopeProvider(IAccessContext access, IHttpContextAccessor http) : IDataScopeProvider
{
    private readonly Dictionary<string, DataScopeView> _cache = new(StringComparer.OrdinalIgnoreCase);

    public DataScopeView For(string resourceKey)
    {
        if (http.HttpContext is null)
        {
            return DataScopeView.Everything;
        }

        if (access.Current is not { } current)
        {
            return DataScopeView.PublicOnly;
        }

        if (!_cache.TryGetValue(resourceKey, out DataScopeView? view))
        {
            var scope = current.GetDataScope(resourceKey);
            long? owner = scope.Own && long.TryParse(current.UserId, NumberStyles.None, CultureInfo.InvariantCulture, out long id) ? id : null;
            _cache[resourceKey] = view = new DataScopeView(scope.All, owner, scope.OrgUnitKeys);
        }

        return view;
    }
}

/// <param name="Key">Unit key; null = public.</param>
public sealed record OrgUnitOption(string? Key, string Title, string? Path);

/// <summary>
/// واحدهای قابل انتخاب در فرم‌ها و واحد پیش‌فرض کاربر. Units a user may give to new or existing rows of a resource:
/// the units of the positions they hold now and the units their data scope covers; with scope "all" every active unit
/// and — where allowed — "public". Default: the unit of their current primary position (else acting, else the first).
/// Also the request's <see cref="IOrgUnitSelection"/>: controllers <see cref="Choose"/> a validated unit before saving.
/// </summary>
public sealed class OrgUnitChoices(IOrgChartReader chart, Borc.DataMapper.Application.Abstractions.Identity.ICurrentUser currentUser, IDataScopeProvider scope, TimeProvider time)
    : IOrgUnitSelection
{
    private IReadOnlyList<UserPosition>? _positions;

    public bool IsExplicit { get; private set; }

    public string? Key { get; private set; }

    public void Choose(string? orgUnitKey) =>
        (IsExplicit, Key) = (true, string.IsNullOrWhiteSpace(orgUnitKey) ? null : orgUnitKey.Trim());

    public async Task<string?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserPosition> held = await HeldNowAsync(cancellationToken);
        return (held.FirstOrDefault(p => p.Kind == AssignmentKind.Primary)
                ?? held.FirstOrDefault(p => p.Kind == AssignmentKind.Acting)
                ?? held.FirstOrDefault())?.OrgUnitKey;
    }

    /// <summary>The choices for a form, in chart order; "public" first when allowed.</summary>
    public async Task<IReadOnlyList<OrgUnitOption>> OptionsAsync(string resourceKey, bool allowPublic, CancellationToken cancellationToken = default)
    {
        OrgChartSnapshot snapshot = await chart.GetSnapshotAsync(cancellationToken);
        DataScopeView view = scope.For(resourceKey);
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        keys.UnionWith((await HeldNowAsync(cancellationToken)).Select(p => p.OrgUnitKey));
        keys.UnionWith(view.OrgUnitKeys);

        List<OrgUnitOption> options = [];
        if (allowPublic && view.All)
        {
            options.Add(new OrgUnitOption(null, "عمومی (همهٔ واحدها)", null));
        }

        foreach (UnitNode unit in snapshot.Units.Where(u => u.IsActive && (view.All || keys.Contains(u.Key))))
        {
            options.Add(new OrgUnitOption(unit.Key, unit.Title, string.Join(" › ", snapshot.GetPath(unit.Key).Select(u => u.Title))));
        }

        return options;
    }

    /// <summary>True when <paramref name="key"/> (null = public) is among <see cref="OptionsAsync"/>.</summary>
    public async Task<bool> IsAllowedAsync(string resourceKey, string? key, bool allowPublic, CancellationToken cancellationToken = default) =>
        (await OptionsAsync(resourceKey, allowPublic, cancellationToken))
            .Any(o => string.Equals(o.Key, string.IsNullOrWhiteSpace(key) ? null : key.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Title of a unit key for display; "عمومی" for null.</summary>
    public async Task<string> TitleAsync(string? key, CancellationToken cancellationToken = default) =>
        key is null ? "عمومی" : (await chart.GetSnapshotAsync(cancellationToken)).FindUnit(key)?.Title ?? key;

    private async Task<IReadOnlyList<UserPosition>> HeldNowAsync(CancellationToken cancellationToken)
    {
        if (_positions is null)
        {
            DateTime now = time.GetUtcNow().UtcDateTime;
            _positions = currentUser.UserId is { } id
                ? (await chart.GetUserPositionsAsync(id.ToString(CultureInfo.InvariantCulture), cancellationToken))
                    .Where(p => p.Kind is AssignmentKind.Primary or AssignmentKind.Acting
                        && (p.ValidFrom is null || p.ValidFrom <= now) && (p.ValidTo is null || p.ValidTo > now))
                    .ToList()
                : [];
        }

        return _positions;
    }
}
