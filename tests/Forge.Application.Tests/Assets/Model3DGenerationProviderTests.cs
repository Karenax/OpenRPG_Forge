using System.Net;
using System.Text;
using Forge.Application.Assets;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Assets;

public sealed class Model3DGenerationProviderTests
{
    [Fact]
    public async Task OfflineProviderIsDeterministicLicensedAndNetworkFree()
    {
        var provider = new DeterministicModel3DGenerationProvider();
        var request = Request(networkAuthorized: false);

        var first = await provider.GenerateAsync(request);
        var second = await provider.GenerateAsync(request);

        Assert.Equal(first.ObjContent, second.ObjContent);
        Assert.Equal(12, first.TriangleCount);
        Assert.Equal("CC0-1.0", first.LicenseSpdx);
        Assert.Equal("deterministic-offline-3d", first.ProviderKey);
        Assert.Contains("f 4 5 8", Encoding.UTF8.GetString(first.ObjContent), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoopbackProviderUsesTypedContractEnvironmentSecretAndCc0Response()
    {
        var obj = "# generated\no model\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nv 0 0 1\nf 1 2 3\nf 1 3 4\nf 1 2 5\nf 2 3 5\n";
        var json = $$"""{"objBase64":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes(obj))}}","triangleCount":4,"licenseSpdx":"CC0-1.0","provenance":"trellis:test"}""";
        var handler = new RecordingHandler(json);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var provider = new LoopbackModel3DGenerationProvider(Definition(), client,
            name => name == "OPENRPG_TRELLIS_TOKEN" ? "secret" : null);

        var result = await provider.GenerateAsync(Request(networkAuthorized: true));

        Assert.Equal("http://127.0.0.1:8091/generate", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer secret", handler.Authorization);
        Assert.Contains("\"maximumTriangles\":10000", handler.Body, StringComparison.Ordinal);
        Assert.Equal(obj, Encoding.UTF8.GetString(result.ObjContent));
        Assert.Equal("CC0-1.0", result.LicenseSpdx);
    }

    [Fact]
    public async Task LoopbackProviderBlocksUnapprovedNetworkAndNonCc0Output()
    {
        var handler = new RecordingHandler("{\"objBase64\":\"dmVy\",\"triangleCount\":4,\"licenseSpdx\":\"MIT\",\"provenance\":\"bad\"}");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var provider = new LoopbackModel3DGenerationProvider(Definition(), client, _ => "secret");

        var denied = await Assert.ThrowsAsync<Model3DProviderException>(() =>
            provider.GenerateAsync(Request(networkAuthorized: false)));
        var invalid = await Assert.ThrowsAsync<Model3DProviderException>(() =>
            provider.GenerateAsync(Request(networkAuthorized: true)));

        Assert.Equal(LoopbackModel3DGenerationProvider.NetworkDeniedCode, denied.Code);
        Assert.Equal(LoopbackModel3DGenerationProvider.ResponseInvalidCode, invalid.Code);
        Assert.Equal(1, handler.Count);
    }

    private static Model3DProviderDefinition Definition() => new(
        1, Guid.Parse("b1330000-0000-4000-8000-000000000020"), "trellis-local",
        Model3DProviderKind.TrellisLocal, "http://127.0.0.1:8091/generate", "OPENRPG_TRELLIS_TOKEN",
        true, 5, 4 * 1024 * 1024, new HashSet<string>(["CC0-1.0"], StringComparer.Ordinal), "provider:test");

    private static Model3DProviderRequest Request(bool networkAuthorized) => new(
        Guid.Parse("b1330000-0000-4000-8000-000000000021"), Model3DInputKind.Text, "stone shrine", null, null,
        20260806, 10_000, networkAuthorized);

    private sealed class RecordingHandler(string response) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Count++;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }
}
