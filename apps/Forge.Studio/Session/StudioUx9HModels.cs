namespace Forge.Studio.Session;

internal sealed record StudioUx9HAuthorityDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    int MaximumProtectionMinutes,
    long MaximumCurrencyAdjustment,
    long MaximumWallet,
    long MaximumExperienceAdjustment,
    bool RequiredAtRuntime);

internal sealed record StudioUx9HProbeOptions(
    string ImpersonationMode,
    string Disclosure,
    int ProtectionMinutes,
    string ProtectionScope,
    string ProtectionIndicator,
    long CurrencyAmount,
    string CurrencyReason,
    long ExperienceAmount,
    string ExperienceReason,
    string DoorState,
    string DoorPortal)
{
    internal static StudioUx9HProbeOptions CreateDefault() => new(
        "voice", "visible", 5, "all", "Bouclier ambre", 500,
        "Récompense de scène", 2500, "Jalon narratif", "relinked", "Sortie surface");
}

internal sealed record StudioUx9HAuthorityProbe(
    string Status,
    string ImpersonationCode,
    bool RenderAdapter,
    bool DisclosureVisible,
    string ProtectionCode,
    bool ProtectionExpired,
    bool DamageAdapter,
    string CurrencyCode,
    long ProjectedBalance,
    bool WalletRoundTrip,
    string CurrencyCompensationCode,
    string ExperienceCode,
    int ProjectedLevel,
    bool ProgressionRoundTrip,
    string ExperienceCompensationCode,
    string DoorCode,
    bool CollisionUpdated,
    bool PortalUpdated,
    string DoorCompensationCode,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9HAuthorityPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ImpersonationRuntime,
    bool ProtectionRuntime,
    bool CurrencyRuntime,
    bool ExperienceRuntime,
    bool DoorRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9HAuthorityProbe Probe);

internal sealed record StudioUx9HActionResult(bool Succeeded, string Message);
internal sealed record StudioUx9HApprovalSnapshot(StudioUx9HAuthorityDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9HSmokeResult(
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
    bool ImpersonationRuntime,
    bool ProtectionRuntime,
    bool CurrencyRuntime,
    bool ExperienceRuntime,
    bool DoorRuntime,
    bool Compensation,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
