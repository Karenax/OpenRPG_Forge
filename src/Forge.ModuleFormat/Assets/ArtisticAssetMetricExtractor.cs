using System.Buffers.Binary;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;

namespace Forge.ModuleFormat.Assets;

public sealed record ArtisticAssetMetrics(BlueprintId BlueprintId, AssetDimensions DimensionsMeters,
    int MaximumTexturePixels, int EstimatedTexelDensity, string ShaderFamily, string Provenance);

public static class ArtisticAssetMetricExtractor
{
    private const uint GlbMagic = 0x46546C67;
    private const uint JsonChunk = 0x4E4F534A;

    public static ArtisticAssetMetrics Extract(BlueprintId blueprintId, string glbPath,
        IReadOnlyList<string> texturePaths, string provenance)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var json = ReadGlbJson(glbPath);
        var textureDimensions = texturePaths.Select(ReadPngDimensions).ToArray();
        return ExtractDocument(blueprintId, json, textureDimensions, provenance);
    }

    public static ArtisticAssetMetrics ExtractDocument(BlueprintId blueprintId, ReadOnlySpan<byte> gltfJson,
        IReadOnlyList<(int Width, int Height)> textureDimensions, string provenance)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        ArgumentNullException.ThrowIfNull(textureDimensions);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        try
        {
            return ExtractDocumentCore(blueprintId, gltfJson, textureDimensions, provenance);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException
                                          or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException("ART_STYLE_GLB_DOCUMENT_INVALID", exception);
        }
    }

    private static ArtisticAssetMetrics ExtractDocumentCore(BlueprintId blueprintId, ReadOnlySpan<byte> gltfJson,
        IReadOnlyList<(int Width, int Height)> textureDimensions, string provenance)
    {
        using var document = JsonDocument.Parse(gltfJson.ToArray());
        var root = document.RootElement;
        var accessorIndex = root.GetProperty("meshes")[0].GetProperty("primitives")[0]
            .GetProperty("attributes").GetProperty("POSITION").GetInt32();
        var accessor = root.GetProperty("accessors")[accessorIndex];
        var min = accessor.GetProperty("min").EnumerateArray().Select(item => item.GetDouble()).ToArray();
        var max = accessor.GetProperty("max").EnumerateArray().Select(item => item.GetDouble()).ToArray();
        if (min.Length != 3 || max.Length != 3) throw new InvalidDataException("ART_STYLE_GLB_BOUNDS_INVALID");
        var dimensions = new AssetDimensions(max[0] - min[0], max[1] - min[1], max[2] - min[2]).Normalize();
        if (textureDimensions.Any(item => item.Width <= 0 || item.Height <= 0))
            throw new InvalidDataException("ART_STYLE_TEXTURE_INVALID");
        var maximumPixels = textureDimensions.Select(item => Math.Max(item.Width, item.Height)).DefaultIfEmpty(0).Max();
        if (maximumPixels <= 0) throw new InvalidDataException("ART_STYLE_TEXTURE_METRICS_MISSING");
        var longest = Math.Max(dimensions.Width, Math.Max(dimensions.Height, dimensions.Depth));
        var texelDensity = Math.Clamp((int)Math.Round(maximumPixels / longest), 16, 8192);
        var shader = root.TryGetProperty("materials", out var materials) && materials.GetArrayLength() > 0 &&
            materials[0].TryGetProperty("pbrMetallicRoughness", out _)
            ? "pbr-metallic-roughness" : "unknown";
        return new(blueprintId, dimensions, maximumPixels, texelDensity, shader, provenance.Trim());
    }

    private static byte[] ReadGlbJson(string path)
    {
        var bytes = File.ReadAllBytes(Path.GetFullPath(path));
        if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != GlbMagic)
            throw new InvalidDataException("ART_STYLE_GLB_INVALID");
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12, 4));
        var kind = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4));
        if (kind != JsonChunk || length <= 0 || 20 + length > bytes.Length)
            throw new InvalidDataException("ART_STYLE_GLB_JSON_MISSING");
        return bytes.AsSpan(20, length).ToArray();
    }

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        var bytes = File.ReadAllBytes(Path.GetFullPath(path));
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("ART_STYLE_TEXTURE_INVALID");
        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }
}
