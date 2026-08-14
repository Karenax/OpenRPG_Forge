using Forge.Application.Projects;
using Forge.Application.Areas;
using Forge.Application.Quality;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Validation;

namespace Forge.Application.Tests.Projects;

public sealed class ForgottenForestModuleAuthoringServiceTests
{
    [Fact]
    public void CreatesAValidatedPlayableExteriorSliceWithStableAssetReferences()
    {
        var authored = ForgottenForestModuleAuthoringService.Create();
        var project = authored.State.Project;
        var area = Assert.Single(project.Areas, candidate => candidate.Slug == "forgotten-forest");

        Assert.Equal("forgotten-forest", project.Module.Slug);
        Assert.Equal(6, project.Areas.Count);
        Assert.Equal(AreaKind.Exterior, area.Kind);
        Assert.Equal(new AreaDimensions(512, 512), area.Dimensions);
        Assert.True(area.World.Terrain.Enabled);
        Assert.Equal(129 * 129, area.World.Terrain.Heights.Count);
        Assert.Equal(16, area.World.Terrain.ChunkSize);
        Assert.Equal(1024, area.World.Loading.CacheBudgetMegabytes);
        Assert.Equal(2, area.World.Loading.ChunkRadius);
        Assert.Equal(13, area.World.Biomes.Count);
        Assert.Equal(10, area.World.Biomes.Count(biome => biome.Provenance ==
            "curated-pack:forgotten-forest-polyhaven-environment@1.1.0"));
        Assert.Equal(3, area.World.Biomes.Count(biome => biome.Provenance ==
            "curated-pack:forgotten-forest-core@1.0.0;source:kenney:mini-forest"));
        Assert.Equal(7, area.World.Features.Count);
        Assert.Contains(area.World.Features, feature => feature.Kind == TerrainFeatureKind.River);
        Assert.Contains(area.World.Features, feature => feature.Kind == TerrainFeatureKind.Bridge);
        var lakeHutClearing = Assert.Single(area.World.Features, feature =>
            feature.Name == "Cour dégagée de la maison du garde du lac");
        Assert.Equal(TerrainFeatureKind.Building, lakeHutClearing.Kind);
        Assert.Equal(26f, lakeHutClearing.Width);
        Assert.All(area.World.Biomes, biome =>
            Assert.Contains("building", biome.ExclusionTags, StringComparer.OrdinalIgnoreCase));
        var lakeHutPieces = area.Entities.Where(entity =>
            entity.Tags.Contains("lake-warden-hut", StringComparer.Ordinal)).ToArray();
        Assert.Equal(10, lakeHutPieces.Length);
        Assert.Single(lakeHutPieces, entity =>
            entity.Overrides.Properties.Any(property => property.Key == "visual.asset" &&
                property.Value == ForgottenForestModuleAuthoringService.TownAssetId("roof-high-gable").ToString()));
        Assert.Equal(3, area.Navigation.OffMeshLinks.Count);
        Assert.DoesNotContain("vfx:forest-ambient", area.Environment.Tags, StringComparer.Ordinal);
        Assert.Equal(SkyProfileMode.Physical, area.Environment.Sky.Mode);
        Assert.Equal("#4f83ad", area.Environment.Sky.ZenithColor);
        Assert.Equal("forest", area.Environment.Reverb.DefaultPreset);
        var lake = Assert.Single(area.Environment.Water.Bodies);
        Assert.Equal("Lac des Reflets", lake.Name);
        Assert.Equal(WaterBodyKind.Plane, lake.Kind);
        Assert.Equal(WaterQualityTier.Ultra, lake.Quality);
        Assert.True(lake.SwimmingEnabled);
        Assert.Equal(AreaSoundMode.Spatial3D, area.Environment.Audio.Spatial.Mode);
        Assert.Equal(3, area.RuntimePolicies.Experience.AudioCues.Count);
        Assert.Equal(3, area.RuntimePolicies.Experience.ScatterEmitters.Count);
        Assert.All(area.RuntimePolicies.Experience.AudioCues, cue =>
        {
            Assert.StartsWith("builtin://forest/", cue.Resource, StringComparison.Ordinal);
            Assert.Equal("LicenseRef-OpenRPG-Forge-Generated", cue.LicenseSpdx);
            Assert.Equal(
                ProceduralAmbientAudioService.Create(cue.Resource).ContentSha256,
                cue.ContentSha256);
            Assert.StartsWith("builtin:openrpg-forge", cue.Provenance, StringComparison.Ordinal);
        });
        Assert.True(area.Entities.Count >= 39);
        Assert.Contains(area.Entities, entity =>
            entity.Transform.Position.X > 150 && entity.Transform.Position.Z > 150 &&
            entity.Overrides.Properties.Any(property => property.Key == "visual.asset"));
        Assert.Contains(project.Blueprints, blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn && blueprint.Combat is not null);
        Assert.Contains(area.Entities, entity => entity.Tags.Contains("boss", StringComparer.Ordinal));
        Assert.Contains(area.Entities, entity => entity.Tags.Contains("exit", StringComparer.Ordinal));
        Assert.All(
            area.World.Biomes,
            biome => Assert.StartsWith("asset:", biome.ScatterResource, StringComparison.Ordinal));
        var expectedScatterAssets = new[]
        {
            "dead_tree_trunk", "didelta_spinosa", "dry_branches_medium_01", "fern_02",
            "grass_medium_01", "moss_01", "rock_moss_set_01", "rock_moss_set_02", "tree_stump_01",
            "tree_stump_02",
        }.Select(key => $"asset:{ForgottenForestModuleAuthoringService.PolyHavenAssetId(key)}")
            .Concat([
                $"asset:{AssetCatalogItemId.ForExternalId("kenney:mini-forest:tree")}",
                $"asset:{AssetCatalogItemId.ForExternalId("kenney:mini-forest:tree")}",
                $"asset:{AssetCatalogItemId.ForExternalId("kenney:mini-forest:tree-high")}",
            ])
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedScatterAssets,
            area.World.Biomes.Select(biome => biome.ScatterResource).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(3, area.World.Biomes.Count(biome =>
            biome.Tags.Contains("tree", StringComparer.OrdinalIgnoreCase) &&
            biome.Tags.Any(tag => tag.StartsWith("scale:", StringComparison.OrdinalIgnoreCase))));
        Assert.Contains(area.Entities, entity => entity.Tags.Contains("ship", StringComparer.Ordinal) &&
            entity.Transform.Position.X is > -8 and < 8 && entity.Transform.Position.Z is > -8 and < 8 &&
            entity.Overrides.Properties.Any(property =>
                property.Key == "visual.asset" &&
                property.Value == ForgottenForestModuleAuthoringService.PolyHavenAssetId("dutch_ship_large_01").ToString()));
        Assert.Equal(4, area.Entities.Count(entity => entity.Tags.Contains("bridge", StringComparer.Ordinal) &&
            entity.Overrides.Properties.Any(property =>
                property.Key == "visual.asset" &&
                property.Value == ForgottenForestModuleAuthoringService.PolyHavenAssetId("modular_wooden_pier").ToString())));
        var animatedEntities = area.Entities.Where(entity =>
            entity.Overrides.Properties.Any(property =>
                property.Key == "runtime.animationProfile" && property.Value == "kaykit-adventurer")).ToArray();
        Assert.Equal(5, animatedEntities.Length);
        var expectedAnimatedAssets = new[] { "knight", "mage", "rogue", "rogue-hooded", "barbarian" }
            .Select(key => ForgottenForestModuleAuthoringService.AnimatedCharacterAssetId(key).ToString())
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualAnimatedAssets = animatedEntities
            .Select(entity => entity.Overrides.Properties.Single(property => property.Key == "visual.asset").Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedAnimatedAssets, actualAnimatedAssets);
        var flyingMount = Assert.Single(area.Entities, entity =>
            entity.Tags.Contains("flying-mount", StringComparer.Ordinal));
        Assert.Contains("interactive", flyingMount.Tags, StringComparer.Ordinal);
        Assert.Equal(
            ForgottenForestModuleAuthoringService.AnimatedMonsterAssetId("dragon").ToString(),
            flyingMount.Overrides.Properties.Single(property => property.Key == "visual.asset").Value);
        Assert.Equal("quaternius-dragon", flyingMount.Overrides.Properties.Single(property =>
            property.Key == "runtime.animationProfile").Value);
        var flyingMountBlueprint = Assert.Single(project.Blueprints, blueprint => blueprint.Id == flyingMount.BlueprintId);
        Assert.Equal("dragon-mount", flyingMountBlueprint.Slug);
        Assert.Null(flyingMountBlueprint.Combat);
        Assert.Equal(10, project.Areas.Sum(candidate => candidate.Portals.Count));
        Assert.All(project.Areas.SelectMany(candidate => candidate.Portals), portal =>
        {
            var destination = Assert.Single(project.Areas, candidate => candidate.Id == portal.DestinationAreaId);
            Assert.Contains(destination.Entities, entity => entity.Id == portal.DestinationEntityId);
        });
        Assert.Equal(4, area.Entities.Count(entity => entity.Tags.Contains("goblin", StringComparer.Ordinal)));

        var village = Assert.Single(project.Areas, candidate => candidate.Slug == "clairval");
        Assert.True(village.Entities.Count(entity =>
            entity.Overrides.Properties.Any(property => property.Key == "visual.asset" &&
                property.Value == ForgottenForestModuleAuthoringService.TownAssetId("wall-wood").ToString())) >= 8);
        Assert.Contains(village.Entities, entity => entity.Tags.Contains("blacksmith", StringComparer.Ordinal));
        Assert.Contains(village.Entities, entity => entity.Tags.Contains("temple", StringComparer.Ordinal));
        Assert.Contains(village.Entities, entity => entity.Tags.Contains("healing", StringComparer.Ordinal));
        Assert.Equal(2, village.Portals.Count);

        var inn = Assert.Single(project.Areas, candidate => candidate.Slug == "ember-inn");
        Assert.Equal(inn.Id, project.Module.EntryAreaId);
        Assert.Equal(AreaKind.Interior, inn.Kind);
        Assert.Equal(4, inn.Entities.Count(entity => entity.Tags.Contains("bed", StringComparer.Ordinal)));
        Assert.Equal(5, inn.Entities.Count(entity => entity.Tags.Contains("table", StringComparer.Ordinal)));
        Assert.Equal(20, inn.Entities.Count(entity => entity.Tags.Contains("chair", StringComparer.Ordinal)));
        Assert.True(inn.Entities.Count(entity => entity.Tags.Contains("floor", StringComparer.Ordinal)) >= 100);
        Assert.Contains(inn.Entities, entity => entity.Tags.Contains("innkeeper", StringComparer.Ordinal));

        var fortress = Assert.Single(project.Areas, candidate => candidate.Slug == "gnoll-fortress");
        Assert.Equal(9, fortress.Entities.Count(entity => entity.Tags.Contains("gnoll", StringComparer.Ordinal)));
        Assert.True(fortress.Entities.Count(entity => entity.Tags.Contains("castle-kit", StringComparer.Ordinal)) >= 50);
        Assert.Contains(fortress.Entities, entity =>
            entity.Tags.Contains("boss", StringComparer.Ordinal) && entity.Tags.Contains("gnoll", StringComparer.Ordinal));

        var desert = Assert.Single(project.Areas, candidate => candidate.Slug == "amber-desert");
        Assert.Contains(desert.Environment.Tags, tag => tag == "desert");
        Assert.Contains(desert.World.Features, feature => feature.Kind == TerrainFeatureKind.Road);
        Assert.True(desert.Entities.Count(entity => entity.Tags.Contains("nature-kit", StringComparer.Ordinal)) >= 20);
        Assert.True(desert.Entities.Count(entity => entity.Tags.Contains("tent", StringComparer.Ordinal)) >= 3);

        var mountain = Assert.Single(project.Areas, candidate => candidate.Slug == "snow-pass");
        Assert.Contains(mountain.Environment.Weather.States, state => state.Kind == WeatherKind.Snow);
        Assert.True(mountain.Entities.Count(entity =>
            entity.Tags.Contains("nature-kit", StringComparer.Ordinal) &&
            entity.Tags.Contains("rock", StringComparer.Ordinal)) >= 8);
        Assert.True(mountain.Entities.Count(entity => entity.Tags.Contains("tree", StringComparer.Ordinal)) >= 18);
        var performance = PerformanceQualityService.Benchmark(project,
            project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance, 1_024_000, 2);
        Assert.All(performance.Measurements.Where(item => item.Kind == AreaBenchmarkKind.Interior),
            item => Assert.Equal("ember-inn", item.AreaSlug));
        Assert.All(performance.Measurements.Where(item => item.Kind != AreaBenchmarkKind.Interior),
            item => Assert.Equal("forgotten-forest", item.AreaSlug));
        Assert.True(performance.Passed);
        Assert.Equal(2, authored.Scripts.Count);
        Assert.Empty(ProjectValidator.Validate(project));
    }
}
