using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public sealed record PlaceableWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    string? FactionId,
    Guid? DialogueId,
    PlaceableWorldProfile.CredentialPolicy? Credential,
    PlaceableWorldProfile.ReactivePolicy Reactive,
    PlaceableWorldProfile.DynamicPhysicsPolicy? Physics,
    PlaceableWorldProfile.FoundationPolicy? Foundation = null,
    bool FoundationRequiredAtRuntime = false)
{
    public enum CredentialConsumption { Retain, Consume, Charge, Shared, VirtualPermission }
    public enum BodyMode { Static, Rigid, Kinematic }

    public sealed record CredentialPolicy(
        string CredentialId,
        CredentialConsumption Consumption,
        int Charges,
        string? VirtualPermission,
        bool AuditUse);

    public sealed record EventBinding(string Event, string ScriptId, bool Before, bool Authoritative);

    public sealed record SchedulerPolicy(
        bool Enabled,
        double IntervalSeconds,
        int BudgetPerMinute,
        bool SuspendWhenUnobserved,
        string CustomNamespace);

    public sealed record ReactivePolicy(
        IReadOnlyList<EventBinding> Events,
        SchedulerPolicy Scheduler);

    public sealed record DynamicPhysicsPolicy(
        BodyMode Mode,
        double Mass,
        IReadOnlyList<string> Constraints,
        bool Replicated,
        double SleepAfterSeconds,
        bool ConvertToStaticWhenSleeping,
        string Authority);

    public sealed record FoundationPolicy(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string Slug,
        IReadOnlyList<LocalizedText> Texts,
        IReadOnlyList<string> Tags,
        string AuthorNotes,
        VisualDefinition Visual,
        CollisionDefinition Collision,
        InteractionPolicy? Interaction,
        StaticPropPolicy? StaticProp,
        ComponentPolicy Components,
        TransformPolicy Transform,
        IReadOnlyDictionary<string, string> Overrides,
        OnUsedPolicy? OnUsed = null,
        RuntimeValidationPolicy? Validation = null,
        bool OnUsedRequiredAtRuntime = false);

    public sealed record LocalizedText(string Locale, string Name, string Prompt);

    public sealed record VisualDefinition(
        string AssetId,
        IReadOnlyList<string> MeshIds,
        IReadOnlyList<string> MaterialIds,
        string IconAssetId,
        string PortraitAssetId,
        IReadOnlyList<string> VisualEffectIds);

    public sealed record CollisionDefinition(
        string Shape,
        Vector3Value Size,
        bool Enabled,
        bool NavigationObstacle,
        string PhysicsLayer);

    public sealed record InteractionVerb(Guid Id, string LabelKey, string EventName, string Condition);

    public sealed record InteractionPolicy(
        IReadOnlyList<InteractionVerb> Verbs,
        double MaximumDistance,
        double MaximumAngleDegrees,
        bool RequiresFocus,
        string PromptKey,
        double DurationSeconds,
        string Authority,
        IReadOnlyList<string> Conditions);

    public sealed record StaticPropPolicy(
        bool Batched,
        bool Instanced,
        bool Occluder,
        bool MergeEligible,
        bool CollisionEnabled);

    public sealed record ComponentPolicy(
        bool HasHealth,
        bool HasContainer,
        bool HasLock,
        bool HasTrap,
        bool HasPhysics,
        IReadOnlyDictionary<string, string> ScriptBindings);

    public sealed record TransformPolicy(
        Vector3Value Position,
        Vector3Value RotationDegrees,
        Vector3Value Scale,
        Vector3Value Pivot,
        double SnapIncrement,
        Guid? ParentId,
        bool AlignToSurface,
        string? TransformAnimationId);

    public sealed record OnUsedPolicy(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string ForgeScriptApiVersion,
        string EventBinding,
        string? BeforeScriptId,
        string? AfterScriptId);

    public sealed record RuntimeValidationPolicy(
        IReadOnlyList<string> AnimationIds,
        IReadOnlyList<string> LockIds,
        IReadOnlyList<string> TrapIds,
        IReadOnlyList<string> InventoryIds,
        IReadOnlyList<string> PhysicsProfileIds,
        int DrawCallBudget,
        int ScriptBudget);
}
