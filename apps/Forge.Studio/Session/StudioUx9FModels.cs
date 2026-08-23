namespace Forge.Studio.Session;

internal sealed record StudioUx9FLiveDirectorDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    int SessionMinutes,
    int MaximumInstances,
    bool RequiredAtRuntime);

internal sealed record StudioUx9FProbeOptions(
    string CatalogText,
    string CatalogPackage,
    string CatalogType,
    int MaximumCost,
    int SpawnCount,
    string Placement,
    string InstanceId,
    double X,
    double Y,
    double Z,
    double Spacing,
    string ChooserText,
    bool AllowPrivacyOverride)
{
    internal static StudioUx9FProbeOptions CreateDefault() => new(
        "gardien", "crypt", "creature", 20, 4, "formation", "crypt-01",
        10, 2, 20, 2, "Place", true);
}

internal sealed record StudioUx9FLiveDirectorProbe(
    string Status,
    string AuthenticationCode,
    bool SecondFactorEnforced,
    bool SessionActive,
    bool ClientConnected,
    int CatalogMatches,
    bool ThumbnailAvailable,
    string SpawnPreviewCode,
    string SpawnApplyCode,
    bool StableObjectIds,
    int DistinctCoordinates,
    string CompensationCode,
    int PublicChooserMatches,
    bool PrivateHidden,
    bool PrivateOverrideAllowed,
    string FocusCode,
    bool FocusReadOnly,
    int AuditEntries,
    bool ResourcesReleased);

internal sealed record StudioUx9FLiveDirectorPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ClientRuntime,
    bool CreatorRuntime,
    bool SpawnRuntime,
    bool ChooserRuntime,
    bool FocusRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9FLiveDirectorProbe Probe);

internal sealed record StudioUx9FActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9FApprovalSnapshot(
    StudioUx9FLiveDirectorDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9FSmokeResult(
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
    bool ClientRuntime,
    bool CreatorRuntime,
    bool SpawnRuntime,
    bool ChooserRuntime,
    bool FocusRuntime,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
