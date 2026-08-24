using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Quality;
using Forge.Network.Authoring;
using Forge.Network.Observability;
using Forge.Network.World;

namespace Forge.Studio.Session;

internal sealed record StudioUx9ONetworkClosureDraft(
    long FoundationRevision,
    string FoundationFingerprint,
    long PlayerRevision,
    string PlayerFingerprint,
    long AssuranceRevision,
    string AssuranceFingerprint,
    string ServerName,
    string ServerHost,
    int ServerPort,
    int ServerCapacity,
    bool DedicatedRequired,
    DedicatedServiceMode ServiceMode,
    string HealthCheckPath,
    bool PvpEnabled,
    FriendlyFirePolicy FriendlyFire,
    bool GlobalCombatPolicy,
    bool AreaOverrides,
    bool PartyOverrides,
    bool ConsentRequired,
    bool DuelsEnabled,
    bool CrimeEnabled,
    bool SafeZonesEnabled,
    bool ServerOverride,
    StructuredLogSeverity MinimumSeverity,
    string LogCategoriesText,
    PlayerPrivacyMode PlayerPrivacy,
    StructuredLogOutputFormat LogOutputFormat,
    int LogRetentionDays,
    int LogMaximumEntries,
    int LogRotationBytes,
    int LogRotationFiles,
    string RemoteSinkName,
    RemoteLogSinkKind RemoteSinkKind,
    string RemoteSinkEndpoint,
    bool RemoteSinkEnabled,
    bool RemoteSinkRequired,
    float MaximumUnitsPerSecond,
    int MaximumIntentsPerSecond,
    int AnomalyThreshold,
    bool RequireSignedContent,
    string TrustedSignerFingerprintsText,
    bool AuthoritativeRandom,
    bool TrustClientSaves,
    int RandomSeed);

internal sealed record StudioUx9ONetworkClosureProbe(
    string Status,
    string DedicatedStatus,
    bool Dedicated,
    string CombatStatus,
    bool Combat,
    bool FriendlyFireRespected,
    string LogStatus,
    bool StructuredLogs,
    bool Privacy,
    int RemoteDeliveries,
    string AntiCheatStatus,
    bool AntiCheat,
    bool ForgedIntentBlocked,
    bool ClientSaveBlocked,
    bool ResourcesReleased);

internal sealed record StudioUx9ONetworkClosurePreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RuntimeReady,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9ONetworkClosureProbe Probe);

internal sealed record StudioUx9OActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9OApprovalSnapshot(
    StudioUx9ONetworkClosureDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx9OSmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool ExplicitSelection,
    bool InvalidBlocked,
    bool PermissionBlocked,
    bool ConfirmationRequired,
    bool CanonicalDryRun,
    bool CanonicalCommands,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool Dedicated,
    bool Combat,
    bool StructuredLogs,
    bool AntiCheat,
    bool NeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioUx9OSourceSnapshot(
    Forge.Domain.Projects.ProjectDefinition? Project,
    ProjectCommandHistory? History,
    ProjectRevision? Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? Area,
    IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
    Forge.Domain.Entities.EntityId? Selected,
    StudioUx9AApprovalSnapshot FoundationApproval,
    StudioUx8MApprovalSnapshot PlayerApproval,
    StudioUx9OApprovalSnapshot ClosureApproval);
