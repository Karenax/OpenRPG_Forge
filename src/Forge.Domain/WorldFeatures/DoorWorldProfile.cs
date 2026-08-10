using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public sealed record DoorWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    DoorWorldProfile.DamagePolicy? Damage,
    DoorWorldProfile.LoadingPresentation? Loading,
    IReadOnlyList<DoorWorldProfile.EventBinding> Events,
    Guid? DialogueId,
    string? FactionId,
    DoorWorldProfile.AssemblyPolicy? Assembly,
    DoorWorldProfile.FoundationPolicy? Foundation = null)
{
    public enum AssemblyKind { Single, Double, Portcullis, Elevator, Airlock }
    public enum PlacementMode { ArchitectureSocket, FreePlacement }
    public enum StateKind { Open, Closed, Opening, Closing, Locked, Jammed, Broken, Disabled, Custom }
    public enum DestinationKind { Door, Trigger, Waypoint, SpawnAnchor }

    public sealed record DamagePolicy(
        double MaximumHealth,
        double Hardness,
        bool PlotProtected,
        string DamagedVisual,
        string BreachedVisual,
        bool UpdateNavigation,
        string DebrisAssetId,
        string RepairServiceId);

    public sealed record LoadingPresentation(
        string ImageAssetId,
        string TitleKey,
        string TipKey,
        bool UseDestinationFallback,
        double MinimumDisplaySeconds);

    public sealed record EventBinding(string Event, string ScriptId, bool Before, bool Authoritative);

    public sealed record Panel(
        Guid Id,
        string Role,
        double OpenOffset,
        double DelaySeconds,
        bool Reversed);

    public sealed record AssemblyPolicy(
        AssemblyKind Kind,
        IReadOnlyList<Panel> Panels,
        string Sequence,
        bool Interlocked,
        string FailureRecovery);

    public sealed record KitAdapter(
        string KitId,
        string SocketKind,
        IReadOnlyDictionary<string, string> MaterialBindings,
        double WidthOffset,
        double HeightOffset,
        double DepthOffset);

    public sealed record State(
        string Id,
        StateKind Kind,
        string AnimationId,
        bool CollisionEnabled,
        bool InteractionEnabled);

    public sealed record Transition(
        string FromStateId,
        string ToStateId,
        string Trigger,
        double DurationSeconds,
        string? Condition);

    public sealed record InteractionPolicy(
        double Range,
        string AllowedSide,
        double ClearanceWidth,
        double ClearanceHeight,
        double ClearanceDepth,
        string Authority,
        int QueueCapacity,
        string FailureFeedbackKey);

    public sealed record PortalPolicy(
        DestinationKind DestinationKind,
        Guid DestinationId,
        string SpawnAnchorId,
        double ExitYawDegrees,
        bool Preload,
        IReadOnlyList<string> Permissions);

    public sealed record LinkPolicy(
        Guid Id,
        Guid TargetId,
        bool OneWay,
        string ReturnNavigationLabel);

    public sealed record NavigationPolicy(
        string ObstacleId,
        string LinkId,
        IReadOnlyList<string> BlockingStateIds,
        IReadOnlyList<string> TraversableStateIds,
        double OpeningPassableAt,
        double ClosingBlockedAt,
        bool VisualizeInEditor);

    public sealed record FoundationPolicy(
        PlacementMode Placement,
        string? ArchitectureSocketId,
        KitAdapter? Adapter,
        string InitialStateId,
        IReadOnlyList<State> States,
        IReadOnlyList<Transition> Transitions,
        InteractionPolicy Interaction,
        PortalPolicy? Portal,
        IReadOnlyList<LinkPolicy> Links,
        IReadOnlyDictionary<string, string> Overrides,
        NavigationPolicy? Navigation = null);
}
