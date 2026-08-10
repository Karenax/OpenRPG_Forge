using Forge.Domain.Assistant;

namespace Forge.Mcp.Assistant;

public sealed record AgentContextQuery(
    int MaximumItems = 200,
    IReadOnlyList<string>? SelectionObjectIds = null);

public sealed record AgentContextItem(
    Guid Id,
    string Kind,
    string DisplayName,
    string DocumentPath,
    string Provenance);

public sealed record AgentContextSection(
    AgentContextResourceKind Kind,
    int Total,
    IReadOnlyList<AgentContextItem> Items);

public sealed record AgentStructuredContextResource(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    long Revision,
    AssistantPermissionLevel Permission,
    int Total,
    IReadOnlyList<AgentContextSection> Sections,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentReferenceNode(
    Guid Id,
    string Kind,
    string DisplayName,
    string DocumentPath);

public sealed record AgentReferenceEdge(
    Guid Id,
    Guid SourceId,
    Guid TargetId,
    string ReferenceType,
    string DocumentPath);

public sealed record AgentReferenceGraphResource(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    long Revision,
    Guid? ImpactTargetId,
    IReadOnlyList<AgentReferenceNode> Nodes,
    IReadOnlyList<AgentReferenceEdge> Edges,
    IReadOnlyList<Guid> ImpactedObjectIds,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentSemanticToolCatalogResource(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AssistantPermissionLevel MaximumPermission,
    IReadOnlyList<AgentSemanticToolDefinition> Tools,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentChangePlanPreviewRequest(
    int SchemaVersion,
    StructuredChangePlan Plan);

public sealed record AgentChangePlanPreviewResult(
    bool Succeeded,
    Guid PlanId,
    Guid TransactionId,
    long BaseRevision,
    long CurrentRevision,
    IReadOnlyList<string> SemanticDiff,
    string ProjectFingerprintBefore,
    string ProjectFingerprintAfter,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool DryRun);

public sealed record AgentChangeTransactionBeginRequest(
    int SchemaVersion,
    StructuredChangePlan Plan,
    string ClientId,
    string UserId,
    string? Provider,
    string? Model,
    bool ProjectContentOptIn,
    bool HumanConfirmed,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost,
    int DurationSeconds);

public sealed record AgentChangeTransactionResult(
    bool Succeeded,
    Guid TransactionId,
    Guid PlanId,
    Guid RollbackId,
    AgentTransactionStage Stage,
    long BaseRevision,
    long CurrentRevision,
    AgentSemanticDiff? SemanticDiff,
    IReadOnlyList<AgentAiAuditEntry> Audit,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentAiAuditResource(
    int SchemaVersion,
    IReadOnlyList<AgentAiAuditEntry> Entries,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentStructuredResultValidationRequest(
    int SchemaVersion,
    string PromptKey,
    Guid PlanId,
    Guid TransactionId,
    AssistantPermissionLevel Permission,
    string ProviderKey,
    string Model,
    string Content,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost,
    string Provenance);

public sealed record AgentStructuredToolCallResource(
    string ToolName,
    Guid TargetObjectId,
    IReadOnlyDictionary<string, string> Arguments);

public sealed record AgentStructuredResultValidationResource(
    bool Succeeded,
    bool Repaired,
    string NormalizedJson,
    IReadOnlyList<AgentStructuredToolCallResource> ToolCalls,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentOfflineReadinessResource(
    bool Ready,
    bool ProfileWasMissing,
    bool ManualAuthoringAvailable,
    bool NetworkRequired,
    bool AiMetadataRequiredByBuild,
    bool AiMetadataRequiredByRuntime,
    string ActiveProvider,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
