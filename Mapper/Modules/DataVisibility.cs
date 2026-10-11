using System.Globalization;
using Acl.Core.Abstractions;
using Acl.Core.Evaluation;
using ICurrentUser = Borc.DataMapper.Application.Abstractions.Identity.ICurrentUser;
using Borc.DataMapper.Application.Abstractions.Identity;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace Borc.DataMapper.Web.Modules;

/// <summary>Mapper:Visibility in appsettings.</summary>
public sealed class VisibilityOptions
{
    /// <summary>A unit head (org chart) also sees the records and imports of their unit and its sub-units. Default true.</summary>
    public bool ManagersSeeSubordinates { get; set; } = true;
}

/// <summary>
/// چه کسی چه رکورد و ایمپورتی را می‌بیند (تصمیم کاربر): هر کس آنچه خودش ثبت کرده؛ مدیر واحد (سمت سرپرست واحد در چارت،
/// قابل خاموش‌کردن) داده‌های واحد و زیرمجموعه‌اش را؛ و نقش‌های Acl با محدودهٔ داده بیشتر. کاربری که دید بیشتر دارد
/// می‌تواند با «فقط داده‌های من» (کوکی) آن را محدود کند.
/// Per request: the current user's held units, the units they head (with sub-units) and the view mode. Loaded once by
/// <see cref="LoadAsync"/> (middleware after UseAccessControl); also the row filter (<see cref="IDataScopeProvider"/>)
/// and the units for data entry (<see cref="ICurrentUserUnits"/>).
/// </summary>
public sealed class DataVisibility(
    IAccessContext access,
    IHttpContextAccessor http,
    ICurrentUser currentUser,
    IOrgChartReader chart,
    TimeProvider time,
    Microsoft.Extensions.Options.IOptions<VisibilityOptions> options) : IDataScopeProvider, ICurrentUserUnits
{
    public const string ModeCookie = "mapper.view";
    public const string MineMode = "mine";

    private readonly Dictionary<string, DataScopeView> _cache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _held = [];
    private IReadOnlyCollection<string> _managed = [];
    private bool _loaded;

    /// <summary>Units the user heads now, with their sub-units (empty when the setting is off).</summary>
    public IReadOnlyCollection<string> ManagedUnitKeys => _managed;

    /// <summary>"Only my data" was chosen.</summary>
    public bool OnlyMine => string.Equals(http.HttpContext?.Request.Cookies[ModeCookie], MineMode, StringComparison.Ordinal);

    /// <summary>The user could see more than their own rows (unit head or an Acl data scope); then the toggle is offered.</summary>
    public bool HasWiderView =>
        _managed.Count > 0
        || Acl(DataScopeResourcesKeys.DataRecords) is { } r && (r.All || r.OrgUnitKeys.Length > 0)
        || Acl(DataScopeResourcesKeys.Imports) is { } i && (i.All || i.OrgUnitKeys.Length > 0);

    /// <summary>Data scope "all" on records; also outside a request (startup, tools), like the row filter.</summary>
    public bool CanEnterForAnyTemplate => http.HttpContext is null || Acl(DataScopeResourcesKeys.DataRecords)?.All == true;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded || currentUser.UserId is not { } id)
        {
            return;
        }

        _loaded = true;
        DateTime now = time.GetUtcNow().UtcDateTime;
        List<UserPosition> held = (await chart.GetUserPositionsAsync(id.ToString(CultureInfo.InvariantCulture), cancellationToken))
            .Where(p => p.Kind is AssignmentKind.Primary or AssignmentKind.Acting
                && (p.ValidFrom is null || p.ValidFrom <= now) && (p.ValidTo is null || p.ValidTo > now))
            .OrderBy(p => p.Kind == AssignmentKind.Primary ? 0 : 1)
            .ToList();
        _held = held.Select(p => p.OrgUnitKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (options.Value.ManagersSeeSubordinates && held.Count > 0)
        {
            OrgChartSnapshot snapshot = await chart.GetSnapshotAsync(cancellationToken);
            HashSet<string> positions = held.Select(p => p.PositionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> managed = new(StringComparer.OrdinalIgnoreCase);
            foreach (UnitNode unit in snapshot.Units.Where(u => u.IsActive && u.ManagerPositionKey is { } m && positions.Contains(m)))
            {
                managed.Add(unit.Key);
                managed.UnionWith(snapshot.GetDescendantKeys(unit.Key));
            }

            _managed = managed;
        }
    }

    public Task<IReadOnlyList<string>> HeldUnitKeysAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(cancellationToken).ContinueWith(_ => _held, cancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public DataScopeView For(string resourceKey)
    {
        if (http.HttpContext is null)
        {
            return DataScopeView.Everything;     // startup, tools
        }

        if (_cache.TryGetValue(resourceKey, out DataScopeView? view))
        {
            return view;
        }

        long? owner = currentUser.UserId;
        if (OnlyMine)
        {
            view = new DataScopeView(false, owner, []);
        }
        else if (Acl(resourceKey) is { } scope)
        {
            HashSet<string> units = new(scope.OrgUnitKeys, StringComparer.OrdinalIgnoreCase);
            units.UnionWith(_managed);
            view = new DataScopeView(scope.All, owner, units);
        }
        else
        {
            view = new DataScopeView(false, owner, _managed);
        }

        return _cache[resourceKey] = view;
    }

    private DataScope? Acl(string resourceKey) => access.Current?.GetDataScope(resourceKey);
}

/// <summary>Keys mirrored from the infrastructure's DataScopeResources (the Web project does not reference it here).</summary>
internal static class DataScopeResourcesKeys
{
    public const string Imports = "Mapper.Imports";
    public const string DataRecords = "Mapper.DataRecords";
}
