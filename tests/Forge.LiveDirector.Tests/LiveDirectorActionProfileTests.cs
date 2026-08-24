using Forge.LiveDirector.Authoring;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorActionProfileTests
{
    [Fact]
    public void R126_RelocateDestroyHealLimboAndPossessionPolicies_AreComplete()
    {
        var p = LiveDirectorActionProfile.CreateDefault();
        Assert.True(p.Relocation.NavigationProbe && p.Relocation.PlayerConsent && p.Relocation.PartyPolicy);
        Assert.True(p.Destruction.ConfirmationRequired && p.Destruction.QuestImpactPreview && p.Destruction.RollbackLimitVisible);
        Assert.Equal(4, p.Healing.Scopes.Count);
        Assert.True(p.Limbo.PreserveIdentity && p.Limbo.RuntimeNodeAdapter);
        Assert.True(p.Possession.ProtectedPlayerConsent && p.Possession.RuntimeInputAdapter && p.Possession.Expiry);
        Assert.Empty(LiveDirectorActionValidator.Validate(p));
    }
    [Fact]
    public void R126_TransactionRoundTripRuntimeAndScriptContracts_AreCanonical()
    {
        var initial = LiveDirectorActionProfile.CreateDefault();
        var candidate = initial with { ServerRevision = 1, Possession = initial.Possession with { MaximumMinutes = 90 } };
        var history = new LiveDirectorActionHistory(initial);
        var command = new LiveDirectorActionCommand(Guid.NewGuid(), 1, "test:r126", 0, "gm:karen", NetworkRole.GameMaster, false, candidate);
        Assert.False(history.Execute(command).Applied); Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("gm:karen", NetworkRole.GameMaster).Profile);
        Assert.Equal(candidate, history.Redo("gm:karen", NetworkRole.GameMaster).Profile);
        var store = new LiveDirectorActionStore(); Assert.Equal(store.Serialize(candidate), store.Serialize(store.Deserialize(store.Serialize(candidate))));
        Assert.Empty(LiveDirectorActionRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("LIVE_ACTION_PROFILE_REQUIRED_MISSING", Assert.Single(LiveDirectorActionRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        Assert.Equal(4, new LiveDirectorActionScriptService(initial).Read(new(initial.Id, 1)).HealScopeCount);
        var runtime = LiveDirectorActionRuntimeAdapter.Create(candidate,
            () => new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero));
        Assert.Same(runtime.Chooser, runtime.WorldActions is not null ? runtime.Chooser : null);
        Assert.NotNull(runtime.Control);
    }
    [Fact]
    public void R126_InvalidPolicyPermissionAndRevision_AreLocalized()
    {
        var initial = LiveDirectorActionProfile.CreateDefault();
        var invalid = initial with { ServerRevision = 1, Possession = initial.Possession with { MaximumMinutes = 0 } };
        Assert.Contains(LiveDirectorActionValidator.Validate(invalid), x => x.Code == "LIVE_POSSESSION_POLICY_INVALID");
        var r = new LiveDirectorActionHistory(initial).Execute(new(Guid.NewGuid(), 1, "test:r126", 9, "player", NetworkRole.Player, true, invalid));
        Assert.Contains(r.Diagnostics, x => x.Code == "LIVE_ACTION_PROFILE_PERMISSION_DENIED");
        Assert.Contains(r.Diagnostics, x => x.Code == "LIVE_ACTION_PROFILE_REVISION_STALE");
        Assert.Equal("Le joueur protégé doit donner son consentement.",
            LiveDirectorActionLocalization.French("LIVE_POSSESSION_PLAYER_PROTECTED"));
        Assert.Contains("runtime", new LiveDirectorActionHistory(initial).Execute(new(
            Guid.NewGuid(), 1, "test:r126", 0, "gm:karen", NetworkRole.GameMaster,
            false, initial with { ServerRevision = 1, RequiredAtRuntime = true })).SemanticDiff,
            StringComparison.Ordinal);
    }
}
