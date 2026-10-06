using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateFields.GetTemplateField;

public sealed record GetTemplateFieldQuery(long Id) : IRequest<Result<TemplateFieldDetailDto>>;
public sealed record TemplateFieldDetailDto(
    long Id,
    long TemplateVersionId,
    long TemplateId,
    string TemplateName,
    int VersionNo,
    TemplateVersionStatus VersionStatus,
    string FieldKey,
    string Label,
    FieldDataType DataType,
    string DbType,
    bool IsRequired,
    int SortOrder,
    int? Length,
    byte? Precision,
    byte? Scale,
    string? Regex,
    string? DefaultValue,
    long? DataSourceId,
    string? ConfigJson,
    IReadOnlyList<FieldAliasItemDto> Aliases);

public sealed record FieldAliasItemDto(long Id, string Alias);

public sealed class GetTemplateFieldHandler
    : IRequestHandler<GetTemplateFieldQuery, Result<TemplateFieldDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetTemplateFieldHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TemplateFieldDetailDto>> Handle(
        GetTemplateFieldQuery request,
        CancellationToken cancellationToken)
    {
        var field = await _db.TemplateFields
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (field is null)
            return Result<TemplateFieldDetailDto>.Failure("فیلد موردنظر پیدا نشد.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == field.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result<TemplateFieldDetailDto>.Failure("نسخه این فیلد پیدا نشد.");

        var templateName = await _db.Templates
            .AsNoTracking()
            .Where(t => t.Id == version.TemplateId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (templateName is null)
            return Result<TemplateFieldDetailDto>.Failure("قالب این فیلد پیدا نشد.");

        var aliases = await _db.TemplateFieldAliases
            .AsNoTracking()
            .Where(a => a.TemplateFieldId == field.Id)
            .OrderBy(a => a.Id)
            .Select(a => new FieldAliasItemDto(a.Id, a.Alias))
            .ToListAsync(cancellationToken);

        return Result<TemplateFieldDetailDto>.Ok(new TemplateFieldDetailDto(
            field.Id,
            field.TemplateVersionId,
            version.TemplateId,
            templateName,
            version.VersionNo,
            version.Status,
            field.FieldKey,
            field.Label,
            field.DataType,
            field.DbType,
            field.IsRequired,
            field.SortOrder,
            field.Length,
            field.Precision,
            field.Scale,
            field.Regex,
            field.DefaultValue,
            field.DataSourceId,
            field.ConfigJson,
            aliases));
    }
}