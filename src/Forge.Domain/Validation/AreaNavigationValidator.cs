using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;

namespace Forge.Domain.Validation;

public static class AreaNavigationValidator
{
    public const string MissingSpawnCode = "area.navigation.spawn_missing";
    public const string UnreachableEntityCode = "area.navigation.entity_unreachable";
    public const string LinkOutOfBoundsCode = "area.navigation.link_out_of_bounds";
    public const string LinkEndpointBlockedCode = "area.navigation.link_endpoint_blocked";
    public const string FlightLinkUnavailableCode = "area.navigation.flight_link_unavailable";

    public static IReadOnlyList<AreaNavigationAnalysis> AnalyzeAll(
        ProjectDefinition project,
        AreaDefinition area,
        NavigationDoorState doorState = NavigationDoorState.Current) =>
        area.Navigation.AgentProfiles
            .Select(agent => Analyze(project, area, agent, doorState))
            .ToArray();

    public static AreaNavigationAnalysis Analyze(
        ProjectDefinition project,
        AreaDefinition area,
        NavigationAgentProfile agent,
        NavigationDoorState doorState = NavigationDoorState.Current)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(agent);

        var diagnostics = new List<DomainDiagnostic>();
        var surface = NavigationSurfaceBuilder.Build(project, area, agent, doorState);
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var spawns = area.Entities.Where(entity =>
            blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
            blueprint.Kind == BlueprintKind.PlayerSpawn).ToArray();
        if (spawns.Length == 0)
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingSpawnCode,
                $"Area '{area.DisplayName}' has no player spawn for agent '{agent.Name}'.",
                $"areas/{area.Id}/entities"));
        }

        ValidateLinks(area, agent, surface, diagnostics);
        var reachableCells = NavigationPathfinder.ReachableFrom(
            surface,
            spawns.Select(spawn => surface.CellAt(spawn.Transform.Position)));
        var reachable = new List<EntityId>();
        var unreachable = new List<EntityId>();
        foreach (var entity in area.Entities.Where(entity => IsReachabilityTarget(entity, blueprints)))
        {
            if (CanReachEntity(surface, reachableCells, entity))
            {
                reachable.Add(entity.Id);
                continue;
            }

            unreachable.Add(entity.Id);
            diagnostics.Add(new DomainDiagnostic(
                UnreachableEntityCode,
                $"Entity '{entity.Id}' is unreachable from every player spawn for agent '{agent.Name}' ({doorState}).",
                $"areas/{area.Id}/entities/{entity.Id}/transform"));
        }

        return new AreaNavigationAnalysis(
            agent,
            doorState,
            surface,
            spawns.Length,
            reachable,
            unreachable,
            diagnostics);
    }

    private static void ValidateLinks(
        AreaDefinition area,
        NavigationAgentProfile agent,
        NavigationSurface surface,
        ICollection<DomainDiagnostic> diagnostics)
    {
        foreach (var link in area.Navigation.OffMeshLinks)
        {
            var start = surface.CellAt(link.Start);
            var end = surface.CellAt(link.End);
            if (start.X < 0 || start.X >= surface.WidthCells || start.Z < 0 || start.Z >= surface.DepthCells ||
                end.X < 0 || end.X >= surface.WidthCells || end.Z < 0 || end.Z >= surface.DepthCells)
            {
                diagnostics.Add(new DomainDiagnostic(
                    LinkOutOfBoundsCode,
                    $"Navigation link '{link.Name}' has an endpoint outside area '{area.DisplayName}'.",
                    $"areas/{area.Id}/navigation/offMeshLinks/{link.Id}"));
                continue;
            }

            if (agent.CanUseOffMeshLinks && agent.Radius <= link.MaximumAgentRadius &&
                (!surface.IsWalkable(start) || !surface.IsWalkable(end)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    LinkEndpointBlockedCode,
                    $"Navigation link '{link.Name}' has a blocked endpoint for agent '{agent.Name}'.",
                    $"areas/{area.Id}/navigation/offMeshLinks/{link.Id}"));
            }

            if (link.Kind == NavigationLinkKind.Flight && !area.Navigation.AgentProfiles.Any(profile => profile.CanFly))
            {
                diagnostics.Add(new DomainDiagnostic(
                    FlightLinkUnavailableCode,
                    $"Flight link '{link.Name}' requires at least one flying agent profile.",
                    $"areas/{area.Id}/navigation/offMeshLinks/{link.Id}"));
            }
        }
    }

    private static bool IsReachabilityTarget(
        EntityInstance entity,
        IReadOnlyDictionary<BlueprintId, BlueprintDefinition> blueprints) =>
        blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
        (blueprint.Kind is BlueprintKind.Creature or BlueprintKind.Door or BlueprintKind.Trigger or BlueprintKind.Item ||
         entity.Tags.Contains("objective", StringComparer.OrdinalIgnoreCase) ||
         entity.Tags.Contains("exit", StringComparer.OrdinalIgnoreCase));

    private static bool CanReachEntity(
        NavigationSurface surface,
        IReadOnlySet<NavigationCell> reachable,
        EntityInstance entity)
    {
        var cell = surface.CellAt(entity.Transform.Position);
        if (reachable.Contains(cell))
        {
            return true;
        }

        var radius = Math.Max(1, (int)Math.Ceiling(
            Math.Max(entity.Transform.Scale.X, entity.Transform.Scale.Z) / (2f * surface.CellSize)) + 1);
        for (var z = -radius; z <= radius; z++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                if (reachable.Contains(new NavigationCell(cell.X + x, cell.Z + z)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
