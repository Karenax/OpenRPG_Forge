using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Forge.Network.Transport;

public sealed record PublicInternetAddressLookupResult(
    bool Succeeded,
    string? Address,
    string Code);

public sealed class PublicInternetAddressResolver
{
    public const string SuccessCode = "NET_PUBLIC_ADDRESS_RESOLVED";
    public const string LookupFailedCode = "NET_PUBLIC_ADDRESS_LOOKUP_FAILED";
    public const string InvalidResponseCode = "NET_PUBLIC_ADDRESS_RESPONSE_INVALID";
    public const int MaximumResponseBytes = 64;

    public static Uri DefaultEndpoint { get; } = new("https://api.ipify.org/");

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;

    public PublicInternetAddressResolver(HttpClient httpClient, Uri? endpoint = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _endpoint = endpoint ?? DefaultEndpoint;
        if (!_endpoint.IsAbsoluteUri || _endpoint.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("NET_PUBLIC_ADDRESS_ENDPOINT_INVALID", nameof(endpoint));
    }

    public async Task<PublicInternetAddressLookupResult> ResolveIpv4Async(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);
            request.Headers.Accept.ParseAdd("text/plain");
            request.Headers.UserAgent.ParseAdd("OpenRPG-Forge-GameLauncher/0.1");
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength is > MaximumResponseBytes)
                return Failed(LookupFailedCode);

            var payload = await ReadBoundedPayloadAsync(response.Content, cancellationToken);
            if (payload is null || !IPAddress.TryParse(payload.Trim(), out var address) ||
                !IsGloballyRoutableIpv4(address))
                return Failed(InvalidResponseCode);

            return new PublicInternetAddressLookupResult(true, address.ToString(), SuccessCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(LookupFailedCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return Failed(LookupFailedCode);
        }
    }

    public static bool IsGloballyRoutableIpv4(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = address.GetAddressBytes();
        var first = bytes[0];
        var second = bytes[1];
        var third = bytes[2];

        if (first is 0 or 10 or 127 || first >= 224) return false;
        if (first == 100 && second is >= 64 and <= 127) return false;
        if (first == 169 && second == 254) return false;
        if (first == 172 && second is >= 16 and <= 31) return false;
        if (first == 192 && second == 168) return false;
        if (first == 192 && second == 0 && third is 0 or 2) return false;
        if (first == 192 && second == 88 && third == 99) return false;
        if (first == 198 && second is 18 or 19) return false;
        if (first == 198 && second == 51 && third == 100) return false;
        if (first == 203 && second == 0 && third == 113) return false;
        return true;
    }

    private static async Task<string?> ReadBoundedPayloadAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumResponseBytes + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (read == 0) break;
            length += read;
        }

        return length > MaximumResponseBytes
            ? null
            : Encoding.ASCII.GetString(buffer, 0, length);
    }

    private static PublicInternetAddressLookupResult Failed(string code) => new(false, null, code);
}
