using Borc.DataMapper.Application;
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
// Lets integration tests start the app (WebApplicationFactory<Program>).
public partial class Program;
