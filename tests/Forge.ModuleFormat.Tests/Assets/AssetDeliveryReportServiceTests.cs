using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssetDeliveryReportServiceTests
{
    [Fact]
    public void MeasuresCatalogVaultModulePackageAndPerformanceAsOneBlockingReport()
    {
        using var fixture = new DeliveryFixture();

        var report = AssetDeliveryReportService.Measure(
            fixture.Catalog,
            fixture.Vault,
            fixture.Workspace,
            fixture.Package,
            fixture.Benchmark,
            fixture.Policy);

        Assert.True(report.Passed);
        Assert.Equal(16, report.Checks.Count);
        Assert.Equal(2, report.Catalog.Assets);
        Assert.Equal(2, report.Vault.Metadata.Files);
        Assert.Equal(2, report.Module.Manifests);
        Assert.Equal(2, report.Module.Chunks);
        Assert.Equal(2, report.Module.AssetReferences);
        Assert.Equal(2, report.Module.ReferencedManifestAssets);
        Assert.Equal(0, report.Module.UngovernedReferences);
        Assert.Equal(2, report.Performance.Checks);
        Assert.Contains("**PASS**", AssetDeliveryReportService.RenderMarkdown(report), StringComparison.Ordinal);
    }

    [Fact]
    public void FailsWhenAGovernedChunkChangesAfterManifestCreation()
    {
        using var fixture = new DeliveryFixture();
        File.AppendAllText(fixture.FirstChunk, "tampered");

        var report = AssetDeliveryReportService.Measure(
            fixture.Catalog,
            fixture.Vault,
            fixture.Workspace,
            fixture.Package,
            fixture.Benchmark,
            fixture.Policy);

        Assert.False(report.Passed);
        Assert.Equal(1, report.Module.ChunkIntegrityFailures);
        Assert.False(report.Checks.Single(check => check.Key == "module-chunk-integrity").Passed);
    }

    private sealed class DeliveryFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"forge-asset-delivery-{Guid.NewGuid():N}");
        private readonly Guid _firstId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        private readonly Guid _secondId = Guid.Parse("22222222-2222-4222-8222-222222222222");

        public string Vault { get; }
        public string Workspace { get; }
        public string Package { get; }
        public string Benchmark { get; }
        public string FirstChunk { get; }
        public AssetCatalog Catalog { get; }
        public AssetDeliveryReportService.Policy Policy { get; } = new(2, 2, 2, 2, 2, "CC0-1.0");

        public DeliveryFixture()
        {
            Vault = Path.Combine(_root, "vault");
            Workspace = Path.Combine(_root, "workspace");
            Package = Path.Combine(_root, "module.orpmod");
            Benchmark = Path.Combine(_root, "benchmark.json");
            FirstChunk = Path.Combine(Workspace, "assets", "imported", "first.glb");
            var secondChunk = Path.Combine(Workspace, "assets", "imported", "second.glb");
            Directory.CreateDirectory(Path.Combine(Vault, "originals"));
            Directory.CreateDirectory(Path.Combine(Vault, "normalized"));
            Directory.CreateDirectory(Path.Combine(Vault, "metadata"));
            Directory.CreateDirectory(Path.GetDirectoryName(FirstChunk)!);
            Directory.CreateDirectory(Path.Combine(Workspace, "areas"));
            File.WriteAllText(Path.Combine(Vault, "originals", "pack.zip"), "source");
            File.WriteAllText(Path.Combine(Vault, "normalized", "model.glb"), "normalized");
            File.WriteAllText(Path.Combine(Vault, "metadata", "first.json"), "{}");
            File.WriteAllText(Path.Combine(Vault, "metadata", "second.json"), "{}");
            File.WriteAllText(FirstChunk, "first-model");
            File.WriteAllText(secondChunk, "second-model");
            File.WriteAllText(Package, "orpmod-fixture");

            var firstHash = HashFile(FirstChunk);
            var secondHash = HashFile(secondChunk);
            var governedBytes = new FileInfo(FirstChunk).Length + new FileInfo(secondChunk).Length;
            File.WriteAllText(Path.Combine(Workspace, "assets", "asset-manifests.json"), JsonSerializer.Serialize(new
            {
                formatVersion = "1.0.0",
                manifests = new object[]
                {
                    Manifest(_firstId, "assets/imported/first.glb", firstHash, new FileInfo(FirstChunk).Length),
                    Manifest(_secondId, "assets/imported/second.glb", secondHash, new FileInfo(secondChunk).Length),
                },
            }));
            File.WriteAllText(Path.Combine(Workspace, "areas", "area.json"), JsonSerializer.Serialize(new
            {
                entities = new[]
                {
                    new { overrides = new { properties = new[] { new { key = "visual.asset", value = _firstId.ToString("D") } } } },
                },
                world = new { biomes = new[] { new { scatterResource = $"asset:{_secondId:D}" } } },
            }));
            File.WriteAllText(Benchmark, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                measuredAtUtc = "2026-08-05T00:00:00Z",
                passed = true,
                checks = new[] { new { passed = true }, new { passed = true } },
                sample = new
                {
                    terrainVertices = 1089,
                    terrainTriangles = 2048,
                    collisionShapes = 181,
                    scatterInstances = 357,
                    navigationPolygons = 1990,
                    lodAssetCount = 2,
                    lodLevelCount = 6,
                    cullingGroupCount = 3,
                    governedAssetBytes = governedBytes,
                    managedAllocatedBytes = 1024,
                    processWorkingSetDeltaBytes = 2048,
                    reconstructionMilliseconds = 10.0,
                    collisionMilliseconds = 2.0,
                    navigationMilliseconds = 3.0,
                    sculptureMilliseconds = 1.0,
                    navigationPathMilliseconds = 4.0,
                },
            }));

            Catalog = new AssetCatalog(
                "2026-08-05",
                "cc0-only",
                Vault,
                [CatalogItem("source:first"), CatalogItem("source:second")],
                "test");
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static object Manifest(Guid id, string path, string hash, long bytes) => new
        {
            assetId = id.ToString("D"),
            licenseSpdx = "CC0-1.0",
            chunks = new[] { new { relativePath = path, contentSha256 = hash, storedBytes = bytes } },
        };

        private static AssetCatalogItem CatalogItem(string externalId) => new(
            AssetCatalogItemId.ForExternalId(externalId),
            externalId,
            externalId,
            "source",
            externalId,
            "https://example.test/source",
            "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/",
            true,
            new string('a', 64),
            10,
            ["3d-model"],
            ["test"],
            "downloaded-and-extracted",
            AssetAvailability.ExtractedAvailable,
            "normalized/model.glb",
            null,
            null,
            $"asset-catalog:{externalId}");

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
