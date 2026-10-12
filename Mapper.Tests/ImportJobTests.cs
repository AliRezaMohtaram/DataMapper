using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.CreateDataSource;
using Borc.DataMapper.Application.Imports.ApplyImportMapping;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Application.Imports.CommitImportBatch;
using Borc.DataMapper.Application.Imports.UploadImportBatch;
using Borc.DataMapper.Application.TemplateFields.CreateTemplateField;
using Borc.DataMapper.Application.Templates.CreateTemplate;
using Borc.DataMapper.Application.TemplateVersions.CreateTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.PublishTemplateVersion;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Infrastructure.Persistence;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Mapper.Tests;

/// <summary>
/// Imports run their validation and commit in the background (ImportJobWorker): list fields are checked against
/// their data source, progress is reported, and a 20,000-row file (the maximum) goes through end to end.
/// </summary>
public sealed class ImportJobTests(MapperApp app, ITestOutputHelper output) : IClassFixture<MapperApp>
{
    [Fact]
    public async Task List_values_missing_from_the_data_source_make_the_row_invalid()
    {
        long versionId = await PublishedTemplateAsync("ایمپورت منبع");
        long batchId = await UploadAsync(versionId, [("کارگاه یک", "DRILL"), ("کارگاه دو", "XXX"), ("کارگاه سه", "PROD")]);

        await MapAsync(batchId);
        ImportBatch validated = await WaitAsync(batchId, ImportBatchStatus.Validated);
        Assert.Equal(2, validated.ValidRows);
        Assert.Equal(1, validated.InvalidRows);
        Assert.Equal(3, validated.ProcessedRows);

        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>();
            string? errors = await db.ImportRows.Where(r => r.ImportBatchId == batchId && r.Status == ImportRowStatus.Invalid)
                .Select(r => r.ErrorJson).SingleAsync();
            Assert.Contains("«XXX» در منبع داده وجود ندارد", JsonSerializer.Deserialize<string[]>(errors!)![0]);
        }

        await SendAsync(new CommitImportBatchCommand(batchId));
        ImportBatch committed = await WaitAsync(batchId, ImportBatchStatus.Imported);
        Assert.Equal(2, committed.ProcessedRows);
        Assert.Equal(2, await RecordCountAsync(versionId));

        // the page polls this while a step runs
        HttpClient admin = await app.AdminAsync();
        HttpResponseMessage progress = await admin.GetAsync($"/Imports/Progress/{batchId}");
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await progress.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("busy").GetBoolean());
        Assert.Equal("Imported", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_file_with_the_maximum_number_of_rows_is_validated_and_committed_in_the_background()
    {
        long versionId = await PublishedTemplateAsync("ایمپورت بزرگ");
        int rows = ImportLimits.MaxRows;
        var data = Enumerable.Range(1, rows).Select(i => ($"کارگاه {i}", i % 1000 == 0 ? "BAD" : i % 2 == 0 ? "DRILL" : "PROD")).ToList();

        Stopwatch watch = Stopwatch.StartNew();
        long batchId = await UploadAsync(versionId, data);
        output.WriteLine($"upload: {watch.Elapsed.TotalSeconds:0.0}s");

        watch.Restart();
        await MapAsync(batchId);
        ImportBatch validated = await WaitAsync(batchId, ImportBatchStatus.Validated, TimeSpan.FromMinutes(3));
        output.WriteLine($"validation: {watch.Elapsed.TotalSeconds:0.0}s");
        Assert.Equal(rows - rows / 1000, validated.ValidRows);
        Assert.Equal(rows / 1000, validated.InvalidRows);

        watch.Restart();
        await SendAsync(new CommitImportBatchCommand(batchId));
        ImportBatch committed = await WaitAsync(batchId, ImportBatchStatus.Imported, TimeSpan.FromMinutes(3));
        output.WriteLine($"commit: {watch.Elapsed.TotalSeconds:0.0}s, managed memory now {GC.GetTotalMemory(true) / 1_048_576} MB");
        Assert.Equal(validated.ValidRows, committed.ProcessedRows);
        Assert.Equal(validated.ValidRows, await RecordCountAsync(versionId));
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>A published template with a required text field (F001) and a list field (F002: DRILL, PROD).</summary>
    private async Task<long> PublishedTemplateAsync(string name)
    {
        long source = await SendAsync(new CreateDataSourceCommand(null, "نوع " + name, DataSourceType.StaticList, ItemsText: "DRILL | حفاری\nPROD | تولید"));
        long template = await SendAsync(new CreateTemplateCommand(null, name, null));
        long version = await SendAsync(new CreateTemplateVersionCommand(template));
        await SendAsync(new CreateTemplateFieldCommand(version, null, "نام کارگاه", FieldDataType.Text, "VARCHAR2", true, null, 100, null, null, null, null, null, null));
        await SendAsync(new CreateTemplateFieldCommand(version, null, "نوع", FieldDataType.Text, "VARCHAR2", false, null, 20, null, null, null, null, source, null));
        await SendAsync(new PublishTemplateVersionCommand(version));
        return version;
    }

    private async Task<long> UploadAsync(long versionId, IReadOnlyList<(string Site, string Kind)> rows)
    {
        byte[] content;
        using (XLWorkbook book = new())
        {
            IXLWorksheet sheet = book.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "نام کارگاه";
            sheet.Cell(1, 2).Value = "نوع";
            for (int i = 0; i < rows.Count; i++)
            {
                sheet.Cell(i + 2, 1).Value = rows[i].Site;
                sheet.Cell(i + 2, 2).Value = rows[i].Kind;
            }

            using MemoryStream stream = new();
            book.SaveAs(stream);
            content = stream.ToArray();
        }

        return await SendAsync(new UploadImportBatchCommand(versionId, $"import-{Guid.NewGuid():N}.xlsx", content, null));
    }

    private Task MapAsync(long batchId) => SendAsync(new ApplyImportMappingCommand(batchId,
    [
        new ColumnMappingInput { SourceColumn = "نام کارگاه", TargetFieldKey = "F001" },
        new ColumnMappingInput { SourceColumn = "نوع", TargetFieldKey = "F002" },
    ], null));

    /// <summary>Waits for the background worker to bring the batch to the given state.</summary>
    private async Task<ImportBatch> WaitAsync(long batchId, ImportBatchStatus status, TimeSpan? timeout = null)
    {
        DateTime until = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
            {
                ImportBatch batch = await scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>().ImportBatches
                    .AsNoTracking().SingleAsync(b => b.Id == batchId);
                if (batch.Status == status)
                    return batch;
                Assert.Null(batch.LastError);
                Assert.True(DateTime.UtcNow < until, $"import still {batch.Status} ({batch.ProcessedRows} rows done)");
            }

            await Task.Delay(100);
        }
    }

    private async Task<int> RecordCountAsync(long versionId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>().DataRecords.CountAsync(r => r.TemplateVersionId == versionId);
    }

    private async Task<T> SendAsync<T>(IRequest<Result<T>> request)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        Result<T> result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
        Assert.True(result.Success, result.Message);
        return result.Data!;
    }

    private async Task SendAsync(IRequest<Result> request)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        Result result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
        Assert.True(result.Success, result.Message);
    }
}
