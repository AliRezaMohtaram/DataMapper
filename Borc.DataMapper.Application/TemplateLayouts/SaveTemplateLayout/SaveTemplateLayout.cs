using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateLayouts.SaveTemplateLayout;

/// <summary>ذخیره (ایجاد یا جایگزینی) Layout یک نسخه پیش‌نویس؛ پس از ذخیره فعال است.</summary>
public sealed record SaveTemplateLayoutCommand(
    long TemplateVersionId,
    string LayoutJson
) : IRequest<Result>;

public sealed class SaveTemplateLayoutHandler
    : IRequestHandler<SaveTemplateLayoutCommand, Result>
{
    private readonly IAppDbContext _db;

    public SaveTemplateLayoutHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
     SaveTemplateLayoutCommand request,
     CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result.Failure("نسخه موردنظر پیدا نشد.");

        // فقط نسخه‌های آرشیوشده قابل ویرایش نیستند
        if (version.Status == TemplateVersionStatus.Archived)
            return Result.Failure("نسخه آرشیوشده قابل تغییر نیست.");

        var keys = (await _db.TemplateFields
                .Where(f => f.TemplateVersionId == version.Id)
                .Select(f => f.FieldKey)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var error = LayoutJsonValidator.Validate(request.LayoutJson, keys);

        if (error is not null)
            return Result.Failure(error);

        var layout = await _db.TemplateLayouts
            .Where(l => l.TemplateVersionId == version.Id)
            .OrderByDescending(l => l.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        if (layout is null)
        {
            layout = TemplateLayout.Create(version.Id, request.LayoutJson);
            _db.TemplateLayouts.Add(layout);
        }
        else
        {
            layout.UpdateLayout(request.LayoutJson);
        }

        layout.Publish();

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("ذخیره Layout انجام نشد؛ دوباره تلاش کنید.");
        }

        return Result.Ok("Layout ذخیره شد.");
    }
}
