using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Navigation;

public static class NavigationTopologyValidator
{
    public const string DestinationMissingCode = "navigation.destination.missing";
    public const string ArrivalAnchorMissingCode = "navigation.arrival_anchor.missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var result = new List<DomainDiagnostic>();
        var catalog = NavigationDestinationCatalog.Build(project);

        foreach (var door in project.Module.WorldFeatures.Doors)
        {
            if (door.Foundation?.Portal is not { } portal)
            {
                continue;
            }

            var root = $"module/worldFeatures/doors/{door.Id}/foundation/portal";
            ValidateDestination(catalog, Map(portal.DestinationKind), portal.DestinationId, root, result);
            ValidateArrivalAnchor(catalog, portal.SpawnAnchorId, root, result);
        }

        foreach (var trigger in project.Module.WorldFeatures.Triggers)
        {
            if (trigger.Foundation?.Portal is not { } portal)
            {
                continue;
            }

            var root = $"module/worldFeatures/triggers/{trigger.Id}/foundation/portal";
            ValidateDestination(catalog, Map(portal.Destination), portal.DestinationId, root, result);
            ValidateArrivalAnchor(catalog, portal.SpawnAnchorId, root, result);
        }

        return result;
    }

    private static void ValidateDestination(NavigationDestinationCatalog catalog, NavigationDestinationKind? kind,
        Guid id, string root, ICollection<DomainDiagnostic> result)
    {
        if (kind is null || catalog.Find(kind.Value, id) is null)
        {
            result.Add(new(DestinationMissingCode,
                "The typed destination does not resolve in the project navigation catalog.",
                $"{root}/destinationId"));
        }
    }

    private static void ValidateArrivalAnchor(NavigationDestinationCatalog catalog, string key, string root,
        ICollection<DomainDiagnostic> result)
    {
        if (catalog.FindArrivalAnchor(key) is null)
        {
            result.Add(new(ArrivalAnchorMissingCode,
                "The arrival anchor key does not resolve to a canonical spawn-anchor definition slug.",
                $"{root}/spawnAnchorId"));
        }
    }

    private static NavigationDestinationKind? Map(DoorWorldProfile.DestinationKind kind) => kind switch
    {
        DoorWorldProfile.DestinationKind.Door => NavigationDestinationKind.Door,
        DoorWorldProfile.DestinationKind.Trigger => NavigationDestinationKind.Trigger,
        DoorWorldProfile.DestinationKind.Waypoint => NavigationDestinationKind.Waypoint,
        DoorWorldProfile.DestinationKind.SpawnAnchor => NavigationDestinationKind.SpawnAnchor,
        _ => null,
    };

    private static NavigationDestinationKind? Map(TriggerWorldProfile.DestinationKind kind) => kind switch
    {
        TriggerWorldProfile.DestinationKind.Door => NavigationDestinationKind.Door,
        TriggerWorldProfile.DestinationKind.Trigger => NavigationDestinationKind.Trigger,
        TriggerWorldProfile.DestinationKind.Waypoint => NavigationDestinationKind.Waypoint,
        TriggerWorldProfile.DestinationKind.SpawnAnchor => NavigationDestinationKind.SpawnAnchor,
        _ => null,
    };
}
