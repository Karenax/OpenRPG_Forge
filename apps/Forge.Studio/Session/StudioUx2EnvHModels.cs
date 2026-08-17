using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvHAreaChoice(
    AreaId AreaId,
    string Label,
    string RangeSummary,
    string ControlSummary);

internal sealed record StudioUx2EnvHDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    AreaSoundMode Mode,
    string InnerRadius,
    string OuterRadius,
    string AirAbsorption,
    string AttenuationCurve,
    bool DebugSpheresVisible,
    bool InitiallyActive,
    AudioInstancePolicy InstancePolicy,
    AudioAuthority Authority,
    string DefaultFadeSeconds,
    string StartedEvent,
    string StoppedEvent,
    string PausedEvent,
    string FadedEvent,
    string ForgeScriptApiVersion);

internal sealed record StudioUx2EnvHAttenuationSample(
    string Distance,
    string CurveGain,
    string GainAfterAirAbsorption);

internal sealed record StudioUx2EnvHDebugSphere(
    string Kind,
    string Radius);

internal sealed record StudioUx2EnvHScriptAction(
    string Name,
    string Signature,
    string Summary,
    string Availability);

internal sealed record StudioUx2EnvHControlStep(
    string Operation,
    string Status,
    string Gain,
    int ActiveInstances,
    int HandleGeneration,
    string Event,
    string Diagnostic);

internal sealed record StudioUx2EnvHDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvHDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvHDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvHDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx2EnvHPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvHDiff Diff,
    IReadOnlyList<StudioUx2EnvHDiagnosticGroup> Groups,
    IReadOnlyList<StudioUx2EnvHAttenuationSample> Attenuation,
    IReadOnlyList<StudioUx2EnvHDebugSphere> DebugSpheres,
    IReadOnlyList<StudioUx2EnvHScriptAction> ScriptActions,
    IReadOnlyList<StudioUx2EnvHControlStep> ControlSteps,
    bool CanonicalDryRun,
    bool ScriptContractAligned,
    bool AuthorityDiagnosticExercised,
    bool PlayerInstantiationSkipped,
    bool ScriptExecutionSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvHActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvHApprovalSnapshot(
    StudioUx2EnvHDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvHEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
    Forge.Domain.Entities.EntityId? SelectedEntityId,
    StudioUx2EnvHApprovalSnapshot Approval);

internal sealed record StudioUx2EnvHSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealSpatialSourceDetected,
    bool InvalidRangeBlocked,
    bool InvalidCurveBlocked,
    bool InvalidAbsorptionBlocked,
    bool InvalidEventsBlocked,
    bool InvalidApiBlocked,
    bool CanonicalDryRun,
    bool AttenuationPlan,
    bool AirAbsorptionPlan,
    bool DebugSpherePlan,
    bool ScriptApiCatalog,
    bool TypedControlPlan,
    bool AuthorityDiagnostic,
    bool StaleRevisionBlocked,
    bool ApprovalFingerprintBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool EnvironmentLotsPreserved,
    bool AudioTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool BoundedRuntimeClaim,
    bool Cleanup);
