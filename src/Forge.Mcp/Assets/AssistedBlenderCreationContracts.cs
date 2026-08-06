using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;
using Forge.ModuleFormat.Assets;

namespace Forge.Mcp.Assets;

public sealed record AssistedBlenderCreationProposalRequest(
    int SchemaVersion,
    AssistedBlenderCreationBrief Brief);

public sealed record AssistedBlenderCreationProposalResource(
    bool Succeeded,
    long BaseRevision,
    AssistedBlenderCreationProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedBlenderCreationApplyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedBlenderCreationProposal Proposal,
    AssistantPermissionLevel Permission);

public sealed record AssistedBlenderCreationApplyPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    string? CreationId,
    string? AssetId,
    long BaseRevision,
    IReadOnlyList<AssistedBlenderFilePatch> FileDiff,
    string SemanticDiff,
    string? RecipeRelativePath,
    string? OutputRelativePath,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> DiagnosticCodes,
    IReadOnlyList<string> TestIds,
    int PassedTests,
    decimal EstimatedCostUsd,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
