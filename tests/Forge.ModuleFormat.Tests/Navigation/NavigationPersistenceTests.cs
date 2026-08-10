using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Serialization;
using System.Text.Json.Nodes;

namespace Forge.ModuleFormat.Tests.Navigation;

public sealed class NavigationPersistenceTests
{
    [Fact]
    public void AreaSerialization_MigratesLegacyAndRoundTripsNavigation()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("73000000-0000-4000-8000-000000000001")),
            "area",
            "Area",
            []);
        var json = AreaJsonSerializer.Serialize(area);
        var legacyNode = JsonNode.Parse(json)!.AsObject();
        legacyNode["formatVersion"] = AreaJsonSerializer.PreNavigationFormatVersion;
        legacyNode["area"]!.AsObject().Remove("navigation");
        var legacy = legacyNode.ToJsonString();

        var restored = AreaJsonSerializer.Deserialize(json);
        var migrated = AreaJsonSerializer.Deserialize(legacy);

        Assert.Equal(4, restored.Navigation.AgentProfiles.Count);
        Assert.Equal(area.Navigation.Id, restored.Navigation.Id);
        Assert.Equal(area.Navigation.Id, migrated.Navigation.Id);
        Assert.Equal(area.Id, migrated.Navigation.Anchors.AreaId);
        Assert.Empty(migrated.Navigation.Anchors.Instances);
        Assert.Equal(json, AreaJsonSerializer.Serialize(restored));
    }

    [Fact]
    public void AreaSerialization_RoundTripsNamedAnchorSpawnPortalAndOverrides()
    {
        var areaId = AreaId.From(Guid.Parse("73000000-0000-4000-8000-000000000021"));
        var definition = new AnchorDefinition(Guid.Parse("73000000-0000-4000-8000-000000000022"), 1,
            "test:r009", "arrival", new LocalizedText("anchor.arrival", "Arrival"),
            AnchorKind.TransitionDestination, AnchorScope.Module,
            ["arrival", "portal"], null, "asset:beacon", true);
        var instanceId = Guid.Parse("73000000-0000-4000-8000-000000000023");
        var instance = new AnchorInstance(instanceId, 1, "test:r009", definition.Id,
            new TransformValue(new Vector3Value(3, 1, -2), QuaternionValue.Identity, Vector3Value.One), 1,
            new EntityPropertyOverrides(1, [new("fx.color", EntityPropertyValueKind.String, "violet")]),
            null, new PortalAnchorDestination(areaId, instanceId, PortalOrientationPolicy.FixedYaw, 135,
                PortalVelocityPolicy.Reset, true));
        var area = new AreaDefinition(areaId, "area", "Area", [], navigation:
            AreaNavigationDefinition.CreateDefault(areaId).WithAnchors(new AnchorCatalog(
                Guid.Parse("73000000-0000-4000-8000-000000000024"), 1, "test:r009", areaId, true,
                [definition], [instance])));

        var json = AreaJsonSerializer.Serialize(area);
        var restored = AreaJsonSerializer.Deserialize(json);

        Assert.Equal(json, AreaJsonSerializer.Serialize(restored));
        var restoredDefinition = Assert.Single(restored.Navigation.Anchors.Definitions);
        var restoredInstance = Assert.Single(restored.Navigation.Anchors.Instances);
        Assert.Equal("anchor.arrival", restoredDefinition.Label.Key);
        Assert.Equal("asset:beacon", restoredDefinition.AssetId);
        Assert.Equal(135, restoredInstance.Portal!.FixedYawDegrees);
        Assert.Equal("violet", Assert.Single(restoredInstance.Overrides.Properties).Value);
    }

    [Fact]
    public void BakeStore_RoundTripsVersionedDerivedSurface()
    {
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("73000000-0000-4000-8000-000000000011")),
            "area",
            "Area",
            []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("73000000-0000-4000-8000-000000000012")),
            "Bake",
            [],
            [area]);
        var agent = area.Navigation.AgentProfiles[0];
        var surface = NavigationSurfaceBuilder.Build(project, area, agent);
        var temporary = Path.Combine(Path.GetTempPath(), $"openrpg-nav-{Guid.NewGuid():N}");
        try
        {
            NavigationBakeStore.Save(temporary, surface);
            var restored = NavigationBakeStore.TryLoad(temporary, area.Id, agent.Id);

            Assert.NotNull(restored);
            Assert.Equal(surface.Id, restored.Id);
            Assert.Equal(surface.SourceFingerprint, restored.SourceFingerprint);
            Assert.Equal(surface.WalkableCells, restored.WalkableCells);
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }
}
