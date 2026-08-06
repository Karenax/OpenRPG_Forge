using Forge.Application.Commands;
using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;
using Forge.RpgKit.Application.Quests;

namespace Forge.Mcp.Quests;

public sealed record AssistedQuestProposalRequest(int SchemaVersion, AssistedQuestBrief Brief);

public sealed record AssistedQuestProposalResource(
    bool Succeeded,
    AssistedQuestProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedQuestApplyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedQuestProposal Proposal,
    AssistantPermissionLevel Permission);

public sealed record AssistedQuestApplyPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    string? QuestId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    string SemanticDiff,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> LintCodes,
    IReadOnlyList<string> TestScenarios,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
