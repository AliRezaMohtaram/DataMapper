using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Borc.DataMapper.Application.Abstractions.Persistence;


//IApp works like Repo , unitOfWork 

public interface IAppDbContext
{
    DbSet<Domain.Templates.Template> Templates { get; }
    DbSet<TemplateVersion> TemplateVersions { get; }
    DbSet<TemplateField> TemplateFields { get; }
    DbSet<TemplateFieldAlias> TemplateFieldAliases { get; }
    DbSet<TemplateLayout> TemplateLayouts { get; }
    DbSet<PredefinedRegex> PredefinedRegexes { get; }


    DbSet<Domain.DataSources.DataSource> DataSources { get; }

    DbSet<MappingProfile> MappingProfiles { get; }
    DbSet<MappingRule> MappingRules { get; }

    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<ImportRow> ImportRows { get; }

    DbSet<DataRecord> DataRecords { get; }
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}