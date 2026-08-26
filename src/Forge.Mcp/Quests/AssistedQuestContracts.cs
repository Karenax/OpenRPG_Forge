using System.ComponentModel;
using Forge.Application.Commands;
using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;
using Forge.RpgKit.Application.Quests;
using System.Text.Json;

namespace Forge.Mcp.Quests;

public sealed record AssistedQuestProposalRequest(int SchemaVersion, AssistedQuestBrief Brief);

public sealed record AssistedQuestProposalResource(
    bool Succeeded,
    AssistedQuestProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

[Description("Serializable MCP assisted-quest JSON boundary request schema v1; proposal and nested IDs are mapped and validated inside Forge.Mcp.")]
public sealed record AssistedQuestApplyPlanRequest(
    [property: Description("Assisted-quest MCP schema version; must be 1.")] int SchemaVersion,
    long BaseRevision,
    [property: Description("Canonical assisted quest proposal JSON resource schema v1.")] JsonElement Proposal,
    string Permission);

public sealed record AssistedQuestDomainApplyPlanRequest(
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
