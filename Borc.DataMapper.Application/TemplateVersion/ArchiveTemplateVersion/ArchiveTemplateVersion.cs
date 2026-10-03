using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateVersions.ArchiveTemplateVersion;

public sealed record ArchiveTemplateVersionCommand(long Id) : IRequest<Result>;

public sealed class ArchiveTemplateVersionHandler
    : IRequestHandler<ArchiveTemplateVersionCommand, Result>
{
    private readonly IAppDbContext _db;

    public ArchiveTemplateVersionHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        ArchiveTemplateVersionCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (version is null)
            return Result.Failure("نسخه موردنظر پیدا نشد.");

        if (version.Status == TemplateVersionStatus.Archived)
            return Result.Failure("این نسخه قبلاً بایگانی شده است.");

        version.Archive();

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok($"نسخه {version.VersionNo} بایگانی شد.");
    }
}