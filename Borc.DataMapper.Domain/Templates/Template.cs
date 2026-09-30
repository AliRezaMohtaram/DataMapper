using Borc.DataMapper.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Borc.DataMapper.Domain.Templates
{
    public sealed class Template : EntityBase
    {
        private Template()
        {
        }

        public string Code { get; private set; } = null!;

        public string Name { get; private set; } = null!;

        public string? Description { get; private set; }

        public bool IsActive { get; private set; }

        public static Template Create(
            string code,
            string name,
            string? description = null)
        {
            return new Template
            {
                Code = code.Trim(),
                Name = name.Trim(),
                Description = description?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(
            string name,
            string? description)
        {
            Name = name.Trim();
            Description = description?.Trim();
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
