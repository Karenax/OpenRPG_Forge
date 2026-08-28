using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;

namespace Forge.WorkspaceMigrator;

internal static class CryptNavigationMigration
{
    private const string EncounterAnchorSlug = "anchor:crypt-north";
    private const string Provenance = "module-author:crypt-of-ashes";

    internal static Guid EncounterPatrolRouteId(AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        return Stable(areaId.Value, "encounter-patrol-route");
    }

    public static AreaDefinition MaterializeEncounterAnchor(AreaDefinition area, BlueprintId playerSpawnBlueprintId)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(playerSpawnBlueprintId);

        var catalog = area.Navigation.Anchors;
        var definitionId = Stable(area.Id.Value, "encounter-anchor:crypt-north:definition");
        var instanceId = Stable(area.Id.Value, "encounter-anchor:crypt-north:instance");
        var replacedDefinitionIds = catalog.Definitions
            .Where(item => item.Id == definitionId ||
                           string.Equals(item.Slug, EncounterAnchorSlug, StringComparison.Ordinal))
            .Select(item => item.Id)
            .Append(definitionId)
            .ToHashSet();

        var definition = new AnchorDefinition(
            definitionId,
            AnchorDefinition.CurrentSchemaVersion,
            Provenance,
            EncounterAnchorSlug,
            new LocalizedText("anchor.crypt-north", "Point d'assaut nord", provenance: Provenance),
            AnchorKind.Spawn,
            AnchorScope.Area,
            ["encounter", "ash-hostile", "north"],
            playerSpawnBlueprintId,
            null,
            true);
        var instance = new AnchorInstance(
            instanceId,
            AnchorInstance.CurrentSchemaVersion,
            Provenance,
            definitionId,
            new TransformValue(new Vector3Value(0, 1, -27), QuaternionValue.Identity, Vector3Value.One),
            1,
            EntityPropertyOverrides.Empty,
            new SpawnAnchorProfile("encounter", new Vector3Value(1.2f, 2.2f, 1.2f), 80,
                "ash-hostile", 180, null, true),
            null);
        var definitions = catalog.Definitions
            .Where(item => !replacedDefinitionIds.Contains(item.Id) &&
                           !string.Equals(item.Slug, EncounterAnchorSlug, StringComparison.Ordinal))
            .Append(definition)
            .ToArray();
        var instances = catalog.Instances
            .Where(item => item.Id != instanceId && !replacedDefinitionIds.Contains(item.DefinitionId))
            .Append(instance)
            .ToArray();
        var authoredCatalog = new AnchorCatalog(
            catalog.Id,
            catalog.SchemaVersion,
            Provenance,
            area.Id,
            true,
            definitions,
            instances);

        var waypointProfile = area.Navigation.Waypoints;
        var patrolRouteId = EncounterPatrolRouteId(area.Id);
        var northWestPosition = new Vector3Value(-6, 1, -24);
        var northEastPosition = new Vector3Value(6, 1, -24);
        var northWestWaypointId = Stable(patrolRouteId, "north-west-waypoint");
        var northEastWaypointId = Stable(patrolRouteId, "north-east-waypoint");
        var encounterWaypoints = new[]
        {
            new WaypointProfile(northWestWaypointId, WaypointAuthoringProfile.CurrentSchemaVersion,
                Provenance, "Patrouille nord-ouest", northWestPosition,
                new("flag", "#ff8844", "encounter", true, 28), null, RequiredAtRuntime: true),
            new WaypointProfile(northEastWaypointId, WaypointAuthoringProfile.CurrentSchemaVersion,
                Provenance, "Patrouille nord-est", northEastPosition,
                new("flag", "#ff8844", "encounter", true, 28), null, RequiredAtRuntime: true),
        };
        var patrolRoute = new PatrolRoute(
            patrolRouteId,
            WaypointAuthoringProfile.CurrentSchemaVersion,
            Provenance,
            PatrolTraversalPolicy.PingPong,
            [
                new PatrolPoint(Stable(patrolRouteId, "north-west"), northWestPosition,
                    1.5, 90, 2.5, "inspect", null),
                new PatrolPoint(Stable(patrolRouteId, "north-east"), northEastPosition,
                    1.5, 270, 2.5, "guard", null),
            ],
            requiredAtRuntime: true);
        var patrolRoutes = waypointProfile.PatrolRoutes
            .Where(item => item.Id != patrolRouteId)
            .Append(patrolRoute)
            .ToArray();
        var waypointIds = encounterWaypoints.Select(item => item.Id).ToHashSet();
        var waypoints = waypointProfile.Waypoints
            .Where(item => !waypointIds.Contains(item.Id))
            .Concat(encounterWaypoints)
            .ToArray();
        var authoredWaypoints = new WaypointAuthoringProfile(
            waypointProfile.Id,
            waypointProfile.SchemaVersion,
            Provenance,
            area.Id,
            true,
            waypoints,
            patrolRoutes,
            waypointProfile.Locators,
            waypointProfile.Collections,
            waypointProfile.RouteVisualization);

        return area.WithNavigation(area.Navigation.WithAnchors(authoredCatalog).WithWaypoints(authoredWaypoints));
    }

    private static Guid Stable(Guid owner, string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}:{key}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guid);
        guid[7] = (byte)((guid[7] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
