using Borc.DataMapper.Application.Abstractions.Identity;
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
    private readonly IDataScopeProvider _scope;

    public BorcDataMapperDbContext(
        DbContextOptions<BorcDataMapperDbContext> options,
        IDataScopeProvider? scope = null)
        : base(options)
    {
        _scope = scope ?? new EverythingDataScope();
    }

    // محدودهٔ دادهٔ هر منبع؛ فیلترهای کوئری این ویژگی‌ها را در هر کوئری دوباره می‌خوانند.
    // Read by the query filters on every query (EF re-evaluates context members per query).
    internal DataScopeView TemplatesScope => _scope.For(DataScopeResources.Templates);
    internal DataScopeView DataSourcesScope => _scope.For(DataScopeResources.DataSources);
    internal DataScopeView MappingProfilesScope => _scope.For(DataScopeResources.MappingProfiles);
    internal DataScopeView ImportsScope => _scope.For(DataScopeResources.Imports);
    internal DataScopeView DataRecordsScope => _scope.For(DataScopeResources.DataRecords);

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<TemplateFieldAlias> TemplateFieldAliases => Set<TemplateFieldAlias>();
    public DbSet<TemplateLayout> TemplateLayouts => Set<TemplateLayout>();
    public DbSet<PredefinedRegex> PredefinedRegexes => Set<PredefinedRegex>();
    
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DataSourceItem> DataSourceItems => Set<DataSourceItem>();

    public DbSet<MappingProfile> MappingProfiles => Set<MappingProfile>();
    public DbSet<MappingRule> MappingRules => Set<MappingRule>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportBatchFile> ImportBatchFiles => Set<ImportBatchFile>();

    public DbSet<DataRecord> DataRecords => Set<DataRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BorcDataMapperDbContext).Assembly);

        // واحد سازمانی: ستون، ایندکس و فیلتر (بدون واحد = عمومی، وگرنه طبق محدودهٔ دادهٔ کاربر).
        modelBuilder.Entity<Template>().HasOrgUnitScope(e => !e.IsDeleted && (e.OrgUnitKey == null || TemplatesScope.All
            || (TemplatesScope.OwnerUserId != null && e.CreatedBy == TemplatesScope.OwnerUserId)
            || TemplatesScope.OrgUnitKeys.Contains(e.OrgUnitKey)));
        modelBuilder.Entity<DataSource>().HasOrgUnitScope(e => !e.IsDeleted && (e.OrgUnitKey == null || DataSourcesScope.All
            || (DataSourcesScope.OwnerUserId != null && e.CreatedBy == DataSourcesScope.OwnerUserId)
            || DataSourcesScope.OrgUnitKeys.Contains(e.OrgUnitKey)));
        modelBuilder.Entity<MappingProfile>().HasOrgUnitScope(e => !e.IsDeleted && (e.OrgUnitKey == null || MappingProfilesScope.All
            || (MappingProfilesScope.OwnerUserId != null && e.CreatedBy == MappingProfilesScope.OwnerUserId)
            || MappingProfilesScope.OrgUnitKeys.Contains(e.OrgUnitKey)));
        modelBuilder.Entity<ImportBatch>().HasOrgUnitScope(e => !e.IsDeleted && (e.OrgUnitKey == null || ImportsScope.All
            || (ImportsScope.OwnerUserId != null && e.CreatedBy == ImportsScope.OwnerUserId)
            || ImportsScope.OrgUnitKeys.Contains(e.OrgUnitKey)));
        modelBuilder.Entity<DataRecord>().HasOrgUnitScope(e => !e.IsDeleted && (e.OrgUnitKey == null || DataRecordsScope.All
            || (DataRecordsScope.OwnerUserId != null && e.CreatedBy == DataRecordsScope.OwnerUserId)
            || DataRecordsScope.OrgUnitKeys.Contains(e.OrgUnitKey)));

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Acl resource keys whose data scope filters each table (the web host's Mapper.* resources).</summary>
public static class DataScopeResources
{
    public const string Templates = "Mapper.Templates";
    public const string DataSources = "Mapper.DataSources";
    public const string MappingProfiles = "Mapper.MappingProfiles";
    public const string Imports = "Mapper.Imports";
    public const string DataRecords = "Mapper.DataRecords";
}

/// <summary>No data scope (tools, tests, work outside a request): every row.</summary>
internal sealed class EverythingDataScope : IDataScopeProvider
{
    public DataScopeView For(string resourceKey) => DataScopeView.Everything;
}

internal static class OrgUnitScopeExtensions
{
    /// <summary>OrgUnitKey column (nvarchar(256), indexed) and the row filter (replaces the soft-delete-only filter).</summary>
    public static void HasOrgUnitScope<T>(this Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> builder,
        System.Linq.Expressions.Expression<Func<T, bool>> filter)
        where T : class, Borc.DataMapper.Domain.Common.IOrgUnitOwned
    {
        builder.Property(e => e.OrgUnitKey).HasMaxLength(Borc.DataMapper.Domain.Common.IOrgUnitOwned.KeyMaxLength);
        builder.HasIndex(e => e.OrgUnitKey);
        builder.HasQueryFilter(filter);
    }
}
