using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Borc.DataMapper.Application.Abstractions.Persistence;

/// <summary>
/// دسترسی Handlerها به دیتابیس. پیاده‌سازی: BorcDataMapperDbContext.
/// </summary>
public interface IAppDbContext
{
    DbSet<Template> Templates { get; }
    DbSet<TemplateVersion> TemplateVersions { get; }
    DbSet<TemplateField> TemplateFields { get; }
    DbSet<TemplateFieldAlias> TemplateFieldAliases { get; }
    DbSet<TemplateLayout> TemplateLayouts { get; }
    DbSet<PredefinedRegex> PredefinedRegexes { get; }

    DbSet<DataSource> DataSources { get; }
    DbSet<DataSourceItem> DataSourceItems { get; }

    DbSet<MappingProfile> MappingProfiles { get; }
    DbSet<MappingRule> MappingRules { get; }

    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<ImportRow> ImportRows { get; }

    DbSet<DataRecord> DataRecords { get; }

    /// <summary>برای تراکنش‌های چندمرحله‌ای (مثلاً کپی نسخه).</summary>
    DatabaseFacade Database { get; }

    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
