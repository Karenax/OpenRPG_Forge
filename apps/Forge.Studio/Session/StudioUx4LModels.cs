using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureVoiceChoiceUx4L(BlueprintId BlueprintId, string Label, bool IsReadOnly);

internal sealed record StudioCreatureVoiceDraftUx4L(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreaturePresentationProfile SourcePresentation,
    bool Enabled,
    string ProfileId,
    string Language,
    string AudioBankIds,
    string EmotionIds,
    string LipSyncProfileId,
    string VariantIds,
    string LicenseSpdx,
    string RightsProvenance);

internal sealed record StudioCreatureVoiceDiagnosticUx4L(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureVoicePreviewUx4L(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureVoiceDiagnosticUx4L> Diagnostics,
    string VoiceSummary,
    string GovernanceSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureVoiceActionResultUx4L(bool Succeeded, string Message);

internal sealed record StudioCreatureVoiceApprovalSnapshotUx4L(
    StudioCreatureVoiceDraftUx4L? Draft,
    CreaturePresentationProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureVoiceEvidenceProjectUx4L(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback,
    string PortraitAssetPath,
    byte[] PortraitBytes);

internal sealed record StudioCreatureVoiceEvidenceSourceSnapshotUx4L(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
    Forge.Domain.Entities.EntityId? SelectedEntityId,
    StudioCreatureVoiceApprovalSnapshotUx4L Approval);

internal sealed record StudioUx4LSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool BanksAndEmotions,
    bool LipSyncAndVariants,
    bool Governance,
    bool OptionalRemoval,
    bool InvalidConfigurationBlocked,
    bool DuplicateBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
