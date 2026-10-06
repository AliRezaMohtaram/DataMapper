using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Domain.DataSources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.GetDataSource;

public sealed record GetDataSourceQuery(long Id) : IRequest<Result<DataSourceDetailDto>>;

public sealed record DataSourceDetailDto(
    long Id,
    string Code,
    string Name,
    DataSourceType SourceType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int UsedByFieldCount,
    int ItemCount,
    string? ItemsText,
    long? TemplateId,
    string? TemplateName,
    string? ValueKey,
    string? DisplayKey,
    string? ApiUrl,
    string? ApiItemsPath,
    string? ApiValueKey,
    string? ApiDisplayKey,
    string? FileName,
    string? FileValueColumn,
    string? FileDisplayColumn,
    DateTime? FileImportedAt);

public sealed class GetDataSourceHandler
    : IRequestHandler<GetDataSourceQuery, Result<DataSourceDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetDataSourceHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<DataSourceDetailDto>> Handle(
        GetDataSourceQuery request,
        CancellationToken cancellationToken)
    {
        var ds = await _db.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ds is null)
            return Result<DataSourceDetailDto>.Failure("منبع داده موردنظر پیدا نشد.");

        var used = await _db.TemplateFields
            .CountAsync(f => f.DataSourceId == ds.Id, cancellationToken);

        var itemCount = 0;
        string? itemsText = null;

        if (ds.SourceType is DataSourceType.StaticList or DataSourceType.File)
        {
            itemCount = await _db.DataSourceItems.CountAsync(i => i.DataSourceId == ds.Id, cancellationToken);

            if (ds.SourceType == DataSourceType.StaticList)
            {
                var items = await _db.DataSourceItems
                    .AsNoTracking()
                    .Where(i => i.DataSourceId == ds.Id)
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Take(DataSourceSettings.MaxItems)
                    .Select(i => new { i.Value, i.Label })
                    .ToListAsync(cancellationToken);

                itemsText = DataSourceSettings.ToItemsText(items.Select(i => (i.Value, i.Label)));
            }
        }

        long? templateId = null;
        string? templateName = null, valueKey = null, displayKey = null;
        string? apiUrl = null, apiPath = null, apiValue = null, apiDisplay = null;
        string? fileName = null, fileValue = null, fileDisplay = null;
        DateTime? fileAt = null;

        switch (ds.SourceType)
        {
            case DataSourceType.Template:
            {
                var cfg = DataSourceConfigJson.Parse<TemplateSourceConfig>(ds.ConfigJson);

                if (cfg is not null)
                {
                    templateId = cfg.TemplateId;
                    valueKey = cfg.ValueKey;
                    displayKey = cfg.DisplayKey;
                    templateName = await _db.Templates
                        .AsNoTracking()
                        .Where(t => t.Id == cfg.TemplateId)
                        .Select(t => t.Name)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                break;
            }

            case DataSourceType.Api:
            {
                var cfg = DataSourceConfigJson.Parse<ApiSourceConfig>(ds.ConfigJson);

                if (cfg is not null)
                {
                    apiUrl = cfg.Url;
                    apiPath = cfg.ItemsPath;
                    apiValue = cfg.ValueKey;
                    apiDisplay = cfg.DisplayKey;
                }

                break;
            }

            case DataSourceType.File:
            {
                var cfg = DataSourceConfigJson.Parse<FileSourceConfig>(ds.ConfigJson);

                if (cfg is not null)
                {
                    fileName = cfg.FileName;
                    fileValue = cfg.ValueColumn;
                    fileDisplay = cfg.DisplayColumn;
                    fileAt = cfg.ImportedAt;
                }

                break;
            }
        }

        return Result<DataSourceDetailDto>.Ok(new DataSourceDetailDto(
            ds.Id, ds.Code, ds.Name, ds.SourceType, ds.IsActive, ds.CreatedAt, ds.UpdatedAt,
            used, itemCount, itemsText,
            templateId, templateName, valueKey, displayKey,
            apiUrl, apiPath, apiValue, apiDisplay,
            fileName, fileValue, fileDisplay, fileAt));
    }
}
