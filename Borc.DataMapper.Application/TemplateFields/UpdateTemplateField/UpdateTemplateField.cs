using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateFields.UpdateTemplateField;

/// <summary>FieldKey تغییر نمی‌کند. خروجی: شناسه نسخه (برای بازگشت به صفحه نسخه).</summary>
public sealed record UpdateTemplateFieldCommand(
    long Id,
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
    string? ConfigJson
) : IRequest<Result<long>>, ITemplateFieldInput;

public sealed class UpdateTemplateFieldValidator
    : AbstractValidator<UpdateTemplateFieldCommand>
{
    public UpdateTemplateFieldValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);

        this.ApplyFieldRules();
    }
}

public sealed class UpdateTemplateFieldHandler
    : IRequestHandler<UpdateTemplateFieldCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public UpdateTemplateFieldHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        UpdateTemplateFieldCommand request,
        CancellationToken cancellationToken)
    {
        var field = await _db.TemplateFields
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (field is null)
            return Result<long>.Failure("فیلد موردنظر پیدا نشد.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == field.TemplateVersionId, cancellationToken);

        if (version is null || !version.IsEditable)
            return Result<long>.Failure("فقط فیلدهای نسخه پیش‌نویس قابل ویرایش‌اند.");

        if (request.DataSourceId.HasValue)
        {
            var dsExists = await _db.DataSources
                .AnyAsync(d => d.Id == request.DataSourceId.Value, cancellationToken);

            if (!dsExists)
                return Result<long>.Failure("منبع داده انتخاب‌شده پیدا نشد.");
        }

        field.Update(
            request.Label,
            request.DataType,
            request.DbType,
            request.IsRequired,
            request.SortOrder,
            request.Length,
            request.Precision,
            request.Scale,
            Normalize(request.Regex),
            Normalize(request.DefaultValue),
            request.DataSourceId,
            Normalize(request.ConfigJson));

        await _db.SaveChangesAsync(cancellationToken);

        return Result<long>.Ok(field.TemplateVersionId, "فیلد ویرایش شد.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}