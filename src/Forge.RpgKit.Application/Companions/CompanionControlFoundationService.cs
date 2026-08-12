using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Application.Companions;

public sealed record CompanionControlResolution(
    CompanionWorldProfile.ControlProfile? Profile,
    IReadOnlyList<CompanionDiagnostic> Diagnostics,
    bool OptionalFallback);

public sealed record CompanionOwnershipTransferPlan(
    Guid PlanId,
    Guid TransactionId,
    string SemanticDiff,
    string AuthorizationLevel,
    bool DryRun,
    CompanionOwnership Previous,
    CompanionOwnership Current,
    OwnershipTransferred Audit);

public static class CompanionControlFoundationService
{
    public static CompanionControlResolution Resolve(CompanionWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Control is not null) return new(profile.Control, [], false);
        return profile.ControlRequiredAtRuntime
            ? new(null,
                [new("RPG_COMPANION_CONTROL_REQUIRED", "companion/control",
                    "Required companion control profile is missing.")], false)
            : new(null, [], true);
    }

    public static CompanionOwnership CreateOwnership(CompanionWorldProfile profile)
    {
        var ownership = Require(profile).Ownership;
        return new(ParseOwnership(ownership.Kind), ownership.ControllerIds.ToHashSet(StringComparer.Ordinal),
            ownership.TransferAllowed, ownership.AllowedTargetKinds.Select(ParseOwnership).ToHashSet(),
            ownership.Revision);
    }

    public static CompanionAiProfile CreateAi(CompanionWorldProfile profile)
    {
        var ai = Require(profile).Ai;
        return new(ai.Autonomy, ai.RiskTolerance, ai.ResourceUsage, ai.AllowFriendlyFire,
            Enum.Parse<EncounterRole>(ai.Role, true), ai.FollowDistance,
            ai.AllowedAbilityTags.ToHashSet(StringComparer.Ordinal));
    }

    public static CompanionEquipmentPolicy CreateEquipment(CompanionWorldProfile profile)
    {
        var equipment = Require(profile).Equipment;
        return new(Enum.Parse<CompanionEquipmentPermission>(equipment.Permission, true),
            equipment.LockedItemIds.ToHashSet(), equipment.SuggestedLoadout, equipment.AllowCompare,
            equipment.InventoryOwnerId);
    }

    public static CompanionOwnershipTransferPlan Transfer(
        CompanionWorldProfile profile,
        Guid companionId,
        string targetKind,
        IReadOnlySet<string> controllers,
        string actorId,
        string reason,
        DateTimeOffset at,
        bool dryRun)
    {
        var control = Require(profile);
        var previous = CreateOwnership(profile);
        var target = ParseOwnership(targetKind);
        var audit = CompanionControlService.Transfer(companionId, previous, target, controllers, actorId, reason, at);
        var seed = $"{control.Id:D}|{companionId:D}|{previous.Revision}|{targetKind}|" +
                   $"{string.Join(',', controllers.Order(StringComparer.Ordinal))}|{actorId}|{reason}";
        var planId = Stable(seed + "|plan");
        var transactionId = Stable(seed + "|transaction");
        var semanticDiff = $"replace:/companion/control/ownership|from:{previous.Kind}|to:{audit.Current.Kind}|" +
                           $"revision:{audit.Current.Revision}|controllers:{string.Join(',', controllers.Order(StringComparer.Ordinal))}";
        return new(planId, transactionId, semanticDiff, control.Ai.AuthorizationLevel, dryRun, previous,
            audit.Current, audit with { EventId = transactionId });
    }

    public static CompanionOrderResult IssueMovement(
        CompanionWorldProfile profile,
        string kind,
        string issuerId,
        string? targetId,
        Vector3Value? destination,
        int priority,
        IReadOnlyList<object> queue)
    {
        var movement = Require(profile).Movement;
        if (!movement.CommandKinds.Contains(kind, StringComparer.Ordinal) ||
            priority < movement.MinimumPriority || priority > movement.MaximumPriority)
            return new(false, "profile-denied", queue);
        var order = new CompanionMovementOrder(Stable($"{profile.Id:D}|movement|{kind}|{issuerId}|{targetId}|" +
                                                        $"{destination}|{priority}"), ParseMovement(kind), issuerId,
            targetId, destination, movement.DefaultFormationId, priority);
        return CompanionControlService.IssueMovement(CreateOwnership(profile), order, queue);
    }

    public static CompanionOrderResult IssueTactical(
        CompanionWorldProfile profile,
        string kind,
        string issuerId,
        string? targetId,
        string stance,
        string? abilityId,
        int priority,
        IReadOnlyList<object> queue)
    {
        var tactical = Require(profile).Tactical;
        if (!tactical.CommandKinds.Contains(kind, StringComparer.Ordinal) ||
            !tactical.Stances.Contains(stance, StringComparer.Ordinal) ||
            priority < tactical.MinimumPriority || priority > tactical.MaximumPriority ||
            abilityId is not null && !tactical.AbilityIds.Contains(abilityId, StringComparer.Ordinal))
            return new(false, "profile-denied", queue);
        var order = new CompanionTacticalOrder(Stable($"{profile.Id:D}|tactical|{kind}|{issuerId}|{targetId}|" +
                                                        $"{stance}|{abilityId}|{priority}"), ParseTactical(kind),
            issuerId, targetId, Enum.Parse<CompanionStance>(stance, true), abilityId, priority);
        return CompanionControlService.IssueTactical(CreateOwnership(profile), order, CreateAi(profile), queue);
    }

    public static EquipmentChangeResult Equip(
        CompanionWorldProfile profile,
        IReadOnlyDictionary<string, Guid> current,
        Guid itemId,
        string slot,
        string actorId)
    {
        var ownership = CreateOwnership(profile);
        var equipment = CreateEquipment(profile);
        if (ownership.Kind == CompanionOwnershipKind.AiOnly ||
            !ownership.ControllerIds.Contains(actorId) ||
            !string.Equals(equipment.InventoryOwnerId, actorId, StringComparison.Ordinal))
            return new(false, "ownership-denied", current, "no-change");
        return CompanionExperienceService.Equip(equipment, current, new(itemId, slot, actorId));
    }

    private static CompanionWorldProfile.ControlProfile Require(CompanionWorldProfile profile) =>
        Resolve(profile).Profile ?? throw new InvalidOperationException("RPG_COMPANION_CONTROL_UNAVAILABLE");

    private static CompanionOwnershipKind ParseOwnership(string value) => value switch
    {
        "individual" => CompanionOwnershipKind.Individual,
        "party" => CompanionOwnershipKind.Party,
        "shared" => CompanionOwnershipKind.Shared,
        "ai-only" => CompanionOwnershipKind.AiOnly,
        "game-master" => CompanionOwnershipKind.GameMaster,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported companion ownership kind."),
    };

    private static CompanionMovementOrderKind ParseMovement(string value) => value switch
    {
        "move" => CompanionMovementOrderKind.Move,
        "follow" => CompanionMovementOrderKind.Follow,
        "hold" => CompanionMovementOrderKind.Hold,
        "rally" => CompanionMovementOrderKind.Rally,
        "guard" => CompanionMovementOrderKind.Guard,
        "interact" => CompanionMovementOrderKind.Interact,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported companion movement order."),
    };

    private static CompanionTacticalOrderKind ParseTactical(string value) => value switch
    {
        "attack" => CompanionTacticalOrderKind.Attack,
        "defend" => CompanionTacticalOrderKind.Defend,
        "focus-fire" => CompanionTacticalOrderKind.FocusFire,
        "protect" => CompanionTacticalOrderKind.Protect,
        "disengage" => CompanionTacticalOrderKind.Disengage,
        "use-ability" => CompanionTacticalOrderKind.UseAbility,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported companion tactical order."),
    };

    private static Guid Stable(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
