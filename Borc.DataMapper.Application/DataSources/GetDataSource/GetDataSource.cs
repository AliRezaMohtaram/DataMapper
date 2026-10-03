using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.DataSources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.GetDataSource;

public sealed record GetDataSourceQuery(long Id) : IRequest<Result<DataSourceDetailDto>>;

public sealed record DataSourceDetailDto(
    long Id,
    string Code,
    string Name,
    DataSourceType SourceType,
    string? ConfigJson,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int UsedByFieldCount);

public sealed class GetDataSourceHandler
    : IRequestHandler<GetDataSourceQuery, Result<DataSourceDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetDataSourceHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<DataSourceDetailDto>> Handle(
        GetDataSourceQuery request,
        CancellationToken cancellationToken)
    {
        var ds = await _db.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ds is null)
            return Result<DataSourceDetailDto>.Failure("منبع داده موردنظر پیدا نشد.");

        var used = await _db.TemplateFields
            .CountAsync(f => f.DataSourceId == ds.Id, cancellationToken);

        return Result<DataSourceDetailDto>.Ok(new DataSourceDetailDto(
            ds.Id, ds.Code, ds.Name, ds.SourceType, ds.ConfigJson,
            ds.IsActive, ds.CreatedAt, ds.UpdatedAt, used));
    }
}