using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Creatures;

public sealed record CreatureDialogueBinding(string Context, int Priority, string DialogueId, string? InterlocutorId, string? RequiredState, bool Fallback);
public sealed record CreatureProtectionPolicy(bool Invulnerable, bool UsesNarrativeDeath, bool DialogueInterruptible, IReadOnlySet<string> GameMasterPermissions);
public sealed record InteractionRequested(Guid EventId, Guid CreatureId, string ActorId, string Context, string? RequestedDialogueId);
public sealed record DialogueStarting(Guid EventId, Guid CreatureId, string ActorId, string DialogueId, string Context);
public sealed record InteractionDecision(bool Accepted, DialogueStarting? Starting, string? RefusalCode);
public sealed record StimulusDetected(Guid EventId, Guid CreatureId, string StimulusId, SenseKind Sense, double Confidence, Vector3Value Position, double RememberUntilSeconds);
public sealed record StimulusLost(Guid EventId, Guid CreatureId, string StimulusId, SenseKind Sense, Vector3Value LastPosition, string Cause);
public sealed record AttackNetworkContext(string Authority, long Tick, long Revision, bool Predicted);
public sealed record AttackDeclared(Guid EventId, Guid CreatureId, string AttackerId, string? WeaponId, string TargetId, AttackNetworkContext Network);
public sealed record AttackResolved(Guid EventId, Guid CreatureId, string AttackerId, string? WeaponId, string TargetId, string Result, double AppliedDamage, AttackNetworkContext Network);

public static class CreatureInteractionService
{
    public static InteractionDecision RequestDialogue(
        Guid creatureId, string actorId, string context, string? state, IReadOnlyList<CreatureDialogueBinding> bindings,
        CreatureProtectionPolicy protection, Func<Guid> eventIdSource)
    {
        if (creatureId == Guid.Empty || string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(context)) return new(false, null, "RPG_CREATURE_INTERACTION_INVALID");
        var selected = bindings.Where(binding => binding.Context == context && (binding.RequiredState is null || binding.RequiredState == state))
            .OrderByDescending(binding => binding.Priority).ThenBy(binding => binding.DialogueId, StringComparer.Ordinal).FirstOrDefault()
            ?? bindings.Where(binding => binding.Fallback).OrderByDescending(binding => binding.Priority).FirstOrDefault();
        if (selected is null) return new(false, null, "RPG_CREATURE_DIALOGUE_NOT_FOUND");
        if (!protection.DialogueInterruptible && context == "interrupt") return new(false, null, "RPG_CREATURE_DIALOGUE_PROTECTED");
        return new(true, new(eventIdSource(), creatureId, actorId, selected.DialogueId, context), null);
    }

    public static (IReadOnlyList<StimulusDetected> Detected, IReadOnlyList<StimulusLost> Lost) DiffPerception(
        Guid creatureId, IReadOnlyList<PerceptionResult> previous, IReadOnlyList<PerceptionResult> current, Func<Guid> eventIdSource)
    {
        var oldIds = previous.Select(item => item.StimulusId).ToHashSet(StringComparer.Ordinal); var newIds = current.Select(item => item.StimulusId).ToHashSet(StringComparer.Ordinal);
        var detected = current.Where(item => !oldIds.Contains(item.StimulusId)).Select(item => new StimulusDetected(eventIdSource(), creatureId, item.StimulusId, item.Sense, item.Confidence, item.Position, item.RememberUntilSeconds)).ToArray();
        var lost = previous.Where(item => !newIds.Contains(item.StimulusId)).Select(item => new StimulusLost(eventIdSource(), creatureId, item.StimulusId, item.Sense, item.Position, "out-of-range")).ToArray();
        return (detected, lost);
    }

    public static AttackResolved ResolveAttack(AttackDeclared declared, string result, double damage, Func<Guid> eventIdSource)
    {
        if (declared.EventId == Guid.Empty || declared.CreatureId == Guid.Empty || string.IsNullOrWhiteSpace(declared.AttackerId) || string.IsNullOrWhiteSpace(declared.TargetId) || declared.Network.Tick < 0 || damage < 0)
            throw new InvalidOperationException("RPG_CREATURE_ATTACK_INVALID");
        return new(eventIdSource(), declared.CreatureId, declared.AttackerId, declared.WeaponId, declared.TargetId, result, damage, declared.Network);
    }
}
