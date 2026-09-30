using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Borc.DataMapper.Domain.Common;

public abstract class EntityBase
{
    public long Id { get; protected set; }

    public DateTime CreatedAt { get; protected set; }

    public long? CreatedBy { get; protected set; }

    public DateTime? UpdatedAt { get; protected set; }

    public long? UpdatedBy { get; protected set; }

    public DateTime? DeletedAt { get; protected set; }

    public long? DeletedBy { get; protected set; }

    public bool IsDeleted { get; protected set; }
}