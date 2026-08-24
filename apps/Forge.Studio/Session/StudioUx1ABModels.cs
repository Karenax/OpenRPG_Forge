using Forge.Domain.Blueprints;

namespace Forge.Studio.Session;

internal sealed record StudioDoorDamageLoadingChoice(
    BlueprintId BlueprintId,
    string Label,
    bool Configured);

internal sealed record StudioDoorDamageLoadingDraft(
    BlueprintId BlueprintId,
    bool DamageEnabled,
    double MaximumHealth,
    double Hardness,
    bool PlotProtected,
    string DamagedVisual,
    string BreachedVisual,
    bool UpdateNavigation,
    string DebrisAssetId,
    string RepairServiceId,
    bool LoadingEnabled,
    string LoadingImageAssetId,
    string LoadingTitleKey,
    string LoadingTipKey,
    bool UseDestinationFallback,
    double MinimumDisplaySeconds,
    bool DamageEventEnabled,
    string DamageScriptId,
    bool DestructionEventEnabled,
    string DestructionScriptId,
    bool AttackTargetedEventEnabled,
    string AttackTargetedScriptId,
    bool AttackResolvedEventEnabled,
    string AttackResolvedScriptId);

internal sealed record StudioDoorDamageLoadingPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    bool DamageSimulationAccepted = false,
    bool EventSimulationAccepted = false);

internal sealed record StudioDoorDamageLoadingActionResult(
    bool Succeeded,
    string Message,
    bool StaleRevision = false);

internal sealed record StudioUx1ABSmokeResult(
    bool Succeeded,
    bool ExplicitDoorSelection,
    bool DamagePolicy,
    bool LoadingPresentation,
    bool AuthoritativeEvents,
    bool NeighborEventMerge,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool CanonicalCommand,
    bool Preservation,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);
