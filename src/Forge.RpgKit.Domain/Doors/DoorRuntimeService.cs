using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Doors;

public sealed record DoorNavigationProfile(string ObstacleId, string LinkId, bool VisualizeInEditor, IReadOnlySet<string> BlockingStateIds, IReadOnlySet<string> TraversableStateIds);
public sealed record DoorNavigationState(string StateId, bool ObstacleEnabled, bool LinkEnabled, double AnimationProgress, string Visualization, string EventName);
public sealed record LocalizedDoorFailure(string Locale, string Text);
public sealed record DoorOpenRejected(Guid EventId, int SchemaVersion, string Provenance, Guid DoorId, string ActorId, string ReasonCode, IReadOnlyList<LocalizedDoorFailure> Texts, string SoundAssetId, string AnimationId, string? ScriptId);
public sealed record DoorTrapBinding(TrapComponent Trap, IReadOnlySet<string> Sides, IReadOnlySet<string> Events);
public sealed record DoorRuntimeProfile(DoorNavigationProfile Navigation, LockComponent? Lock, DoorTrapBinding? Trap, IReadOnlyList<string> ScriptBindings, IReadOnlySet<Guid> KnownDestinations, IReadOnlySet<string> KnownAnimations, IReadOnlySet<string> KnownCollisionProfiles, IReadOnlySet<string> KnownCredentials, IReadOnlySet<string> KnownScripts);
public sealed record DoorUnlockResult(LockResult LockResult, IReadOnlySet<string> RemainingCredentials, bool CredentialConsumed);

public static class DoorRuntimeService
{
    public static DoorNavigationState SynchronizeNavigation(DoorNavigationProfile profile, string stateId, double animationProgress)
    {
        var progress = Math.Clamp(animationProgress, 0, 1); var traversable = profile.TraversableStateIds.Contains(stateId); var blocking = profile.BlockingStateIds.Contains(stateId);
        if (!traversable && !blocking) traversable = stateId == "opening" ? progress >= 0.75 : stateId == "closing" && progress < 0.25;
        var obstacle = !traversable; var visualization = profile.VisualizeInEditor ? $"obstacle:{profile.ObstacleId}:{(obstacle ? "blocked" : "clear")}|link:{profile.LinkId}:{(traversable ? "enabled" : "disabled")}" : "hidden";
        return new(stateId, obstacle, traversable, progress, visualization, "DoorNavigationChanged");
    }

    public static DoorUnlockResult Unlock(LockComponent component, LockAttempt attempt, IReadOnlySet<string> actorCredentials, bool consumeCredential)
    {
        var result = PlaceableSecurityService.AttemptUnlock(component, attempt with { Credentials = actorCredentials });
        var used = result.Succeeded && result.Method == "credential" ? component.Credentials.FirstOrDefault(actorCredentials.Contains) : null;
        var remaining = used is not null && consumeCredential ? actorCredentials.Where(item => item != used).ToHashSet(StringComparer.Ordinal) : actorCredentials;
        return new(result, remaining, used is not null && consumeCredential);
    }

    public static DoorOpenRejected Reject(Guid doorId, string actorId, string reasonCode, IReadOnlyList<LocalizedDoorFailure> texts, string soundAssetId, string animationId, string? scriptId, string provenance)
    {
        if (doorId == Guid.Empty || string.IsNullOrWhiteSpace(reasonCode) || texts.Count == 0 || texts.Any(item => string.IsNullOrWhiteSpace(item.Locale) || string.IsNullOrWhiteSpace(item.Text)) || string.IsNullOrWhiteSpace(soundAssetId) || string.IsNullOrWhiteSpace(animationId)) throw new InvalidOperationException("RPG_DOOR_REJECTION_INVALID");
        return new(Guid.NewGuid(), 1, provenance, doorId, actorId, reasonCode, texts, soundAssetId, animationId, scriptId);
    }

    public static TrapResult TriggerTrap(DoorTrapBinding binding, string side, string eventName, string actorFaction)
    {
        if (!binding.Sides.Contains(side) || !binding.Events.Contains(eventName)) return new(binding.Trap, false, "TrapIgnored", "side-or-event");
        return PlaceableSecurityService.Trigger(binding.Trap, actorFaction);
    }

    public static IReadOnlyList<DoorDiagnostic> Validate(DoorDefinition definition, DoorRuntimeProfile profile, IReadOnlyList<DoorLink> links)
    {
        var diagnostics = DoorRules.Validate(definition).ToList(); var root = $"doors/{definition.Id}";
        if (definition.Portal is { } portal && !profile.KnownDestinations.Contains(portal.DestinationId)) diagnostics.Add(new("RPG_DOOR_DESTINATION_MISSING", $"{root}/portal", "Portal destination is missing."));
        if (definition.StateMachine.States.Any(item => !profile.KnownAnimations.Contains(item.AnimationId))) diagnostics.Add(new("RPG_DOOR_ANIMATION_MISSING", $"{root}/states", "A door animation is missing."));
        if (profile.KnownCollisionProfiles.Count == 0) diagnostics.Add(new("RPG_DOOR_COLLISION_PROFILE_MISSING", $"{root}/collision", "Collision profile is missing."));
        if (profile.Navigation.BlockingStateIds.Overlaps(profile.Navigation.TraversableStateIds) || string.IsNullOrWhiteSpace(profile.Navigation.ObstacleId) || string.IsNullOrWhiteSpace(profile.Navigation.LinkId)) diagnostics.Add(new("RPG_DOOR_NAVIGATION_INVALID", $"{root}/navigation", "Navigation states, obstacle or link are invalid."));
        if (profile.Lock is { } doorLock && doorLock.Credentials.Any(item => !profile.KnownCredentials.Contains(item))) diagnostics.Add(new("RPG_DOOR_CREDENTIAL_MISSING", $"{root}/lock", "Door credential is missing."));
        if (profile.Trap is { } trap && (trap.Sides.Count == 0 || trap.Events.Count == 0)) diagnostics.Add(new("RPG_DOOR_TRAP_BINDING_INVALID", $"{root}/trap", "Door trap requires sides and events."));
        if (profile.ScriptBindings.Any(string.IsNullOrWhiteSpace)) diagnostics.Add(new("RPG_DOOR_SCRIPT_INVALID", $"{root}/scripts", "Script binding is invalid."));
        if (profile.ScriptBindings.Where(binding => !string.IsNullOrWhiteSpace(binding)).Any(binding => !profile.KnownScripts.Contains(binding))) diagnostics.Add(new("RPG_DOOR_SCRIPT_MISSING", $"{root}/scripts", "Door script is missing from the validation context."));
        diagnostics.AddRange(DoorLinkAuthoringValidation(links, definition.Id));
        if (links.Any(link => link.SourceDoorId == definition.Id && link.TargetDoorId == definition.Id)) diagnostics.Add(new("RPG_DOOR_TRANSITION_LOOP", $"{root}/links", "Door transition loops to itself."));
        return diagnostics;
    }

    private static IEnumerable<DoorDiagnostic> DoorLinkAuthoringValidation(IReadOnlyList<DoorLink> links, Guid doorId)
    {
        foreach (var link in links.Where(item => item.SourceDoorId == doorId && !item.OneWay && links.All(reverse => reverse.SourceDoorId != item.TargetDoorId || reverse.TargetDoorId != doorId))) yield return new("RPG_DOOR_LINK_REVERSE_MISSING", $"doors/links/{doorId}/{link.TargetDoorId}", "Bidirectional door link has no reverse link.");
    }
}
