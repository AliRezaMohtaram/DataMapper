using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Records;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.ListDataRecords;

public sealed record ListDataRecordsQuery(
    string? Search = null,
    long? TemplateVersionId = null,
    RecordSource? Source = null,
    long? ImportBatchId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<DataRecordListItemDto>>>
{
    public const int MaxPageSize = 100;
}

public sealed record DataRecordListItemDto(
    long Id,
    string TemplateName,
    int VersionNo,
    long TemplateVersionId,
    RecordSource Source,
    long? ImportBatchId,
    string Preview,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class ListDataRecordsHandler
    : IRequestHandler<ListDataRecordsQuery, Result<PagedResult<DataRecordListItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListDataRecordsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<DataRecordListItemDto>>> Handle(
        ListDataRecordsQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListDataRecordsQuery.MaxPageSize);

        var query =
            from r in _db.DataRecords.AsNoTracking()
            join v in _db.TemplateVersions on r.TemplateVersionId equals v.Id
            join t in _db.Templates on r.TemplateId equals t.Id
            select new { r, v, t };

        if (request.TemplateVersionId.HasValue)
            query = query.Where(x => x.r.TemplateVersionId == request.TemplateVersionId.Value);

        if (request.Source.HasValue)
            query = query.Where(x => x.r.SourceType == request.Source.Value);

        if (request.ImportBatchId.HasValue)
            query = query.Where(x => x.r.ImportBatchId == request.ImportBatchId.Value);

        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(x => EF.Functions.Like(x.r.DataJson, pattern));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.r.CreatedAt)
            .ThenByDescending(x => x.r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.r.Id,
                TemplateName = x.t.Name,
                x.v.VersionNo,
                x.r.TemplateVersionId,
                x.r.SourceType,
                x.r.ImportBatchId,
                x.r.DataJson,
                x.r.CreatedAt,
                x.r.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x => new DataRecordListItemDto(
                x.Id, x.TemplateName, x.VersionNo, x.TemplateVersionId, x.SourceType,
                x.ImportBatchId, ImportJson.Preview(x.DataJson), x.CreatedAt, x.UpdatedAt))
            .ToList();

        return Result<PagedResult<DataRecordListItemDto>>.Ok(
            new PagedResult<DataRecordListItemDto>(items, page, pageSize, total));
    }
}
