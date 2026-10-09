using Acl.Core.Attributes;
using Acl.Core.Model;

// منابع Acl برای Mapper؛ هنگام اجرا با پایگاه داده همگام می‌شوند و در صفحهٔ «نقش‌ها» قابل دسترسی‌دادن هستند.
// Acl resources of Mapper, synced at startup; grant them to roles on the Acl admin pages.
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.Root, ResourceType.Module, Title = "Data Mapper")]
[assembly: Resource(Borc.DataMapper.Web.Modules.MapperResources.OrgChart, ResourceType.Module, Title = "چارت سازمانی", SortOrder = 10)]

namespace Borc.DataMapper.Web.Modules;

public static class MapperResources
{
    public const string Root = "Mapper";

    /// <summary>View = see the org chart; Edit = change units, positions, assignments and delegations.</summary>
    public const string OrgChart = "Mapper.OrgChart";
}
