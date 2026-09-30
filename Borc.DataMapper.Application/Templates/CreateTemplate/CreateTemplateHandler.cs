using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;

namespace Borc.DataMapper.Application.Templates.CreateTemplate;

public sealed class CreateTemplateHandler
    : IRequestHandler<CreateTemplateCommand, Result<long>>
{
    private readonly ITemplateRepository _templateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTemplateHandler(
        ITemplateRepository templateRepository,
        IUnitOfWork unitOfWork)
    {
        _templateRepository = templateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<long>> Handle(
        CreateTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await _templateRepository.ExistsByCodeAsync(
            request.Code,
            cancellationToken);

        if (exists)
        {
            return Result<long>.Failure(
                $"Template with code '{request.Code}' already exists.");
        }

        var template = Template.Create(
            request.Code,
            request.Name,
            request.Description);

        await _templateRepository.AddAsync(
            template,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return Result<long>.Ok(template.Id);
    }
}