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
        Directory.CreateDirectory(Path.Combine(root, "addons", "pcgodot"));
        File.WriteAllText(Path.Combine(root, "project.godot"), "[application]");
        File.WriteAllText(Path.Combine(root, "addons", "pcgodot", "plugin.cfg"), "[plugin]");
        try
        {
            var capability = WorldGenerationBackendCapabilityProbe.Probe(root)
                .Single(item => item.Backend == WorldGenerationBackend.PcgGodot);

            Assert.True(capability.Available);
            Assert.Equal(WorldGenerationProviderKeys.GfPcgodot, capability.ProviderKey);
            Assert.Equal("unqualified", capability.VersionOrRevision);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
