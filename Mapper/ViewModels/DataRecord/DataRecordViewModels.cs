using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataRecords.ListDataRecords;
using Borc.DataMapper.Domain.Records;

namespace Borc.DataMapper.Web.ViewModels.DataRecords;

/// <param name="Filter">TemplateVersionId = نسخه‌ای که واقعاً نمایش داده می‌شود (شاید پیش‌فرض).</param>
/// <param name="TemplateVersions">The template's versions that have visible records (chips: «همهٔ نسخه‌ها» + each).</param>
/// <param name="AllVersions">The page shows the records of every version of the template.</param>
public sealed record DataRecordIndexViewModel(
    ListDataRecordsQuery Filter,
    PagedResult<DataRecordListItemDto> Result,
    IReadOnlyList<RecordColumnDto> Columns,
    IReadOnlyList<VersionFilterOption> Versions,
    long? TemplateId = null,
    string? TemplateName = null,
    IReadOnlyList<RecordVersionCountDto>? TemplateVersions = null,
    bool AllVersions = false);

/// <summary>جدول رکوردها (partial «_RecordGrid»). Compact = زیر فرم ورود داده.</summary>
public sealed record RecordGridViewModel(
    IReadOnlyList<RecordColumnDto> Columns,
    IReadOnlyList<DataRecordListItemDto> Items,
    bool Compact = false,
    bool CanCopy = true,
    bool ShowVersion = false,
    IReadOnlySet<long>? EditableVersions = null);

/// <param name="AcceptsData">The template takes new records (published version of a template not «غیرفعال» / «بایگانی»).</param>
public sealed record VersionFilterOption(long Id, string Label, bool AcceptsData = true, long TemplateId = 0);

/// <summary>صفحهٔ اصلی «داده‌های ثبت‌شده» (همهٔ قالب‌ها، بدون مقدار فیلدها).</summary>
public sealed record RecordOverviewViewModel(
    ListRecordsOverviewQuery Filter,
    PagedResult<RecordVersionSummaryDto> Result,
    IReadOnlyList<VersionFilterOption> Versions);

/// <summary>داده‌ای که صفحهٔ فرم به JavaScript می‌دهد.</summary>
public sealed record RecordFormPageViewModel(
    long? RecordId,
    string TemplateName,
    int VersionNo,
    long TemplateVersionId,
    string DataJson,
    RecentRecordsViewModel? Recent = null,
    long? CopiedFrom = null);

/// <summary>آخرین رکوردهای یک نسخه زیر فرم ورود داده (partial «_RecentRecords»).</summary>
public sealed record RecentRecordsViewModel(
    long TemplateVersionId,
    int TotalCount,
    RecordGridViewModel Grid);

/// <summary>درخواست JSON ذخیرهٔ رکورد (RecordId خالی = رکورد جدید). Stay = فرم باز می‌ماند (بدون پیام TempData).</summary>
public sealed record SaveRecordRequest(
    long VersionId,
    long? RecordId,
    Dictionary<string, string?>? Values,
    bool Stay = false);

public static class RecordSourceLabels
{
    public static string ToLabel(this RecordSource source) => source switch
    {
        RecordSource.Manual => "ورود دستی",
        RecordSource.Import => "ایمپورت",
        _ => source.ToString()
    };
}
