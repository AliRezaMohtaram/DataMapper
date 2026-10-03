using Borc.DataMapper.Application.Common.Validation;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;

namespace Borc.DataMapper.Application.TemplateFields;

/// <summary>ورودی‌های مشترک فرم ساخت و ویرایش فیلد؛ قواعد اعتبارسنجی یک‌بار نوشته می‌شود.</summary>
public interface ITemplateFieldInput
{
    string Label { get; }
    FieldDataType DataType { get; }
    string DbType { get; }
    bool IsRequired { get; }
    int? Length { get; }
    byte? Precision { get; }
    byte? Scale { get; }
    string? Regex { get; }
    string? DefaultValue { get; }
    long? DataSourceId { get; }
    string? ConfigJson { get; }
}

public static class TemplateFieldRules
{
    public static void ApplyFieldRules<T>(this AbstractValidator<T> validator)
        where T : ITemplateFieldInput
    {
        validator.RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(250);

        validator.RuleFor(x => x.DataType).IsInEnum();

        validator.RuleFor(x => x.DbType)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^[A-Za-z0-9_(), ]+$")
            .WithMessage("نوع در مقصد فقط می‌تواند شامل حروف انگلیسی، عدد و علائم _ ( ) , باشد.");

        validator.RuleFor(x => x.Length)
            .Must(l => l is null or (>= 1 and <= 4000))
            .WithMessage("طول باید بین 1 و 4000 باشد.");

        validator.RuleFor(x => x.Precision)
            .Must(p => p is null or (>= 1 and <= 38))
            .WithMessage("Precision باید بین 1 و 38 باشد.");

        validator.RuleFor(x => x.Scale)
            .Must(s => s is null or (>= 0 and <= 38))
            .WithMessage("Scale باید بین 0 و 38 باشد.");

        validator.RuleFor(x => x.Scale)
            .Must((x, s) => s is null || x.Precision is null || s <= x.Precision)
            .WithMessage("Scale نمی‌تواند از Precision بیشتر باشد.");

        validator.RuleFor(x => x.Regex)
            .MaximumLength(1000)
            .Must(ValidationRules.IsValidRegex)
            .WithMessage("الگوی Regex معتبر نیست.");

        validator.RuleFor(x => x.DefaultValue)
            .MaximumLength(2000);

        validator.RuleFor(x => x.ConfigJson)
            .Must(ValidationRules.IsValidJson)
            .WithMessage("ConfigJson معتبر نیست؛ باید JSON درست باشد.");
    }
}