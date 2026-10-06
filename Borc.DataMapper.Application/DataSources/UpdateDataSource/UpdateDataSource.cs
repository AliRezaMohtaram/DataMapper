using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Domain.DataSources;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.UpdateDataSource;

/// <summary>کد و نوع منبع پس از ایجاد تغییر نمی‌کنند (فیلدهای قالب‌ها به آن‌ها وابسته‌اند).</summary>
public sealed record UpdateDataSourceCommand(
    long Id,
    string Name,
    string? ItemsText = null,
    long? TemplateId = null,
    string? ValueKey = null,
    string? DisplayKey = null,
    string? ApiUrl = null,
    string? ApiItemsPath = null,
    string? ApiValueKey = null,
    string? ApiDisplayKey = null
) : IRequest<Result>;

public sealed class UpdateDataSourceValidator
    : AbstractValidator<UpdateDataSourceCommand>
{
    public UpdateDataSourceValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(250);
    }
}

public sealed class UpdateDataSourceHandler
    : IRequestHandler<UpdateDataSourceCommand, Result>
{
    private readonly IAppDbContext _db;

    public UpdateDataSourceHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        UpdateDataSourceCommand request,
        CancellationToken cancellationToken)
    {
        var current = await _db.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (current is null)
            return Result.Failure("منبع داده موردنظر پیدا نشد.");

        var type = current.SourceType;
        var config = current.ConfigJson;

        if (type is DataSourceType.Template or DataSourceType.Api)
        {
            var built = await DataSourceSettings.BuildConfigAsync(
                _db, type, request.TemplateId, request.ValueKey, request.DisplayKey,
                request.ApiUrl, request.ApiItemsPath, request.ApiValueKey, request.ApiDisplayKey,
                cancellationToken);

            if (built.Error is not null)
                return Result.Failure(built.Error);

            config = built.ConfigJson;
        }

        List<(string Value, string Label)>? items = null;

        if (type == DataSourceType.StaticList)
        {
            var parsed = DataSourceSettings.ParseItemsText(request.ItemsText);

            if (parsed.Error is not null)
                return Result.Failure(parsed.Error);

            if (parsed.Items.Count == 0)
                return Result.Failure("حداقل یک گزینه وارد کنید.");

            items = parsed.Items;
        }

        var now = DateTime.UtcNow;

        try
        {
            return await _db.InTransactionAsync(async () =>
            {
                var ds = await _db.DataSources.FirstAsync(x => x.Id == request.Id, cancellationToken);
                ds.Update(request.Name, ds.SourceType, config);

                if (items is not null)
                {
                    await _db.DataSourceItems
                        .Where(i => i.DataSourceId == ds.Id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(i => i.IsDeleted, true)
                            .SetProperty(i => i.DeletedAt, (DateTime?)now),
                            cancellationToken);

                    for (var i = 0; i < items.Count; i++)
                        _db.DataSourceItems.Add(DataSourceItem.Create(ds.Id, items[i].Value, items[i].Label, i));
                }

                await _db.SaveChangesAsync(cancellationToken);

                return Result.Ok("منبع داده ویرایش شد.");
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("ویرایش منبع داده انجام نشد؛ دوباره تلاش کنید.");
        }
    }
}
