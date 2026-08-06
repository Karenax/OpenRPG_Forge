namespace Forge.Mcp.Creatures;

using Forge.Application.Commands;
using Forge.Domain.Creatures;

public sealed record CreatureEventResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<CreatureEventResourceItem> Items);

public sealed record CreatureEventResourceItem(
    string BlueprintId,
    string BlueprintSlug,
    string DisplayName,
    string ProfileId,
    int ProfileSchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string? CombatBoundary,
    int BindingCount,
    int HeartbeatIntervalMilliseconds,
    int HeartbeatBudgetMilliseconds,
    bool SuspendOutsideInterest,
    IReadOnlyList<string> CustomEvents);

public sealed record CreatureEventPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    CreatureEventProfile Profile);

public sealed record CreatureEventPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
