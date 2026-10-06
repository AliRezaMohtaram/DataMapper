using System.Collections.Concurrent;
using System.Reflection;
using Borc.DataMapper.Application.Common.Results;
using FluentValidation;
using MediatR;

namespace Borc.DataMapper.Application.Common.Validation;

/// <summary>
/// اجرای همهٔ validatorهای FluentValidation یک درخواست پیش از Handler.
/// در صورت خطا، Handler اجرا نمی‌شود و همان نوع خروجی Handler (Result یا Result&lt;T&gt;)
/// با Success=false، پیام خلاصه و خطاهای هر فیلد (ValidationErrors) برمی‌گردد.
/// درخواست‌هایی که validator ندارند بدون تغییر عبور می‌کنند.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken);

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        var message = string.Join(" — ", failures.Select(f => f.ErrorMessage).Distinct());

        return ValidationFailure.Create<TResponse>(message, errors)
               ?? throw new ValidationException(failures);
    }
}

/// <summary>ساخت نمونهٔ ناموفق از نوع خروجی Handler (Result یا Result&lt;T&gt;).</summary>
internal static class ValidationFailure
{
    private static readonly ConcurrentDictionary<Type, MethodInfo?> FailureMethods = new();

    public static TResponse? Create<TResponse>(string message, IReadOnlyDictionary<string, string[]> errors)
    {
        var type = typeof(TResponse);

        if (!typeof(Result).IsAssignableFrom(type))
            return default;

        var factory = FailureMethods.GetOrAdd(type, t =>
            t.GetMethod(nameof(Result.Failure), BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) }));

        if (factory?.Invoke(null, new object[] { message }) is not Result result)
            return default;

        result.ValidationErrors = errors;
        return (TResponse)(object)result;
    }
}
