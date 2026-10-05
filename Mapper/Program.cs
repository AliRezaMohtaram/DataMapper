using Borc.DataMapper.Application;
using Borc.DataMapper.Application.Abstractions.Files;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Infrastructure;
using Borc.DataMapper.Infrastructure.Files;
using Borc.DataMapper.Infrastructure.Persistence;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services
    .AddApplication().AddScoped<IExcelReader, ClosedXmlExcelReader>()
    .AddInfrastructure(builder.Configuration);



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
    pattern: "{controller=Tempaltes}/{action=Index}/{id?}");

app.Run();