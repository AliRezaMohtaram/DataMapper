using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateFields.GetFieldLookups;

/// <summary>گزینه‌های فهرست‌های کشویی فرم فیلد: منابع داده و الگوهای Regex آماده.</summary>
public sealed record GetFieldLookupsQuery : IRequest<Result<FieldLookupsDto>>;

public sealed record FieldLookupsDto(
    IReadOnlyList<DataSourceOption> DataSources,
    IReadOnlyList<PredefinedRegexOption> Regexes);

public sealed record DataSourceOption(long Id, string Name, bool IsActive);

public sealed record PredefinedRegexOption(string Code, string Name, string Pattern);

public sealed class GetFieldLookupsHandler
    : IRequestHandler<GetFieldLookupsQuery, Result<FieldLookupsDto>>
{
    private readonly IAppDbContext _db;

    public GetFieldLookupsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<FieldLookupsDto>> Handle(
        GetFieldLookupsQuery request,
        CancellationToken cancellationToken)
    {
        var dataSources = await _db.DataSources
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DataSourceOption(d.Id, d.Name, d.IsActive))
            .ToListAsync(cancellationToken);

        var regexes = await _db.PredefinedRegexes
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new PredefinedRegexOption(r.Code, r.Name, r.Pattern))
            .ToListAsync(cancellationToken);

        return Result<FieldLookupsDto>.Ok(new FieldLookupsDto(dataSources, regexes));
    }
}