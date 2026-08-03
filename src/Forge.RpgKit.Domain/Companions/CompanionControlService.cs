using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Companions;

public enum CompanionOwnershipKind { Individual, Party, Shared, AiOnly, GameMaster }
public enum CompanionMovementOrderKind { Move, Follow, Hold, Rally, Guard, Interact }
public enum CompanionTacticalOrderKind { Attack, Defend, FocusFire, Protect, Disengage, UseAbility }
public enum CompanionStance { Passive, Defensive, Balanced, Aggressive }
public sealed record PartyPolicy(int CompanionSlots, int SummonSlots, int PetSlots, int TemporaryAllySlots, int ServerActorLimit, IReadOnlyDictionary<EncounterRole, int> RoleLimits);
public sealed record PartyRoster(IReadOnlyList<CompanionComponent> Companions, int Summons, int Pets, int TemporaryAllies);
public sealed record CompanionOwnership(CompanionOwnershipKind Kind, IReadOnlySet<string> ControllerIds, bool TransferAllowed, IReadOnlySet<CompanionOwnershipKind> AllowedTargets, long Revision);
public sealed record OwnershipTransferred(Guid EventId, Guid CompanionId, CompanionOwnership Previous, CompanionOwnership Current, string ActorId, string Reason, DateTimeOffset At);
public sealed record CompanionMovementOrder(Guid Id, CompanionMovementOrderKind Kind, string IssuerId, string? TargetId, Vector3Value? Destination, string FormationId, int Priority);
public sealed record CompanionTacticalOrder(Guid Id, CompanionTacticalOrderKind Kind, string IssuerId, string? TargetId, CompanionStance Stance, string? AbilityId, int Priority);
public sealed record CompanionAiProfile(double Autonomy, double RiskTolerance, double ResourceUsage, bool AllowFriendlyFire, EncounterRole Role, double FollowDistance, IReadOnlySet<string> AllowedAbilityTags);
public sealed record CompanionOrderResult(bool Accepted, string Reason, IReadOnlyList<object> Queue);

public static class CompanionControlService
{
    public static IReadOnlyList<CompanionDiagnostic> ValidateParty(PartyPolicy policy, PartyRoster roster)
    {
        var result = new List<CompanionDiagnostic>();
        if (policy.CompanionSlots < 0 || policy.SummonSlots < 0 || policy.PetSlots < 0 || policy.TemporaryAllySlots < 0 || policy.ServerActorLimit <= 0) result.Add(new("RPG_PARTY_POLICY_INVALID", "party/policy", "Party slot and server limits are invalid."));
        if (roster.Companions.Count > policy.CompanionSlots || roster.Summons > policy.SummonSlots || roster.Pets > policy.PetSlots || roster.TemporaryAllies > policy.TemporaryAllySlots || roster.Companions.Count + roster.Summons + roster.Pets + roster.TemporaryAllies > policy.ServerActorLimit) result.Add(new("RPG_PARTY_CAPACITY_EXCEEDED", "party/roster", "Party roster exceeds configured capacity."));
        foreach (var limit in policy.RoleLimits) if (roster.Companions.Count(item => item.CommandIds.Contains($"role:{limit.Key}")) > limit.Value) result.Add(new("RPG_PARTY_ROLE_LIMIT_EXCEEDED", $"party/roles/{limit.Key}", "Companion role limit is exceeded."));
        return result;
    }

    public static OwnershipTransferred Transfer(Guid companionId, CompanionOwnership ownership, CompanionOwnershipKind targetKind, IReadOnlySet<string> controllers, string actorId, string reason, DateTimeOffset at)
    {
        if (!ownership.TransferAllowed || !ownership.AllowedTargets.Contains(targetKind) || controllers.Count == 0 || string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Ownership transfer is not permitted.");
        var next = ownership with { Kind = targetKind, ControllerIds = controllers, Revision = checked(ownership.Revision + 1) };
        return new(Guid.NewGuid(), companionId, ownership, next, actorId, reason, at);
    }

    public static CompanionOrderResult IssueMovement(CompanionOwnership ownership, CompanionMovementOrder order, IReadOnlyList<object> queue)
    {
        if (!CanControl(ownership, order.IssuerId)) return new(false, "not-authorized", queue);
        if (order.Id == Guid.Empty || order.Priority < 0 || (order.Kind == CompanionMovementOrderKind.Move && order.Destination is null) || (order.Kind is CompanionMovementOrderKind.Guard or CompanionMovementOrderKind.Interact && string.IsNullOrWhiteSpace(order.TargetId))) return new(false, "invalid-order", queue);
        return new(true, "queued", queue.Append(order).OrderByDescending(item => item is CompanionMovementOrder movement ? movement.Priority : 0).ToArray());
    }

    public static CompanionOrderResult IssueTactical(CompanionOwnership ownership, CompanionTacticalOrder order, CompanionAiProfile ai, IReadOnlyList<object> queue)
    {
        if (!CanControl(ownership, order.IssuerId)) return new(false, "not-authorized", queue);
        if (order.Id == Guid.Empty || order.Priority < 0 || (order.Kind is CompanionTacticalOrderKind.Attack or CompanionTacticalOrderKind.FocusFire or CompanionTacticalOrderKind.Protect && string.IsNullOrWhiteSpace(order.TargetId)) || (order.Kind == CompanionTacticalOrderKind.UseAbility && (string.IsNullOrWhiteSpace(order.AbilityId) || !ai.AllowedAbilityTags.Contains(order.AbilityId)))) return new(false, "invalid-order", queue);
        return new(true, "queued", queue.Append(order).OrderByDescending(item => item is CompanionTacticalOrder tactical ? tactical.Priority : 0).ToArray());
    }

    public static IReadOnlyList<CompanionDiagnostic> ValidateAi(CompanionAiProfile ai)
    {
        var result = new List<CompanionDiagnostic>();
        if (ai.Autonomy is < 0 or > 1 || ai.RiskTolerance is < 0 or > 1 || ai.ResourceUsage is < 0 or > 1 || ai.FollowDistance < 0) result.Add(new("RPG_COMPANION_AI_INVALID", "companion/ai", "Autonomy, risk, resource usage and follow distance are outside bounds."));
        return result;
    }

    private static bool CanControl(CompanionOwnership ownership, string issuerId) => ownership.Kind != CompanionOwnershipKind.AiOnly && ownership.ControllerIds.Contains(issuerId);
}
