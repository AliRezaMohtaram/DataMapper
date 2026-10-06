namespace Borc.DataMapper.Application.Abstractions.Http;

/// <summary>خواندن JSON از یک API خارجی برای منبع دادهٔ نوع Api. پیاده‌سازی (با محافظ SSRF) در Infrastructure است.</summary>
public interface IApiOptionFetcher
{
    Task<ApiFetchResult> GetJsonAsync(string url, CancellationToken cancellationToken);
}

public sealed record ApiFetchResult(bool Ok, string? Json, string? Error)
{
    public static ApiFetchResult Success(string json) => new(true, json, null);

    public static ApiFetchResult Fail(string error) => new(false, null, error);
}
