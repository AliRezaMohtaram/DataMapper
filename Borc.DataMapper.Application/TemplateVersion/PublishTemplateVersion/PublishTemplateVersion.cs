using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateVersions.PublishTemplateVersion;

/// <summary>
/// انتشار نسخه پیش‌نویس، یا انتشار دوبارهٔ نسخهٔ بایگانی‌شده‌ای که قبلاً منتشر شده بود (برگشت سریع به نسخهٔ قبل).
/// همزمان نسخه منتشرشده قبلیِ همان قالب بایگانی می‌شود (هر لحظه فقط یک نسخه منتشرشده).
/// </summary>
public sealed record PublishTemplateVersionCommand(long Id) : IRequest<Result>;

public sealed class PublishTemplateVersionHandler
    : IRequestHandler<PublishTemplateVersionCommand, Result>
{
    private readonly IAppDbContext _db;

    public PublishTemplateVersionHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        PublishTemplateVersionCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (version is null)
            return Result.Failure("نسخه موردنظر پیدا نشد.");

        bool again = version.CanRepublish;
        if (version.Status != TemplateVersionStatus.Draft && !again)
            return Result.Failure("فقط نسخهٔ پیش‌نویس، یا نسخهٔ بایگانی‌شده‌ای که قبلاً منتشر شده، قابل انتشار است.");

        var hasFields = await _db.TemplateFields
            .AnyAsync(f => f.TemplateVersionId == version.Id, cancellationToken);

        if (!hasFields)
            return Result.Failure("برای انتشار، نسخه باید حداقل یک فیلد داشته باشد.");

        var previous = await _db.TemplateVersions
            .Where(v => v.TemplateId == version.TemplateId
                        && v.Status == TemplateVersionStatus.Published
                        && v.Id != version.Id)
            .ToListAsync(cancellationToken);

        foreach (var p in previous)
            p.Archive();

        if (again)
            version.Republish();
        else
            version.Publish();

        // هر دو تغییر در یک SaveChanges (یک تراکنش) ذخیره می‌شوند.
        await _db.SaveChangesAsync(cancellationToken);

        string archived = previous.Count == 0 ? "" : $"؛ نسخهٔ {string.Join("، ", previous.Select(p => p.VersionNo))} بایگانی شد";
        return Result.Ok(again ? $"نسخه {version.VersionNo} دوباره منتشر شد{archived}." : $"نسخه {version.VersionNo} منتشر شد{archived}.");
    }
}