using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public sealed record TriggerWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    AreaId AreaId,
    TriggerWorldProfile.DiscoveryPolicy? Discovery,
    TriggerWorldProfile.InteractionPolicy? Interaction,
    TriggerWorldProfile.SchedulerPolicy? Scheduler,
    string CustomEventNamespace,
    TriggerWorldProfile.AccessPolicy? Access,
    TriggerWorldProfile.RestPolicy? Rest,
    TriggerWorldProfile.CluePolicy? Clue,
    TriggerWorldProfile.NarrativeCuePolicy? Narrative,
    TriggerWorldProfile.DirectorPolicy? Director,
    TriggerWorldProfile.FoundationPolicy? Foundation = null,
    bool FoundationRequiredAtRuntime = false)
{
    public enum DiscoveryScope { Player, Party, World }
    public enum ShapeKind { ExtrudedPolygon2D, Box3D, Sphere3D, Capsule3D, ConvexHull3D }
    public enum FirePolicy { Once, OncePerEntity, Repeat, WhileInside }
    public enum DestinationKind { Door, Trigger, Waypoint, SpawnAnchor }

    public sealed record DiscoveryPolicy(
        IReadOnlyList<string> Senses,
        int Difficulty,
        Guid RevealTargetId,
        string FeedbackId,
        bool Permanent,
        DiscoveryScope Scope);

    public sealed record InteractionPolicy(
        string Verb,
        string Icon,
        string Outline,
        string PromptKey,
        string ScriptId,
        bool Visible,
        string Permission);

    public sealed record SchedulerPolicy(
        double IntervalSeconds,
        int BudgetPerMinute,
        bool WhileOccupiedOnly,
        bool AggregateOccupants);

    public sealed record AccessPolicy(string CredentialId, bool Consume, string DeniedFeedbackKey);

    public sealed record RestPolicy(
        bool Safe,
        string DangerQuery,
        bool WholePartyRequired,
        bool AllowCampSetup,
        string AllowedMessageKey,
        string DeniedMessageKey);

    public sealed record CluePolicy(
        string SkillId,
        int Difficulty,
        string TraceId,
        double DirectionDegrees,
        double AgeHours,
        string? FalseClueId,
        Guid? QuestId);

    public sealed record NarrativeCuePolicy(
        IReadOnlyList<BlueprintId> Participants,
        int Priority,
        bool Once,
        double CooldownSeconds,
        string MultiplayerPolicy,
        Guid DialogueId);

    public sealed record DirectorPolicy(
        bool AllowManualEnter,
        bool AllowManualExit,
        bool AllowManualFire,
        bool RequireTarget,
        bool Audit);

    public sealed record FoundationPolicy(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string Slug,
        ShapePolicy Shape,
        FilterPolicy Filter,
        IReadOnlyList<string> Conditions,
        double CooldownSeconds,
        FirePolicy Fire,
        IReadOnlyList<ActionPolicy> Actions,
        PortalPolicy? Portal,
        TransformPolicy Transform,
        IReadOnlyDictionary<string, string> Overrides,
        EventPolicy OnEnter,
        EventPolicy OnExit,
        RuntimeValidationPolicy? Validation = null,
        bool ValidationRequiredAtRuntime = false);

    public sealed record ShapePolicy(
        ShapeKind Kind,
        IReadOnlyList<Vector3Value> Vertices,
        IReadOnlyList<IReadOnlyList<Vector3Value>> Holes,
        Vector3Value Size,
        double Height,
        double SnapIncrement,
        bool VisualizeInEditor);

    public sealed record FilterPolicy(
        IReadOnlyList<string> EntityKinds,
        IReadOnlyList<string> RequiredTags,
        IReadOnlyList<string> ExcludedTags,
        IReadOnlyList<string> Layers);

    public sealed record ActionPolicy(Guid Id, string Kind, string TargetId,
        IReadOnlyDictionary<string, string> Parameters);

    public sealed record PortalPolicy(DestinationKind Destination, Guid DestinationId, string SpawnAnchorId,
        Vector3Value ExitOrientationDegrees, bool Preload, IReadOnlyList<string> Permissions);

    public sealed record TransformPolicy(Vector3Value Position, Vector3Value RotationDegrees, Vector3Value Scale,
        Guid? ParentId);

    public sealed record EventPolicy(Guid Id, int SchemaVersion, string Provenance, string ForgeScriptApiVersion,
        string EventBinding, string? BeforeScriptId, string? AfterScriptId);

    public sealed record RuntimeValidationPolicy(
        IReadOnlyList<Guid> KnownPortalDestinationIds,
        IReadOnlyList<string> KnownTargetIds,
        IReadOnlyList<string> KnownScriptIds,
        IReadOnlyList<string> AccessibleLayers);
}
