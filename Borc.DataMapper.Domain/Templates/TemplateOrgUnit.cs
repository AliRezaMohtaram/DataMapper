namespace Borc.DataMapper.Domain.Templates;

/// <summary>
/// واحدی که اجازهٔ ثبت داده و ایمپورت با یک قالب را دارد. A unit (stable org chart key) whose members may enter records
/// and import files for the template. A template can have several; without any, only users with data scope "all" can.
/// </summary>
public sealed class TemplateOrgUnit
{
    private TemplateOrgUnit()
    {
    }

    public long TemplateId { get; private set; }

    public string OrgUnitKey { get; private set; } = null!;

    public static TemplateOrgUnit Create(long templateId, string orgUnitKey) =>
        new() { TemplateId = templateId, OrgUnitKey = orgUnitKey.Trim() };
}
