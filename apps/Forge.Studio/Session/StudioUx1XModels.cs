using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioDoorFoundationChoice(
    BlueprintId BlueprintId,
    string Label,
    bool Configured);

internal sealed record StudioDoorFoundationDraft(
    BlueprintId BlueprintId,
    DoorWorldProfile.PlacementMode Placement,
    string ArchitectureAnchor,
    string ArchitectureKit,
    string OpeningKind,
    double WidthAdjustment,
    double HeightAdjustment,
    double DepthAdjustment,
    DoorWorldProfile.StateKind InitialState,
    double InteractionRange,
    string AllowedSide,
    double ClearanceWidth,
    double ClearanceHeight,
    double ClearanceDepth,
    string Authority,
    int QueueCapacity,
    string FailureFeedbackKey);

internal sealed record StudioDoorFoundationPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    string PlacementSummary = "",
    string StateSummary = "",
    string InteractionSummary = "",
    bool RuntimeInteractionAccepted = false);

internal sealed record StudioDoorFoundationActionResult(
    bool Succeeded,
    string Message);

internal sealed record StudioUx1XSmokeResult(
    bool Succeeded,
    bool ExplicitDoorSelection,
    bool SocketAndUniversalPlacement,
    bool NineStateMachine,
    bool OpeningInteraction,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool CanonicalCommand,
    bool NavigationInternalOnly,
    bool Preservation,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);
