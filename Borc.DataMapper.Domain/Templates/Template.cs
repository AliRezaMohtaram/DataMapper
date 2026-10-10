using Borc.DataMapper.Domain.Common;
using System;

namespace Borc.DataMapper.Domain.Templates
{
    public sealed class Template : EntityBase, IOrgUnitOwned
    {
        /// <summary>واحد سازمانی مالک (کلید چارت)؛ null = عمومی.</summary>
        public string? OrgUnitKey { get; private set; }

        public void AssignOrgUnit(string? orgUnitKey) =>
            OrgUnitKey = string.IsNullOrWhiteSpace(orgUnitKey) ? null : orgUnitKey.Trim();

        private Template()
        {
        }

        public string Code { get; private set; } = null!;

        public string Name { get; private set; } = null!;

        public string? Description { get; private set; }

        public TemplateStatus Status { get; private set; }

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
                Status = TemplateStatus.Draft,
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

        /// <summary>
        /// گذارهای مجاز: Draft→Active/Archived، Active→Inactive/Archived، Inactive→Active/Archived.
        /// Archived پایانی است.
        /// </summary>
        public bool CanChangeStatusTo(TemplateStatus target) => (Status, target) switch
        {
            (TemplateStatus.Draft, TemplateStatus.Active) => true,
            (TemplateStatus.Draft, TemplateStatus.Archived) => true,
            (TemplateStatus.Active, TemplateStatus.Inactive) => true,
            (TemplateStatus.Active, TemplateStatus.Archived) => true,
            (TemplateStatus.Inactive, TemplateStatus.Active) => true,
            (TemplateStatus.Inactive, TemplateStatus.Archived) => true,
            _ => false
        };

        public void ChangeStatus(TemplateStatus target)
        {
            if (!CanChangeStatusTo(target))
                throw new InvalidOperationException(
                    $"تغییر وضعیت از {Status} به {target} مجاز نیست.");

            Status = target;
            UpdatedAt = DateTime.UtcNow;
        }
    }



    /// <summary>وضعیت همان نسخه. مقدارها در ستون SMALLINT ذخیره می‌شوند (پیش‌فرض دیتابیس = 1 = Draft).</summary>
    public enum TemplateVersionStatus : short
    {
        Draft = 1,
        Published = 2,
        Archived = 3
    }
}