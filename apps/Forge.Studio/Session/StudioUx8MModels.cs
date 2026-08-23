using Forge.Network.Authoring;

namespace Forge.Studio.Session;

internal sealed record StudioUx8MNetworkDraft(
    long BaseServerRevision,
    string SourceProfileFingerprint,
    bool DirectAddress,
    bool LanDiscovery,
    bool CuratedDirectory,
    bool Favorites,
    bool RecentServers,
    bool InviteLinks,
    string LobbyRulesetId,
    string ContentFingerprint,
    int MaximumSlots,
    int MaximumTeams,
    bool CharacterSelection,
    bool PermissionsVisible,
    bool ReadyState,
    string BuilderRulesetId,
    IReadOnlyList<string> TemplateIds,
    bool PreviewEnabled,
    bool ImportAllowed,
    bool ServerApprovalRequired,
    int MinimumAttribute,
    int MaximumAttribute,
    IReadOnlyList<CharacterAuthorityMode> AllowedModes,
    bool SignedUploadsRequired,
    bool EncryptedAtRest,
    bool MigrationEnabled,
    bool AccountBindingRequired);

internal sealed record StudioUx8MNetworkPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RuntimeResolved,
    bool ScriptProjectionReadable,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx8MActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8MApprovalSnapshot(
    StudioUx8MNetworkDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx8MSmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool ExplicitSelection,
    bool InvalidBlocked,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool RuntimeAndScriptProjection,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool CombatPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
