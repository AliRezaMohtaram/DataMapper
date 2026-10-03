using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateVersions.CreateTemplateVersion;

/// <summary>
/// ساخت نسخه پیش‌نویس جدید؛ فیلدها، aliasها و Layout آخرین نسخه به آن کپی می‌شوند.
/// خروجی: شناسه نسخه جدید.
/// </summary>
public sealed record CreateTemplateVersionCommand(long TemplateId) : IRequest<Result<long>>;

public sealed class CreateTemplateVersionHandler
    : IRequestHandler<CreateTemplateVersionCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public CreateTemplateVersionHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        CreateTemplateVersionCommand request,
        CancellationToken cancellationToken)
    {
        var templateId = request.TemplateId;

        // ---- 1) اعتبارسنجی‌های اولیه (خارج از تراکنش) ----

        var templateExists = await _db.Templates
            .AnyAsync(x => x.Id == templateId, cancellationToken);

        if (!templateExists)
            return Result<long>.Failure("قالب موردنظر پیدا نشد.");

        var hasDraft = await _db.TemplateVersions.AnyAsync(
            v => v.TemplateId == templateId && v.Status == TemplateVersionStatus.Draft,
            cancellationToken);

        if (hasDraft)
            return Result<long>.Failure("این قالب یک نسخه پیش‌نویس دارد؛ ابتدا آن را منتشر یا حذف کنید.");

        // ---- 2) خواندن source (فقط خواندنی؛ خارج از تراکنش بی‌خطر است) ----

        var source = await _db.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        // ---- 3) اجرای اتمیک داخل execution strategy ----
        //
        //  • CreateExecutionStrategy برای سازگاری با EnableRetryOnFailure لازم است.
        //  • محاسبه maxNo داخل تراکنش انجام می‌شود تا از race condition جلوگیری شود.
        //  • کل عملیات به‌عنوان یک واحد retriable اجرا می‌شود.

        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database
                    .BeginTransactionAsync(cancellationToken);

                // محاسبه شماره نسخه داخل تراکنش تا همزمانی مشکلی ایجاد نکند.
                // IgnoreQueryFilters چون نسخه‌های حذف‌شده هم باید در شمارش بیایند.
                var maxNo = await _db.TemplateVersions
                    .IgnoreQueryFilters()
                    .Where(v => v.TemplateId == templateId)
                    .MaxAsync(v => (int?)v.VersionNo, cancellationToken) ?? 0;

                var version = TemplateVersion.Create(
                    templateId,
                    maxNo + 1,
                    source?.SchemaJson);

                _db.TemplateVersions.Add(version);
                await _db.SaveChangesAsync(cancellationToken);

                if (source is not null)
                    await CopyContentAsync(source.Id, version.Id, cancellationToken);

                await tx.CommitAsync(cancellationToken);

                return Result<long>.Ok(version.Id, $"نسخه {version.VersionNo} ایجاد شد.");
            });
        }
        catch (DbUpdateException)
        {
            // احتمالاً collision روی unique index (TemplateId, VersionNo).
            return Result<long>.Failure("ساخت نسخه انجام نشد؛ دوباره تلاش کنید.");
        }
    }

    /// <summary>
    /// کپی فیلدها، aliasها و آخرین Layout از نسخه مبدأ به نسخه جدید.
    /// فرض: caller داخل یک تراکنش فعال این متد را صدا می‌زند.
    /// </summary>
    private async Task CopyContentAsync(
        long sourceVersionId,
        long newVersionId,
        CancellationToken ct)
    {
        // --- فیلدها ---
        var sourceFields = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => f.TemplateVersionId == sourceVersionId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(ct);

        if (sourceFields.Count == 0)
        {
            // حتی اگر فیلدی نیست، باز Layout را کپی کن (ممکن است خالی باشد).
            await CopyLayoutAsync(sourceVersionId, newVersionId, ct);
            return;
        }

        var copies = new Dictionary<long, TemplateField>(sourceFields.Count);

        foreach (var f in sourceFields)
        {
            var copy = TemplateField.Create(
                newVersionId,
                f.FieldKey,
                f.Label,
                f.DataType,
                f.DbType,
                f.IsRequired,
                f.SortOrder,
                f.Length,
                f.Precision,
                f.Scale,
                f.Regex,
                f.DefaultValue,
                f.DataSourceId,
                f.ConfigJson);

            _db.TemplateFields.Add(copy);
            copies[f.Id] = copy;
        }

        // شناسه فیلدهای جدید برای ساخت aliasها لازم است.
        await _db.SaveChangesAsync(ct);

        // --- aliasها ---
        var oldIds = copies.Keys.ToList();

        var aliases = await _db.TemplateFieldAliases
            .AsNoTracking()
            .Where(a => oldIds.Contains(a.TemplateFieldId))
            .ToListAsync(ct);

        if (aliases.Count > 0)
        {
            foreach (var a in aliases)
            {
                _db.TemplateFieldAliases.Add(
                    TemplateFieldAlias.Create(copies[a.TemplateFieldId].Id, a.Alias));
            }
        }

        // --- Layout ---
        await CopyLayoutAsync(sourceVersionId, newVersionId, ct);

        await _db.SaveChangesAsync(ct);
    }

    private async Task CopyLayoutAsync(
        long sourceVersionId,
        long newVersionId,
        CancellationToken ct)
    {
        var layout = await _db.TemplateLayouts
            .AsNoTracking()
            .Where(l => l.TemplateVersionId == sourceVersionId)
            .OrderByDescending(l => l.VersionNo)
            .FirstOrDefaultAsync(ct);

        if (layout is not null)
        {
            _db.TemplateLayouts.Add(
                TemplateLayout.Create(newVersionId, layout.LayoutJson));
        }
    }
}