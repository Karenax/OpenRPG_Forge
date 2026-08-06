using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Scripts;

public sealed record AssistedForgeScriptProposalRequest(int SchemaVersion, AssistedForgeScriptBrief Brief);

public sealed record AssistedForgeScriptProposalResource(
    bool Succeeded,
    AssistedForgeScriptProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedForgeScriptApplyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedForgeScriptProposal Proposal,
    AssistantPermissionLevel Permission);

public sealed record AssistedForgeScriptApplyPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    string? ScriptId,
    string? RelativePath,
    long BaseRevision,
    AssistedForgeScriptFilePatch? Diff,
    string SemanticDiff,
    string ApiVersion,
    string EventBinding,
    IReadOnlyList<string> TestIds,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    decimal EstimatedCostUsd,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
