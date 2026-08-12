using Forge.LiveDirector.Authoring;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorWorkspaceProfileTests
{
    [Fact]
    public void R125_ClientCreatorSpawnChooserAndFocusPolicies_AreComplete()
    {
        var profile = LiveDirectorWorkspaceProfile.CreateDefault();
        Assert.True(profile.Client.SeparateClient && profile.Client.EngineIndependent && profile.Client.SecondFactor);
        Assert.True(profile.Creator.ServerAllowlist && profile.Creator.Thumbnails && profile.Creator.PermissionFilter);
        Assert.Equal(3, profile.Spawn.PlacementModes.Count);
        Assert.True(profile.Spawn.FormationCoordinates && profile.Spawn.SafeUndo);
        Assert.True(profile.Chooser.HierarchicalZones && profile.Chooser.InstanceNavigation);
        Assert.True(profile.Focus.SpectatorCamera && profile.Focus.PrivacyOverrideRequiresPermission && profile.Focus.ReadOnly);
        Assert.Empty(LiveDirectorWorkspaceValidator.Validate(profile));
    }

    [Fact]
    public void R125_PreviewApplyUndoRedoRoundTripRuntimeAndScripts_AreCanonical()
    {
        var initial = LiveDirectorWorkspaceProfile.CreateDefault();
        var candidate = initial with { ServerRevision = 1, Spawn = initial.Spawn with { MaximumInstances = 80 } };
        var history = new LiveDirectorWorkspaceHistory(initial);
        var command = new LiveDirectorWorkspaceCommand(Guid.NewGuid(), 1, "test:r125", 0, "gm:karen", NetworkRole.GameMaster, false, candidate);
        Assert.False(history.Execute(command).Applied);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("gm:karen", NetworkRole.GameMaster).Profile);
        Assert.Equal(candidate, history.Redo("gm:karen", NetworkRole.GameMaster).Profile);
        var store = new LiveDirectorWorkspaceStore();
        Assert.Equal(store.Serialize(candidate), store.Serialize(store.Deserialize(store.Serialize(candidate))));
        Assert.Empty(LiveDirectorWorkspaceRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("LIVE_WORKSPACE_REQUIRED_MISSING", Assert.Single(LiveDirectorWorkspaceRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        var scripts = new LiveDirectorWorkspaceScriptService(initial);
        Assert.Equal(5, scripts.Read(scripts.Handle).FilterCount);
    }

    [Fact]
    public void R125_InvalidPolicyPermissionAndRevision_ReturnLocalizedDiagnostics()
    {
        var initial = LiveDirectorWorkspaceProfile.CreateDefault();
        var invalid = initial with { ServerRevision = 1, Spawn = initial.Spawn with { MaximumInstances = 0 } };
        Assert.Contains(LiveDirectorWorkspaceValidator.Validate(invalid), x => x.Code == "LIVE_SPAWN_POLICY_INVALID");
        var result = new LiveDirectorWorkspaceHistory(initial).Execute(new(Guid.NewGuid(), 1, "test:r125", 9, "player", NetworkRole.Player, true, invalid));
        Assert.Contains(result.Diagnostics, x => x.Code == "LIVE_WORKSPACE_PERMISSION_DENIED");
        Assert.Contains(result.Diagnostics, x => x.Code == "LIVE_WORKSPACE_REVISION_STALE");
    }
}
