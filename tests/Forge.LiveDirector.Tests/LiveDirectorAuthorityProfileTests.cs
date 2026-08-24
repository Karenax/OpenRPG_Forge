using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Control;
using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorAuthorityProfileTests
{
    [Fact] public void R127_ImpersonationProtectionCurrencyExperienceAndDoors_AreGoverned() { var p = LiveDirectorAuthorityProfile.CreateDefault(); Assert.Equal(4, p.ImpersonationModes.Count); Assert.True(p.ImpersonationDisclosure && p.ProtectionIndicator); Assert.True(p.PersistentWalletAdapter && p.PersistentProgressionAdapter && p.RulesetLevelProjection); Assert.Equal(5, p.DoorStates.Count); Assert.True(p.PortalRelink && p.DoorRuntimeAdapter); Assert.Empty(LiveDirectorAuthorityValidator.Validate(p)); }
    [Fact] public void R127_TransactionRoundTripRuntimeAndScripts_AreCanonical() { var i = LiveDirectorAuthorityProfile.CreateDefault(); var c = i with { ServerRevision = 1, MaximumCurrencyAdjustment = 120_000 }; var h = new LiveDirectorAuthorityHistory(i); var cmd = new LiveDirectorAuthorityCommand(Guid.NewGuid(), 1, "test:r127", 0, "gm:karen", NetworkRole.GameMaster, false, c); Assert.False(h.Execute(cmd).Applied); Assert.True(h.Execute(cmd with { Apply = true }).Applied); Assert.Equal(i, h.Undo("gm:karen", NetworkRole.GameMaster).Profile); Assert.Equal(c, h.Redo("gm:karen", NetworkRole.GameMaster).Profile); var s = new LiveDirectorAuthorityStore(); Assert.Equal(s.Serialize(c), s.Serialize(s.Deserialize(s.Serialize(c)))); Assert.Empty(LiveDirectorAuthorityRuntimeAdapter.Resolve(null, false).Diagnostics); Assert.Equal("LIVE_AUTHORITY_REQUIRED_MISSING", Assert.Single(LiveDirectorAuthorityRuntimeAdapter.Resolve(null, true).Diagnostics).Code); Assert.Equal(5, new LiveDirectorAuthorityScriptService(i).Read(new(i.Id, 1)).DoorStates); }
    [Fact] public void R127_InvalidPolicyPermissionAndRevision_AreLocalized() { var i = LiveDirectorAuthorityProfile.CreateDefault(); var invalid = i with { ServerRevision = 1, MaximumProtectionMinutes = 0 }; Assert.Contains(LiveDirectorAuthorityValidator.Validate(invalid), x => x.Code == "LIVE_PROTECTION_POLICY_INVALID"); var r = new LiveDirectorAuthorityHistory(i).Execute(new(Guid.NewGuid(), 1, "test:r127", 9, "player", NetworkRole.Player, true, invalid)); Assert.Contains(r.Diagnostics, x => x.Code == "LIVE_AUTHORITY_PERMISSION_DENIED"); Assert.Contains(r.Diagnostics, x => x.Code == "LIVE_AUTHORITY_REVISION_STALE"); }

    [Fact]
    public void R127_RuntimeBundleExercisesRenderingProtectionWalletXpAndDoorAdapters()
    {
        var now = new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        var profile = LiveDirectorAuthorityProfile.CreateDefault() with
        {
            MaximumProtectionMinutes = 15,
            MaximumCurrencyAdjustment = 1_000,
            MaximumExperienceAdjustment = 10_000
        };
        var runtime = LiveDirectorAuthorityRuntimeAdapter.Create(profile, () => now,
            xp => (int)(xp / 1_000) + 1);
        var zone = Guid.Parse("d1270000-0000-4000-8000-000000000100");
        var actorId = Guid.Parse("d1270000-0000-4000-8000-000000000101");
        var targetId = Guid.Parse("d1270000-0000-4000-8000-000000000102");
        var doorId = Guid.Parse("d1270000-0000-4000-8000-000000000103");
        var walletId = Guid.Parse("d1270000-0000-4000-8000-000000000104");
        runtime.Chooser.Upsert(new(targetId, zone, "creature", "Elyra", "player", null,
            "active", []));
        runtime.Chooser.Upsert(new(doorId, zone, "door", "Porte", "portal", null,
            "door:locked", []));
        var actor = new AuthorizationSubject(actorId, NetworkRole.Administrator,
            new HashSet<Guid> { zone });

        var impersonation = runtime.Control.Execute(actor, new(Guid.NewGuid(),
            "director.impersonate", targetId, 0, TimeSpan.Zero,
            new Dictionary<string, string>
            {
                ["mode"] = "voice",
                ["disclosure"] = "visible",
                ["renderAdapter"] = "audio"
            }), true);
        Assert.Contains("renderer:audio", impersonation.Target!.State);
        Assert.Equal("LIVE_CONTROL_RESTORED",
            runtime.Control.RestoreTarget(actor, targetId, "director.impersonate", true).Code);

        var protection = runtime.Control.Execute(actor, new(Guid.NewGuid(), "director.protect",
            targetId, 2, TimeSpan.FromMinutes(5), new Dictionary<string, string>
            {
                ["scope"] = "all",
                ["indicator"] = "bouclier ambre",
                ["damageAdapter"] = "runtime"
            }), true);
        Assert.Contains("damage-adapter:runtime", protection.Target!.State);
        now = now.AddMinutes(6);
        Assert.StartsWith("LIVE_PROTECTION_RETURNED:",
            runtime.Control.ReleaseExpiredProtection(targetId), StringComparison.Ordinal);
        Assert.Equal("active", runtime.Chooser.Find(targetId)!.State);

        var economyCommand = Guid.NewGuid();
        var economy = runtime.Economy.Adjust(actor, new(economyCommand, walletId, 500,
            "récompense live", 0), true);
        Assert.Equal(500, economy.Balance);
        Assert.Equal(500, runtime.Economy.ExportWallets()[walletId]);
        Assert.Equal("LIVE_ECONOMY_COMPENSATED",
            runtime.Economy.Compensate(actor, economyCommand, 1, true).Code);

        var xpCommand = Guid.NewGuid();
        var xp = runtime.Progression.Execute(actor, new(xpCommand, "progression.adjust",
            targetId, 0, new Dictionary<string, string>
            {
                ["amount"] = "2500",
                ["reason"] = "jalon narratif"
            }), true);
        Assert.Contains("level=1->3", xp.SemanticDiff);
        Assert.Equal(2500, runtime.Progression.ExportExperience()[targetId]);
        Assert.Equal("LIVE_RULE_COMPENSATED",
            runtime.Progression.Compensate(actor, xpCommand, 1, true).Code);

        var doorCommand = Guid.NewGuid();
        var door = runtime.Progression.Execute(actor, new(doorCommand, "door.set-state",
            doorId, 2, new Dictionary<string, string>
            {
                ["state"] = "relinked",
                ["portal"] = "sortie-surface"
            }), true);
        Assert.Contains("collision=passable", door.SemanticDiff);
        Assert.Contains("sortie-surface", runtime.Chooser.Find(doorId)!.State);
        Assert.Equal("LIVE_RULE_COMPENSATED",
            runtime.Progression.Compensate(actor, doorCommand, 3, true).Code);
        Assert.Equal("door:locked", runtime.Chooser.Find(doorId)!.State);
    }

    [Fact]
    public void R127_DiffRuntimeRequirementAndFrenchDiagnosticsAreExplicit()
    {
        var initial = LiveDirectorAuthorityProfile.CreateDefault();
        var result = new LiveDirectorAuthorityHistory(initial).Execute(new(Guid.NewGuid(), 1,
            "test:r127", 0, "gm:karen", NetworkRole.GameMaster, false,
            initial with { ServerRevision = 1, RequiredAtRuntime = true }));
        Assert.Equal("runtime", result.SemanticDiff);
        Assert.Equal("L'ajustement d'or dépasse le plafond du profil.",
            LiveDirectorAuthorityLocalization.French("LIVE_ECONOMY_ADJUSTMENT_CAP"));
        Assert.Contains(LiveDirectorAuthorityValidator.Validate(null),
            item => item.Code == "LIVE_AUTHORITY_INCOMPLETE");
    }
}
