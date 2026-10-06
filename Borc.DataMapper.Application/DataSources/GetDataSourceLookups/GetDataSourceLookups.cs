using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.GetDataSourceLookups;

/// <summary>قالب‌هایی که نسخهٔ منتشرشده دارند، با فیلدهایشان؛ برای انتخاب «فیلد مقدار» و «فیلد نمایشی» در فرم منبع داده.</summary>
public sealed record GetDataSourceLookupsQuery : IRequest<Result<DataSourceLookupsDto>>;

public sealed record DataSourceLookupsDto(IReadOnlyList<LookupTemplate> Templates);

public sealed record LookupTemplate(long Id, string Name, IReadOnlyList<LookupField> Fields);

public sealed record LookupField(string Key, string Label);

public sealed class GetDataSourceLookupsHandler
    : IRequestHandler<GetDataSourceLookupsQuery, Result<DataSourceLookupsDto>>
{
    private readonly IAppDbContext _db;

    public GetDataSourceLookupsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<DataSourceLookupsDto>> Handle(
        GetDataSourceLookupsQuery request,
        CancellationToken cancellationToken)
    {
        var published = await (
            from v in _db.TemplateVersions.AsNoTracking()
            join t in _db.Templates on v.TemplateId equals t.Id
            where v.Status == TemplateVersionStatus.Published
            select new { v.Id, TemplateId = t.Id, t.Name, v.VersionNo })
            .ToListAsync(cancellationToken);

        // آخرین نسخهٔ منتشرشدهٔ هر قالب
        var latest = published
            .GroupBy(x => x.TemplateId)
            .Select(g => g.OrderByDescending(x => x.VersionNo).First())
            .OrderBy(x => x.Name)
            .ToList();

        var versionIds = latest.Select(x => x.Id).ToList();

        var fields = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => versionIds.Contains(f.TemplateVersionId))
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(f => new { f.TemplateVersionId, f.FieldKey, f.Label })
            .ToListAsync(cancellationToken);

        var byVersion = fields
            .GroupBy(f => f.TemplateVersionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<LookupField>)g
                .Select(f => new LookupField(f.FieldKey, f.Label)).ToList());

        var templates = latest
            .Select(x => new LookupTemplate(
                x.TemplateId, x.Name,
                byVersion.TryGetValue(x.Id, out var list) ? list : Array.Empty<LookupField>()))
            .ToList();

        return Result<DataSourceLookupsDto>.Ok(new DataSourceLookupsDto(templates));
    }
}
