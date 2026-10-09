using Acl.Core.Attributes;
using Acl.Core.Model;

// منابع Acl برای Mapper؛ هنگام اجرا با پایگاه داده همگام می‌شوند و در صفحهٔ «نقش‌ها» قابل دسترسی‌دادن هستند.
// Acl resources of Mapper, synced at startup; grant them to roles on the Acl admin pages.
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.Root, ResourceType.Module, Title = "Data Mapper")]
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.Templates, ResourceType.Page, Title = "قالب‌ها", SortOrder = 2)]
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.DataSources, ResourceType.Page, Title = "منابع داده", SortOrder = 3)]
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.MappingProfiles, ResourceType.Page, Title = "پروفایل‌های نگاشت", SortOrder = 4)]
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.Imports, ResourceType.Page, Title = "ایمپورت داده", SortOrder = 5)]
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.DataRecords, ResourceType.Page, Title = "داده‌های ثبت‌شده", SortOrder = 6)]
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.OrgChart, ResourceType.Module, Title = "چارت سازمانی", SortOrder = 10)]

namespace Borc.DataMapper.Web.Modules;

/// <remarks>
/// Actions: View = open; Create / Edit / Delete as named; Approve = publish/archive a template version, commit an import.
/// Sub-pages share their parent's resource (template versions, fields and layouts → Templates). A grant on the
/// "Mapper" module is inherited by every page below it (e.g. View on Mapper = read-only access everywhere).
/// </remarks>
public static class MapperResources
{
    public const string Root = "Mapper";
    public const string Templates = "Mapper.Templates";
    public const string DataSources = "Mapper.DataSources";
    public const string MappingProfiles = "Mapper.MappingProfiles";
    public const string Imports = "Mapper.Imports";
    public const string DataRecords = "Mapper.DataRecords";

    /// <summary>View = see the org chart; Edit = change units, positions, assignments and delegations.</summary>
    public const string OrgChart = "Mapper.OrgChart";
}
