using System.ComponentModel;
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
    AgentSpatialContextResource? Spatial,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentSpatialContextQuery(
    [property: Description("Spatial context schema version; must be 1.")] int SchemaVersion = 1,
    [property: Description("Maximum selected entities; must be between 1 and 64.")] int MaximumEntities = 32,
    [property: Description("Maximum typed references; must be between 0 and 256.")] int MaximumReferences = 64,
    [property: Description("Explicit canonical area UUID selection; at least one area or entity is required.")] IReadOnlyList<string>? AreaIds = null,
    [property: Description("Explicit canonical entity UUID selection; at least one area or entity is required.")] IReadOnlyList<string>? EntityIds = null,
    [property: Description("Requested read permission, bounded by the workspace maximum permission.")] string Permission = "ReadOnly");

public sealed record AgentSpatialContextResource(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    long Revision,
    string Permission,
    int MaximumEntities,
    int MaximumReferences,
    IReadOnlyList<string> AreaIds,
    IReadOnlyList<string> EntityIds,
    IReadOnlyList<AgentSpatialEntityResource> Items,
    IReadOnlyList<AgentSpatialReferenceResource> References,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AgentSpatialEntityResource(
    string AreaId,
    string AreaDisplayName,
    string EntityId,
    string DisplayName,
    string BlueprintId,
    string? AssetId,
    IReadOnlyList<string> Tags,
    AgentSpatialTransformResource Transform,
    string DocumentPath);

public sealed record AgentSpatialTransformResource(
    AgentSpatialVector3Resource Position,
    AgentSpatialQuaternionResource Rotation,
    AgentSpatialVector3Resource Scale);

public sealed record AgentSpatialVector3Resource(float X, float Y, float Z);

public sealed record AgentSpatialQuaternionResource(float X, float Y, float Z, float W);

public sealed record AgentSpatialReferenceResource(
    string ReferenceId,
    string SourceId,
    string TargetId,
    string ReferenceType,
    string DocumentPath);

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
