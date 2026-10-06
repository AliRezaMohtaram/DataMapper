using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.MappingProfiles.ListMappingProfiles;
using Borc.DataMapper.Domain.Mappings;

namespace Borc.DataMapper.Web.ViewModels.MappingProfiles;

public sealed record MappingProfileIndexViewModel(
    ListMappingProfilesQuery Filter,
    PagedResult<MappingProfileListItemDto> Result);

public static class MappingProfileLabels
{
    public static string ToLabel(this MappingProfileStatus status) => status switch
    {
        MappingProfileStatus.Active => "فعال",
        MappingProfileStatus.Inactive => "غیرفعال",
        _ => status.ToString()
    };

    public static string ToLabel(this ImportSourceType type) => type switch
    {
        ImportSourceType.Excel => "Excel",
        ImportSourceType.Csv => "CSV",
        _ => type.ToString()
    };
}
