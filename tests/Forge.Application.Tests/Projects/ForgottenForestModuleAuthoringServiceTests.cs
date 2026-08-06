using Forge.Application.Projects;
using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Validation;

namespace Forge.Application.Tests.Projects;

public sealed class ForgottenForestModuleAuthoringServiceTests
{
    [Fact]
    public void CreatesAValidatedPlayableExteriorSliceWithStableAssetReferences()
    {
        var authored = ForgottenForestModuleAuthoringService.Create();
        var project = authored.State.Project;
        var area = Assert.Single(project.Areas);

        Assert.Equal("forgotten-forest", project.Module.Slug);
        Assert.Equal(AreaKind.Exterior, area.Kind);
        Assert.Equal(new AreaDimensions(512, 512), area.Dimensions);
        Assert.True(area.World.Terrain.Enabled);
        Assert.Equal(129 * 129, area.World.Terrain.Heights.Count);
        Assert.Equal(16, area.World.Terrain.ChunkSize);
        Assert.Equal(1024, area.World.Loading.CacheBudgetMegabytes);
        Assert.Equal(2, area.World.Loading.ChunkRadius);
        Assert.Equal(17, area.World.Biomes.Count);
        Assert.Equal(12, area.World.Biomes.Count(biome =>
            biome.Provenance == "curated-pack:forgotten-forest-nature-dressing@1.0.0"));
        Assert.Equal(5, area.World.Features.Count);
        Assert.Contains(area.World.Features, feature => feature.Kind == TerrainFeatureKind.River);
        Assert.Contains(area.World.Features, feature => feature.Kind == TerrainFeatureKind.Bridge);
        Assert.Equal(2, area.Navigation.OffMeshLinks.Count);
        Assert.Contains("vfx:forest-ambient", area.Environment.Tags, StringComparer.Ordinal);
        Assert.Equal(SkyProfileMode.Physical, area.Environment.Sky.Mode);
        Assert.Equal("#4f83ad", area.Environment.Sky.ZenithColor);
        Assert.Equal("forest", area.Environment.Reverb.DefaultPreset);
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
        Assert.Equal(
            AssetCatalogItemId.ForExternalId("kenney:mini-forest:tree-high"),
            ForgottenForestModuleAuthoringService.AssetId("tree-high"));
        Assert.Equal(
            AssetCatalogItemId.ForExternalId("kenney:nature-kit:oak-tree"),
            ForgottenForestModuleAuthoringService.NatureAssetId("oak-tree"));
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
        Assert.Equal(2, authored.Scripts.Count);
        Assert.Empty(ProjectValidator.Validate(project));
    }
}
