using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Infrastructure.Persistence.Repositories;

public sealed class TemplateRepository
    : ITemplateRepository
{
    private readonly BorcDataMapperDbContext _db;

    public TemplateRepository(
        BorcDataMapperDbContext db)
    {
        _db = db;
    }

    public async Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        return await _db.Templates
            .AsNoTracking()
            .AnyAsync(
                x => x.Code == code &&
                     !x.IsDeleted,
                cancellationToken);
    }

    public async Task AddAsync(
        Template template,
        CancellationToken cancellationToken)
    {
        await _db.Templates.AddAsync(
            template,
            cancellationToken);
    }
}