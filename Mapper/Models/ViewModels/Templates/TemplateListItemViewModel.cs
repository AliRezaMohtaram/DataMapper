namespace Borc.DataMapper.Web.Models.ViewModels.Templates;

public sealed class TemplateListItemViewModel
{
    public long Id { get; init; }

    public string Code { get; init; } = null!;

    public string Name { get; init; } = null!;

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }
}