using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Infrastructure.Persistence;

public sealed class BorcDataMapperDbContext : DbContext
{
    public BorcDataMapperDbContext(
        DbContextOptions<BorcDataMapperDbContext> options)
        : base(options)
    {
    }

    public DbSet<Template> Templates => Set<Template>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BorcDataMapperDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}