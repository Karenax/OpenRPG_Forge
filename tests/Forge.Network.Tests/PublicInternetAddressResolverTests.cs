using System.Net;
using System.Text;
using Forge.Network.Transport;

namespace Forge.Network.Tests;

public sealed class PublicInternetAddressResolverTests
{
    [Fact]
    public async Task ResolveIpv4Async_ReturnsTheValidatedPublicAddress()
    {
        using var client = Client(HttpStatusCode.OK, "8.8.4.4\n");
        var resolver = new PublicInternetAddressResolver(client);

        var result = await resolver.ResolveIpv4Async();

        Assert.True(result.Succeeded);
        Assert.Equal("8.8.4.4", result.Address);
        Assert.Equal(PublicInternetAddressResolver.SuccessCode, result.Code);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("100.64.12.4")]
    [InlineData("192.168.1.20")]
    [InlineData("203.0.113.42")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("not-an-address")]
    public async Task ResolveIpv4Async_RejectsAddressesThatCannotBeSharedOnThePublicInternet(string payload)
    {
        using var client = Client(HttpStatusCode.OK, payload);
        var resolver = new PublicInternetAddressResolver(client);

        var result = await resolver.ResolveIpv4Async();

        Assert.False(result.Succeeded);
        Assert.Null(result.Address);
        Assert.Equal(PublicInternetAddressResolver.InvalidResponseCode, result.Code);
    }

    [Fact]
    public async Task ResolveIpv4Async_FailsClosedForHttpAndOversizedResponses()
    {
        using var failedClient = Client(HttpStatusCode.ServiceUnavailable, "unavailable");
        var failed = await new PublicInternetAddressResolver(failedClient).ResolveIpv4Async();
        using var oversizedClient = Client(HttpStatusCode.OK, new string('1', 65));
        var oversized = await new PublicInternetAddressResolver(oversizedClient).ResolveIpv4Async();

        Assert.False(failed.Succeeded);
        Assert.Equal(PublicInternetAddressResolver.LookupFailedCode, failed.Code);
        Assert.False(oversized.Succeeded);
        Assert.Equal(PublicInternetAddressResolver.LookupFailedCode, oversized.Code);
    }

    [Fact]
    public void Constructor_RejectsAnUnencryptedLookupEndpoint()
    {
        using var client = Client(HttpStatusCode.OK, "8.8.8.8");

        var exception = Assert.Throws<ArgumentException>(() =>
            new PublicInternetAddressResolver(client, new Uri("http://address.example.test/")));

        Assert.Equal("endpoint", exception.ParamName);
        Assert.Contains("NET_PUBLIC_ADDRESS_ENDPOINT_INVALID", exception.Message, StringComparison.Ordinal);
    }

    private static HttpClient Client(HttpStatusCode statusCode, string payload) => new(
        new StubHandler(statusCode, payload))
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private sealed class StubHandler(HttpStatusCode statusCode, string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(PublicInternetAddressResolver.DefaultEndpoint, request.RequestUri);
            Assert.Equal("text/plain", Assert.Single(request.Headers.Accept).MediaType);
            Assert.Contains("OpenRPG-Forge-GameLauncher/0.1", request.Headers.UserAgent.ToString(),
                StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(payload, Encoding.ASCII, "text/plain"),
            });
        }
    }
}
