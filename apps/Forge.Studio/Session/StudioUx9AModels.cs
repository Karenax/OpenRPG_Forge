using Forge.Network.Authoring;

namespace Forge.Studio.Session;

internal sealed record StudioUx9AFoundationDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    string ServerName,
    int Port,
    int ServerCapacity,
    int PartyCapacity,
    PartyLootPolicy Loot,
    PartySeparationPolicy Separation,
    int MaximumDeltaBytes,
    float MaximumDistance,
    bool InterestByArea,
    bool InterestByDistance,
    bool InterestByVisibility,
    bool InterestByParty,
    bool InterestByOwnership,
    bool ExplicitSubscriptions);

internal sealed record StudioUx9ALiveSnapshot(
    bool Connected,
    string Status,
    int ConnectedPlayers,
    int PartyMembers,
    int ReplicatedEntities,
    bool DeltaCompressed,
    string AuthorityDecision,
    int AuditEntries);

internal sealed record StudioUx9AFoundationPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool AuthoritativeRuntime,
    bool PartyRuntime,
    bool ReplicationRuntime,
    bool InterestRuntime,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9ALiveSnapshot Probe);

internal sealed record StudioUx9AActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9AApprovalSnapshot(
    StudioUx9AFoundationDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9ASmokeResult(
    bool Succeeded,
    bool FourRoutes,
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
    bool AuthoritativeRuntime,
    bool PartyRuntime,
    bool ReplicationRuntime,
    bool InterestRuntime,
    bool Disconnect,
    bool Audit,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
