using System.Security.Cryptography;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssetRoundTripFinalizerTests
{
    [Fact]
    public void FinalizeValidWorkerOutputEnrichesTheModelManifest()
    {
        using var fixture = new RoundTripFixture();

        var result = AssetRoundTripFinalizer.Finalize(
            fixture.Root,
            fixture.Item,
            fixture.Manifest,
            fixture.Configuration);

        Assert.True(result.Succeeded);
        var manifest = result.Manifest!;
        var chunk = Assert.Single(manifest.Chunks);
        Assert.Equal(fixture.OutputHash, chunk.ContentSha256);
        Assert.Equal("assets/imported/apple/model.glb", chunk.RelativePath);
        Assert.Equal(chunk.Id, manifest.Collision!.ChunkId);
        Assert.All(manifest.Lod!.Levels, level => Assert.Equal(chunk.Id, level.ChunkId));
        var metadata = manifest.ModelMetadata!;
        Assert.Equal(500, metadata.Metrics.TriangleCount);
        Assert.Equal(99, metadata.Metrics.QualityScore);
        Assert.Equal(1.25m, metadata.Dimensions.ExtentXMeters);
        Assert.Equal(1.1m, metadata.Dimensions.ExtentYMeters);
        Assert.Equal(1.5m, metadata.Dimensions.ExtentZMeters);
        Assert.Contains("apple", metadata.Styles);
        Assert.Equal(ArtisticRealism.Realistic, metadata.Compatibility.Realism);
        Assert.False(metadata.Compatibility.RuntimeImportVerified);
        Assert.Equal("default", Assert.Single(metadata.Variants).Key);
        Assert.Equal(fixture.PreviewHash, metadata.PreviewContentSha256);
        Assert.Empty(AssetPackageManifestValidator.Validate(manifest));
    }

    [Fact]
    public void FinalizeRejectsAnArtifactChangedAfterTheWorkerResponse()
    {
        using var fixture = new RoundTripFixture();
        File.AppendAllText(fixture.OutputPath, "tampered");

        var result = AssetRoundTripFinalizer.Finalize(
            fixture.Root,
            fixture.Item,
            fixture.Manifest,
            fixture.Configuration);

        Assert.False(result.Succeeded);
        Assert.Null(result.Manifest);
        Assert.Equal(AssetRoundTripFinalizer.ArtifactInvalidCode, Assert.Single(result.Diagnostics).Code);
    }

    private sealed class RoundTripFixture : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"openrpg-roundtrip-{Guid.NewGuid():N}");

        public AssetCatalogItem Item { get; }

        public AssetPackageManifest Manifest { get; }

        public AssetExternalEditConfiguration Configuration { get; }

        public string OutputPath { get; }

        public string OutputHash { get; }

        public string PreviewHash { get; }

        public RoundTripFixture()
        {
            Directory.CreateDirectory(Root);
            var assetId = AssetCatalogItemId.ForExternalId("ambientcg:3DApple001");
            var sourceHash = new string('a', 64);
            Item = new AssetCatalogItem(
                assetId,
                "ambientcg:3DApple001",
                "3D Apple 001",
                "ambientcg",
                "3DApple001",
                "https://ambientcg.com/a/3DApple001",
                "CC0-1.0",
                "https://docs.ambientcg.com/license/",
                true,
                new string('b', 64),
                4096,
                ["3d-model"],
                ["apple", "fruit", "red"],
                "extracted",
                AssetAvailability.ExtractedAvailable,
                "normalized/ambientcg/3DApple001/model.obj",
                "normalized/ambientcg/3DApple001/preview.png",
                null,
                "asset-catalog:ambientcg:3DApple001");
            var chunkId = Guid.Parse("a3000000-0000-4000-8000-000000000001");
            Manifest = new AssetPackageManifest(
                AssetPackageManifest.CurrentSchemaVersion,
                AssetPackageManifestId.ForAsset(assetId),
                assetId,
                "author:test",
                "CC0-1.0",
                Item.ContentSha256,
                new Dictionary<AssetTargetPlatform, long>
                {
                    [AssetTargetPlatform.Windows] = 8 * 1024 * 1024,
                },
                [new AssetManifestChunk(
                    chunkId,
                    AssetChunkRole.Model,
                    true,
                    "assets/imported/apple/source.obj",
                    new string('c', 64),
                    16,
                    16,
                    AssetChunkCompression.None,
                    [AssetTargetPlatform.Windows])],
                new AssetCollisionManifest(
                    true,
                    null,
                    AssetCollisionGeneration.ConvexDecomposition,
                    ["default"],
                    [AssetTargetPlatform.Windows]),
                null,
                null,
                new AssetLodManifest(
                    false,
                    0.1m,
                    [new(0, null, 1m, 1000), new(1, null, 0.5m, 500)]));
            Configuration = new AssetExternalEditConfiguration(
                AssetExternalEditConfiguration.CurrentSchemaVersion,
                AssetExternalEditId.ForAsset(assetId),
                assetId,
                Manifest.Id,
                "external-edit:blender:ambientcg:3DApple001",
                "CC0-1.0",
                sourceHash,
                "normalized/ambientcg/3DApple001/model.obj",
                "assets/imported/apple/model.glb",
                AssetExternalTool.Blender,
                [
                    AssetExternalOperation.ValidateModel,
                    AssetExternalOperation.NormalizeModel,
                    AssetExternalOperation.GenerateCollision,
                    AssetExternalOperation.GenerateLods,
                    AssetExternalOperation.ExportGlb,
                    AssetExternalOperation.GenerateThumbnail,
                    AssetExternalOperation.AuditQuality,
                ],
                true,
                [AssetTargetPlatform.Windows]);

            OutputPath = WriteBytes("assets/imported/apple/model.glb", [0x67, 0x6c, 0x54, 0x46, 1, 2, 3, 4]);
            var previewPath = WriteBytes("assets/imported/apple/model.preview.png", [0x89, 0x50, 0x4e, 0x47, 5, 6, 7, 8]);
            OutputHash = HashFile(OutputPath);
            PreviewHash = HashFile(previewPath);
            var qualityRelativePath = "assets/imported/apple/model.quality.json";
            var qualityPath = Resolve(qualityRelativePath);
            File.WriteAllText(qualityPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                assetId = assetId.ToString(),
                sourceSha256 = sourceHash,
                licenseSpdx = "CC0-1.0",
                provenance = Configuration.ModificationProvenance,
                passed = true,
                metrics = new
                {
                    baseMeshCount = 1,
                    lodMeshCount = 2,
                    collisionMeshCount = 1,
                    vertexCount = 252,
                    triangleCount = 500,
                    materialCount = 1,
                    textureCount = 2,
                    dimensionsMeters = new[] { 1.25m, 1.5m, 1.1m },
                },
                scores = new { overall = 99 },
            }, JsonOptions));
            var responsePath = Resolve(AssetRoundTripFinalizer.ResponseRelativePath);
            File.WriteAllText(responsePath, JsonSerializer.Serialize(new
            {
                valid = true,
                executed = true,
                schemaVersion = BlenderWorkerRequestStore.CurrentSchemaVersion,
                jobId = Configuration.Id.Value,
                sourceSha256 = sourceHash,
                outputRelativePath = Configuration.OutputRelativePath,
                outputSha256 = OutputHash,
                outputBytes = new FileInfo(OutputPath).Length,
                preview = new
                {
                    relativePath = "assets/imported/apple/model.preview.png",
                    sha256 = PreviewHash,
                    bytes = new FileInfo(previewPath).Length,
                    width = 512,
                    height = 512,
                },
                quality = new { passed = true, scores = new { overall = 99 } },
                qualityReportRelativePath = qualityRelativePath,
            }, JsonOptions));
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private string WriteBytes(string relativePath, byte[] content)
        {
            var path = Resolve(relativePath);
            File.WriteAllBytes(path, content);
            return path;
        }

        private string Resolve(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return path;
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
