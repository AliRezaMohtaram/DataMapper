using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Templates.ListTemplates;

namespace Borc.DataMapper.Web.ViewModels.Templates;

public sealed record TemplateIndexViewModel(
    ListTemplatesQuery Filter,
    PagedResult<TemplateListItemDto> Result);