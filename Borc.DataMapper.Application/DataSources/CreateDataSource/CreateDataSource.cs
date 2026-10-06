using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Domain.DataSources;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.CreateDataSource;

/// <summary>
/// فیلدهای تنظیمات بسته به SourceType پر می‌شوند:
/// StaticList → ItemsText؛ Template → TemplateId/ValueKey/DisplayKey؛ Api → Api*؛ File → بدون تنظیمات (بعد از ساخت فایل بارگذاری می‌شود).
/// </summary>
public sealed record CreateDataSourceCommand(
    string Code,
    string Name,
    DataSourceType SourceType,
    string? ItemsText = null,
    long? TemplateId = null,
    string? ValueKey = null,
    string? DisplayKey = null,
    string? ApiUrl = null,
    string? ApiItemsPath = null,
    string? ApiValueKey = null,
    string? ApiDisplayKey = null
) : IRequest<Result<long>>;

public sealed class CreateDataSourceValidator
    : AbstractValidator<CreateDataSourceCommand>
{
    public CreateDataSourceValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("کد فقط می‌تواند شامل حروف انگلیسی، عدد، _ و - باشد.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(x => x.SourceType).IsInEnum();
    }
}

public sealed class CreateDataSourceHandler
    : IRequestHandler<CreateDataSourceCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public CreateDataSourceHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        CreateDataSourceCommand request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        var exists = await _db.DataSources
            .AnyAsync(x => x.Code == code, cancellationToken);

        if (exists)
            return Result<long>.Failure("منبع داده‌ای با این کد از قبل وجود دارد.");

        var (config, configError) = await DataSourceSettings.BuildConfigAsync(
            _db, request.SourceType, request.TemplateId, request.ValueKey, request.DisplayKey,
            request.ApiUrl, request.ApiItemsPath, request.ApiValueKey, request.ApiDisplayKey,
            cancellationToken);

        if (configError is not null)
            return Result<long>.Failure(configError);

        var items = new List<(string Value, string Label)>();

        if (request.SourceType == DataSourceType.StaticList)
        {
            var parsed = DataSourceSettings.ParseItemsText(request.ItemsText);

            if (parsed.Error is not null)
                return Result<long>.Failure(parsed.Error);

            if (parsed.Items.Count == 0)
                return Result<long>.Failure("حداقل یک گزینه وارد کنید.");

            items = parsed.Items;
        }

        try
        {
            return await _db.InTransactionAsync(async () =>
            {
                var ds = DataSource.Create(code, request.Name, request.SourceType, config);
                _db.DataSources.Add(ds);
                await _db.SaveChangesAsync(cancellationToken);

                for (var i = 0; i < items.Count; i++)
                    _db.DataSourceItems.Add(DataSourceItem.Create(ds.Id, items[i].Value, items[i].Label, i));

                if (items.Count > 0)
                    await _db.SaveChangesAsync(cancellationToken);

                return Result<long>.Ok(
                    ds.Id,
                    request.SourceType == DataSourceType.File
                        ? "منبع داده ایجاد شد؛ حالا فایل Excel یا CSV را بارگذاری کنید."
                        : "منبع داده ایجاد شد.");
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<long>.Failure("ذخیره منبع داده انجام نشد؛ احتمالاً کد تکراری است.");
        }
    }
}
