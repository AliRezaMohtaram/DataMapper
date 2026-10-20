using System.Net;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataRecords.Common;
using Borc.DataMapper.Application.DataRecords.CreateDataRecord;
using Borc.DataMapper.Application.Imports.GetImportLookups;
using Borc.DataMapper.Application.TemplateFields.CreateTemplateField;
using Borc.DataMapper.Application.Templates.ChangeTemplateStatus;
using Borc.DataMapper.Application.Templates.CreateTemplate;
using Borc.DataMapper.Application.Templates.Entry;
using Borc.DataMapper.Application.TemplateVersions.CreateTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.PublishTemplateVersion;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mapper.Tests;

/// <summary>
/// A deactivated (or archived) template takes no new data, as one without a published version; an archived version
/// that was published before can be published again (back to the previous version quickly).
/// </summary>
public sealed class TemplateLifecycleTests(MapperApp app) : IClassFixture<MapperApp>
{
    private static async Task<string> TextAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    private async Task<Result> SendAsync(IRequest<Result> request)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<Result<T>> SendAsync<T>(IRequest<Result<T>> request)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private async Task<long> VersionWithFieldAsync(long template, string field)
    {
        long version = (await SendAsync(new CreateTemplateVersionCommand(template))).Data;
        Assert.True((await SendAsync(new CreateTemplateFieldCommand(version, null, field, FieldDataType.Text, "VARCHAR2", false, null, 50, null, null, null, null, null, null))).Success);
        return version;
    }

    [Fact]
    public async Task A_deactivated_template_takes_no_new_records_until_it_is_active_again()
    {
        long template = (await SendAsync(new CreateTemplateCommand(null, "قالب غیرفعال‌شونده", null))).Data;
        long version = await VersionWithFieldAsync(template, "عنوان");
        Assert.True((await SendAsync(new PublishTemplateVersionCommand(version))).Success);
        Assert.True((await SendAsync(new ChangeTemplateStatusCommand(template, TemplateStatus.Active))).Success);
        Assert.True((await SendAsync(new CreateDataRecordCommand(version, new Dictionary<string, string?> { ["F001"] = "اول" }))).Success);

        Assert.True((await SendAsync(new ChangeTemplateStatusCommand(template, TemplateStatus.Inactive))).Success);
        Result<SaveRecordOutcome> refused = await SendAsync(new CreateDataRecordCommand(version, new Dictionary<string, string?> { ["F001"] = "دوم" }));
        Assert.False(refused.Success);
        Assert.Equal(TemplateEntryPolicy.InactiveMessage, refused.Message);

        // not offered for entry or import, no "new record" link, the form itself refused
        Assert.DoesNotContain((await SendAsync(new GetImportLookupsQuery())).Data!.Versions, v => v.Id == version);
        Assert.Contains((await SendAsync(new GetImportLookupsQuery(ForEntry: false))).Data!.Versions, v => v.Id == version && !v.AcceptsData);
        HttpClient admin = await app.AdminAsync();
        string list = await TextAsync(await admin.GetAsync($"/DataRecords/Version?templateVersionId={version}"));
        Assert.Contains("اول", list);
        Assert.DoesNotContain($"/DataRecords/Create?versionId={version}", list);
        Assert.DoesNotContain("copyFrom=", list);
        HttpResponseMessage form = await admin.GetAsync($"/DataRecords/Create?versionId={version}");
        Assert.Equal(HttpStatusCode.Redirect, form.StatusCode);

        Assert.True((await SendAsync(new ChangeTemplateStatusCommand(template, TemplateStatus.Active))).Success);
        Assert.True((await SendAsync(new CreateDataRecordCommand(version, new Dictionary<string, string?> { ["F001"] = "سوم" }))).Success);
        Assert.Contains($"/DataRecords/Create?versionId={version}", await TextAsync(await admin.GetAsync($"/DataRecords/Version?templateVersionId={version}")));
    }

    [Fact]
    public async Task The_records_of_every_version_of_a_template_are_shown_together()
    {
        long template = (await SendAsync(new CreateTemplateCommand(null, "قالب چندنسخه‌ای", null))).Data;
        long first = await VersionWithFieldAsync(template, "عنوان");
        Assert.True((await SendAsync(new PublishTemplateVersionCommand(first))).Success);
        Assert.True((await SendAsync(new CreateDataRecordCommand(first, new Dictionary<string, string?> { ["F001"] = "رکورد نسخهٔ یک" }))).Success);

        // the second version keeps «عنوان» (same key) and adds «توضیح»
        long second = (await SendAsync(new CreateTemplateVersionCommand(template))).Data;
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>();
            if (!await db.TemplateFields.AnyAsync(f => f.TemplateVersionId == second))
                Assert.True((await SendAsync(new CreateTemplateFieldCommand(second, null, "عنوان", FieldDataType.Text, "VARCHAR2", false, null, 50, null, null, null, null, null, null))).Success);
        }

        Assert.True((await SendAsync(new CreateTemplateFieldCommand(second, null, "توضیح", FieldDataType.Text, "VARCHAR2", false, null, 50, null, null, null, null, null, null))).Success);
        Assert.True((await SendAsync(new PublishTemplateVersionCommand(second))).Success);
        Assert.True((await SendAsync(new CreateDataRecordCommand(second, new Dictionary<string, string?> { ["F001"] = "رکورد نسخهٔ دو", ["F002"] = "با توضیح" }))).Success);

        HttpClient admin = await app.AdminAsync();
        string overview = await TextAsync(await admin.GetAsync("/DataRecords"));
        Assert.Contains($"data-row-href=\"/DataRecords/Version?templateId={template}\"", overview);
        Assert.Contains($"/DataRecords/Version?templateVersionId={first}", overview);
        Assert.Contains($"/DataRecords/Version?templateVersionId={second}", overview);

        string all = await TextAsync(await admin.GetAsync($"/DataRecords/Version?templateId={template}"));
        Assert.Contains("همهٔ نسخه‌ها", all);
        Assert.Contains("رکورد نسخهٔ یک", all);
        Assert.Contains("رکورد نسخهٔ دو", all);
        Assert.Contains("<th>نسخه</th>", all);
        Assert.Contains("<th>عنوان</th>", all);
        Assert.Contains("<th>توضیح</th>", all);
        // only the record of the published version can be edited (row click + edit icon); the other opens its page
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(all, "/DataRecords/Edit/").Count);

        string one = await TextAsync(await admin.GetAsync($"/DataRecords/Version?templateVersionId={first}"));
        Assert.Contains("رکورد نسخهٔ یک", one);
        Assert.DoesNotContain("رکورد نسخهٔ دو", one);

        HttpResponseMessage excel = await admin.GetAsync($"/DataRecords/Export?templateId={template}");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
    }

    [Fact]
    public async Task An_archived_version_can_be_published_again_and_the_current_one_is_archived()
    {
        long template = (await SendAsync(new CreateTemplateCommand(null, "قالب با برگشت نسخه", null))).Data;
        long first = await VersionWithFieldAsync(template, "عنوان");
        Assert.True((await SendAsync(new PublishTemplateVersionCommand(first))).Success);
        long second = await VersionWithFieldAsync(template, "عنوان جدید");
        Result published = await SendAsync(new PublishTemplateVersionCommand(second));
        Assert.True(published.Success);
        Assert.Contains("بایگانی شد", published.Message);

        HttpClient admin = await app.AdminAsync();
        Assert.Contains("انتشار دوباره", await TextAsync(await admin.GetAsync($"/TemplateVersions/Detail/{first}")));

        Result again = await SendAsync(new PublishTemplateVersionCommand(first));
        Assert.True(again.Success, again.Message);
        Assert.Contains("دوباره منتشر شد", again.Message);
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>();
            Dictionary<long, TemplateVersionStatus> states = await db.TemplateVersions.Where(v => v.TemplateId == template).ToDictionaryAsync(v => v.Id, v => v.Status);
            Assert.Equal(TemplateVersionStatus.Published, states[first]);
            Assert.Equal(TemplateVersionStatus.Archived, states[second]);
        }

        Assert.True((await SendAsync(new CreateDataRecordCommand(first, new Dictionary<string, string?> { ["F001"] = "دوباره" }))).Success);
        Assert.False((await SendAsync(new PublishTemplateVersionCommand(first))).Success); // already the published one

        // an archived draft (never published) does not come back
        long draft = await VersionWithFieldAsync(template, "پیش‌نویس");
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>();
            (await db.TemplateVersions.FirstAsync(v => v.Id == draft)).Archive();
            await db.SaveChangesAsync();
        }

        Assert.False((await SendAsync(new PublishTemplateVersionCommand(draft))).Success);
    }
}
