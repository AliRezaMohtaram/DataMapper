namespace Borc.DataMapper.Application.Abstractions.Imports;

/// <summary>
/// Hands an import batch to the background worker, which runs its pending step (validation or commit) through
/// <see cref="Borc.DataMapper.Application.Imports.RunImportJob.RunImportJobCommand"/>.
/// </summary>
public interface IImportJobQueue
{
    void Enqueue(long importBatchId);
}
