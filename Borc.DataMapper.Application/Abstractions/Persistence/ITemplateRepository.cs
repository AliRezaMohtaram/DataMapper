using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Borc.DataMapper.Domain.Templates;

namespace Borc.DataMapper.Application.Abstractions.Persistence;

public interface ITemplateRepository
{
    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task AddAsync(
        Template template,
        CancellationToken cancellationToken);
}