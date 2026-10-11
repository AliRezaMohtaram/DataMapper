using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Records;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.ListDataRecords;

/// <summary>
/// صفحهٔ اصلی «داده‌های ثبت‌شده»: رکوردهای همهٔ قالب‌ها با قالب، نسخه، واحد، منبع و زمان ثبت (بدون مقدار فیلدها؛
/// جدول مقدارها در صفحهٔ هر نسخه است).
/// </summary>
public sealed record ListRecordsOverviewQuery(
    string? Search = null,
    long? TemplateVersionId = null,
    RecordSource? Source = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<RecordOverviewItemDto>>>
{
    public const int MaxPageSize = 100;
}

public sealed record RecordOverviewItemDto(
    long Id,
    long TemplateId,
    string TemplateName,
    long TemplateVersionId,
    int VersionNo,
    string? OrgUnitKey,
    RecordSource Source,
    long? ImportBatchId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class ListRecordsOverviewHandler
    : IRequestHandler<ListRecordsOverviewQuery, Result<PagedResult<RecordOverviewItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListRecordsOverviewHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<RecordOverviewItemDto>>> Handle(
        ListRecordsOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListRecordsOverviewQuery.MaxPageSize);

        var query =
            from r in _db.DataRecords.AsNoTracking()
            join v in _db.TemplateVersions on r.TemplateVersionId equals v.Id
            join t in _db.Templates on r.TemplateId equals t.Id
            select new { r, v, t };

        if (request.TemplateVersionId.HasValue)
            query = query.Where(x => x.r.TemplateVersionId == request.TemplateVersionId.Value);

        if (request.Source.HasValue)
            query = query.Where(x => x.r.SourceType == request.Source.Value);

        if (search is not null)
        {
            // نام قالب یا مقدارهای رکورد
            var pattern = $"%{search}%";
            query = query.Where(x => EF.Functions.Like(x.t.Name, pattern) || EF.Functions.Like(x.r.DataJson, pattern));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.r.CreatedAt)
            .ThenByDescending(x => x.r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RecordOverviewItemDto(
                x.r.Id, x.t.Id, x.t.Name, x.v.Id, x.v.VersionNo, x.r.OrgUnitKey,
                x.r.SourceType, x.r.ImportBatchId, x.r.CreatedAt, x.r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<RecordOverviewItemDto>>.Ok(
            new PagedResult<RecordOverviewItemDto>(items, page, pageSize, total));
    }
}
