namespace Borc.DataMapper.Web.ViewModels;

/// <summary>آیتم منوی کناری. Controllers: کنترلرهایی که این آیتم را «فعال» نشان می‌دهند.</summary>
public sealed record NavItem(string Section, string Controller, string Label, string Icon, string[] Controllers);
