namespace Borc.DataMapper.Application.Abstractions.Identity;

/// <summary>
/// کدام ایمپورت‌ها و رکوردها برای کاربر جاری قابل دیدن است (فیلتر پایگاه داده).
/// Which imports and records the current user may see, per resource key (e.g. "Mapper.DataRecords"): always the rows
/// they created, plus the units their Acl data scope or — as a unit head — the org chart gives them. Definitions
/// (templates, data sources, mapping profiles) are not filtered.
/// </summary>
public interface IDataScopeProvider
{
    DataScopeView For(string resourceKey);
}

/// <param name="All">Every row.</param>
/// <param name="OwnerUserId">Rows created by this user (the current user), or null.</param>
/// <param name="OrgUnitKeys">Rows of these units (sub-units already included).</param>
public sealed record DataScopeView(bool All, long? OwnerUserId, IReadOnlyCollection<string> OrgUnitKeys)
{
    public static DataScopeView Everything { get; } = new(true, null, []);

    public static DataScopeView Nothing { get; } = new(false, null, []);
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
