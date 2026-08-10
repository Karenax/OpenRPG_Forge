using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public sealed record EncounterWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AreaId AreaId,
    EncounterWorldProfile.RespawnPolicy? Respawn,
    string? SpawnFactionId,
    EncounterWorldProfile.SchedulerPolicy? Scheduler,
    EncounterWorldProfile.ReinforcementPolicy? Reinforcements,
    EncounterWorldProfile.VisibilityPolicy Visibility,
    EncounterWorldProfile.CleanupPolicy Cleanup,
    EncounterWorldProfile.FoundationPolicy? Foundation = null)
{
    public enum VisibilityMode { Hidden, BehindCover, Portal, Effect, InViewAllowed }
    public enum VolumeShape { ExtrudedPolygon2D, Box3D, Sphere3D, Capsule3D }
    public enum CandidateRole { Brute, Skirmisher, Controller, Support, Boss, Ambient }

    public sealed record RespawnPolicy(
        double DelaySeconds,
        int MaximumCycles,
        string Condition,
        bool ResetWhenEmpty,
        bool Persistent,
        double AntiFarmMultiplier);

    public sealed record SchedulerPolicy(
        bool HeartbeatEnabled,
        double IntervalSeconds,
        int BudgetPerMinute,
        string CustomEventNamespace);

    public sealed record ReinforcementPolicy(
        int MinimumAlertLevel,
        string FactionId,
        Guid RouteId,
        int MaximumActors,
        double DelaySeconds,
        string CancelCondition);

    public sealed record VisibilityPolicy(
        VisibilityMode Mode,
        double MinimumCameraDistance,
        bool RequireLineOfSightBlocked,
        string SpawnEffectId);

    public sealed record CleanupPolicy(
        double DeadAfterSeconds,
        double EscapedAfterSeconds,
        double AbandonedAfterSeconds,
        bool CleanupOnAreaUnload,
        bool PreservePersistentActors);

    public sealed record ActivationVolume(
        VolumeShape Shape,
        IReadOnlyList<Vector3Value> Points,
        Vector3Value Center,
        double Height,
        double Radius,
        IReadOnlyList<string> RequiredEntityTags,
        IReadOnlyList<string> AllowedFactions,
        bool RequireLineOfSight,
        double ThreatRadius,
        string? ActivationScriptId,
        bool VisualizeInEditor);

    public sealed record SpawnCandidate(
        Guid Id,
        BlueprintId BlueprintId,
        CandidateRole Role,
        double Weight,
        double Cost,
        IReadOnlyList<string> Conditions,
        IReadOnlyList<string> Variants,
        int MaximumAlive);

    public sealed record FoundationPolicy(
        ActivationVolume Volume,
        IReadOnlyList<SpawnCandidate> Candidates,
        double SpawnBudget,
        IReadOnlyList<string> SpawnAnchorIds,
        IReadOnlyList<string> RewardIds,
        int NetworkActorCap);
}
