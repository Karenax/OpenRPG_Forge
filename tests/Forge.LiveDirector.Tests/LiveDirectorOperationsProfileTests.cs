using Forge.LiveDirector.Authoring;
using Forge.Network.Operations;
namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorOperationsProfileTests
{
    [Fact]
    public void R129_SearchScriptsVariablesScenesVisibility_AreGoverned()
    {
        var profile = LiveDirectorOperationsProfile.CreateDefault();
        Assert.True(profile.VisualFocus && profile.ForgeScriptServerAdapter &&
                    profile.PersistentStateAdapter && profile.SceneRuntimeAdapters &&
                    profile.ViewportOverlayAdapter);
        Assert.Equal(["bool", "number", "enum", "string"], profile.VariableKinds);
        Assert.Equal(["spawns", "dialogue", "music", "weather", "triggers"],
            profile.SceneFacets);
        Assert.Equal(["hidden-objects", "secrets", "navigation", "encounters"],
            profile.VisibilityLayers);
        Assert.Empty(LiveDirectorOperationsValidator.Validate(profile));
    }

    [Fact]
    public void R129_TransactionRoundTripRuntimeScripts_AreCanonical()
    {
        var initial = LiveDirectorOperationsProfile.CreateDefault();
        var candidate = initial with { ServerRevision = 1, RequiredAtRuntime = true };
        var history = new LiveDirectorOperationsHistory(initial);
        var command = new LiveDirectorOperationsCommand(Guid.NewGuid(), 1, "test:r129", 0,
            "gm", NetworkRole.GameMaster, false, candidate);
        var preview = history.Execute(command);
        Assert.False(preview.Applied);
        Assert.Equal("runtime", preview.SemanticDiff);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("gm", NetworkRole.GameMaster).Profile);
        Assert.Equal(candidate, history.Redo("gm", NetworkRole.GameMaster).Profile);
        var store = new LiveDirectorOperationsStore();
        Assert.Equal(store.Serialize(candidate),
            store.Serialize(store.Deserialize(store.Serialize(candidate))));
        Assert.Empty(LiveDirectorOperationsRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("LIVE_OPS_REQUIRED_MISSING",
            Assert.Single(LiveDirectorOperationsRuntimeAdapter.Resolve(null, true).Diagnostics)
                .Code);
        var runtime = LiveDirectorOperationsRuntimeAdapter.Create(initial,
            value => value.EndsWith(":valid", StringComparison.Ordinal),
            () => DateTimeOffset.UnixEpoch);
        Assert.True(new LiveDirectorOperationsScriptService(initial)
            .Read(new(initial.Id, 1)).VisualFocus);
        Assert.Same(initial, runtime.Profile);
    }

    [Fact]
    public void R129_InvalidPermissionRevisionAndCanonicalLists_AreLocalized()
    {
        var initial = LiveDirectorOperationsProfile.CreateDefault();
        var bad = initial with
        {
            ServerRevision = 1,
            ViewportOverlayAdapter = false,
            VariableKinds = ["string", "enum", "number", "bool"]
        };
        var diagnostics = LiveDirectorOperationsValidator.Validate(bad);
        Assert.Contains(diagnostics, item => item.Code == "LIVE_VISIBILITY_POLICY_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "LIVE_VARIABLE_POLICY_INVALID");
        var result = new LiveDirectorOperationsHistory(initial).Execute(new(Guid.NewGuid(), 1,
            "test", 9, "player", NetworkRole.Player, true, bad));
        Assert.Contains(result.Diagnostics, item => item.Code == "LIVE_OPS_PERMISSION_DENIED");
        Assert.Contains(result.Diagnostics, item => item.Code == "LIVE_OPS_REVISION_STALE");
    }
}
