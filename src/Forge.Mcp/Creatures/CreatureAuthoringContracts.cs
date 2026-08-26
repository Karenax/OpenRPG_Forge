using Forge.Application.Commands;
using Forge.Domain.Creatures;

namespace Forge.Mcp.Creatures;

public enum CreatureAuthoringFacet
{
    Foundation,
    Presentation,
    Progression,
    RoleBehavior,
}

public sealed record CreatureAuthoringPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    CreatureAuthoringFacet Facet,
    CreatureFoundationProfile? Foundation = null,
    CreaturePresentationProfile? Presentation = null,
    CreatureProgressionProfile? Progression = null,
    CreatureRoleBehaviorProfile? RoleBehavior = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record CreatureAuthoringAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded,
    string BlueprintId,
    CreatureAuthoringFacet Facet);

public sealed record CreatureAuthoringPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    string? BlueprintId,
    CreatureAuthoringFacet? Facet,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<CreatureAuthoringAuditEntry> Audit);
