using Forge.Application.Commands;
using Forge.Domain.Assistant;
using Forge.RpgKit.Application.Dialogues;

namespace Forge.Mcp.Dialogues;

public sealed record AssistedDialogueProposalRequest(
    int SchemaVersion,
    AssistedDialogueBrief Brief);

public sealed record AssistedDialogueProposalResource(
    bool Succeeded,
    AssistedDialogueProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedDialogueApplyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedDialogueProposal Proposal,
    AssistantPermissionLevel Permission);

public sealed record AssistedDialogueApplyPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    string? DialogueId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    string SemanticDiff,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> LocalizationPlaceholders,
    IReadOnlyList<string> LintCodes,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
