using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.GetImportLookups;

/// <summary>گزینه‌های صفحهٔ آپلود: نسخه‌های منتشرشده و پروفایل‌های فعال نگاشت.</summary>
public sealed record GetImportLookupsQuery : IRequest<Result<ImportLookupsDto>>;

public sealed record ImportLookupsDto(
    IReadOnlyList<ImportVersionOption> Versions,
    IReadOnlyList<ImportProfileOption> Profiles);

public sealed record ImportVersionOption(long Id, string Label);

public sealed record ImportProfileOption(long Id, long VersionId, string Name);

public sealed class GetImportLookupsHandler
    : IRequestHandler<GetImportLookupsQuery, Result<ImportLookupsDto>>
{
    private readonly IAppDbContext _db;

    public GetImportLookupsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ImportLookupsDto>> Handle(
        GetImportLookupsQuery request,
        CancellationToken cancellationToken)
    {
        var versions = await (
            from v in _db.TemplateVersions.AsNoTracking()
            join t in _db.Templates on v.TemplateId equals t.Id
            where v.Status == TemplateVersionStatus.Published
            orderby t.Name, v.VersionNo descending
            select new { v.Id, t.Name, v.VersionNo }).ToListAsync(cancellationToken);

        var profiles = await _db.MappingProfiles
            .AsNoTracking()
            .Where(p => p.Status == MappingProfileStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new ImportProfileOption(p.Id, p.TemplateVersionId, p.Name))
            .ToListAsync(cancellationToken);

        return Result<ImportLookupsDto>.Ok(new ImportLookupsDto(
            versions.Select(v => new ImportVersionOption(v.Id, $"{v.Name} — نسخه {v.VersionNo}")).ToList(),
            profiles));
    }
}