namespace Borc.DataMapper.Domain.Common;

/// <summary>
/// داده‌ای که به یک واحد سازمانی (کلید پایدار چارت) تعلق دارد؛ null = عمومی (همه می‌بینند).
/// Data owned by an org unit (the chart's stable unit key); null = public, visible to everyone.
/// </summary>
public interface IOrgUnitOwned
{
    public const int KeyMaxLength = 256;

    string? OrgUnitKey { get; }

    void AssignOrgUnit(string? orgUnitKey);
}
