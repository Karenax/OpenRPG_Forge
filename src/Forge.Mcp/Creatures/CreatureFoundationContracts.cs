using Forge.Application.Commands;
using Forge.Domain.Creatures;

namespace Forge.Mcp.Creatures;

public sealed record CreatureFoundationResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<CreatureFoundationResourceItem> Items);

public sealed record CreatureFoundationResourceItem(
    string BlueprintId,
    string BlueprintSlug,
    string DisplayName,
    string ProfileId,
    int ProfileSchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string MovementMode,
    IReadOnlyDictionary<string, double> ResourceState,
    string SpawnEvent,
    string SpawnHandler);

public sealed record CreatureFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    CreatureFoundationProfile Profile);

public sealed record CreatureFoundationPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
