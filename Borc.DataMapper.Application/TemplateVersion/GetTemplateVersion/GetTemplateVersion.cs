using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Borc.DataMapper.Application.TemplateVersions.GetTemplateVersion;

public sealed record GetTemplateVersionQuery(long Id) : IRequest<Result<TemplateVersionDetailDto>>;

public sealed record TemplateVersionDetailDto(
    long Id,
    long TemplateId,
    string TemplateCode,
    string TemplateName,
    int VersionNo,
    TemplateVersionStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? PublishedAt,
    IReadOnlyList<TemplateFieldItemDto> Fields);

public sealed record TemplateFieldItemDto(
    long Id,
    string FieldKey,
    string Label,
    FieldDataType DataType,
    string DbType,
    bool IsRequired,
    int SortOrder,
    int AliasCount);

public sealed class GetTemplateVersionHandler
    : IRequestHandler<GetTemplateVersionQuery, Result<TemplateVersionDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetTemplateVersionHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TemplateVersionDetailDto>> Handle(
        GetTemplateVersionQuery request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (version is null)
            return Result<TemplateVersionDetailDto>.Failure("نسخه موردنظر پیدا نشد.");

        var template = await _db.Templates
            .AsNoTracking()
            .Where(t => t.Id == version.TemplateId)
            .Select(t => new { t.Code, t.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            return Result<TemplateVersionDetailDto>.Failure("قالب این نسخه پیدا نشد.");

        var fields = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => f.TemplateVersionId == version.Id)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(f => new TemplateFieldItemDto(
                f.Id,
                f.FieldKey,
                f.Label,
                f.DataType,
                f.DbType,
                f.IsRequired,
                f.SortOrder,
                _db.TemplateFieldAliases.Count(a => a.TemplateFieldId == f.Id)))
            .ToListAsync(cancellationToken);

        var dto = new TemplateVersionDetailDto(
            version.Id,
            version.TemplateId,
            template.Code,
            template.Name,
            version.VersionNo,
            version.Status,
            version.CreatedAt,
            version.UpdatedAt,
            version.PublishedAt,
            fields);

        return Result<TemplateVersionDetailDto>.Ok(dto);
    }
}