using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class DeterministicModel3DGenerationProvider : IModel3DGenerationProvider
{
    public Task<Model3DProviderResponse> GenerateAsync(
        Model3DProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Prompt) ||
            request.MaximumTriangles is < 12 or > 500_000)
            throw new Model3DProviderException("assistant.model3d.request_invalid", "The deterministic 3D request is invalid.");
        if (request.InputKind == Model3DInputKind.Image &&
            (request.ImageContent is not { Length: > 0 } || string.IsNullOrWhiteSpace(request.ImageSha256)))
            throw new Model3DProviderException("assistant.model3d.image_missing", "Image-to-3D requires bounded image content.");

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{request.Prompt.Trim()}|{request.Seed}"));
        var hash = BinaryPrimitives.ReadUInt32LittleEndian(digest);
        var width = 0.75m + hash % 50 / 100m;
        var height = 1.00m + hash / 50 % 75 / 100m;
        var depth = 0.75m + hash / 3750 % 50 / 100m;
        var x = width / 2m;
        var z = depth / 2m;
        var ci = CultureInfo.InvariantCulture;
        var obj = $"# OpenRPG Forge deterministic offline 3D provider\n" +
                  $"# prompt-seed {request.Seed}\n" +
                  "o GeneratedModel\n" +
                  $"v {(-x).ToString(ci)} 0 {(-z).ToString(ci)}\n" +
                  $"v {x.ToString(ci)} 0 {(-z).ToString(ci)}\n" +
                  $"v {x.ToString(ci)} 0 {z.ToString(ci)}\n" +
                  $"v {(-x).ToString(ci)} 0 {z.ToString(ci)}\n" +
                  $"v {(-x).ToString(ci)} {height.ToString(ci)} {(-z).ToString(ci)}\n" +
                  $"v {x.ToString(ci)} {height.ToString(ci)} {(-z).ToString(ci)}\n" +
                  $"v {x.ToString(ci)} {height.ToString(ci)} {z.ToString(ci)}\n" +
                  $"v {(-x).ToString(ci)} {height.ToString(ci)} {z.ToString(ci)}\n" +
                  "f 1 3 2\nf 1 4 3\nf 5 6 7\nf 5 7 8\nf 1 2 6\nf 1 6 5\n" +
                  "f 2 3 7\nf 2 7 6\nf 3 4 8\nf 3 8 7\nf 4 1 5\nf 4 5 8\n";
        return Task.FromResult(new Model3DProviderResponse(
            Model3DProviderDefinition.CreateOffline().Key,
            Encoding.UTF8.GetBytes(obj),
            12,
            "CC0-1.0",
            $"openrpg-forge:model3d-provider:offline:{request.RequestId:D}",
            0m));
    }
}
