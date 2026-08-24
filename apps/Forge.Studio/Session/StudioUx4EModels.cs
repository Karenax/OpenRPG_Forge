using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreaturePresentationChoiceUx4E(
    BlueprintId BlueprintId,
    string Label,
    bool HasPresentation,
    bool IsReadOnly);

internal sealed record StudioCreaturePresentationDraftUx4E(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreaturePresentationProfile? SourcePresentation,
    string GenderId,
    string BodyTypeId,
    string PronounSubject,
    string PronounObject,
    string PronounPossessive,
    string PronounReflexive,
    bool UseModelFallback,
    string RigId,
    IReadOnlyList<CreaturePresentationProfile.PortraitVariant> Portraits,
    IReadOnlyList<CreaturePresentationProfile.BodySlot> BodySlots,
    IReadOnlyList<CreaturePresentationProfile.MaterialColor> MaterialColors,
    IReadOnlyList<CreaturePresentationProfile.Attachment> Attachments);

internal sealed record StudioCreaturePresentationDiagnosticUx4E(
    string Code,
    string Path,
    string HumanMessage);

internal sealed record StudioCreaturePresentationPreviewUx4E(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreaturePresentationDiagnosticUx4E> Diagnostics,
    string IdentitySummary,
    string PortraitSummary,
    string AppearanceSummary,
    string RuntimeSummary);

internal sealed record StudioCreaturePresentationActionResultUx4E(bool Succeeded, string Message);

internal sealed record StudioCreaturePresentationApprovalSnapshotUx4E(
    StudioCreaturePresentationDraftUx4E? Draft,
    CreaturePresentationProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreaturePresentationEvidenceSourceSnapshotUx4E(
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
    StudioCreaturePresentationApprovalSnapshotUx4E Approval);

internal sealed record StudioCreaturePresentationEvidenceProjectUx4E(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealPresentation,
    bool UsesCanonicalFallback,
    string PortraitAssetPath,
    byte[] PortraitBytes);

internal sealed record StudioUx4ESmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool GenderAndPronouns,
    bool Portraits,
    bool AppearanceAndModel,
    bool BodyParts,
    bool ColorsWingsTails,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
