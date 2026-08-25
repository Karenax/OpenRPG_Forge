using Forge.Domain.Areas;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class BiomeScatterPlanBuilderTests
{
    [Fact]
    public void BuildsStablePlacementsForTheSameRecipeAndSeed()
    {
        var world = BuildWorld([]);
        var biome = world.Biomes.Single(candidate => candidate.Tags.Contains("meadow"));

        var first = BiomeScatterPlanBuilder.Build(world, biome);
        var second = BiomeScatterPlanBuilder.Build(world, biome);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.All(first, placement =>
        {
            Assert.InRange(placement.Position.X, -world.Terrain.Width / 2f, world.Terrain.Width / 2f);
            Assert.InRange(placement.Position.Z, -world.Terrain.Depth / 2f, world.Terrain.Depth / 2f);
            Assert.InRange(placement.YawRadians, 0, MathF.Tau);
            Assert.True(placement.UniformScale > 0);
        });
    }

    [Fact]
    public void PaintedMaterialSelectsTheMatchingBiomeRecipe()
    {
        var source = BuildWorld([]);
        var rock = source.Terrain.MaterialLayers.Single(candidate => candidate.Name == "Roche");
        var paint = Enumerable.Range(0, source.Terrain.SamplesZ - 1)
            .SelectMany(z => Enumerable.Range(0, source.Terrain.SamplesX - 1)
                .Select(x => new TerrainMaterialPaintCell(x, z, rock.Id)))
            .ToArray();
        var world = BuildWorld(paint);
        var meadowBiome = world.Biomes.Single(candidate => candidate.Tags.Contains("meadow"));
        var rockBiome = world.Biomes.Single(candidate => candidate.Tags.Contains("rock"));

        var meadowPlacements = BiomeScatterPlanBuilder.Build(world, meadowBiome);
        var rockPlacements = BiomeScatterPlanBuilder.Build(world, rockBiome);

        Assert.Empty(meadowPlacements);
        Assert.NotEmpty(rockPlacements);
    }

    [Fact]
    public void MeterScaleAssetsKeepTheirAuthoredRealWorldScale()
    {
        var source = BuildWorld([]);
        var meadow = source.Terrain.MaterialLayers.Single(candidate => candidate.Name == "Prairie");
        var biome = new BiomeLayerDefinition(
            Guid.Parse("a23d0000-0000-4000-8000-000000000004"),
            "Realistic trees",
            meadow.Id,
            -512,
            512,
            0,
            90,
            0,
            1,
            .5f,
            "asset:polyhaven-tree",
            ["tree", "meter-scale"],
            ["water"],
            20,
            "test:meter-scale");

        var placements = BiomeScatterPlanBuilder.Build(source, biome);

        Assert.NotEmpty(placements);
        Assert.All(placements, placement => Assert.InRange(placement.UniformScale, .84f, 1.26f));
    }

    [Fact]
    public void ExplicitScaleTagMultipliesMeterScaleAssets()
    {
        var source = BuildWorld([]);
        var meadow = source.Terrain.MaterialLayers.Single(candidate => candidate.Name == "Prairie");
        var biome = new BiomeLayerDefinition(
            Guid.Parse("a23d0000-0000-4000-8000-000000000005"),
            "Large trees",
            meadow.Id,
            -512,
            512,
            0,
            90,
            0,
            1,
            .5f,
            "asset:polyhaven-tree",
            ["tree", "meter-scale", "scale:4"],
            ["water"],
            20,
            "test:meter-scale");

        var placements = BiomeScatterPlanBuilder.Build(source, biome);

        Assert.NotEmpty(placements);
        Assert.All(placements, placement => Assert.InRange(placement.UniformScale, 3.36f, 5.04f));
    }

    [Fact]
    public void LushMeadowUsesTheBoundedProductionDensityProfile()
    {
        var areaId = AreaId.From(Guid.Parse("a23d0000-0000-4000-8000-000000000011"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(512, 512));
        var meadow = source.Terrain.MaterialLayers[0];
        BiomeLayerDefinition Create(string suffix, IReadOnlyList<string> tags) => new(
            Guid.Parse($"a23d0000-0000-4000-8000-0000000000{suffix}"),
            $"Meadow {suffix}", meadow.Id, -512, 512, 0, 90, 0, 1, .9f,
            $"asset:meadow-{suffix}", tags, [], 20, "test:lush-meadow");
        var standard = Create("12", ["ground-cover"]);
        var lush = Create("13", ["ground-cover", "lush-meadow"]);
        AreaWorldProfile World(BiomeLayerDefinition biome) => new(
            source.Id, source.SchemaVersion, source.Terrain, [biome], source.Features,
            source.VolumetricNavigation, source.FlightRoutes, source.Loading, source.Provenance);
        var standardWorld = World(standard);
        var lushWorld = World(lush);

        var standardPlacements = BiomeScatterPlanBuilder.Build(standardWorld, standard);
        var lushPlacements = BiomeScatterPlanBuilder.Build(lushWorld, lush);

        Assert.InRange(standardPlacements.Count, 2_000, 2_600);
        Assert.True(lushPlacements.Count > standardPlacements.Count * 2);
        Assert.InRange(lushPlacements.Count, 12_000, 16_000);
    }

    [Fact]
    public void FocusRegionConcentratesAStableLayerAroundItsAuthoredLandscapeCenter()
    {
        var source = BuildWorld([]);
        var meadow = source.Terrain.MaterialLayers.Single(candidate => candidate.Name == "Prairie");
        var biome = new BiomeLayerDefinition(
            Guid.Parse("a23d0000-0000-4000-8000-000000000006"),
            "Village meadow",
            meadow.Id,
            -512,
            512,
            0,
            90,
            0,
            1,
            .5f,
            "asset:village-meadow",
            ["ground-cover", "focus-radius:12", "focus-center:4,-3"],
            [],
            20,
            "test:focus-region");

        var placements = BiomeScatterPlanBuilder.Build(source, biome);

        Assert.NotEmpty(placements);
        Assert.All(placements, placement =>
            Assert.True(MathF.Pow(placement.Position.X - 4, 2) + MathF.Pow(placement.Position.Z + 3, 2) <= 12 * 12));
    }

    private static AreaWorldProfile BuildWorld(IReadOnlyList<TerrainMaterialPaintCell> paint)
    {
        var areaId = AreaId.From(Guid.Parse("a23d0000-0000-4000-8000-000000000001"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var terrain = new TerrainDefinition(
            source.Terrain.Id,
            source.Terrain.SchemaVersion,
            source.Terrain.Provider,
            source.Terrain.Width,
            source.Terrain.Depth,
            source.Terrain.SamplesX,
            source.Terrain.SamplesZ,
            source.Terrain.ChunkSize,
            source.Terrain.Seed,
            source.Terrain.HeightScale,
            source.Terrain.Heights,
            source.Terrain.MaterialLayers,
            source.Terrain.Holes,
            source.Terrain.HeightmapResource,
            source.Terrain.VoxelResource,
            source.Terrain.MeshResource,
            "test:biome-scatter",
            paint);
        var meadow = terrain.MaterialLayers.Single(candidate => candidate.Name == "Prairie");
        var rock = terrain.MaterialLayers.Single(candidate => candidate.Name == "Roche");
        IReadOnlyList<BiomeLayerDefinition> biomes =
        [
            new(Guid.Parse("a23d0000-0000-4000-8000-000000000002"), "Prairie", meadow.Id,
                -512, 512, 0, 90, 0, 1, .5f, "asset:test-meadow",
                ["ground-cover", "meadow"], ["road", "water"], 20, "test:biome-scatter"),
            new(Guid.Parse("a23d0000-0000-4000-8000-000000000003"), "Roche", rock.Id,
                -512, 512, 0, 90, 0, 1, .5f, "asset:test-rock",
                ["rock"], ["water"], 30, "test:biome-scatter"),
        ];
        return new AreaWorldProfile(
            source.Id,
            source.SchemaVersion,
            terrain,
            biomes,
            source.Features,
            source.VolumetricNavigation,
            source.FlightRoutes,
            source.Loading,
            "test:biome-scatter");
    }
}
