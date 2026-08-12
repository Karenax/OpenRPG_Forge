using Forge.Domain.Validation;

namespace Forge.Domain.Gameplay;

public static class GameplaySessionProfileValidator
{
    public const string IdentityCode = "gameplay.session.identity_invalid";
    public const string PersistenceCode = "gameplay.session.persistence_invalid";
    public const string CheckpointCode = "gameplay.session.checkpoint_invalid";
    public const string WaypointReferenceCode = "gameplay.session.checkpoint_waypoint_missing";
    public const string ShellCode = "gameplay.session.shell_invalid";
    public const string ControlCode = "gameplay.session.control_invalid";
    public const string ReplayCode = "gameplay.session.replay_invalid";
    public const string RequiredMissingCode = "runtime.gameplay.session_required_missing";

    private static readonly HashSet<string> PrivacyClassifications =
        new(["public", "internal", "personal", "sensitive"], StringComparer.Ordinal);

    public static IReadOnlyList<DomainDiagnostic> Validate(GameplaySessionProfile profile,
        IReadOnlySet<string>? waypointIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        const string root = "module/runtimePolicies/gameplay/session";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != GameplaySessionProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance) || profile.AllIds().Any(id => id == Guid.Empty) ||
            profile.AllIds().Distinct().Count() != profile.AllIds().Count)
            diagnostics.Add(new(IdentityCode, "Gameplay session requires unique UUIDs, schema v1 and provenance.",
                root));

        var persistence = profile.Persistence;
        if (persistence.SchemaVersion != 1 || string.IsNullOrWhiteSpace(persistence.Provenance) ||
            string.IsNullOrWhiteSpace(persistence.CloudAdapter) ||
            !PrivacyClassifications.Contains(persistence.PrivacyClassification) ||
            persistence.MaximumSnapshots is < 1 or > 10_000 ||
            persistence.RequiredAtRuntime && !persistence.Enabled ||
            persistence.CloudAdapterEnabled && persistence.CloudAdapter == "none" ||
            persistence.ContainsPersonalData && persistence.PrivacyClassification == "public")
            diagnostics.Add(new(PersistenceCode,
                "Persistence scope, authority, conflict, cloud and privacy controls are invalid.",
                $"{root}/persistence/{persistence.Id:D}"));

        if (profile.Checkpoints.Any(checkpoint => checkpoint.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(checkpoint.Provenance) || string.IsNullOrWhiteSpace(checkpoint.WaypointId) ||
                checkpoint.CurrencyCost < 0 || checkpoint.SafeRadius <= 0 || checkpoint.RespawnDelaySeconds < 0 ||
                string.IsNullOrWhiteSpace(checkpoint.BeforeRespawnEvent) ||
                string.IsNullOrWhiteSpace(checkpoint.AfterRespawnEvent) ||
                checkpoint.RequiredAtRuntime && !checkpoint.Enabled))
            diagnostics.Add(new(CheckpointCode,
                "Checkpoints require waypoint, cost, party/corpse policy, safe radius, delay and hooks.",
                $"{root}/checkpoints"));
        if (waypointIds is not null && profile.Checkpoints.Any(checkpoint =>
                checkpoint.Enabled && !waypointIds.Contains(checkpoint.WaypointId)))
            diagnostics.Add(new(WaypointReferenceCode, "Checkpoint references an unknown waypoint.",
                $"{root}/checkpoints"));

        var shell = profile.Shell;
        if (shell.SchemaVersion != 1 || string.IsNullOrWhiteSpace(shell.Provenance) ||
            string.IsNullOrWhiteSpace(shell.SkinId) || string.IsNullOrWhiteSpace(shell.KitId) ||
            shell.Scale is < .5 or > 3 || string.IsNullOrWhiteSpace(shell.LayoutDocument) ||
            shell.InputActions.Count == 0 || shell.InputActions.Any(string.IsNullOrWhiteSpace) ||
            shell.InputActions.Distinct(StringComparer.Ordinal).Count() != shell.InputActions.Count ||
            shell.RequiredAtRuntime && !shell.Enabled)
            diagnostics.Add(new(ShellCode,
                "Runtime shell requires kit, skin, scalable layout and unique input actions.",
                $"{root}/shell/{shell.Id:D}"));

        var locks = profile.ControlLocks;
        if (locks.SchemaVersion != 1 || string.IsNullOrWhiteSpace(locks.Provenance) ||
            locks.RequiredAtRuntime && !locks.Enabled || locks.Contexts.Count == 0 ||
            locks.Contexts.Any(context => context.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(context.Provenance) || string.IsNullOrWhiteSpace(context.OwnerId) ||
                string.IsNullOrWhiteSpace(context.CameraMode) || context.AllowedInputActions.Count == 0 ||
                context.AllowedInputActions.Any(string.IsNullOrWhiteSpace)) ||
            locks.Contexts.Select(item => item.OwnerId).Distinct(StringComparer.Ordinal).Count() != locks.Contexts.Count)
            diagnostics.Add(new(ControlCode,
                "Control contexts require unique owners, cursor, camera, input allowlist and restoration.",
                $"{root}/controlLocks/{locks.Id:D}"));

        var replay = profile.Replay;
        if (replay.SchemaVersion != 1 || string.IsNullOrWhiteSpace(replay.Provenance) ||
            replay.MaximumFrames is < 1 or > 10_000_000 || string.IsNullOrWhiteSpace(replay.PackageDirectory) ||
            !replay.RecordStateHashes || !replay.HeadlessReplay ||
            replay.EmitMinimalPackage && !replay.EmitDivergenceReport ||
            replay.RequiredAtRuntime && !replay.Enabled)
            diagnostics.Add(new(ReplayCode,
                "Replay requires bounded frames, state hashes, headless mode and governed divergence package.",
                $"{root}/replay/{replay.Id:D}"));
        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateRuntime(GameplaySessionProfile? profile, bool required)
    {
        if (profile is null || !profile.Enabled)
            return required
                ? [new(RequiredMissingCode, "Required gameplay session profile is unavailable.",
                    "module/runtimePolicies/gameplay/session")]
                : [];
        return Validate(profile);
    }
}
