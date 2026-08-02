using Forge.Application.Commands;

namespace Forge.Mcp.Dialogues;

public sealed record DialoguePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string DialogueId,
    string Slug,
    string DisplayName,
    string EntryNodeId,
    IReadOnlyList<DialogueNodeInput> Nodes);

public sealed record DialogueNodeInput(
    string NodeId,
    string? SpeakerBlueprintId,
    string LocalizationKey,
    string FallbackText,
    IReadOnlyList<DialogueActionInput>? OnEnterActions = null,
    IReadOnlyList<DialogueChoiceInput>? Choices = null);

public sealed record DialogueChoiceInput(
    string ChoiceId,
    string LocalizationKey,
    string FallbackText,
    string? TargetNodeId = null,
    IReadOnlyList<DialogueConditionInput>? Conditions = null,
    IReadOnlyList<DialogueActionInput>? Actions = null);

public sealed record DialogueConditionInput(string Kind, string Key, string ExpectedValue);

public sealed record DialogueActionInput(string Kind, string Key, string Value);

public sealed record DialoguePlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);

public sealed record PlanAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset Timestamp,
    bool Succeeded);
