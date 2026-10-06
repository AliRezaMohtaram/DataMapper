using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Borc.DataMapper.Application.Common.Results;

public class Result
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    /// <summary>
    /// خطاهای اعتبارسنجی به تفکیک نام ویژگی درخواست (مثلاً "Name")؛ فقط وقتی ValidationBehavior
    /// درخواست را رد کرده پر است. لایهٔ وب آن‌ها را زیر همان فیلدهای فرم نشان می‌دهد.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; internal set; }

    public static Result Ok(string? message = null)
    {
        return new Result
        {
            Success = true,
            Message = message
        };
    }

    public static Result Failure(string message)
    {
        return new Result
        {
            Success = false,
            Message = message
        };
    }
}

public sealed class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Ok(
        T data,
        string? message = null)
    {
        return new Result<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public new static Result<T> Failure(string message)
    {
        return new Result<T>
        {
            Success = false,
            Message = message
        };
    }
}