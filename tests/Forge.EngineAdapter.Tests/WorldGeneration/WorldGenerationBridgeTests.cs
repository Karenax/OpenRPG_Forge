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
            Assert.True(capabilities.Single(item => item.Backend == WorldGenerationBackend.Terrain3D).Available);
            Assert.True(capabilities.Single(item => item.Backend == WorldGenerationBackend.PcgGodot).Available);
            Assert.True(capabilities.Single(item => item.Backend == WorldGenerationBackend.GodotNative).Available);
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
