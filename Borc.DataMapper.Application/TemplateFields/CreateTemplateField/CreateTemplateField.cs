using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Codes;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateFields.CreateTemplateField;

/// <summary>
/// SortOrder خالی = انتهای فهرست. FieldKey خالی = خودکار (F001، …؛ یکتا در همهٔ نسخه‌های قالب تا کلید حذف‌شده
/// برای معنای دیگری دوباره استفاده نشود). خروجی: شناسه فیلد.
/// </summary>
public sealed record CreateTemplateFieldCommand(
    long TemplateVersionId,
    string? FieldKey,
    string Label,
    FieldDataType DataType,
    string DbType,
    bool IsRequired,
    int? SortOrder,
    int? Length,
    byte? Precision,
    byte? Scale,
    string? Regex,
    string? DefaultValue,
    long? DataSourceId,
    string? ConfigJson
) : IRequest<Result<long>>, ITemplateFieldInput;

public sealed class CreateTemplateFieldValidator
    : AbstractValidator<CreateTemplateFieldCommand>
{
    public CreateTemplateFieldValidator()
    {
        RuleFor(x => x.TemplateVersionId).GreaterThan(0);

        RuleFor(x => x.FieldKey)
            .MaximumLength(200)
            .Matches(@"^[\x20-\x7E]+$")
            .WithMessage("کلید فیلد فقط می‌تواند شامل حروف انگلیسی و علائم ASCII باشد.")
            .When(x => !string.IsNullOrWhiteSpace(x.FieldKey));

        this.ApplyFieldRules();
    }
}

public sealed class CreateTemplateFieldHandler
    : IRequestHandler<CreateTemplateFieldCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public CreateTemplateFieldHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        CreateTemplateFieldCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result<long>.Failure("نسخه موردنظر پیدا نشد.");

        if (!version.IsEditable)
            return Result<long>.Failure("فقط نسخه پیش‌نویس قابل ویرایش است.");

        var key = string.IsNullOrWhiteSpace(request.FieldKey)
            ? await GeneratedCodes.NextAsync(
                from f in _db.TemplateFields.IgnoreQueryFilters()
                join v in _db.TemplateVersions.IgnoreQueryFilters() on f.TemplateVersionId equals v.Id
                where v.TemplateId == version.TemplateId
                select f.FieldKey,
                GeneratedCodes.FieldPrefix, 3, cancellationToken)
            : request.FieldKey.Trim();

        // collation دیتابیس CI است؛ کلید در هر نسخه یکتاست.
        var keyExists = await _db.TemplateFields.AnyAsync(
            f => f.TemplateVersionId == version.Id && f.FieldKey == key,
            cancellationToken);

        if (keyExists)
            return Result<long>.Failure("فیلدی با این کلید در این نسخه وجود دارد.");

        if (request.DataSourceId.HasValue)
        {
            var dsExists = await _db.DataSources
                .AnyAsync(d => d.Id == request.DataSourceId.Value, cancellationToken);

            if (!dsExists)
                return Result<long>.Failure("منبع داده انتخاب‌شده پیدا نشد.");
        }

        var sortOrder = request.SortOrder
            ?? (await _db.TemplateFields
                   .Where(f => f.TemplateVersionId == version.Id)
                   .MaxAsync(f => (int?)f.SortOrder, cancellationToken) ?? 0) + 1;

        var field = TemplateField.Create(
            version.Id,
            key,
            request.Label,
            request.DataType,
            request.DbType,
            request.IsRequired,
            sortOrder,
            request.Length,
            request.Precision,
            request.Scale,
            Normalize(request.Regex),
            Normalize(request.DefaultValue),
            request.DataSourceId,
            Normalize(request.ConfigJson));

        _db.TemplateFields.Add(field);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<long>.Failure("ذخیره فیلد انجام نشد؛ احتمالاً کلید تکراری است.");
        }

        return Result<long>.Ok(field.Id, "فیلد ایجاد شد.");
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}