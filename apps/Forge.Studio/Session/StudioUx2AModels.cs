using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx2AAreaChoice(
    AreaId AreaId,
    string Label,
    bool IsEvidence);

internal sealed record StudioUx2AHeartbeatDraft(
    Guid Id,
    string Name,
    bool Enabled,
    int IntervalMilliseconds,
    int BudgetMilliseconds,
    bool SuspendWhenInactive,
    string ForgeScriptApiVersion,
    string Handler,
    string? ScriptPath,
    bool RequiredAtRuntime);

internal sealed record StudioUx2APerceptionModifierDraft(
    Guid Id,
    string SenseId,
    double Multiplier,
    double AdditiveModifier,
    string ConditionTag);

internal sealed record StudioUx2ARestVolumeDraft(
    Guid Id,
    string Name,
    Vector3Value Center,
    Vector3Value Size,
    RestPermissionMode Mode,
    string DenialReason,
    string Condition,
    int CooldownSeconds,
    float RequiredSecureRadius,
    int Priority);

internal sealed record StudioUx2ADraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    int AreaWidth,
    int AreaDepth,
    bool ProjectRestEnabled,
    IReadOnlyList<StudioUx2AHeartbeatDraft> Heartbeats,
    string PerceptionRulesetId,
    IReadOnlyList<StudioUx2APerceptionModifierDraft> PerceptionModifiers,
    RestPermissionMode RestMode,
    string RestDenialReason,
    string RestCondition,
    int RestCooldownSeconds,
    float RestRequiredSecureRadius,
    IReadOnlyList<StudioUx2ARestVolumeDraft> RestVolumes);

internal sealed record StudioUx2ADiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2ADiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2ADiagnostic> Diagnostics);

internal sealed record StudioUx2ASemanticDiff(
    IReadOnlyList<string> Heartbeats,
    IReadOnlyList<string> Perception,
    IReadOnlyList<string> Rest)
{
    internal bool HasChanges => Heartbeats.Count > 0 || Perception.Count > 0 || Rest.Count > 0;
}

internal sealed record StudioUx2APreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2ASemanticDiff Diff,
    IReadOnlyList<StudioUx2ADiagnosticGroup> Groups,
    bool ProjectRestDormant,
    bool SchedulerPolicyEvaluated,
    bool PerceptionPolicyEvaluated,
    bool RestPolicyEvaluated);

internal sealed record StudioUx2AActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2AApprovalSnapshot(
    StudioUx2ADraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2AEvidenceSourceSnapshot(
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
    StudioUx2AApprovalSnapshot Approval);

internal sealed record StudioUx2ASmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealScriptCatalog,
    bool CompleteCollections,
    bool NumericValuesPreserved,
    bool InvalidHeartbeatBlocked,
    bool InvalidPerceptionBlocked,
    bool InvalidRestBlocked,
    bool DryRun,
    bool HumanPerceptionDiff,
    bool DormantRestHonest,
    bool SchedulerPolicy,
    bool PerceptionPolicy,
    bool RestPolicy,
    bool StaleDraftBlocked,
    bool StaleFingerprintBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText);
