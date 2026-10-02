using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Templates.DeleteTemplate;

public sealed record DeleteTemplateCommand(long Id) : IRequest<Result>;

public sealed class DeleteTemplateHandler
    : IRequestHandler<DeleteTemplateCommand, Result>
{
    private readonly IAppDbContext _db;

    public DeleteTemplateHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        DeleteTemplateCommand request,
        CancellationToken cancellationToken)
    {
        // فیلتر سراسری، قالب‌های قبلاً حذف‌شده را کنار می‌گذارد.
        var template = await _db.Templates
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure("قالب موردنظر پیدا نشد.");

        // TODO: وقتی TemplateVersion / DataRecord نوشته شد، حذف قالبِ دارای داده را مسدود کنید.
        template.MarkAsDeleted();

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("قالب حذف شد.");
    }
}