using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.ListDataSources;
using Borc.DataMapper.Domain.DataSources;

namespace Borc.DataMapper.Web.ViewModels.DataSources;

public sealed record DataSourceIndexViewModel(
    ListDataSourcesQuery Filter,
    PagedResult<DataSourceListItemDto> Result);

public static class DataSourceTypeLabels
{
    public static string ToLabel(this DataSourceType type) => type switch
    {
        DataSourceType.StaticList => "لیست ثابت",
        DataSourceType.SqlQuery => "کوئری SQL",
        DataSourceType.Api => "سرویس (API)",
        _ => type.ToString()
    };
}