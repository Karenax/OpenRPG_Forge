using Forge.Application.Commands;
using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;
using Forge.RpgKit.Application.Bosses;

namespace Forge.Mcp.Bosses;

public sealed record AssistedBossProposalRequest(int SchemaVersion, AssistedBossBrief Brief);

public sealed record AssistedBossProposalResource(
    bool Succeeded,
    AssistedBossProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedBossApplyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedBossProposal Proposal,
    AssistantPermissionLevel Permission);

public sealed record AssistedBossApplyPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    string? BossId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    string SemanticDiff,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> DiagnosticCodes,
    int BalanceRuns,
    bool BalancePassed,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
