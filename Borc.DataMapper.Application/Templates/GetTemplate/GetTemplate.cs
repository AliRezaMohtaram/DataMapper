using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Templates.GetTemplate;

public sealed record GetTemplateQuery(long Id) : IRequest<Result<TemplateDetailDto>>;

public sealed record TemplateDetailDto(
    long Id,
    string Code,
    string Name,
    string? Description,
    TemplateStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<TemplateStatus> AllowedStatuses,
    IReadOnlyList<TemplateVersionItemDto> Versions);

public sealed record TemplateVersionItemDto(
    long Id,
    int VersionNo,
    TemplateVersionStatus Status,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    int FieldCount,
    bool HasLayout);

public sealed class GetTemplateHandler
    : IRequestHandler<GetTemplateQuery, Result<TemplateDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetTemplateHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TemplateDetailDto>> Handle(
        GetTemplateQuery request,
        CancellationToken cancellationToken)
    {
        var template = await _db.Templates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (template is null)
            return Result<TemplateDetailDto>.Failure("قالب موردنظر پیدا نشد.");

        var versions = await _db.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.VersionNo)
            .Select(v => new TemplateVersionItemDto(
                v.Id,
                v.VersionNo,
                v.Status,
                v.CreatedAt,
                v.PublishedAt,
                _db.TemplateFields.Count(f => f.TemplateVersionId == v.Id),
                _db.TemplateLayouts.Any(l => l.TemplateVersionId == v.Id)))
            .ToListAsync(cancellationToken);

        var allowed = Enum.GetValues<TemplateStatus>()
            .Where(template.CanChangeStatusTo)
            .ToList();

        var dto = new TemplateDetailDto(
            template.Id,
            template.Code,
            template.Name,
            template.Description,
            template.Status,
            template.CreatedAt,
            template.UpdatedAt,
            allowed,
            versions);

        return Result<TemplateDetailDto>.Ok(dto);
    }
}