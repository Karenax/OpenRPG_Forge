using Forge.Application.Commands;

namespace Forge.Mcp.Dialogues;

public sealed record DialogueVoiceImportPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string DialogueId,
    string NodeId,
    string ParticipantBlueprintId,
    string SourceRoot,
    string SourceRelativePath,
    string ExpectedSha256,
    string MimeType,
    string Locale,
    string Take,
    string SubtitleKey,
    string Subtitle,
    string LipSyncProfileId,
    string Emotion,
    int DurationMilliseconds,
    string LicenseSpdx,
    bool AllowSubtitleFallback,
    string Provenance);

public sealed record DialogueVoiceImportAuditEntry(
    string Event,
    long Revision,
    DateTimeOffset TimestampUtc,
    bool Succeeded);

public sealed record DialogueVoiceImportPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    string? DestinationRelativePath,
    string? ContentSha256,
    long SourceBytes,
    string? MimeType,
    bool Reused,
    bool Applied,
    bool Reverted,
    bool Idempotent,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    IReadOnlyList<DialogueVoiceImportAuditEntry> Audit);
