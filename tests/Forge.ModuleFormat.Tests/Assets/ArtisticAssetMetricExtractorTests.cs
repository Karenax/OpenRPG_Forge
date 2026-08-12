using System.Buffers.Binary;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class ArtisticAssetMetricExtractorTests
{
    [Fact]
    public void ExtractReadsGlbBoundsPngResolutionAndPbrShaderFamily()
    {
        var directory = Directory.CreateTempSubdirectory("openrpg-art-metrics-");
        try
        {
            var glbPath = Path.Combine(directory.FullName, "pillar.glb");
            var texturePath = Path.Combine(directory.FullName, "pillar.png");
            File.WriteAllBytes(glbPath, CreateGlb("""
                {"asset":{"version":"2.0"},"accessors":[{"min":[-1,0,-0.5],"max":[1,3,0.5]}],"meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],"materials":[{"pbrMetallicRoughness":{}}]}
                """));
            File.WriteAllBytes(texturePath, CreatePngHeader(1024, 1536));

            var metrics = ArtisticAssetMetricExtractor.Extract(
                BlueprintId.From(Guid.Parse("a3200000-0000-4000-8000-000000000001")),
                glbPath,
                [texturePath],
                "test://artistic-metrics/pillar");

            Assert.Equal(2, metrics.DimensionsMeters.Width);
            Assert.Equal(3, metrics.DimensionsMeters.Height);
            Assert.Equal(1, metrics.DimensionsMeters.Depth);
            Assert.Equal(1536, metrics.MaximumTexturePixels);
            Assert.Equal(512, metrics.EstimatedTexelDensity);
            Assert.Equal("pbr-metallic-roughness", metrics.ShaderFamily);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ExtractRejectsMalformedSourceDocuments()
    {
        Assert.Throws<InvalidDataException>(() => ArtisticAssetMetricExtractor.ExtractDocument(
            BlueprintId.From(Guid.Parse("a3200000-0000-4000-8000-000000000002")),
            "{}"u8,
            [(1024, 1024)],
            "test://artistic-metrics/invalid"));
    }

    private static byte[] CreateGlb(string json)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedLength = (jsonBytes.Length + 3) & ~3;
        var bytes = new byte[20 + paddedLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), paddedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x4E4F534A);
        jsonBytes.CopyTo(bytes.AsSpan(20));
        bytes.AsSpan(20 + jsonBytes.Length).Fill((byte)' ');
        return bytes;
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        var bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }
}
