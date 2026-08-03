using Forge.RpgKit.Domain.Companions;

namespace Forge.RpgKit.Domain.Tests.Companions;

public sealed class CompanionExperienceServiceTests
{
    [Fact]
    public void Permissioned_equipment_respects_locks_owner_suggestions_compare_and_semantic_diff()
    {
        var locked = Guid.NewGuid(); var item = Guid.NewGuid(); var policy = Equipment() with { LockedItemIds = new HashSet<Guid> { locked } }; var current = new Dictionary<string, Guid> { ["amulet"] = locked };
        Assert.False(CompanionExperienceService.Equip(policy, current, new(item, "amulet", "player")).Accepted);
        var equipped = CompanionExperienceService.Equip(policy, current, new(item, "weapon", "player")); Assert.True(equipped.Accepted); Assert.Equal(item, equipped.Equipment["weapon"]); Assert.Contains("replace:/equipment/weapon", equipped.SemanticDiff);
        Assert.False(CompanionExperienceService.Equip(policy with { Permission = CompanionEquipmentPermission.View }, current, new(item, "weapon", "player")).Accepted);
    }

    [Fact]
    public void Death_policy_supports_downed_permanent_corpse_resurrection_replacement_and_memorial()
    {
        Assert.Equal(6, Enum.GetValues<CompanionDeathMode>().Length); var at = DateTimeOffset.UnixEpoch;
        Assert.True(CompanionExperienceService.Defeat(Death(CompanionDeathMode.Downed), at).State.Downed);
        Assert.True(CompanionExperienceService.Defeat(Death(CompanionDeathMode.RecoverableCorpse), at).State.CorpseAvailable);
        Assert.Contains("requires:resource:ember", CompanionExperienceService.Defeat(Death(CompanionDeathMode.Resurrection), at).Effects);
        Assert.Equal("creature:heir", CompanionExperienceService.Defeat(Death(CompanionDeathMode.Replacement), at).State.ReplacementId);
        Assert.True(CompanionExperienceService.Defeat(Death(CompanionDeathMode.Memorial), at).State.Memorialized);
    }

    [Fact]
    public void Cohesion_uses_navigation_portal_then_hidden_safe_teleport_with_audit()
    {
        var policy = new CohesionPolicy(10, 30, 60, true, true, new HashSet<string> { "party-safe" });
        Assert.Equal(CohesionRecoveryKind.Navigate, CompanionExperienceService.Recover(policy, Context(15, true, false)).Kind);
        Assert.Equal(CohesionRecoveryKind.FollowPortal, CompanionExperienceService.Recover(policy, Context(40, false, true)).Kind);
        var teleported = CompanionExperienceService.Recover(policy, Context(80, false, false)); Assert.Equal(CohesionRecoveryKind.SafeTeleport, teleported.Kind); Assert.Equal("anchor:a", teleported.AnchorId); Assert.Contains("safe-teleport", teleported.Audit);
        Assert.Equal(CohesionRecoveryKind.None, CompanionExperienceService.Recover(policy, Context(80, false, false) with { InCombat = true }).Kind);
    }

    [Fact]
    public void Validation_requires_accessible_hud_and_complete_equipment_death_cohesion_contracts()
    {
        var hud = new PartyHudConfiguration(true, true, true, true, true, true, true, true, true, true, ["health", "focus"]);
        Assert.Empty(CompanionExperienceService.Validate(Equipment(), Death(CompanionDeathMode.Resurrection), new(10, 30, 60, true, true, new HashSet<string> { "party-safe" }), hud));
        var diagnostics = CompanionExperienceService.Validate(Equipment() with { InventoryOwnerId = "", AllowCompare = false }, Death(CompanionDeathMode.Resurrection) with { ResurrectionResourceId = null }, new(30, 20, 10, true, true, new HashSet<string>()), hud with { Keyboard = false, Gamepad = false, ScreenReaderLabels = false });
        Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_EQUIPMENT_UI_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_DEATH_POLICY_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_COHESION_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_COMPANION_HUD_INACCESSIBLE");
    }

    private static CompanionEquipmentPolicy Equipment() => new(CompanionEquipmentPermission.Full, new HashSet<Guid>(), new Dictionary<string, Guid> { ["weapon"] = Guid.NewGuid() }, true, "player");
    private static CompanionDeathPolicy Death(CompanionDeathMode mode) => new(mode, TimeSpan.FromSeconds(20), "resource:ember", "creature:heir", "memorial:lyra", true);
    private static CohesionContext Context(double distance, bool nav, bool portal) => new(distance, nav, portal, false, false, [("anchor:a", new(2, 0, 2), new HashSet<string> { "party-safe" }, true)]);
}
