using Borc.DataMapper.Application;
using Borc.DataMapper.Application.Abstractions.Files;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Infrastructure;
using Borc.DataMapper.Infrastructure.Files;
using Borc.DataMapper.Infrastructure.Http;
using Borc.DataMapper.Infrastructure.Persistence;

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();