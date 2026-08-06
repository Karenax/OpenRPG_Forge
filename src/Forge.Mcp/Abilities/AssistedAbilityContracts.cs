using Forge.Application.Commands;
using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;
using Forge.RpgKit.Application.Abilities;

namespace Forge.Mcp.Abilities;

public sealed record AssistedAbilityProposalRequest(int SchemaVersion, AssistedAbilityBrief Brief);

public sealed record AssistedAbilityProposalResource(
    bool Succeeded,
    AssistedAbilityProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedAbilityApplyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedAbilityProposal Proposal,
    AssistantPermissionLevel Permission);

public sealed record AssistedAbilityApplyPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    string? AbilityId,
    string? OwnerBlueprintId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    string SemanticDiff,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> DiagnosticCodes,
    IReadOnlyList<string> TestKinds,
    int PassedTests,
    bool BalancePassed,
    double PowerScore,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
