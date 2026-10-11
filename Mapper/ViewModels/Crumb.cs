namespace Borc.DataMapper.Web.ViewModels;

/// <summary>
/// یک حلقه از مسیر صفحه (breadcrumb). _Layout خودش حلقهٔ اول (بخش منو) و آخر (عنوان صفحه) را می‌گذارد؛
/// صفحه‌ها حلقه‌های میانی را با ViewData["Crumbs"] می‌دهند.
/// </summary>
public sealed record Crumb(string Label, string? Url);
