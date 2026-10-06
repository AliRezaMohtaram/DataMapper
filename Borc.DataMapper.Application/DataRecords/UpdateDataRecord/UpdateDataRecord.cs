using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataRecords.Common;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.UpdateDataRecord;

public sealed record UpdateDataRecordCommand(
    long Id,
    IReadOnlyDictionary<string, string?> Values
) : IRequest<Result<SaveRecordOutcome>>;

public sealed class UpdateDataRecordValidator : AbstractValidator<UpdateDataRecordCommand>
{
    public UpdateDataRecordValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Values).NotNull();
    }
}

public sealed class UpdateDataRecordHandler
    : IRequestHandler<UpdateDataRecordCommand, Result<SaveRecordOutcome>>
{
    private readonly IAppDbContext _db;
    private readonly DataSourceOptionService _options;

    public UpdateDataRecordHandler(IAppDbContext db, DataSourceOptionService options)
    {
        _db = db;
        _options = options;
    }

    public async Task<Result<SaveRecordOutcome>> Handle(
        UpdateDataRecordCommand request,
        CancellationToken cancellationToken)
    {
        var record = await _db.DataRecords
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (record is null)
            return Result<SaveRecordOutcome>.Failure("رکورد موردنظر پیدا نشد.");

        var status = await _db.TemplateVersions
            .AsNoTracking()
            .Where(v => v.Id == record.TemplateVersionId)
            .Select(v => (TemplateVersionStatus?)v.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status != TemplateVersionStatus.Published)
            return Result<SaveRecordOutcome>.Failure("نسخهٔ این رکورد دیگر منتشرشده نیست؛ ویرایش داده ممکن نیست.");

        var fields = await RecordDataBuilder.LoadFieldsAsync(_db, record.TemplateVersionId, cancellationToken);

        var layoutJson = await _db.TemplateLayouts
            .AsNoTracking()
            .Where(l => l.TemplateVersionId == record.TemplateVersionId)
            .OrderByDescending(l => l.VersionNo)
            .Select(l => l.LayoutJson)
            .FirstOrDefaultAsync(cancellationToken);

        var hidden = LayoutVisibility.HiddenFieldKeys(layoutJson, request.Values);
        var built = await RecordDataBuilder.BuildAsync(fields, request.Values, hidden, _options, cancellationToken);

        if (built.Json is null)
            return Result<SaveRecordOutcome>.Ok(
                new SaveRecordOutcome(null, built.Errors),
                "برخی فیلدها نامعتبرند.");

        record.UpdateData(built.Json);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<SaveRecordOutcome>.Failure("ذخیرهٔ تغییرات انجام نشد؛ دوباره تلاش کنید.");
        }

        return Result<SaveRecordOutcome>.Ok(
            new SaveRecordOutcome(record.Id, new Dictionary<string, string>()),
            "رکورد ویرایش شد.");
    }
}
