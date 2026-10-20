using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Templates.Entry;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.GetImportLookups;

/// <summary>
/// گزینه‌های آپلود و ورود دستی: نسخه‌های منتشرشدهٔ قالب‌هایی که کاربر اجازهٔ ثبت داده با آن‌ها را دارد
/// (واحدهای مجاز قالب) و پروفایل‌های فعال نگاشت همان نسخه‌ها.
/// </summary>
/// <param name="ForEntry">Only templates the user may enter data for (false = every published version, e.g. list filters).</param>
public sealed record GetImportLookupsQuery(bool ForEntry = true) : IRequest<Result<ImportLookupsDto>>;

public sealed record ImportLookupsDto(
    IReadOnlyList<ImportVersionOption> Versions,
    IReadOnlyList<ImportProfileOption> Profiles);

/// <param name="AcceptsData">The template takes new data (not «غیرفعال» / «بایگانی»).</param>
public sealed record ImportVersionOption(long Id, string Label, bool AcceptsData = true, long TemplateId = 0);

public sealed record ImportProfileOption(long Id, long VersionId, string Name);

public sealed class GetImportLookupsHandler
    : IRequestHandler<GetImportLookupsQuery, Result<ImportLookupsDto>>
{
    private readonly IAppDbContext _db;
    private readonly ITemplateEntryPolicy _entry;

    public GetImportLookupsHandler(IAppDbContext db, ITemplateEntryPolicy entry)
    {
        _db = db;
        _entry = entry;
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
            select new { v.Id, TemplateId = t.Id, t.Name, v.VersionNo, t.Status }).ToListAsync(cancellationToken);

        if (request.ForEntry)
            versions = versions.Where(v => Domain.Templates.Template.AcceptsDataIn(v.Status)).ToList();
        if (request.ForEntry && await _entry.AllowedTemplateIdsAsync(cancellationToken) is { } allowed)
            versions = versions.Where(v => allowed.Contains(v.TemplateId)).ToList();

        var versionIds = versions.Select(v => v.Id).ToHashSet();

        var profiles = await _db.MappingProfiles
            .AsNoTracking()
            .Where(p => p.Status == MappingProfileStatus.Active)
            .OrderBy(p => p.Name)
            .Select(p => new ImportProfileOption(p.Id, p.TemplateVersionId, p.Name))
            .ToListAsync(cancellationToken);
        profiles = profiles.Where(p => versionIds.Contains(p.VersionId)).ToList();

        return Result<ImportLookupsDto>.Ok(new ImportLookupsDto(
            versions.Select(v => new ImportVersionOption(v.Id, $"{v.Name} — نسخه {v.VersionNo}", Domain.Templates.Template.AcceptsDataIn(v.Status), v.TemplateId)).ToList(),
            profiles));
    }
}