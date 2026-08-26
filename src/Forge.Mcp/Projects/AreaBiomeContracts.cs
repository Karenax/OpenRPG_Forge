using Forge.Application.Commands;
using Forge.Domain.Areas;

namespace Forge.Mcp.Projects;

public sealed record AreaBiomeLayerResource(
    BiomeLayerDefinition Layer,
    BiomeScatterSettings Scatter);

public sealed record AreaBiomeResource(
    int SchemaVersion,
    long Revision,
    string AreaId,
    string? DisplayName,
    IReadOnlyList<AreaBiomeLayerResource> Biomes,
    IReadOnlyList<AgentDiagnostic> Diagnostics);

public sealed record AreaBiomePlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string AreaId,
    IReadOnlyList<BiomeScatterPatch> Patches);

public sealed record AreaBiomePlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<AreaBiomeAuditEntry> Audit);

public sealed record AreaBiomeAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
