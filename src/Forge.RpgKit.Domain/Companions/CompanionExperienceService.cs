using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Companions;

public enum CompanionEquipmentPermission { None, View, Suggest, Equip, Full }
public enum CompanionDeathMode { Downed, PermanentDeath, RecoverableCorpse, Resurrection, Replacement, Memorial }
public enum CohesionRecoveryKind { None, Navigate, FollowPortal, SafeTeleport }
public sealed record CompanionEquipmentPolicy(CompanionEquipmentPermission Permission, IReadOnlySet<Guid> LockedItemIds, IReadOnlyDictionary<string, Guid> SuggestedLoadout, bool AllowCompare, string InventoryOwnerId);
public sealed record EquipmentChange(Guid ItemId, string Slot, string ActorId);
public sealed record EquipmentChangeResult(bool Accepted, string Reason, IReadOnlyDictionary<string, Guid> Equipment, string SemanticDiff);
public sealed record CompanionDeathPolicy(CompanionDeathMode Mode, TimeSpan DownedDuration, string? ResurrectionResourceId, string? ReplacementCreatureId, string? MemorialId, bool DropInventory);
public sealed record CompanionLifeState(bool Alive, bool Downed, bool CorpseAvailable, bool Memorialized, DateTimeOffset? RecoverableAt, string? ReplacementId);
public sealed record CompanionLifeTransition(CompanionLifeState State, string EventName, IReadOnlyList<string> Effects);
public sealed record CohesionPolicy(double NavigateThreshold, double PortalThreshold, double TeleportThreshold, bool FollowPortals, bool AllowSafeTeleport, IReadOnlySet<string> SafeAnchorTags);
public sealed record CohesionContext(double Distance, bool NavPathAvailable, bool PortalTransitioned, bool InCombat, bool VisibleToPlayers, IReadOnlyList<(string Id, Vector3Value Position, IReadOnlySet<string> Tags, bool Navigable)> Anchors);
public sealed record CohesionDecision(CohesionRecoveryKind Kind, string? AnchorId, Vector3Value? Destination, string Audit);
public sealed record PartyHudConfiguration(bool Portraits, bool Health, bool Resources, bool Statuses, bool QuickCommands, bool Reorder, bool Keyboard, bool Gamepad, bool ScreenReaderLabels, bool HighContrast, IReadOnlyList<string> VisibleResourceIds);

public static class CompanionExperienceService
{
    public static EquipmentChangeResult Equip(CompanionEquipmentPolicy policy, IReadOnlyDictionary<string, Guid> current, EquipmentChange change)
    {
        if (policy.Permission < CompanionEquipmentPermission.Equip) return new(false, "permission-denied", current, "no-change");
        if (policy.LockedItemIds.Contains(change.ItemId) || current.TryGetValue(change.Slot, out var existing) && policy.LockedItemIds.Contains(existing)) return new(false, "item-locked", current, "no-change");
        if (string.IsNullOrWhiteSpace(change.ActorId) || string.IsNullOrWhiteSpace(change.Slot) || change.ItemId == Guid.Empty) return new(false, "invalid-change", current, "no-change");
        var updated = current.ToDictionary(); updated[change.Slot] = change.ItemId;
        return new(true, "equipped", updated, $"replace:/equipment/{change.Slot}|item:{change.ItemId}|actor:{change.ActorId}");
    }

    public static CompanionLifeTransition Defeat(CompanionDeathPolicy policy, DateTimeOffset at)
    {
        return policy.Mode switch
        {
            CompanionDeathMode.Downed => new(new(false, true, false, false, at + policy.DownedDuration, null), "CompanionDowned", []),
            CompanionDeathMode.PermanentDeath => new(new(false, false, false, false, null, null), "CompanionPermanentlyDied", policy.DropInventory ? ["drop-inventory"] : []),
            CompanionDeathMode.RecoverableCorpse => new(new(false, false, true, false, at, null), "CompanionCorpseCreated", []),
            CompanionDeathMode.Resurrection => new(new(false, false, true, false, at, null), "CompanionAwaitingResurrection", [$"requires:{policy.ResurrectionResourceId}"]),
            CompanionDeathMode.Replacement => new(new(false, false, false, false, null, policy.ReplacementCreatureId), "CompanionReplacementAvailable", []),
            CompanionDeathMode.Memorial => new(new(false, false, false, true, null, null), "CompanionMemorialCreated", [$"memorial:{policy.MemorialId}"]),
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
    }

    public static CohesionDecision Recover(CohesionPolicy policy, CohesionContext context)
    {
        if (context.Distance < policy.NavigateThreshold) return new(CohesionRecoveryKind.None, null, null, "cohesion:none");
        if (context.NavPathAvailable && context.Distance < policy.PortalThreshold) return new(CohesionRecoveryKind.Navigate, null, null, $"cohesion:navigate|distance:{context.Distance:0.##}");
        if (policy.FollowPortals && context.PortalTransitioned && context.Distance < policy.TeleportThreshold) return new(CohesionRecoveryKind.FollowPortal, null, null, "cohesion:portal-follow");
        var safe = context.Anchors.Where(item => item.Navigable && policy.SafeAnchorTags.All(item.Tags.Contains)).OrderBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        if (policy.AllowSafeTeleport && !context.InCombat && !context.VisibleToPlayers && safe != default) return new(CohesionRecoveryKind.SafeTeleport, safe.Id, safe.Position, $"cohesion:safe-teleport|anchor:{safe.Id}");
        return new(CohesionRecoveryKind.None, null, null, "cohesion:blocked");
    }

    public static IReadOnlyList<CompanionDiagnostic> Validate(CompanionEquipmentPolicy equipment, CompanionDeathPolicy death, CohesionPolicy cohesion, PartyHudConfiguration hud)
    {
        var result = new List<CompanionDiagnostic>();
        if (string.IsNullOrWhiteSpace(equipment.InventoryOwnerId) || !equipment.AllowCompare || equipment.SuggestedLoadout.Keys.Any(string.IsNullOrWhiteSpace)) result.Add(new("RPG_COMPANION_EQUIPMENT_UI_INVALID", "companion/equipment", "Equipment ownership, comparison and slots are required."));
        if (death.DownedDuration < TimeSpan.Zero || death.Mode == CompanionDeathMode.Resurrection && string.IsNullOrWhiteSpace(death.ResurrectionResourceId) || death.Mode == CompanionDeathMode.Replacement && string.IsNullOrWhiteSpace(death.ReplacementCreatureId) || death.Mode == CompanionDeathMode.Memorial && string.IsNullOrWhiteSpace(death.MemorialId)) result.Add(new("RPG_COMPANION_DEATH_POLICY_INVALID", "companion/death", "Death policy parameters are incomplete."));
        if (cohesion.NavigateThreshold < 0 || cohesion.PortalThreshold < cohesion.NavigateThreshold || cohesion.TeleportThreshold < cohesion.PortalThreshold || cohesion.AllowSafeTeleport && cohesion.SafeAnchorTags.Count == 0) result.Add(new("RPG_COMPANION_COHESION_INVALID", "companion/cohesion", "Cohesion thresholds and safe anchors are invalid."));
        if ((!hud.Keyboard && !hud.Gamepad) || !hud.ScreenReaderLabels || !hud.Portraits || !hud.Health || !hud.Statuses || !hud.QuickCommands) result.Add(new("RPG_COMPANION_HUD_INACCESSIBLE", "companion/hud", "Party HUD navigation, labels and essential information are required."));
        return result;
    }
}
