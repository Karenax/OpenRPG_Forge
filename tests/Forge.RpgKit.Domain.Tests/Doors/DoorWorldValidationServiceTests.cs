using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Doors;

namespace Forge.RpgKit.Domain.Tests.Doors;

public sealed class DoorWorldValidationServiceTests
{
    [Fact]
    public void Canonical_facade_rejects_missing_context_without_synthesizing_references_from_the_door()
    {
        var project = Project();
        var door = project.Module.WorldFeatures.Doors.Single();

        var valid = DoorWorldValidationService.Validate(project, door.Id);
        var emptyContext = door with
        {
            Validation = new([], [], [], [], []),
        };
        var invalid = DoorWorldValidationService.Validate(WithDoor(project, emptyContext), door.Id);
        var codes = invalid.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.True(valid.Available);
        Assert.Empty(valid.Diagnostics);
        Assert.False(invalid.Available);
        Assert.Contains("RPG_DOOR_DESTINATION_MISSING", codes);
        Assert.Contains("RPG_DOOR_ANIMATION_MISSING", codes);
        Assert.Contains("RPG_DOOR_COLLISION_PROFILE_MISSING", codes);
        Assert.Contains("RPG_DOOR_CREDENTIAL_MISSING", codes);
        Assert.Contains("RPG_DOOR_SCRIPT_MISSING", codes);
        Assert.All(invalid.Diagnostics,
            item => Assert.StartsWith("module/worldFeatures/doors/", item.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void Optional_required_and_explicit_selection_states_remain_distinct()
    {
        var project = Project();
        var door = project.Module.WorldFeatures.Doors.Single();
        var optional = DoorWorldValidationService.Validate(WithDoor(project, door with
        {
            Validation = null,
            ValidationRequiredAtRuntime = false,
        }), door.Id);
        var required = DoorWorldValidationService.Validate(WithDoor(project, door with
        {
            Validation = null,
            ValidationRequiredAtRuntime = true,
        }), door.Id);
        var unknown = DoorWorldValidationService.Validate(project,
            Guid.Parse("21000000-0000-4000-8000-000000000099"));

        Assert.False(optional.Available);
        Assert.True(optional.OptionalFallback);
        Assert.Empty(optional.Diagnostics);
        Assert.False(required.OptionalFallback);
        Assert.Contains(required.Diagnostics, item => item.Code == "world.door.validation_required_missing");
        Assert.Contains(unknown.Diagnostics, item => item.Code == "world.door.validation_profile_missing");
    }

    private static ProjectDefinition Project()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("21000000-0000-4000-8000-000000000001")),
            "door-area", "Door area", []);
        var source = new ProjectDefinition(
            ProjectId.From(Guid.Parse("21000000-0000-4000-8000-000000000002")),
            "Door validation", [], [area]);
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("21000000-0000-4000-8000-000000000003")),
            "ash-gate", "Ash gate", BlueprintKind.Door);
        var doorId = Guid.Parse("21000000-0000-4000-8000-000000000004");
        var destinationId = Guid.Parse("21000000-0000-4000-8000-000000000005");
        var accessId = Guid.Parse("21000000-0000-4000-8000-000000000010");
        var access = new DoorAccessEventProfile(accessId, 1, "test:ux1-aj", "assets/doors/ash-gate.glb",
            TransformValue.Identity,
            new(Guid.Parse("21000000-0000-4000-8000-000000000011"), DoorAccessLockState.Locked,
                ["key:ember"], "mechanics", 12, null, true, true, true, true, ["door.unlock"]),
            new(Guid.Parse("21000000-0000-4000-8000-000000000012"), "1.0.0", "DoorOpenRejected",
                [new("en-US", "Locked.")], "audio:locked", "animation:rattle", "scripts/rejected"),
            new(Guid.Parse("21000000-0000-4000-8000-000000000013"), "door:open", "damage:ember", 10,
                12, 30, "ash-watch", "vfx:ember", "ash-watch", true, false, ["front"], ["interact"]),
            new(Guid.Parse("21000000-0000-4000-8000-000000000014"), "1.0.0", "PortalUseRequested",
                "PortalUseCompleted"),
            new(Guid.Parse("21000000-0000-4000-8000-000000000015"), "1.0.0", "DoorOpened", "DoorClosed"),
            new Dictionary<string, string>());
        var foundation = new DoorWorldProfile.FoundationPolicy(DoorWorldProfile.PlacementMode.FreePlacement,
            null, null, "closed",
            [new("open", DoorWorldProfile.StateKind.Open, "animation:open", false, true),
             new("closed", DoorWorldProfile.StateKind.Closed, "animation:closed", true, true),
             new("opening", DoorWorldProfile.StateKind.Opening, "animation:opening", false, false)],
            [new("closed", "opening", "interact", 1, null)],
            new(2, "both", 1, 2, 1, "server", 2, "door.unavailable"),
            new(DoorWorldProfile.DestinationKind.Waypoint, destinationId, "anchor:exit", 180, true,
                ["portal.travel"]), [], new Dictionary<string, string>(),
            new("nav-obstacle:ash-gate", "nav-link:ash-gate", ["closed"], ["open"], .75, .25, true));
        var validation = new DoorWorldProfile.RuntimeValidationPolicy([destinationId],
            ["animation:open", "animation:closed", "animation:opening"], ["collision:ash-gate"],
            ["key:ember"], ["scripts/door", "scripts/rejected"]);
        var door = new DoorWorldProfile(doorId, 1, "test:ux1-aj", blueprint.Id, null, null,
            [new("interact", "scripts/door", false, true)], null, null, null, foundation, access, true,
            validation, true);
        var catalog = new WorldFeatureCatalog(source.Module.Id.Value, 1, "test:ux1-aj", doors: [door]);
        return new(source.Id, source.Title, source.Module.WithWorldFeatures(catalog), [blueprint], source.Areas);
    }

    private static ProjectDefinition WithDoor(ProjectDefinition project, DoorWorldProfile door)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, [door], source.Triggers, source.Encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        return new(project.Id, project.Title, project.Module.WithWorldFeatures(catalog), project.Blueprints,
            project.Areas, project.Dialogues, project.Quests);
    }
}
