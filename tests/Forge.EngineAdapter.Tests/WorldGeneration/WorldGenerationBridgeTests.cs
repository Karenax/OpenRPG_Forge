using System.Security.Cryptography;
using System.Text.Json;
using Forge.Application.WorldGeneration;
using Forge.Domain.WorldGeneration;
using Forge.EngineAdapter.WorldGeneration;

namespace Forge.EngineAdapter.Tests.WorldGeneration;

public sealed class WorldGenerationBridgeTests
{
    [Fact]
    public async Task TerrainAdapterWritesVersionedRoundTrippableBridgeRequest()
    {
        var recipe = WorldGenerationRecipe.CreateDefault(
                Guid.Parse("f0520000-0000-4000-8000-000000000001"), "Bridge test", 82)
            .WithApproval(true, "test:approved");
        var plan = new WorldGenerationPlanner().Build(recipe, 4, new(3),
            Guid.Parse("f0520000-0000-4000-8000-000000000002"),
            Guid.Parse("f0520000-0000-4000-8000-000000000003"));
        var sink = new MemorySink();
        var context = new WorldGenerationExecutionContext("/workspace", recipe, plan, true, sink);
        var step = plan.Steps.Single(item => item.Stage == WorldGenerationStage.Terrain);

        var result = await new Terrain3DWorldGenerationAdapter().ExecuteAsync(context, step);

        Assert.True(result.Succeeded);
        var artifact = Assert.Single(result.Artifacts);
        var request = WorldGenerationBridgeProtocol.DeserializeRequest(sink.Content[artifact]);
        Assert.Equal(WorldGenerationBridgeProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal(plan.PlanId, request.PlanId);
        Assert.Equal(recipe.Fingerprint(), request.RecipeFingerprint);
        Assert.Equal(plan.PlanFingerprint, request.PlanFingerprint);
        Assert.True(request.DryRun);
        Assert.Equal("terrain3d.generate_heightfield", request.Step.Operation);
    }

    [Fact]
    public void CapabilityProbeFindsPinnedAddonMarkersAndAlwaysKeepsNativeFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-worldgen-probe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "addons", "terrain_3d"));
        Directory.CreateDirectory(Path.Combine(root, "addons", "flow_nodes_editor"));
        File.WriteAllText(Path.Combine(root, "project.godot"), "[application]");
        File.WriteAllText(Path.Combine(root, "addons", "terrain_3d", "plugin.cfg"), "[plugin]");
        File.WriteAllText(Path.Combine(root, "addons", "flow_nodes_editor", "plugin.cfg"), "[plugin]");
        try
        {
            var capabilities = WorldGenerationBackendCapabilityProbe.Probe(root);
            var terrain = capabilities.Single(item => item.Backend == WorldGenerationBackend.Terrain3D);
            var graphPcg = capabilities.Single(item => item.Backend == WorldGenerationBackend.PcgGodot);
            var native = capabilities.Single(item => item.Backend == WorldGenerationBackend.GodotNative);

            Assert.True(terrain.Available);
            Assert.Equal(WorldGenerationProviderKeys.Terrain3D, terrain.ProviderKey);
            Assert.True(graphPcg.Available);
            Assert.Equal(WorldGenerationProviderKeys.GodotFlow, graphPcg.ProviderKey);
            Assert.True(native.Available);
            Assert.Equal(WorldGenerationProviderKeys.OpenRpgNative, native.ProviderKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CapabilityProbeIdentifiesAlternativeGraphPcgProviderWithoutChangingCanonicalBackend()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-worldgen-provider-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "addons", "flow_nodes_editor"));
        File.WriteAllText(Path.Combine(root, "project.godot"), "[application]");
        var marker = Path.Combine(root, "addons", "flow_nodes_editor", "plugin.cfg");
        File.WriteAllText(marker, "[plugin]\nname=\"Flow Nodes Editor\"");
        WriteProviderReceipt(root, new ProviderReceiptSpec(
            "pcgodot",
            WorldGenerationProviderKeys.GfPcgodot,
            "https://github.com/Framedworld/GF-PCGODOT",
            "91471c10d671ecf9e9214d801e8f5b68d3a7df07",
            "addons/flow_nodes_editor/plugin.cfg",
            Sha256(marker)));
        try
        {
            var capability = WorldGenerationBackendCapabilityProbe.Probe(root)
                .Single(item => item.Backend == WorldGenerationBackend.PcgGodot);

            Assert.True(capability.Available);
            Assert.Equal(WorldGenerationProviderKeys.GfPcgodot, capability.ProviderKey);
            Assert.Equal("91471c10d671ecf9e9214d801e8f5b68d3a7df07", capability.VersionOrRevision);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CapabilityProbeTreatsMultipleMarkersForOneProviderAsOneInstallation()
    {
        var root = CreateProject("same-provider");
        var addon = Path.Combine(root, "addons", "terrain_3d");
        Directory.CreateDirectory(addon);
        File.WriteAllText(Path.Combine(addon, "plugin.cfg"), "[plugin]");
        File.WriteAllText(Path.Combine(addon, "terrain.gdextension"), "[configuration]");
        try
        {
            var capability = WorldGenerationBackendCapabilityProbe.Probe(root)
                .Single(item => item.Backend == WorldGenerationBackend.Terrain3D);

            Assert.True(capability.Available);
            Assert.DoesNotContain(capability.Diagnostics,
                diagnostic => diagnostic.Contains("Multiple", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CapabilityProbeRejectsReceiptWhoseEvidenceHashWasForged()
    {
        var root = CreateProject("forged-receipt");
        var addon = Path.Combine(root, "addons", "flow_nodes_editor");
        Directory.CreateDirectory(addon);
        File.WriteAllText(Path.Combine(addon, "plugin.cfg"), "[plugin]");
        WriteProviderReceipt(root, new ProviderReceiptSpec(
            "pcgodot",
            WorldGenerationProviderKeys.GodotFlow,
            "https://github.com/yabadabu/godot_flow",
            "284231f060099a8943eb56567c21732b7cd678d3",
            "addons/flow_nodes_editor/plugin.cfg",
            new string('0', 64)));
        try
        {
            var capability = WorldGenerationBackendCapabilityProbe.Probe(root)
                .Single(item => item.Backend == WorldGenerationBackend.PcgGodot);

            Assert.False(capability.Available);
            Assert.Contains(capability.Diagnostics,
                diagnostic => diagnostic.Contains("SHA-256", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CapabilityProbeRejectsTwoReceiptedOwnersForOneBackend()
    {
        var root = CreateProject("concurrent-owner");
        var addon = Path.Combine(root, "addons", "flow_nodes_editor");
        Directory.CreateDirectory(addon);
        var marker = Path.Combine(addon, "plugin.cfg");
        File.WriteAllText(marker, "[plugin]");
        var hash = Sha256(marker);
        WriteProviderReceipt(root,
            new ProviderReceiptSpec(
                "pcgodot",
                WorldGenerationProviderKeys.GodotFlow,
                "https://github.com/yabadabu/godot_flow",
                "284231f060099a8943eb56567c21732b7cd678d3",
                "addons/flow_nodes_editor/plugin.cfg",
                hash),
            new ProviderReceiptSpec(
                "pcgodot",
                WorldGenerationProviderKeys.GfPcgodot,
                "https://github.com/Framedworld/GF-PCGODOT",
                "91471c10d671ecf9e9214d801e8f5b68d3a7df07",
                "addons/flow_nodes_editor/plugin.cfg",
                hash));
        try
        {
            var capability = WorldGenerationBackendCapabilityProbe.Probe(root)
                .Single(item => item.Backend == WorldGenerationBackend.PcgGodot);

            Assert.False(capability.Available);
            Assert.Contains(capability.Diagnostics,
                diagnostic => diagnostic.Contains("owners", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateProject(string scenario)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "openrpg-worldgen-provider-tests",
            scenario,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "project.godot"), "[application]");
        return root;
    }

    private static void WriteProviderReceipt(string root, params ProviderReceiptSpec[] providers)
    {
        var receiptDirectory = Path.Combine(root, ".openrpg", "world-generation");
        Directory.CreateDirectory(receiptDirectory);
        var receipt = new
        {
            schemaVersion = 1,
            generatedAtUtc = "2026-08-27T00:00:00Z",
            providers = providers.Select(provider => new
            {
                backend = provider.Backend,
                providerKey = provider.ProviderKey,
                repository = provider.Repository,
                versionOrRevision = provider.VersionOrRevision,
                treeRevision = new string('a', 40),
                submodules = Array.Empty<object>(),
                evidence = new[]
                {
                    new
                    {
                        relativePath = provider.EvidencePath,
                        sha256 = provider.EvidenceSha256,
                    },
                },
            }),
        };
        File.WriteAllText(
            Path.Combine(receiptDirectory, "provider-installation.json"),
            JsonSerializer.Serialize(receipt));
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record ProviderReceiptSpec(
        string Backend,
        string ProviderKey,
        string Repository,
        string VersionOrRevision,
        string EvidencePath,
        string EvidenceSha256);

    private sealed class MemorySink : IWorldGenerationArtifactSink
    {
        internal Dictionary<string, string> Content { get; } = new(StringComparer.Ordinal);

        public ValueTask WriteTextAsync(string relativePath, string content, CancellationToken cancellationToken = default)
        {
            Content[relativePath] = content;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            Content.Remove(relativePath);
            return ValueTask.CompletedTask;
        }
    }
}
