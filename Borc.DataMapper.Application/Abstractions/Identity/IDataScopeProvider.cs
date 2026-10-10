namespace Borc.DataMapper.Application.Abstractions.Identity;

/// <summary>
/// کدام ردیف‌ها برای کاربر جاری قابل دیدن است (برای فیلتر پایگاه داده). پیاده‌سازی وب از Acl می‌خواند.
/// Which rows the current user may see, per resource key (e.g. "Mapper.Templates"). Rows without an org unit are
/// public and always visible; this decides the rest. The web host implements it over Acl's data scope.
/// </summary>
public interface IDataScopeProvider
{
    DataScopeView For(string resourceKey);
}

/// <param name="All">Every row.</param>
/// <param name="OwnerUserId">Rows created by this user (scope "own"), or null.</param>
/// <param name="OrgUnitKeys">Rows of these units (already including sub-units where the rule says so).</param>
public sealed record DataScopeView(bool All, long? OwnerUserId, IReadOnlyCollection<string> OrgUnitKeys)
{
    public static DataScopeView Everything { get; } = new(true, null, []);

    public static DataScopeView PublicOnly { get; } = new(false, null, []);
}

/// <summary>
/// واحدی که ردیف‌های جدید (بدون واحد) هنگام ذخیره می‌گیرند. Set by the web layer from the form (validated) or the
/// user's default unit. <see cref="IsExplicit"/> with a null key = "public" was chosen.
/// </summary>
public interface IOrgUnitSelection
{
    bool IsExplicit { get; }

    string? Key { get; }

    void Choose(string? orgUnitKey);

    /// <summary>The user's default unit (e.g. their primary position's unit), used when nothing was chosen.</summary>
    Task<string?> GetDefaultAsync(CancellationToken cancellationToken = default);
}
