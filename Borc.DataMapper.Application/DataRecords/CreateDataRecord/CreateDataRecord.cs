using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataRecords.Common;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.CreateDataRecord;

public sealed record CreateDataRecordCommand(
    long TemplateVersionId,
    IReadOnlyDictionary<string, string?> Values
) : IRequest<Result<SaveRecordOutcome>>;

public sealed class CreateDataRecordValidator : AbstractValidator<CreateDataRecordCommand>
{
    public CreateDataRecordValidator()
    {
        RuleFor(x => x.TemplateVersionId).GreaterThan(0);
        RuleFor(x => x.Values).NotNull();
    }
}

public sealed class CreateDataRecordHandler
    : IRequestHandler<CreateDataRecordCommand, Result<SaveRecordOutcome>>
{
    private readonly IAppDbContext _db;
    private readonly DataSourceOptionService _options;

    public CreateDataRecordHandler(IAppDbContext db, DataSourceOptionService options)
    {
        _db = db;
        _options = options;
    }

    public async Task<Result<SaveRecordOutcome>> Handle(
        CreateDataRecordCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result<SaveRecordOutcome>.Failure("نسخهٔ موردنظر پیدا نشد.");

        if (version.Status != TemplateVersionStatus.Published)
            return Result<SaveRecordOutcome>.Failure("ثبت داده فقط برای نسخهٔ منتشرشده ممکن است.");

        var fields = await RecordDataBuilder.LoadFieldsAsync(_db, version.Id, cancellationToken);

        var layoutJson = await _db.TemplateLayouts
            .AsNoTracking()
            .Where(l => l.TemplateVersionId == version.Id)
            .OrderByDescending(l => l.VersionNo)
            .Select(l => l.LayoutJson)
            .FirstOrDefaultAsync(cancellationToken);

        var hidden = LayoutVisibility.HiddenFieldKeys(layoutJson, request.Values);
        var built = await RecordDataBuilder.BuildAsync(fields, request.Values, hidden, _options, cancellationToken);

        if (built.Json is null)
            return Result<SaveRecordOutcome>.Ok(
                new SaveRecordOutcome(null, built.Errors),
                "برخی فیلدها نامعتبرند.");

        var record = DataRecord.CreateManual(version.TemplateId, version.Id, built.Json);
        _db.DataRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<SaveRecordOutcome>.Failure("ذخیرهٔ رکورد انجام نشد؛ دوباره تلاش کنید.");
        }

        return Result<SaveRecordOutcome>.Ok(
            new SaveRecordOutcome(record.Id, new Dictionary<string, string>()),
            "رکورد ثبت شد.");
    }
}
