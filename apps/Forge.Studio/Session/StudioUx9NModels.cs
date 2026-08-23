using Forge.Application.Commands;
using Forge.Application.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx9NNetworkProductionDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string CuratedChannel,
    string CuratedCompatibility,
    string AllowedLicensesText,
    bool RequireTrustedSignature,
    bool AllowOfflineFallback,
    bool VoiceEnabled,
    string VoiceCodec,
    int VoiceBitrateKbps,
    bool VoicePositional,
    bool VoicePushToTalk,
    bool VoiceModeration,
    bool HasShard,
    string ShardKey,
    Guid ShardAreaId,
    int ShardMaximumPlayers,
    string ShardRegion,
    bool HotReloadEnabled,
    int MaximumPackageMegabytes,
    int DrainTimeoutSeconds,
    bool ExtensionsEnabled,
    string AllowedCapabilitiesText,
    int ExtensionCpuBudgetMilliseconds,
    string ProtocolFamily,
    int ProtocolMinimumVersion,
    int ProtocolMaximumVersion,
    string ProtocolPlatformsText,
    bool RejectUnknownFields);

internal sealed record StudioUx9NAreaChoice(Guid Id, string Label);

internal sealed record StudioUx9NNetworkProductionProbe(
    string Status,
    string ContentCode,
    bool CuratedContent,
    bool UntrustedContentBlocked,
    bool Voice,
    int VoiceBitrateKbps,
    string ReloadCode,
    bool Shard,
    bool HotReload,
    bool Rollback,
    string ProtocolCode,
    bool Protocol,
    bool Extensions,
    bool UnsignedExtensionBlocked,
    bool ResourcesReleased);

internal sealed record StudioUx9NNetworkProductionPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RuntimeReady,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9NNetworkProductionProbe Probe);

internal sealed record StudioUx9NActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9NApprovalSnapshot(
    StudioUx9NNetworkProductionDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx9NSmokeResult(
    bool Succeeded,
    bool SixRoutes,
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
    bool CuratedContent,
    bool Voice,
    bool Shard,
    bool HotReload,
    bool Extensions,
    bool Protocol,
    bool ProjectNeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioUx9NSourceSnapshot(
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
    StudioUx9NApprovalSnapshot Approval);
