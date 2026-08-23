namespace Forge.Studio.Session;

internal sealed record StudioUx9JOperationsDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    bool RequiredAtRuntime);

internal sealed record StudioUx9JOperationsProbeOptions(
    string SearchText,
    string ScriptKind,
    string ScriptSource,
    string? ScriptSignature,
    int ScriptBudget,
    string VariableName,
    string VariableKind,
    string VariableValue,
    string VisibilityMode)
{
    internal static StudioUx9JOperationsProbeOptions CreateDefault() => new(
        "Garde", "registered-command", "announce", null, 256,
        "tension", "enum", "crisis", "selected-player");
}

internal sealed record StudioUx9JOperationsProbe(
    string Status,
    int SearchMatches,
    bool SearchAdvanced,
    bool StableDeepLink,
    bool VisualFocus,
    string ScriptCode,
    string ScriptAdapter,
    bool ScriptOutput,
    string VariableCode,
    string VariableKind,
    bool VariablePersisted,
    bool VariableRolledBack,
    string SceneCode,
    int SceneFacets,
    bool SceneRolledBack,
    string VisibilityMode,
    int VisibilityLayers,
    bool PlayerProjection,
    bool ViewportOverlay,
    bool Compensated,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9JOperationsPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool SearchRuntime,
    bool ScriptRuntime,
    bool VariableRuntime,
    bool SceneRuntime,
    bool VisibilityRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9JOperationsProbe Probe);

internal sealed record StudioUx9JActionResult(bool Succeeded, string Message);
internal sealed record StudioUx9JApprovalSnapshot(StudioUx9JOperationsDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9JSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool InvalidBlocked,
    bool PermissionBlocked,
    bool ConfirmationRequired,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool SearchRuntime,
    bool ScriptRuntime,
    bool VariableRuntime,
    bool SceneRuntime,
    bool VisibilityRuntime,
    bool Compensation,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
