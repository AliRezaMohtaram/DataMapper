using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Borc.DataMapper.Domain.Templates
{
 public sealed record GetTemplateQuery(long Id) : IRequest<Result<TemplateDetailDto>>;

}
