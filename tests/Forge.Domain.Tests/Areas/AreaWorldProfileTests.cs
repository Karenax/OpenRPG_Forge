using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaWorldProfileTests
{
    private static readonly AreaId ExteriorAreaId = AreaId.From(Guid.Parse("a1100000-0000-4000-8000-000000000001"));

    [Fact]
    public void ExteriorDefaults_CreateDeterministicGenericTerrainAndBiomes()
    {
        var dimensions = new AreaDimensions(128, 96);

        var first = AreaWorldProfile.CreateDefault(ExteriorAreaId, AreaKind.Exterior, dimensions);
        var second = AreaWorldProfile.CreateDefault(ExteriorAreaId, AreaKind.Exterior, dimensions);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Terrain.Id, second.Terrain.Id);
        Assert.Equal(first.Terrain.Heights, second.Terrain.Heights);
        Assert.Equal(first.Terrain.MaterialLayers.Select(item => item.Id), second.Terrain.MaterialLayers.Select(item => item.Id));
        Assert.Equal(first.Biomes.Select(item => item.Id), second.Biomes.Select(item => item.Id));
        Assert.Equal(TerrainProviderKind.Heightmap, first.Terrain.Provider);
        Assert.Equal(17 * 17, first.Terrain.Heights.Count);
        Assert.Equal(3, first.Terrain.MaterialLayers.Count);
        Assert.Equal(2, first.Biomes.Count);
        Assert.True(float.IsFinite(first.Terrain.HeightAt(5, -7)));
        Assert.True(first.VolumetricNavigation.Enabled);
        Assert.Equal(VolumetricNavigationMode.Flight, first.VolumetricNavigation.Modes);
    }

    [Fact]
    public void TerrainSupportsHolesAndBilinearHeightQueries()
    {
        var material = new TerrainMaterialLayer(Guid.NewGuid(), "Ground", "#506040", string.Empty, 4);
        var terrain = new TerrainDefinition(Guid.NewGuid(), 1, TerrainProviderKind.Heightmap,
            20, 20, 3, 3, 2, 7, 10,
            [0, 0, 0, 0, .5f, 0, 0, 0, 1], [material],
            [new TerrainHoleDefinition(Guid.NewGuid(), "Cave entrance", 0, 1, 1, 1, "test")],
            string.Empty, string.Empty, string.Empty, "test");

        Assert.Equal(5, terrain.HeightAt(0, 0), 3);
        Assert.True(terrain.IsHoleCell(0, 1));
        Assert.False(terrain.IsHoleCell(1, 1));
    }

    [Fact]
    public void ValidatorRejectsUnknownBiomeMaterialsAndUnbridgedCrossings()
    {
        var area = new AreaDefinition(ExteriorAreaId, "wilds", "Wilds", [], AreaKind.Exterior,
            new AreaDimensions(64, 64));
        var source = area.World;
        var badBiome = new BiomeLayerDefinition(Guid.NewGuid(), "Unknown", Guid.NewGuid(), -10, 10,
            0, 90, 0, 1, 0, string.Empty, ["unknown"], [], 1, "test");
        var road = new TerrainFeatureDefinition(Guid.NewGuid(), 1, "Road", TerrainFeatureKind.Road,
            [new Vector3Value(-10, 0, 0), new Vector3Value(10, 0, 0)], 2, .1f, 0,
            TerrainFeatureCrossingPolicy.RequiresBridge, [], string.Empty, "test");
        var river = new TerrainFeatureDefinition(Guid.NewGuid(), 1, "River", TerrainFeatureKind.River,
            [new Vector3Value(0, 0, -10), new Vector3Value(0, 0, 10)], 3, .2f, 0,
            TerrainFeatureCrossingPolicy.RequiresBridge, [], string.Empty, "test");
        var invalid = new AreaWorldProfile(source.Id, source.SchemaVersion, source.Terrain, [badBiome],
            [road, river], source.VolumetricNavigation, [], source.Loading, "test");

        var diagnostics = AreaWorldValidator.Validate(area, invalid);

        Assert.Contains(diagnostics, item => item.Code == AreaWorldValidator.BiomeMaterialCode);
        Assert.Contains(diagnostics, item => item.Code == AreaWorldValidator.CrossingBridgeCode);
    }

    [Fact]
    public void CloneForAreaRemapsAllOwnedIdentifiers()
    {
        var source = AreaWorldProfile.CreateDefault(ExteriorAreaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var destination = AreaId.From(Guid.Parse("a1100000-0000-4000-8000-000000000002"));

        var clone = AreaWorldProfile.CloneForArea(source, destination);

        Assert.NotEqual(source.Id, clone.Id);
        Assert.NotEqual(source.Terrain.Id, clone.Terrain.Id);
        Assert.Empty(source.Terrain.MaterialLayers.Select(item => item.Id)
            .Intersect(clone.Terrain.MaterialLayers.Select(item => item.Id)));
        Assert.All(clone.Biomes, biome => Assert.Contains(clone.Terrain.MaterialLayers,
            material => material.Id == biome.MaterialLayerId));
        Assert.Equal(source.Terrain.Heights, clone.Terrain.Heights);
    }

    [Fact]
    public void ResizePreservesCanonicalSamplesAndAdaptsTerrainAndVolumeBounds()
    {
        var source = AreaWorldProfile.CreateDefault(ExteriorAreaId, AreaKind.Exterior, new AreaDimensions(64, 64));

        var resized = AreaWorldProfile.ResizeForArea(source, new AreaDimensions(96, 48));

        Assert.Equal(source.Id, resized.Id);
        Assert.Equal(96, resized.Terrain.Width);
        Assert.Equal(48, resized.Terrain.Depth);
        Assert.Equal(source.Terrain.Heights, resized.Terrain.Heights);
        Assert.Equal(96, resized.VolumetricNavigation.Size.X);
        Assert.Equal(48, resized.VolumetricNavigation.Size.Z);
    }
}
