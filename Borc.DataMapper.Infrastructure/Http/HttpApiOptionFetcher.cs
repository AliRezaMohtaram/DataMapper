using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Borc.DataMapper.Application.Abstractions.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Borc.DataMapper.Infrastructure.Http;

/// <summary>
/// خواندن JSON از API خارجی برای منبع دادهٔ نوع Api.
/// محافظ SSRF: فقط http/https، بدون تغییر مسیر خودکار، و اتصال به آدرس‌های محلی/شبکهٔ داخلی
/// (loopback، 10/8، 172.16/12، 192.168/16، link-local، ...) در لحظهٔ اتصال رد می‌شود؛
/// مگر اینکه AllowPrivateNetworks روشن باشد (برای APIهای داخل سازمان).
/// </summary>
public sealed class HttpApiOptionFetcher : IApiOptionFetcher
{
    private const int MaxResponseBytes = 2 * 1024 * 1024;

    private readonly HttpClient _http;

    public HttpApiOptionFetcher(HttpClient http)
    {
        _http = http;
    }

    public async Task<ApiFetchResult> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ApiFetchResult.Fail("آدرس API معتبر نیست.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode is >= 300 and < 400)
                return ApiFetchResult.Fail("API درخواست را به آدرس دیگری هدایت کرد؛ آدرس نهایی را وارد کنید.");

            if (!response.IsSuccessStatusCode)
                return ApiFetchResult.Fail($"API خطا برگرداند ({(int)response.StatusCode}).");

            if (response.Content.Headers.ContentLength is > MaxResponseBytes)
                return ApiFetchResult.Fail("پاسخ API بیش از حد بزرگ است (حداکثر 2 مگابایت).");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;

            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                    return ApiFetchResult.Fail("پاسخ API بیش از حد بزرگ است (حداکثر 2 مگابایت).");

                buffer.Write(chunk, 0, read);
            }

            return ApiFetchResult.Success(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiFetchResult.Fail("پاسخ API در زمان مجاز نرسید.");
        }
        catch (HttpRequestException ex)
        {
            return ApiFetchResult.Fail("اتصال به API برقرار نشد: " + (ex.InnerException?.Message ?? ex.Message));
        }
    }
}

public static class ApiOptionFetcherRegistration
{
    /// <summary>
    /// services.AddApiOptionFetcher(configuration.GetValue&lt;bool&gt;("DataSources:AllowPrivateNetworks"));
    /// نیازمند پکیج Microsoft.Extensions.Http در پروژهٔ Infrastructure.
    /// </summary>
    public static IServiceCollection AddApiOptionFetcher(
        this IServiceCollection services,
        bool allowPrivateNetworks = false)
    {
        services
            .AddHttpClient<IApiOptionFetcher, HttpApiOptionFetcher>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Borc-DataMapper/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                ConnectCallback = async (context, ct) =>
                {
                    var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                    var allowed = addresses
                        .Where(a => allowPrivateNetworks || IsPublic(a))
                        .ToArray();

                    if (allowed.Length == 0)
                        throw new HttpRequestException("اتصال به آدرس‌های محلی یا شبکهٔ داخلی مجاز نیست.");

                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

                    try
                    {
                        await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            });

        return services;
    }

    private static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                return false;

            var first = address.GetAddressBytes()[0];
            return (first & 0xFE) != 0xFC; // fc00::/7 (unique local)
        }

        var b = address.GetAddressBytes();

        return !(b[0] == 0
                 || b[0] == 10
                 || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                 || (b[0] == 169 && b[1] == 254)
                 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                 || (b[0] == 192 && b[1] == 168)
                 || b[0] >= 224);
    }
}
