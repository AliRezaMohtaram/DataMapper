using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Imports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.GetImportProgress;

/// <summary>Light state of an import for the progress bar (polled while a background step runs).</summary>
public sealed record GetImportProgressQuery(long Id) : IRequest<Result<ImportProgressDto>>;

/// <param name="Total">Rows the running step goes through: all rows while validating, the valid ones while committing.</param>
public sealed record ImportProgressDto(ImportBatchStatus Status, int Processed, int Total, string? LastError)
{
    public bool IsBusy => Status is ImportBatchStatus.Validating or ImportBatchStatus.Committing;
}

public sealed class GetImportProgressHandler : IRequestHandler<GetImportProgressQuery, Result<ImportProgressDto>>
{
    private readonly IAppDbContext _db;

    public GetImportProgressHandler(IAppDbContext db) => _db = db;

    public async Task<Result<ImportProgressDto>> Handle(GetImportProgressQuery request, CancellationToken cancellationToken)
    {
        var dto = await _db.ImportBatches.AsNoTracking()
            .Where(b => b.Id == request.Id)
            .Select(b => new ImportProgressDto(
                b.Status,
                b.ProcessedRows,
                b.Status == ImportBatchStatus.Committing || b.Status == ImportBatchStatus.Imported ? b.ValidRows : b.TotalRows,
                b.LastError))
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result<ImportProgressDto>.Failure("ایمپورت موردنظر پیدا نشد.")
            : Result<ImportProgressDto>.Ok(dto);
    }
}
