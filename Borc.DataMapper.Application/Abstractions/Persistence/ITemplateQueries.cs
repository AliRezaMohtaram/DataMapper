using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Templates.ListTemplates;

namespace Borc.DataMapper.Application.Abstractions.Persistence;

/// <summary>Read side for Template: no tracking, projects straight to DTOs.</summary>
public interface ITemplateQueries
{
    Task<PagedResult<TemplateListItemDto>> SearchAsync(
        ListTemplatesQuery query,
        CancellationToken cancellationToken);
}