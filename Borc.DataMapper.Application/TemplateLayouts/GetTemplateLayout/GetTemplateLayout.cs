using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateLayouts.GetTemplateLayout;

public sealed record GetTemplateLayoutQuery(long TemplateVersionId) : IRequest<Result<TemplateLayoutDto>>;

public sealed record TemplateLayoutDto(
    long TemplateVersionId,
    int VersionNo,
    TemplateVersionStatus VersionStatus,
    long TemplateId,
    string TemplateName,
    string? LayoutJson,
    bool IsPublished,
    IReadOnlyList<LayoutFieldDto> Fields);

public sealed record LayoutFieldDto(
    string Key,
    string Label,
    FieldDataType DataType,
    string DbType,
    bool IsRequired,
    int? Length,
    byte? Precision,
    byte? Scale,
    string? Regex);

public sealed class GetTemplateLayoutHandler
    : IRequestHandler<GetTemplateLayoutQuery, Result<TemplateLayoutDto>>
{
    private readonly IAppDbContext _db;

    public GetTemplateLayoutHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TemplateLayoutDto>> Handle(
        GetTemplateLayoutQuery request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result<TemplateLayoutDto>.Failure("نسخه موردنظر پیدا نشد.");

        var templateName = await _db.Templates
            .AsNoTracking()
            .Where(t => t.Id == version.TemplateId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (templateName is null)
            return Result<TemplateLayoutDto>.Failure("قالب این نسخه پیدا نشد.");

        var fields = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => f.TemplateVersionId == version.Id)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(f => new LayoutFieldDto(
                f.FieldKey, f.Label, f.DataType, f.DbType, f.IsRequired,
                f.Length, f.Precision, f.Scale, f.Regex))
            .ToListAsync(cancellationToken);

        var layout = await _db.TemplateLayouts
            .AsNoTracking()
            .Where(l => l.TemplateVersionId == version.Id)
            .OrderByDescending(l => l.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<TemplateLayoutDto>.Ok(new TemplateLayoutDto(
            version.Id,
            version.VersionNo,
            version.Status,
            version.TemplateId,
            templateName,
            layout?.LayoutJson,
            layout?.IsPublished ?? false,
            fields));
    }
}
