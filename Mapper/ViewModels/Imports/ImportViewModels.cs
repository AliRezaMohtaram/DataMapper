using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Mappings;

namespace Borc.DataMapper.Web.ViewModels.Imports;

public sealed record ImportIndexViewModel(
    ListImportBatchesQuery Filter,
    PagedResult<ImportBatchListItemDto> Result);

public static class ImportLabels
{
    public static string ToLabel(this ImportBatchStatus status) => status switch
    {
        ImportBatchStatus.Uploaded => "آپلودشده (نیازمند تطبیق)",
        ImportBatchStatus.Validating => "در حال اعتبارسنجی",
        ImportBatchStatus.Validated => "اعتبارسنجی‌شده",
        ImportBatchStatus.Imported => "ثبت‌شده",
        ImportBatchStatus.Failed => "ناموفق",
        _ => status.ToString()
    };

    public static string ToLabel(this ImportRowStatus status) => status switch
    {
        ImportRowStatus.Pending => "در انتظار",
        ImportRowStatus.Valid => "معتبر",
        ImportRowStatus.Invalid => "نامعتبر",
        ImportRowStatus.Imported => "ثبت‌شده",
        _ => status.ToString()
    };

    public static string ToLabel(this MappingMethod method) => method switch
    {
        MappingMethod.Manual => "دستی",
        MappingMethod.Alias => "alias",
        MappingMethod.Regex => "Regex",
        MappingMethod.Auto => "خودکار",
        _ => method.ToString()
    };
}
