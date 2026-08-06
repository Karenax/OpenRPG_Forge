using Forge.Domain.Assistant;
using Forge.Mcp.Dialogues;
using Forge.ModuleFormat.Assets;

namespace Forge.Mcp.Assets;

public sealed record AssistedModel3DGenerationProposalRequest(
    int SchemaVersion,
    AssistedModel3DGenerationBrief Brief,
    bool NetworkAuthorized = false);

public sealed record AssistedModel3DGenerationProposalResource(
    bool Succeeded,
    long BaseRevision,
    AssistedModel3DGenerationProposal? Proposal,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AssistedModel3DGenerationApplyPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    AssistedModel3DGenerationProposal Proposal,
    AssistantPermissionLevel Permission);

public sealed record AssistedModel3DGenerationApplyPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    string? GenerationId,
    string? AssetId,
    long BaseRevision,
    IReadOnlyList<AssistedModel3DFilePatch> FileDiff,
    string SemanticDiff,
    string? SourceRelativePath,
    string? ReceiptRelativePath,
    string? OutputRelativePath,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> DiagnosticCodes,
    IReadOnlyList<string> TestIds,
    int PassedTests,
    decimal EstimatedCostUsd,
    string? ProviderKey,
    string? LicenseSpdx,
    string? Provenance,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<PlanAuditEntry> Audit);
