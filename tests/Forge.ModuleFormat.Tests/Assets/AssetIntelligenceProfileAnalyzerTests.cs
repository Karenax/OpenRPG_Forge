using System.Security.Cryptography;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssetIntelligenceProfileAnalyzerTests
{
    [Fact]
    public void AnalyzeBuildsIndividualIdentityGeometryPivotSemanticsAndDependencyClosure()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-asset-intelligence", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var model = Path.Combine(root, "oak-door.gltf");
            var buffer = Path.Combine(root, "oak-door.bin");
            File.WriteAllBytes(buffer, [1, 2, 3, 4]);
            File.WriteAllText(model, """
                {
                  "asset": { "version": "2.0" },
                  "buffers": [{ "uri": "oak-door.bin", "byteLength": 4 }],
                  "accessors": [
                    { "count": 4, "type": "VEC3", "min": [-1, 0, -0.25], "max": [1, 2, 0.25] },
                    { "count": 6, "type": "SCALAR" }
                  ],
                  "materials": [{ "name": "Oak" }],
                  "images": [{ "uri": "data:image/png;base64,AA==" }],
                  "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 }, "indices": 1 }] }],
                  "animations": [{ "name": "open" }]
                }
                """);
            var item = Item("fixture:oak-door", model);

            var profile = AssetIntelligenceProfileAnalyzer.Analyze(item, model);

            Assert.Empty(AssetIntelligenceProfileValidator.Validate(profile));
            Assert.Equal("door", profile.SemanticType);
            Assert.Equal("interactive-leaf", profile.AssemblyRole);
            Assert.Equal("opening", profile.Placement.Surface);
            Assert.Equal("hinge-side", profile.Placement.SuggestedPivot!.Kind);
            Assert.True(profile.Placement.SuggestedPivot.RequiresReview);
            Assert.Equal("center-base", profile.Geometry!.PivotClass);
            Assert.Equal(2, profile.Geometry.DimensionsMeters!.X);
            Assert.Equal(2, profile.Geometry.DimensionsMeters.Y);
            Assert.Equal(0.5, profile.Geometry.DimensionsMeters.Z);
            Assert.Equal(4, profile.Technical.VertexCount);
            Assert.Equal(2, profile.Technical.TriangleCount);
            Assert.Equal(["oak"], profile.Technical.Materials);
            Assert.Equal(["open"], profile.Technical.Animations);
            Assert.Equal(Hash(model), profile.ModelContentSha256);
            Assert.Equal(64, profile.DependencyClosureSha256.Length);
            Assert.NotEqual(item.ContentSha256, profile.ModelContentSha256);
            Assert.Contains("porte", profile.Description("fr"), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("generated-review-required", profile.ReviewStatus);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidBinaryStillProducesTraceableSemanticProfileWithoutBlockingImport()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-asset-intelligence", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var model = Path.Combine(root, "crate.glb");
            File.WriteAllText(model, "not-a-glb");

            var profile = AssetIntelligenceProfileAnalyzer.Analyze(Item("fixture:crate", model), model);

            Assert.Equal(64, profile.ModelContentSha256.Length);
            Assert.Equal(64, profile.DependencyClosureSha256.Length);
            Assert.Null(profile.Geometry);
            Assert.Equal("semantic-enriched", profile.Readiness);
            Assert.Contains(profile.Evidence, item => item.Source == "model-inspection");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IndexStoreRoundTripsProfilesAtomically()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-asset-intelligence-index", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var profile = AssetIntelligenceProfile.CreateUnclassified("fixture:crate", "Crate", "fixture");

            var saved = AssetIntelligenceIndexStore.Save(root, [profile]);
            var loaded = AssetIntelligenceIndexStore.Load(root);

            Assert.True(saved.Succeeded, string.Join(" | ", saved.Diagnostics.Select(item => item.Message)));
            Assert.True(loaded.Succeeded, string.Join(" | ", loaded.Diagnostics.Select(item => item.Message)));
            var reloaded = Assert.Single(loaded.Profiles);
            Assert.Equal(profile.ExternalId, reloaded.ExternalId);
            Assert.Equal(profile.Description("fr"), reloaded.Description("fr"));
            Assert.Equal(profile.SemanticType, reloaded.SemanticType);
            Assert.Equal(profile.ReviewStatus, reloaded.ReviewStatus);
            Assert.EndsWith(
                AssetIntelligenceIndexStore.RelativePath.Replace('/', Path.DirectorySeparatorChar),
                saved.Path,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AnalysisDowngradesUnhashedHumanReviewAndMalformedIndexesBecomeDiagnostics()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-asset-intelligence-review", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var model = Path.Combine(root, "crate.gltf");
            File.WriteAllText(model, """
                {
                  "asset": { "version": "2.0" },
                  "accessors": [{ "count": 3, "type": "VEC3", "min": [0, 0, 0], "max": [1, 1, 1] }],
                  "meshes": [{ "primitives": [{ "attributes": { "POSITION": 0 } }] }]
                }
                """);
            var item = Item("fixture:crate", model);
            item = item.WithIntelligence(item.Intelligence with
            {
                ReviewStatus = "human-reviewed",
                Confidence = 1,
            });

            var analyzed = AssetIntelligenceProfileAnalyzer.Analyze(item, model);

            Assert.Equal("generated-review-required", analyzed.ReviewStatus);
            var malformedPath = Path.Combine(root, "malformed.json");
            File.WriteAllText(malformedPath, """
                { "schema_version": 1, "profiles": [null] }
                """);
            var malformed = AssetIntelligenceIndexStore.LoadFile(malformedPath);
            Assert.False(malformed.Succeeded);
            Assert.Contains(malformed.Diagnostics, diagnostic =>
                diagnostic.Code == AssetIntelligenceIndexStore.InvalidCode);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static AssetCatalogItem Item(string externalId, string modelPath) => new(
        AssetCatalogItemId.ForExternalId(externalId),
        externalId,
        externalId.Split(':').Last().Replace('-', ' '),
        "fixture",
        externalId.Split(':').Last(),
        "https://example.invalid/asset",
        "CC0-1.0",
        "https://creativecommons.org/publicdomain/zero/1.0/",
        true,
        new string('a', 64),
        new FileInfo(modelPath).Length,
        ["architecture"],
        ["door", "oak"],
        "downloaded-and-extracted",
        AssetAvailability.ExtractedAvailable,
        Path.GetFileName(modelPath),
        null,
        null,
        "fixture");

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
