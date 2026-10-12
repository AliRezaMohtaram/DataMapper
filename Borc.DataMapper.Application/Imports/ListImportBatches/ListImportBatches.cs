using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Imports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.ListImportBatches;

public sealed record ListImportBatchesQuery(
    ImportBatchStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    IReadOnlyList<long>? BatchIds = null
) : IRequest<Result<PagedResult<ImportBatchListItemDto>>>
{
    public const int MaxPageSize = 100;
}

public sealed record ImportBatchListItemDto(
    long Id,
    string FileName,
    string TemplateName,
    int VersionNo,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    ImportBatchStatus Status,
    DateTime CreatedAt)
{
    /// <summary>Who uploaded it.</summary>
    public long? CreatedBy { get; init; }
}

public sealed class ListImportBatchesHandler
    : IRequestHandler<ListImportBatchesQuery, Result<PagedResult<ImportBatchListItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListImportBatchesHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<ImportBatchListItemDto>>> Handle(
        ListImportBatchesQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListImportBatchesQuery.MaxPageSize);

        var query =
            from b in _db.ImportBatches.AsNoTracking()
            join v in _db.TemplateVersions on b.TemplateVersionId equals v.Id
            join t in _db.Templates on v.TemplateId equals t.Id
            select new { Batch = b, TemplateName = t.Name, v.VersionNo };

        // exactly these batches in this order (a page chosen by the advanced filter); the caller sets the total
        var ids = request.BatchIds;
        if (ids is not null)
            query = query.Where(x => ids.Contains(x.Batch.Id));

        if (ids is null && request.Status.HasValue)
        {
            var status = request.Status.Value;
            query = query.Where(x => x.Batch.Status == status);
        }

        if (ids is null && search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Batch.FileName, pattern) ||
                EF.Functions.Like(x.TemplateName, pattern));
        }

        var total = ids is null ? await query.CountAsync(cancellationToken) : ids.Count;

        var items = await query
            .OrderByDescending(x => x.Batch.CreatedAt)
            .ThenByDescending(x => x.Batch.Id)
            .Skip(ids is null ? (page - 1) * pageSize : 0)
            .Take(ids is null ? pageSize : ids.Count)
            .Select(x => new ImportBatchListItemDto(
                x.Batch.Id,
                x.Batch.FileName,
                x.TemplateName,
                x.VersionNo,
                x.Batch.TotalRows,
                x.Batch.ValidRows,
                x.Batch.InvalidRows,
                x.Batch.Status,
                x.Batch.CreatedAt) { CreatedBy = x.Batch.CreatedBy })
            .ToListAsync(cancellationToken);

        if (ids is not null)
        {
            var order = ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i);
            items = items.OrderBy(x => order.GetValueOrDefault(x.Id, int.MaxValue)).ToList();
        }

        return Result<PagedResult<ImportBatchListItemDto>>.Ok(
            new PagedResult<ImportBatchListItemDto>(items, page, pageSize, total));
    }
}