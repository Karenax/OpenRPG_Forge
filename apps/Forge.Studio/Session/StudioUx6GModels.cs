using Forge.RpgKit.Domain.Scripting;

namespace Forge.Studio.Session;

internal enum StudioUx6GWizardAction
{
    Message,
    DeferredAction,
    CombatAttack,
    ApplyEffect,
}

internal sealed record StudioUx6GWizardDraft(
    string FunctionName,
    string BooleanCondition,
    StudioUx6GWizardAction Action,
    string ActionOrAbility,
    int Priority,
    double Amount,
    double DurationSeconds,
    int Seed);

internal sealed record StudioUx6GGameplayDraft(
    string ActionName,
    int Priority,
    string AbilityId,
    double Damage,
    double ArmorPenetration,
    int Seed,
    string EffectId,
    double Magnitude,
    double DurationSeconds,
    double PeriodSeconds,
    EffectStacking Stacking);

internal sealed record StudioUx6GEditorAnalysis(
    bool Succeeded,
    string Message,
    string FormattedSource,
    string? SignatureHelp,
    IReadOnlyList<string> Completions,
    IReadOnlyList<string> Symbols,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics,
    int SyntaxSpanCount);

internal sealed record StudioUx6GWizardResult(
    bool Succeeded,
    string Message,
    string ReviewedSource,
    string CandidateSource,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6GGameplayResult(
    bool Succeeded,
    string Message,
    bool OptionalFallbackSafe,
    bool RequiredMissingDiagnostic,
    bool TaskQueued,
    bool TaskCompleted,
    bool UnauthorizedTaskBlocked,
    bool CombatCommitted,
    bool UnauthorizedCombatBlocked,
    double AppliedDamage,
    double RemainingHealth,
    bool EffectApplied,
    bool EffectAdvanced,
    bool EffectRoundTrip,
    int EffectCount,
    IReadOnlyList<string> Events,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6GSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool SharedSourceAuthority,
    bool EditorAnalysis,
    bool CompletionAndSignature,
    bool Formatting,
    bool ClickableDiagnostics,
    bool WizardReviewedSource,
    bool InvalidWizardBlocked,
    bool DeferredActions,
    bool CombatApi,
    bool EffectApi,
    bool AuthorityBlocked,
    bool CanonicalDryRun,
    bool Compilation,
    bool SandboxExecution,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
