using Forge.Domain.Entities;
using Forge.Domain.Validation;

namespace Forge.Domain.Navigation;

public sealed record AreaNavigationAnalysis(
    NavigationAgentProfile AgentProfile,
    NavigationDoorState DoorState,
    NavigationSurface Surface,
    int SpawnCount,
    IReadOnlyList<EntityId> ReachableEntityIds,
    IReadOnlyList<EntityId> UnreachableEntityIds,
    IReadOnlyList<DomainDiagnostic> Diagnostics);
