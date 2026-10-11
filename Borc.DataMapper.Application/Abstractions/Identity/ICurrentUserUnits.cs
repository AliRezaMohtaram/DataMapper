namespace Borc.DataMapper.Application.Abstractions.Identity;

/// <summary>واحدهای کاربر جاری در چارت. The current user's org units (web host: the org chart).</summary>
public interface ICurrentUserUnits
{
    /// <summary>Units of the positions the user holds now (holder, not delegated), primary position first.</summary>
    Task<IReadOnlyList<string>> HeldUnitKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>The user may enter records and import for any template (data scope "all" on records in Acl).</summary>
    bool CanEnterForAnyTemplate { get; }
}
