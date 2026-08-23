using Forge.LiveDirector.Authoring;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorNarrativeProfileTests
{
    [Fact]
    public void R130_QuestsAndEncounters_AreGoverned()
    {
        var profile = LiveDirectorNarrativeProfile.CreateDefault();
        Assert.True(profile.QuestInspection && profile.ConsequencePreview &&
                    profile.QuestRuntimeAdapter);
        Assert.Equal(["all-players", "selected-players"], profile.QuestScopes);
        Assert.Equal(["activate", "pause", "wave", "scale", "despawn"],
            profile.EncounterActions);
        Assert.True(profile.BoundedScaling && profile.CreatureSpawnAdapter &&
                    profile.AiRuntimeAdapter);
        Assert.Empty(LiveDirectorNarrativeValidator.Validate(profile));
    }

    [Fact]
    public void R130_TransactionRoundTripRuntimeScripts()
    {
        var initial = LiveDirectorNarrativeProfile.CreateDefault();
        var candidate = initial with { ServerRevision = 1, RequiredAtRuntime = true };
        var history = new LiveDirectorNarrativeHistory(initial);
        var command = new LiveDirectorNarrativeCommand(Guid.NewGuid(), 1, "test:r130", 0,
            "gm", NetworkRole.GameMaster, false, candidate);
        var preview = history.Execute(command);
        Assert.False(preview.Applied);
        Assert.Equal("runtime", preview.SemanticDiff);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("gm", NetworkRole.GameMaster).Profile);
        Assert.Equal(candidate, history.Redo("gm", NetworkRole.GameMaster).Profile);
        var store = new LiveDirectorNarrativeStore();
        Assert.Equal(store.Serialize(candidate),
            store.Serialize(store.Deserialize(store.Serialize(candidate))));
        Assert.Empty(LiveDirectorNarrativeRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("LIVE_NARRATIVE_REQUIRED_MISSING",
            Assert.Single(LiveDirectorNarrativeRuntimeAdapter.Resolve(null, true).Diagnostics)
                .Code);
        var runtime = LiveDirectorNarrativeRuntimeAdapter.Create(initial,
            () => DateTimeOffset.UnixEpoch);
        Assert.Same(initial, runtime.Profile);
        Assert.Equal(5, new LiveDirectorNarrativeScriptService(initial)
            .Read(new(initial.Id, 1)).EncounterActions);
    }

    [Fact]
    public void R130_InvalidPermissionRevisionAndCanonicalLists_AreLocalized()
    {
        var initial = LiveDirectorNarrativeProfile.CreateDefault();
        var bad = initial with
        {
            ServerRevision = 1,
            AiRuntimeAdapter = false,
            EncounterActions = ["despawn", "scale", "wave", "pause", "activate"]
        };
        Assert.Contains(LiveDirectorNarrativeValidator.Validate(bad),
            item => item.Code == "LIVE_ENCOUNTER_POLICY_INVALID");
        var result = new LiveDirectorNarrativeHistory(initial).Execute(new(Guid.NewGuid(), 1,
            "test", 9, "player", NetworkRole.Player, true, bad));
        Assert.Contains(result.Diagnostics,
            item => item.Code == "LIVE_NARRATIVE_PERMISSION_DENIED");
        Assert.Contains(result.Diagnostics,
            item => item.Code == "LIVE_NARRATIVE_REVISION_STALE");
    }
}
