using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.GetMappingProfile;

public sealed record GetMappingProfileQuery(long Id) : IRequest<Result<MappingProfileDetailDto>>;

public sealed record MappingProfileDetailDto(
    long Id,
    string Name,
    long TemplateVersionId,
    long TemplateId,
    string TemplateName,
    int VersionNo,
    TemplateVersionStatus VersionStatus,
    ImportSourceType SourceType,
    MappingProfileStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int BatchCount,
    int FieldCount,
    IReadOnlyList<MappingRuleDto> Rules,
    IReadOnlyList<string> UnmappedRequiredFields);

public sealed record MappingRuleDto(
    long Id,
    string SourceColumn,
    long TargetFieldId,
    string FieldKey,
    string FieldLabel,
    MappingMethod Method,
    decimal? Confidence,
    int SortOrder,
    DateTime CreatedAt);

public sealed class GetMappingProfileHandler
    : IRequestHandler<GetMappingProfileQuery, Result<MappingProfileDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetMappingProfileHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<MappingProfileDetailDto>> Handle(
        GetMappingProfileQuery request,
        CancellationToken cancellationToken)
    {
        var head = await (
            from p in _db.MappingProfiles.AsNoTracking()
            join v in _db.TemplateVersions on p.TemplateVersionId equals v.Id
            join t in _db.Templates on v.TemplateId equals t.Id
            where p.Id == request.Id
            select new { p, v, t }).FirstOrDefaultAsync(cancellationToken);

        if (head is null)
            return Result<MappingProfileDetailDto>.Failure("پروفایل نگاشت موردنظر پیدا نشد.");

        var fields = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => f.TemplateVersionId == head.v.Id)
            .Select(f => new { f.Id, f.FieldKey, f.Label, f.IsRequired, f.DefaultValue })
            .ToListAsync(cancellationToken);

        var rules = await (
            from r in _db.MappingRules.AsNoTracking()
            join f in _db.TemplateFields on r.TargetFieldId equals f.Id
            where r.MappingProfileId == head.p.Id
            orderby r.SortOrder, r.Id
            select new MappingRuleDto(
                r.Id, r.SourceColumn, r.TargetFieldId, f.FieldKey, f.Label,
                r.MappingMethod, r.Confidence, r.SortOrder, r.CreatedAt))
            .ToListAsync(cancellationToken);

        var mapped = rules.Select(r => r.TargetFieldId).ToHashSet();

        var unmappedRequired = fields
            .Where(f => f.IsRequired && string.IsNullOrWhiteSpace(f.DefaultValue) && !mapped.Contains(f.Id))
            .Select(f => f.Label)
            .ToList();

        var batchCount = await _db.ImportBatches
            .CountAsync(b => b.MappingProfileId == head.p.Id, cancellationToken);

        return Result<MappingProfileDetailDto>.Ok(new MappingProfileDetailDto(
            head.p.Id, head.p.Name, head.v.Id, head.t.Id, head.t.Name, head.v.VersionNo,
            head.v.Status, head.p.SourceType, head.p.Status, head.p.CreatedAt, head.p.UpdatedAt,
            batchCount, fields.Count, rules, unmappedRequired));
    }
}
