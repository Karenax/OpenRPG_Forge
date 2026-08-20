using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;

namespace Forge.Studio.Session;

internal sealed record StudioAreaVoiceAreaChoiceUx4Q(AreaId AreaId, string Label);
internal sealed record StudioAreaVoiceCreatureChoiceUx4Q(BlueprintId BlueprintId, string Label);

internal sealed record StudioLocalizedVoiceTakeUx4Q(
    string Locale,
    string Resource,
    string Subtitle,
    string PhonemeResource,
    string LicenseSpdx,
    string ContentSha256);

internal sealed record StudioAreaVoiceDraftUx4Q(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    AreaId AreaId,
    BlueprintId CreatureBlueprintId,
    AreaExperienceProfile SourceExperience,
    Guid VoiceId,
    string DefaultLocale,
    IReadOnlyList<StudioLocalizedVoiceTakeUx4Q> Takes,
    string TtsPluginId,
    bool SyntheticVoice,
    bool TtsUseAllowed,
    string ConsentReference,
    string SpeakerAttribution,
    string Provenance);

internal sealed record StudioAreaVoiceDiagnosticUx4Q(string Code, string Path, string HumanMessage);

internal sealed record StudioAreaVoicePreviewUx4Q(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioAreaVoiceDiagnosticUx4Q> Diagnostics,
    string VoiceSummary,
    string RuntimeSummary);

internal sealed record StudioAreaVoiceActionResultUx4Q(bool Succeeded, string Message);

internal sealed record StudioAreaVoiceApprovalSnapshotUx4Q(
    StudioAreaVoiceDraftUx4Q? Draft,
    AreaExperienceProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioAreaVoiceEvidenceProjectUx4Q(
    ProjectDefinition Project,
    AreaId AreaId,
    BlueprintId BlueprintId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioAreaVoiceEvidenceSourceSnapshotUx4Q(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
    Forge.Domain.Entities.EntityId? ActiveEntity,
    StudioAreaVoiceApprovalSnapshotUx4Q Approval);

internal sealed record StudioUx4QSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool LocalizedTakes,
    bool SubtitlePhoneme,
    bool TtsFallback,
    bool ConsentGovernance,
    bool InvalidConsentBlocked,
    bool DuplicateLocaleBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
