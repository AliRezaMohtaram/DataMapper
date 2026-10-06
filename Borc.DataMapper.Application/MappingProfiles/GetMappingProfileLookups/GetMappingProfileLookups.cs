using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.GetMappingProfileLookups;

/// <summary>
/// گزینه‌های فرم‌ها: نسخه‌های غیربایگانی (برای ساخت پروفایل) و فیلدهای یک نسخه (برای فرم قاعده).
/// </summary>
public sealed record GetMappingProfileLookupsQuery(long? TemplateVersionId = null)
    : IRequest<Result<MappingProfileLookupsDto>>;

public sealed record MappingProfileLookupsDto(
    IReadOnlyList<ProfileVersionOption> Versions,
    IReadOnlyList<ProfileFieldOption> Fields);

public sealed record ProfileVersionOption(long Id, string Label);

public sealed record ProfileFieldOption(long Id, string Key, string Label, bool IsRequired);

public sealed class GetMappingProfileLookupsHandler
    : IRequestHandler<GetMappingProfileLookupsQuery, Result<MappingProfileLookupsDto>>
{
    private readonly IAppDbContext _db;

    public GetMappingProfileLookupsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<MappingProfileLookupsDto>> Handle(
        GetMappingProfileLookupsQuery request,
        CancellationToken cancellationToken)
    {
        var versions = await (
            from v in _db.TemplateVersions.AsNoTracking()
            join t in _db.Templates on v.TemplateId equals t.Id
            where v.Status != TemplateVersionStatus.Archived
            orderby t.Name, v.VersionNo descending
            select new { v.Id, t.Name, v.VersionNo, v.Status }).ToListAsync(cancellationToken);

        var fields = new List<ProfileFieldOption>();

        if (request.TemplateVersionId.HasValue)
        {
            fields = await _db.TemplateFields
                .AsNoTracking()
                .Where(f => f.TemplateVersionId == request.TemplateVersionId.Value)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Id)
                .Select(f => new ProfileFieldOption(f.Id, f.FieldKey, f.Label, f.IsRequired))
                .ToListAsync(cancellationToken);
        }

        return Result<MappingProfileLookupsDto>.Ok(new MappingProfileLookupsDto(
            versions.Select(v => new ProfileVersionOption(
                v.Id,
                $"{v.Name} — نسخه {v.VersionNo} ({(v.Status == TemplateVersionStatus.Published ? "منتشرشده" : "پیش‌نویس")})"))
                .ToList(),
            fields));
    }
}
