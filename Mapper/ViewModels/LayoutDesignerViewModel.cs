using Borc.DataMapper.Domain.Templates;

namespace Borc.DataMapper.Web.ViewModels.TemplateLayouts;

public sealed record LayoutDesignerViewModel(
    long TemplateVersionId,
    long TemplateId,
    string TemplateName,
    int VersionNo,
    TemplateVersionStatus VersionStatus,
    bool HasLayout,
    bool IsPublished,
    string DataJson);

/// <summary>بدنه درخواست‌های JSON طراح.</summary>
public sealed record SaveLayoutRequest(long VersionId, string? LayoutJson);

public sealed record DeleteLayoutRequest(long VersionId);
