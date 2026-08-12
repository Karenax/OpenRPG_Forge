using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public sealed record DoorAccessEventResolution(
    bool Available,
    bool OptionalFallback,
    DoorAccessEventProfile? Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record DoorLockEventResolution(
    bool Available,
    bool OptionalFallback,
    DoorAccessEventProfile.DoorLockEventPolicy? Profile,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class DoorAccessEventProfileValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(DoorAccessEventProfile profile, string path)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != DoorAccessEventProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance) || string.IsNullOrWhiteSpace(profile.AssetId))
            Add(diagnostics, "world.door.access_events_identity_invalid",
                "Door access/events require UUID, schema v1, provenance and an asset identity.", path);

        var componentIds = new List<Guid>
        {
            profile.Lock.Id,
            profile.Rejection.Id,
            profile.Trap.Id,
            profile.PortalUse.Id,
            profile.StateEvents.Id,
        };
        if (profile.LockStateEvents is not null)
            componentIds.Add(profile.LockStateEvents.Id);
        if (componentIds.Any(id => id == Guid.Empty) || componentIds.Distinct().Count() != componentIds.Count ||
            componentIds.Contains(profile.Id))
            Add(diagnostics, "world.door.access_events_uuid_invalid",
                "Door access/event component UUIDs must be non-empty and unique.", $"{path}/components");

        var doorLock = profile.Lock;
        if (doorLock.CredentialIds.Count == 0 || doorLock.CredentialIds.Any(string.IsNullOrWhiteSpace) ||
            doorLock.CredentialIds.Distinct(StringComparer.Ordinal).Count() != doorLock.CredentialIds.Count ||
            string.IsNullOrWhiteSpace(doorLock.SkillId) || !double.IsFinite(doorLock.Difficulty) ||
            doorLock.Difficulty < 0 || doorLock.Permissions.Any(string.IsNullOrWhiteSpace))
            Add(diagnostics, "world.door.lock_invalid",
                "Door lock credentials, skill, difficulty and permissions are invalid.", $"{path}/lock");

        var rejection = profile.Rejection;
        if (!string.Equals(rejection.EventBinding, "DoorOpenRejected", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(rejection.ForgeScriptApiVersion) || rejection.Texts.Count == 0 ||
            rejection.Texts.Any(item => string.IsNullOrWhiteSpace(item.Locale) || string.IsNullOrWhiteSpace(item.Text)) ||
            rejection.Texts.Select(item => item.Locale).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rejection.Texts.Count ||
            string.IsNullOrWhiteSpace(rejection.SoundAssetId) || string.IsNullOrWhiteSpace(rejection.AnimationId) ||
            string.IsNullOrWhiteSpace(rejection.ScriptId))
            Add(diagnostics, "world.door.open_rejected_invalid",
                "DoorOpenRejected requires a typed binding, localized text, sound, animation and script.",
                $"{path}/rejection");

        var trap = profile.Trap;
        if (string.IsNullOrWhiteSpace(trap.TriggerId) || string.IsNullOrWhiteSpace(trap.PayloadId) ||
            !double.IsFinite(trap.DetectionDifficulty) || trap.DetectionDifficulty < 0 ||
            !double.IsFinite(trap.DisarmDifficulty) || trap.DisarmDifficulty < 0 ||
            !double.IsFinite(trap.ResetDelaySeconds) || trap.ResetDelaySeconds < 0 ||
            string.IsNullOrWhiteSpace(trap.OwnerId) || string.IsNullOrWhiteSpace(trap.VisualEffectId) ||
            string.IsNullOrWhiteSpace(trap.FriendlyPolicy) || trap.Sides.Count == 0 || trap.Events.Count == 0 ||
            trap.Sides.Any(string.IsNullOrWhiteSpace) || trap.Events.Any(string.IsNullOrWhiteSpace))
            Add(diagnostics, "world.door.trap_invalid",
                "Door trap requires trigger, payload, difficulties, reset, ownership, sides and events.",
                $"{path}/trap");

        var portal = profile.PortalUse;
        if (string.IsNullOrWhiteSpace(portal.ForgeScriptApiVersion) ||
            !string.Equals(portal.RequestedBinding, "PortalUseRequested", StringComparison.Ordinal) ||
            !string.Equals(portal.CompletedBinding, "PortalUseCompleted", StringComparison.Ordinal))
            Add(diagnostics, "world.door.portal_events_invalid",
                "Door portal events require PortalUseRequested and PortalUseCompleted typed bindings.",
                $"{path}/portalUse");

        var states = profile.StateEvents;
        if (string.IsNullOrWhiteSpace(states.ForgeScriptApiVersion) ||
            !string.Equals(states.OpenedBinding, "DoorOpened", StringComparison.Ordinal) ||
            !string.Equals(states.ClosedBinding, "DoorClosed", StringComparison.Ordinal))
            Add(diagnostics, "world.door.state_events_invalid",
                "Door state events require DoorOpened and DoorClosed terminal bindings.",
                $"{path}/stateEvents");

        if (profile.LockStateEvents is { } lockEvents)
            diagnostics.AddRange(ValidateLockEvents(lockEvents, $"{path}/lockStateEvents"));
        else if (profile.LockStateEventsRequiredAtRuntime)
            Add(diagnostics, "world.door.lock_events_required",
                "Door lock events are required by this runtime profile.", $"{path}/lockStateEvents");

        if (profile.Overrides.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            Add(diagnostics, "world.door.access_events_override_invalid",
                "Door access/event overrides require non-empty keys and values.", $"{path}/overrides");

        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateLockEvents(
        DoorAccessEventProfile.DoorLockEventPolicy profile,
        string path)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != DoorAccessEventProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance) || string.IsNullOrWhiteSpace(profile.ForgeScriptApiVersion) ||
            !string.Equals(profile.LockedBinding, "DoorLocked", StringComparison.Ordinal) ||
            !string.Equals(profile.UnlockedBinding, "DoorUnlocked", StringComparison.Ordinal))
            Add(diagnostics, "world.door.lock_events_invalid",
                "Door lock events require UUID, schema v1, provenance, API version and exact DoorLocked/DoorUnlocked bindings.",
                path);
        return diagnostics;
    }

    public static DoorAccessEventResolution Resolve(DoorWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var path = $"module/worldFeatures/doors/{profile.Id}/accessEvents";
        if (profile.AccessEvents is null)
        {
            var missing = profile.AccessEventsRequiredAtRuntime
                ? new[] { new DomainDiagnostic("world.door.access_events_required",
                    "Door access/events are required by this runtime profile.", path) }
                : [];
            return new(false, !profile.AccessEventsRequiredAtRuntime, null, missing);
        }

        var diagnostics = Validate(profile.AccessEvents, path);
        return new(diagnostics.Count == 0, false, profile.AccessEvents, diagnostics);
    }

    public static DoorLockEventResolution ResolveLockEvents(DoorWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var access = Resolve(profile);
        if (!access.Available || access.Profile is null)
            return new(false, access.OptionalFallback, null, access.Diagnostics);

        var path = $"module/worldFeatures/doors/{profile.Id}/accessEvents/lockStateEvents";
        if (access.Profile.LockStateEvents is null)
        {
            var missing = access.Profile.LockStateEventsRequiredAtRuntime
                ? new[] { new DomainDiagnostic("world.door.lock_events_required",
                    "Door lock events are required by this runtime profile.", path) }
                : [];
            return new(false, !access.Profile.LockStateEventsRequiredAtRuntime, null, missing);
        }

        var diagnostics = ValidateLockEvents(access.Profile.LockStateEvents, path);
        return new(diagnostics.Count == 0, false, access.Profile.LockStateEvents, diagnostics);
    }

    private static void Add(List<DomainDiagnostic> diagnostics, string code, string message, string path) =>
        diagnostics.Add(new(code, message, path));
}
