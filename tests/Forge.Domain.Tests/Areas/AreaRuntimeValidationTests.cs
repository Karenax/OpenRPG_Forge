using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaRuntimeValidationTests
{
    [Fact]
    public void Portal_resolution_applies_orientation_velocity_and_reverse_travel_deterministically()
    {
        var fixture = PortalFixture();

        var forward = PortalLinkRuntimeService.Resolve(fixture.Project, fixture.Source.Id, fixture.Portal.Id,
            new Vector3Value(3, 1, 4), 12);
        var reverse = PortalLinkRuntimeService.Resolve(fixture.Project, fixture.Destination.Id, fixture.Portal.Id,
            new Vector3Value(3, 1, 4), 12);

        Assert.False(forward.Reversed);
        Assert.Equal(fixture.Destination.Id, forward.DestinationAreaId);
        Assert.Equal(90, forward.ArrivalYawDegrees);
        Assert.Equal(5, forward.ArrivalVelocity.X, 4);
        Assert.Equal(1, forward.ArrivalVelocity.Y, 4);
        Assert.Equal(0, forward.ArrivalVelocity.Z, 4);
        Assert.True(reverse.Reversed);
        Assert.Equal(fixture.Source.Id, reverse.DestinationAreaId);
        Assert.Equal(fixture.Source.Entities[0].Transform, reverse.DestinationTransform);
        Assert.Equal("scripts/before.forge", forward.BeforeTransitionScript);
    }

    [Fact]
    public void Portal_validator_rejects_unsafe_required_hooks_with_a_localized_path()
    {
        var fixture = PortalFixture();
        var invalid = new PortalLink(fixture.Portal.Id, fixture.Portal.DisplayName,
            fixture.Portal.SourceEntityId, fixture.Portal.DestinationAreaId, fixture.Portal.DestinationEntityId,
            true, false, provenance: "test", beforeTransitionScript: "../escape.forge",
            hooksRequiredAtRuntime: true);

        var diagnostic = Assert.Single(PortalLinkValidator.Validate(fixture.Source, invalid));

        Assert.Equal(PortalLinkValidator.HookCode, diagnostic.Code);
        Assert.Contains(fixture.Portal.Id.ToString(), diagnostic.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Play_from_here_profile_validates_camera_and_player_blueprint_without_mutation()
    {
        var fixture = PortalFixture();
        var defaults = AreaPlaytestProfile.CreateDefault(fixture.Source.Id);
        var profile = new AreaPlaytestProfile(defaults.Id, 1, "author:r029-test", true, false,
            PlayFromHereStartMode.Camera, null,
            new TransformValue(new Vector3Value(8, 2, -3), QuaternionValue.Identity, Vector3Value.One),
            null, fixture.PlayerBlueprint.Id, "combat-debug", []);

        var diagnostics = AreaPlaytestProfileValidator.Validate(fixture.Project, fixture.Source, profile);

        Assert.Empty(diagnostics);
        Assert.False(fixture.Source.Playtest.Enabled);
        Assert.Equal(defaults.Id, fixture.Source.Playtest.Id);
    }

    private static (ProjectDefinition Project, AreaDefinition Source, AreaDefinition Destination,
        PortalLink Portal, BlueprintDefinition PlayerBlueprint) PortalFixture()
    {
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("a0290000-0000-4000-8000-000000000001")),
            "player", "Player", BlueprintKind.PlayerSpawn);
        var sourceId = AreaId.From(Guid.Parse("a0290000-0000-4000-8000-000000000002"));
        var destinationId = AreaId.From(Guid.Parse("a0290000-0000-4000-8000-000000000003"));
        var sourceEntity = new EntityInstance(EntityId.From(Guid.Parse("a0290000-0000-4000-8000-000000000004")),
            blueprint.Id, new TransformValue(new Vector3Value(1, 0, 2), QuaternionValue.Identity, Vector3Value.One));
        var destinationEntity = new EntityInstance(EntityId.From(Guid.Parse("a0290000-0000-4000-8000-000000000005")),
            blueprint.Id, new TransformValue(new Vector3Value(20, 0, 30), QuaternionValue.Identity, Vector3Value.One));
        var portal = new PortalLink(PortalId.From(Guid.Parse("a0290000-0000-4000-8000-000000000006")),
            "Crossing", sourceEntity.Id, destinationId, destinationEntity.Id, true, true,
            provenance: "author:r029-test", orientation: PortalOrientationPolicy.FixedYaw, fixedYawDegrees: 90,
            velocity: PortalVelocityPolicy.RedirectForward, beforeTransitionScript: "scripts/before.forge");
        var source = new AreaDefinition(sourceId, "source", "Source", [sourceEntity], portals: [portal]);
        var destination = new AreaDefinition(destinationId, "destination", "Destination", [destinationEntity]);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("a0290000-0000-4000-8000-000000000007")),
            "R029 fixture", [blueprint], [source, destination]);
        return (project, source, destination, portal, blueprint);
    }
}
