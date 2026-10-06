using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Borc.DataMapper.Web.Mvc;

/// <summary>
/// پیام فارسی برای Requiredهایی که پیام ندارند — از جمله Required ضمنیِ
/// پارامترهای string غیر nullable در commandها («The X field is required.»).
/// </summary>
public sealed class PersianValidationMetadataProvider : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var required in context.ValidationMetadata.ValidatorMetadata.OfType<RequiredAttribute>())
        {
            if (string.IsNullOrEmpty(required.ErrorMessage) && required.ErrorMessageResourceType is null)
                required.ErrorMessage = "این فیلد الزامی است.";
        }
    }
}
