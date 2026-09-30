using Borc.DataMapper.Application.Common.Results;
using MediatR;

namespace Borc.DataMapper.Application.Templates.CreateTemplate;

public sealed record CreateTemplateCommand(
    string Code,
    string Name,
    string? Description
) : IRequest<Result<long>>;