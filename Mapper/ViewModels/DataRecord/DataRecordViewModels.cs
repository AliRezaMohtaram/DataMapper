using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataRecords.ListDataRecords;
using Borc.DataMapper.Domain.Records;

namespace Borc.DataMapper.Web.ViewModels.DataRecords;

public sealed record DataRecordIndexViewModel(
    ListDataRecordsQuery Filter,
    PagedResult<DataRecordListItemDto> Result,
    IReadOnlyList<VersionFilterOption> Versions);

public sealed record VersionFilterOption(long Id, string Label);

/// <summary>داده‌ای که صفحهٔ فرم به JavaScript می‌دهد.</summary>
public sealed record RecordFormPageViewModel(
    long? RecordId,
    string TemplateName,
    int VersionNo,
    long TemplateVersionId,
    string DataJson);

/// <summary>درخواست JSON ذخیرهٔ رکورد (RecordId خالی = رکورد جدید).</summary>
public sealed record SaveRecordRequest(
    long VersionId,
    long? RecordId,
    Dictionary<string, string?>? Values);

public static class RecordSourceLabels
{
    public static string ToLabel(this RecordSource source) => source switch
    {
        RecordSource.Manual => "ورود دستی",
        RecordSource.Import => "ایمپورت",
        _ => source.ToString()
    };
}
