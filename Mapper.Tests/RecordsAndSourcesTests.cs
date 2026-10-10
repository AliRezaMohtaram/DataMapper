using System.Net;
using Acl.Core.Admin;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataRecords.Common;
using Borc.DataMapper.Application.DataRecords.CreateDataRecord;
using Borc.DataMapper.Application.DataSources.CreateDataSource;
using Borc.DataMapper.Application.Imports.UploadImportBatch;
using Borc.DataMapper.Application.TemplateFields.CreateTemplateField;
using Borc.DataMapper.Application.Templates.CreateTemplate;
using Borc.DataMapper.Application.TemplateVersions.CreateTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.PublishTemplateVersion;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Infrastructure.Persistence;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrgChart.Core.Admin;

namespace Mapper.Tests;

/// <summary>
/// Generated codes and keys, records shown as a table (columns = template fields), the list under the entry form,
/// stored import files, the data source pages on MX, and units on Acl's role assignments page.
/// </summary>
public sealed class RecordsAndSourcesTests(MapperApp app) : IClassFixture<MapperApp>
{
    private static async Task<string> TextAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

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

    /// <summary>A published template "کارگاه" with a text field and a field whose options come from a static list.</summary>
    private async Task<(long TemplateId, long VersionId)> PublishedTemplateAsync(string name)
    {
        long source = await SendAsync(new CreateDataSourceCommand(null, "نوع کارگاه " + name, DataSourceType.StaticList, ItemsText: "DRILL | حفاری\nPROD | تولید"));
        long template = await SendAsync(new CreateTemplateCommand(null, name, null));
        long version = await SendAsync(new CreateTemplateVersionCommand(template));
        await SendAsync(new CreateTemplateFieldCommand(version, null, "نام کارگاه", FieldDataType.Text, "VARCHAR2", true, null, 100, null, null, null, null, null, null));
        await SendAsync(new CreateTemplateFieldCommand(version, null, "نوع", FieldDataType.Text, "VARCHAR2", false, null, 20, null, null, null, null, source, null));
        await SendAsync(new PublishTemplateVersionCommand(version));
        return (template, version);
    }

    [Fact]
    public async Task Codes_and_field_keys_are_generated_and_forms_do_not_ask_for_them()
    {
        (long templateId, long versionId) = await PublishedTemplateAsync("کدها");
        long next;

        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            BorcDataMapperDbContext db = scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>();
            Template template = await db.Templates.SingleAsync(t => t.Id == templateId);
            Assert.Matches("^TPL-[0-9]{4}$", template.Code);
            Assert.All(await db.DataSources.Select(d => d.Code).ToListAsync(), code => Assert.Matches("^DS-[0-9]{4}$", code));
            Assert.Equal(["F001", "F002"], await db.TemplateFields.Where(f => f.TemplateVersionId == versionId).OrderBy(f => f.SortOrder).Select(f => f.FieldKey).ToListAsync());

            // A new version keeps the keys; a field added to it continues the template's numbering.
            next = await SendAsync(new CreateTemplateVersionCommand(templateId));
            await SendAsync(new CreateTemplateFieldCommand(next, null, "توضیح", FieldDataType.Text, "VARCHAR2", false, null, 100, null, null, null, null, null, null));
            Assert.Equal(["F001", "F002", "F003"], await db.TemplateFields.Where(f => f.TemplateVersionId == next).OrderBy(f => f.SortOrder).Select(f => f.FieldKey).ToListAsync());
        }

        HttpClient admin = await app.AdminAsync();
        foreach ((string path, string input) in new[] { ("/Templates/Create", "name=\"Code\""), ("/DataSources/Create", "name=\"Code\""), ($"/TemplateFields/Create?templateVersionId={next}", "name=\"FieldKey\"") })
        {
            HttpResponseMessage response = await admin.GetAsync(path);
            Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode}");
            Assert.DoesNotContain(input, await TextAsync(response));
        }

        string chartForm = await TextAsync(await admin.GetAsync("/OrgChart/Units/Edit"));
        Assert.DoesNotContain("name=\"Input.Key\"", chartForm);
    }

    [Fact]
    public async Task Records_are_listed_as_a_table_whose_columns_are_the_template_fields()
    {
        (_, long versionId) = await PublishedTemplateAsync("جدول");
        foreach ((string site, string kind) in new[] { ("کارگاه اهواز", "DRILL"), ("کارگاه عسلویه", "PROD") })
        {
            SaveRecordOutcome saved = await SendAsync(new CreateDataRecordCommand(versionId, new Dictionary<string, string?> { ["F001"] = site, ["F002"] = kind }));
            Assert.True(saved.Saved, string.Join(", ", saved.FieldErrors.Values));
        }

        HttpClient admin = await app.AdminAsync();
        string list = await TextAsync(await admin.GetAsync($"/DataRecords?templateVersionId={versionId}"));
        Assert.Contains("<th>نام کارگاه</th>", list);
        Assert.Contains("<th>نوع</th>", list);
        Assert.Contains("کارگاه عسلویه", list);
        Assert.Contains("حفاری", list);            // the option's title, not the stored value
        Assert.DoesNotContain(">DRILL<", list);

        string form = await TextAsync(await admin.GetAsync($"/DataRecords/Create?versionId={versionId}"));
        Assert.Contains("id=\"rfRecent\"", form);
        Assert.Contains("کارگاه اهواز", form);

        string recent = await TextAsync(await admin.GetAsync($"/DataRecords/Recent?versionId={versionId}"));
        Assert.StartsWith("<header class=\"card-head\">", recent.TrimStart());
        Assert.Contains("۲ رکورد", recent);
    }

    [Fact]
    public async Task The_uploaded_import_file_is_kept_and_can_be_downloaded()
    {
        (_, long versionId) = await PublishedTemplateAsync("ایمپورت");
        byte[] content;
        using (XLWorkbook book = new())
        {
            IXLWorksheet sheet = book.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "نام کارگاه";
            sheet.Cell(2, 1).Value = "کارگاه بندرعباس";
            using MemoryStream stream = new();
            book.SaveAs(stream);
            content = stream.ToArray();
        }

        long batchId = await SendAsync(new UploadImportBatchCommand(versionId, "sites.xlsx", content, null));

        HttpClient admin = await app.AdminAsync();
        Assert.Contains("/Imports/Download/" + batchId, await TextAsync(await admin.GetAsync($"/Imports/Detail/{batchId}")));

        HttpResponseMessage download = await admin.GetAsync($"/Imports/Download/{batchId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("sites.xlsx", download.Content.Headers.ContentDisposition!.FileNameStar ?? download.Content.Headers.ContentDisposition.FileName);
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Data_source_pages_use_the_mx_components()
    {
        long id = await SendAsync(new CreateDataSourceCommand(null, "پیمانکاران", DataSourceType.StaticList, ItemsText: "C1 | پیمانکار یک"));
        HttpClient admin = await app.AdminAsync();

        foreach (string path in new[] { "/DataSources", $"/DataSources/Detail/{id}" })
        {
            HttpResponseMessage response = await admin.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            string html = await TextAsync(response);
            Assert.Contains("class=\"card rise\"", html);
            Assert.DoesNotContain("vx-", html);
            Assert.DoesNotContain("class=\"btn primary", html);
        }
    }

    [Fact]
    public async Task Role_assignments_show_the_unit_of_each_position()
    {
        int roleId;
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            IServiceProvider sp = scope.ServiceProvider;
            var chart = sp.GetRequiredService<IOrgChartAdministration>();
            string type = await chart.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput(null, "اداره"));
            string unit = await chart.CreateUnitAsync(new UnitInput(null, "ادارهٔ تدارکات", type));
            string position = await chart.CreatePositionAsync(new PositionInput(null, "کارشناس خرید", unit));
            roleId = await sp.GetRequiredService<IRoleAdministration>().CreateRoleAsync(new RoleInput { Name = "Buyers", IsActive = true });
            await sp.GetRequiredService<IAssignmentAdministration>().AddPositionRoleAsync(roleId, new PositionRoleInput { PositionKey = position });
        }

        HttpClient admin = await app.AdminAsync();
        string page = await TextAsync(await admin.GetAsync($"/Acl/Roles/Assignments/{roleId}"));
        Assert.Contains("<td>کارشناس خرید</td>", page);
        Assert.Contains("<td>ادارهٔ تدارکات</td>", page);
        Assert.Contains("acl-form--add", page);
    }
}
