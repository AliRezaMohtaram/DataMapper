using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Infrastructure.Persistence;

public sealed class BorcDataMapperDbContext : DbContext, IAppDbContext
{
    public BorcDataMapperDbContext(
        DbContextOptions<BorcDataMapperDbContext> options)
        : base(options)
    {
    }

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<TemplateFieldAlias> TemplateFieldAliases => Set<TemplateFieldAlias>();
    public DbSet<TemplateLayout> TemplateLayouts => Set<TemplateLayout>();
    public DbSet<PredefinedRegex> PredefinedRegexes => Set<PredefinedRegex>();
    
    public DbSet<DataSource> DataSources => Set<DataSource>();

    public DbSet<MappingProfile> MappingProfiles => Set<MappingProfile>();
    public DbSet<MappingRule> MappingRules => Set<MappingRule>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();

    public DbSet<DataRecord> DataRecords => Set<DataRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BorcDataMapperDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}