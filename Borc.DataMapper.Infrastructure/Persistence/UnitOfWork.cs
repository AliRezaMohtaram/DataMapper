using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Borc.DataMapper.Application.Abstractions.Persistence;

namespace Borc.DataMapper.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BorcDataMapperDbContext _db;

    public UnitOfWork(
        BorcDataMapperDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}