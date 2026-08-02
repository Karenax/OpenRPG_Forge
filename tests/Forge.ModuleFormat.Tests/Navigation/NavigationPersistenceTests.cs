using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
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
        Assert.Equal(json, AreaJsonSerializer.Serialize(restored));
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
