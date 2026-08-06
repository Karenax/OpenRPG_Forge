using Forge.Application.Commands;
using Forge.Domain.Assistant;

namespace Forge.Mcp.Projects;

public sealed record LoreIndexResource(
    int SchemaVersion,
    long Revision,
    LoreIndexDefinition Index,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record LoreContextResource(
    int SchemaVersion,
    long Revision,
    string Query,
    string Content,
    string ContentSha256,
    IReadOnlyList<string> SourceIds,
    int EntryCount,
    bool Truncated,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record LoreIndexPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    LoreIndexDefinition Index,
    AssistantPermissionLevel Permission);

public sealed record LoreIndexPlanResult(
    bool Succeeded,
    string? PlanId,
    string? TransactionId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<LoreIndexAuditEntry> Audit);

public sealed record LoreIndexAuditEntry(
    string PlanId,
    string TransactionId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
