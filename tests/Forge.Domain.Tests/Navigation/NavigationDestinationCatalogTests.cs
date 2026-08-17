using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.Navigation;

public sealed class NavigationDestinationCatalogTests
{
    [Fact]
    public void Catalog_uses_human_disambiguated_labels_and_canonical_spawn_slugs()
    {
        var data = CreateProject();

        var catalog = NavigationDestinationCatalog.Build(data.Project);
        var spawn = catalog.Find(NavigationDestinationKind.SpawnAnchor, data.SpawnId);

        Assert.NotNull(spawn);
        Assert.Equal("camp-spawn", spawn.ArrivalAnchorKey);
        Assert.Contains("clairiere", spawn.HumanLabel, StringComparison.Ordinal);
        Assert.DoesNotContain(data.SpawnId.ToString("D"), spawn.HumanLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Same(spawn, catalog.FindArrivalAnchor("camp-spawn"));
        Assert.Contains(catalog.Entries, item => item.Kind == NavigationDestinationKind.Waypoint &&
                                                item.Id == data.WaypointId);
    }

    [Fact]
    public void Topology_validation_resolves_typed_destination_and_rejects_only_unknown_arrival_key()
    {
        var data = CreateProject();
        var valid = NavigationTopologyValidator.Validate(data.Project);
        var door = Assert.Single(data.Project.Module.WorldFeatures.Doors);
        var originalFoundation = door.Foundation!;
        var changedFoundation = originalFoundation with
        {
            Portal = originalFoundation.Portal! with { SpawnAnchorId = "legacy:opaque" },
        };
        var changedDoor = door with { Foundation = changedFoundation };
        var world = data.Project.Module.WorldFeatures;
        var changedCatalog = new WorldFeatureCatalog(world.Id, world.SchemaVersion, world.Provenance,
            world.Placeables, [changedDoor], world.Triggers, world.Encounters, world.VehicleRoutes,
            world.Merchants, world.Companions, world.Scripts);
        var changed = new ProjectDefinition(data.Project.Id, data.Project.Title,
            data.Project.Module.WithWorldFeatures(changedCatalog), data.Project.Blueprints, data.Project.Areas);

        var diagnostics = NavigationTopologyValidator.Validate(changed);

        Assert.Empty(valid);
        Assert.Contains(diagnostics, item => item.Code == NavigationTopologyValidator.ArrivalAnchorMissingCode);
        Assert.DoesNotContain(diagnostics, item => item.Code == NavigationTopologyValidator.DestinationMissingCode);
    }

    private static Data CreateProject()
    {
        var firstAreaId = AreaId.From(Guid.Parse("72010000-0000-4000-8000-000000000001"));
        var secondAreaId = AreaId.From(Guid.Parse("72010000-0000-4000-8000-000000000002"));
        var playerId = BlueprintId.From(Guid.Parse("72010000-0000-4000-8000-000000000003"));
        var doorBlueprintId = BlueprintId.From(Guid.Parse("72010000-0000-4000-8000-000000000004"));
        var player = new BlueprintDefinition(playerId, "hero-spawn", "Héros", BlueprintKind.PlayerSpawn);
        var doorBlueprint = new BlueprintDefinition(doorBlueprintId, "gate", "Porte du camp", BlueprintKind.Door);
        var spawnDefinition = new AnchorDefinition(
            Guid.Parse("72010000-0000-4000-8000-000000000010"), 1, "test:ux2", "camp-spawn",
            new LocalizedText("anchor.camp", "Camp"), AnchorKind.Spawn, AnchorScope.Area, ["player"],
            playerId, null, true);
        var spawnId = Guid.Parse("72010000-0000-4000-8000-000000000011");
        var spawn = new AnchorInstance(spawnId, 1, "test:ux2", spawnDefinition.Id, TransformValue.Identity, 1,
            EntityPropertyOverrides.Empty, new("hero", new(1.2f, 2, 1.2f), 100, "heroes", 0, null, true), null);
        var waypointDefinition = new AnchorDefinition(
            Guid.Parse("72010000-0000-4000-8000-000000000012"), 1, "test:ux2", "camp-waypoint",
            new LocalizedText("anchor.waypoint", "Camp"), AnchorKind.Waypoint, AnchorScope.Area, ["travel"],
            null, null, false);
        var waypointId = Guid.Parse("72010000-0000-4000-8000-000000000013");
        var waypoint = new AnchorInstance(waypointId, 1, "test:ux2", waypointDefinition.Id,
            TransformValue.Identity, 2, EntityPropertyOverrides.Empty, null, null);
        var anchors = new AnchorCatalog(Guid.Parse("72010000-0000-4000-8000-000000000014"), 1,
            "test:ux2", firstAreaId, true, [spawnDefinition, waypointDefinition], [spawn, waypoint]);
        var first = new AreaDefinition(firstAreaId, "clairiere", "Clairière", [],
            navigation: AreaNavigationDefinition.CreateDefault(firstAreaId).WithAnchors(anchors));
        var second = new AreaDefinition(secondAreaId, "lisiere", "Lisière", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("72010000-0000-4000-8000-000000000020")),
            "Navigation destinations", [player, doorBlueprint], [first, second]);
        var doorId = Guid.Parse("72010000-0000-4000-8000-000000000021");
        var foundation = new DoorWorldProfile.FoundationPolicy(DoorWorldProfile.PlacementMode.FreePlacement,
            null, null, "closed", [], [], new(2, "both", 1, 2, 1, "server", 1, "door.blocked"),
            new(DoorWorldProfile.DestinationKind.SpawnAnchor, spawnId, spawnDefinition.Slug, 0, true,
                ["portal.travel"]), [], new Dictionary<string, string>(), null);
        var door = new DoorWorldProfile(doorId, 1, "test:ux2", doorBlueprintId, null, null, [], null, null,
            null, foundation);
        var world = new WorldFeatureCatalog(project.Module.Id.Value, 1, "test:ux2", doors: [door]);
        project = new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(world),
            project.Blueprints, project.Areas);
        return new(project, spawnId, waypointId);
    }

    private sealed record Data(ProjectDefinition Project, Guid SpawnId, Guid WaypointId);
}
