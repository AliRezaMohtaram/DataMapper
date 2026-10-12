using MReporter.Razor;
using MReporter.AspNetCore;
using MReporter.OrgChart;
using Microsoft.Extensions.DependencyInjection.Extensions;
﻿using Borc.DataMapper.Application;
using Borc.DataMapper.Application.Abstractions.Identity;
using Borc.DataMapper.Application.Abstractions.Files;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Infrastructure;
using Borc.DataMapper.Infrastructure.Files;
using Borc.DataMapper.Infrastructure.Http;
using Borc.DataMapper.Infrastructure.Persistence;
using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Web.Mvc;
using OrgChart.Acl;
using Borc.Users;
using Borc.Users.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // پیام‌های اعتبارسنجی پیش‌فرض MVC به فارسی
    options.ModelMetadataDetailsProviders.Add(new Borc.DataMapper.Web.Mvc.PersianValidationMetadataProvider());
    var messages = options.ModelBindingMessageProvider;
    messages.SetValueMustNotBeNullAccessor(_ => "این فیلد الزامی است.");
    messages.SetAttemptedValueIsInvalidAccessor((value, _) => $"مقدار «{value}» معتبر نیست.");
    messages.SetValueIsInvalidAccessor(value => $"مقدار «{value}» معتبر نیست.");
    messages.SetValueMustBeANumberAccessor(_ => "این فیلد باید عدد باشد.");
    messages.SetMissingBindRequiredValueAccessor(_ => "این فیلد الزامی است.");
});

// کاربران و ورود (ماژول Borc.Users): جدول‌ها در schema «usr» همان پایگاه داده؛ همهٔ صفحه‌ها نیاز به ورود دارند.
builder.Services.AddBorcUsers(
    builder.Configuration.GetConnectionString("BorcDataMapper")!,
    options => builder.Configuration.GetSection("Users").Bind(options));
builder.Services.AddBorcUsersUi(ui =>
{
    // Who manages accounts is decided in Acl (resource Mapper.Users), not by the account's administrator flag.
    ui.ViewPolicy = p => p.AddRequirements(new PermissionRequirement(MapperResources.Users, WellKnownActions.View));
    ui.ManagePolicy = p => p.AddRequirements(new PermissionRequirement(MapperResources.Users, WellKnownActions.Edit));
    ui.ShowAdministratorFlag = false;
});
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.Configure<VisibilityOptions>(builder.Configuration.GetSection("Mapper:Visibility"));
builder.Services.AddScoped<UserNames>();
builder.Services.AddScoped<DataVisibility>();   // who sees which records/imports (own, unit head, Acl data scope)
builder.Services.AddScoped<IDataScopeProvider>(sp => sp.GetRequiredService<DataVisibility>());
builder.Services.AddScoped<ICurrentUserUnits>(sp => sp.GetRequiredService<DataVisibility>());
builder.Services.AddScoped<OrgUnitChoices>();
builder.Services.AddScoped<IOrgUnitSelection>(sp => sp.GetRequiredService<OrgUnitChoices>());

// چارت سازمانی و دسترسی‌ها (ماژول‌های OrgChart و Acl): جدول‌ها در schemaهای org و acl همان پایگاه داده.
var connectionString = builder.Configuration.GetConnectionString("BorcDataMapper")!;
builder.Services.AddHostedService<ModuleDatabaseMigrator>(); // before AddAccessControl: Acl's startup work needs its tables
builder.Services.AddUserStatusListener<EndOrgChartRolesOnDeactivation>();
builder.Services.AddOrgChart()
    .AddSqlServerStore(connectionString)
    .AddHttpContextUser()
    .AddUserDirectory<MapperUserDirectory>()
    .AddAcl() // Acl reads positions (with delegations) from the chart; chart changes refresh Acl's cache
    .AddAdminUi(ui =>
    {
        ui.ViewPolicy = p => p.AddRequirements(new PermissionRequirement(MapperResources.OrgChart, WellKnownActions.View));
        ui.EditPolicy = p => p.AddRequirements(new PermissionRequirement(MapperResources.OrgChart, WellKnownActions.Edit));
    });
builder.Services.AddAccessControl(o =>
    {
        o.ApplicationKey = "Mapper";
        foreach (var id in builder.Configuration.GetSection("Acl:SuperAdminUserIds").Get<string[]>() ?? [])
        {
            o.SuperAdminUserIds.Add(id);
        }
    })
    .AddSqlServerStore(connectionString)
    .AddUserDirectory<MapperUserDirectory>()
    .AddAdminUi();
builder.Services.AddHostedService<MapperAccessBootstrapper>(); // after AddAccessControl: needs the synced resources

// Reports (MReporter module, schema rpt): Mapper's sources are the records of each template and the imports.
builder.Services.AddMReporter()
    .AddSqlServerStore(connectionString)
    .AddCurrentUser<Borc.DataMapper.Web.Modules.Reports.MapperReportUser>()
    .AddDataProvider<Borc.DataMapper.Web.Modules.Reports.MapperReportData>()
    .AddOptionProvider<Borc.DataMapper.Web.Modules.Reports.MapperReportOptions>()
    .AddOrgChart() // MReporter.OrgChart: audiences by unit/position, and the chart's units, positions and users in the report forms
    .AddUi(ui =>
    {
        ui.RunPolicy = p => p.AddRequirements(new PermissionRequirement(MapperResources.Reports, WellKnownActions.View));
        ui.ExportPolicy = p => p.AddRequirements(new PermissionRequirement(MapperResources.Reports, WellKnownActions.Export));
        ui.ManagePolicy = p => p.AddRequirements(new PermissionRequirement(MapperResources.Reports, WellKnownActions.Edit));
        ui.PrintFontUrl = "~/font/Vazir.woff2";
    });
builder.Services.AddScoped<Borc.DataMapper.Web.Modules.Reports.ListFilters>();
builder.Services.AddScoped<Borc.DataMapper.Web.Modules.Reports.MapperReportSources>();
builder.Services.Replace(ServiceDescriptor.Scoped<MReporter.Core.Abstractions.IReportSourceCatalog>(
    sp => sp.GetRequiredService<Borc.DataMapper.Web.Modules.Reports.MapperReportSources>()));

// Import validation and commit run in the background, one batch at a time (progress shown on the import page).
builder.Services.AddSingleton<Borc.DataMapper.Web.Background.ImportJobQueue>();
builder.Services.AddSingleton<Borc.DataMapper.Application.Abstractions.Imports.IImportJobQueue>(
    sp => sp.GetRequiredService<Borc.DataMapper.Web.Background.ImportJobQueue>());
builder.Services.AddHostedService<Borc.DataMapper.Web.Background.ImportJobWorker>(); // after the migrators

builder.Services
    .AddApplication().AddScoped<IExcelReader, ClosedXmlExcelReader>()
    .AddInfrastructure(builder.Configuration)
    .AddApiOptionFetcher(builder.Configuration.GetValue<bool>("DataSources:AllowPrivateNetworks"));



// Infrastructure/DependencyInjection.cs

builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<BorcDataMapperDbContext>());
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
// WWWROOT
app.UseStaticFiles();   

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAccessControl();
app.Use(async (context, next) =>
{
    // the user's units and the units they head, before any query is filtered
    await context.RequestServices.GetRequiredService<DataVisibility>().LoadAsync(context.RequestAborted);
    await next();
});
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapMReporter(); // options of list columns for the advanced filter

app.Run();
// Lets integration tests start the app (WebApplicationFactory<Program>).
public partial class Program;
