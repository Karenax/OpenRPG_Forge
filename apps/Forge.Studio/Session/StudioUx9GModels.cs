namespace Forge.Studio.Session;

internal sealed record StudioUx9GLiveActionDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    int MaximumPossessionMinutes,
    bool RequiredAtRuntime);

internal sealed record StudioUx9GProbeOptions(
    string PartyPolicy,
    bool PlayerConsent,
    string DestructionCause,
    string LootPolicy,
    string QuestImpact,
    string RollbackLimit,
    bool DestructionConfirmed,
    string HealingScope,
    string HealingReason,
    string AiState,
    string MovementPolicy,
    string CollisionPolicy,
    string VisibilityPolicy,
    int PossessionMinutes,
    string InputContext,
    string ReturnPoint,
    bool ProtectedPlayerConsent)
{
    internal static StudioUx9GProbeOptions CreateDefault() => new(
        "party-consenting", true, "Décision du maître de jeu", "preserve",
        "previewed", "session-only", true, "all", "Réinitialisation de rencontre",
        "suspended", "blocked", "preserved", "hidden", 5, "director",
        "Entrée de la crypte", true);
}

internal sealed record StudioUx9GLiveActionProbe(
    string Status,
    string RelocationCode,
    bool OrientationPreserved,
    string RelocationCompensationCode,
    string DestructionCode,
    bool DestructionRestored,
    string HealingCode,
    bool ResourcesRestored,
    string LimboCode,
    bool IdentityPreserved,
    string PossessionCode,
    bool PossessionExpired,
    bool ReturnPointRestored,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9GLiveActionPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RelocationRuntime,
    bool DestructionRuntime,
    bool HealingRuntime,
    bool LimboRuntime,
    bool PossessionRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9GLiveActionProbe Probe);

internal sealed record StudioUx9GActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9GApprovalSnapshot(
    StudioUx9GLiveActionDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9GSmokeResult(
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
    bool RelocationRuntime,
    bool DestructionRuntime,
    bool HealingRuntime,
    bool LimboRuntime,
    bool PossessionRuntime,
    bool Compensation,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
