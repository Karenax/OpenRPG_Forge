using Forge.Domain.Areas;

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
    EncounterWorldProfile.CleanupPolicy Cleanup)
{
    public enum VisibilityMode { Hidden, BehindCover, Portal, Effect, InViewAllowed }

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
}
