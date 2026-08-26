using Forge.Domain.Areas;

namespace Forge.Domain.Tests.Areas;

public sealed class BiomeScatterAuthoringTests
{
    [Fact]
    public void AppliesEveryAuthoredScatterFieldWithoutChangingNeighbouringWorldData()
    {
        var areaId = AreaId.From(Guid.Parse("7f3750be-31d8-4eeb-8981-a348a3110101"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, new AreaDimensions(64, 64));
        var biome = source.Biomes[0];
        var taggedBiome = new BiomeLayerDefinition(
            biome.Id, biome.Name, biome.MaterialLayerId, biome.MinimumAltitude, biome.MaximumAltitude,
            biome.MinimumSlopeDegrees, biome.MaximumSlopeDegrees, biome.MinimumMoisture, biome.MaximumMoisture,
            biome.ScatterDensity, biome.ScatterResource, [.. biome.Tags, "custom-neighbour:preserved"],
            biome.ExclusionTags, biome.Priority, biome.Provenance);
        source = new AreaWorldProfile(source.Id, source.SchemaVersion, source.Terrain,
            [taggedBiome, .. source.Biomes.Skip(1)], source.Features, source.VolumetricNavigation,
            source.FlightRoutes, source.Loading, source.Provenance);

        var updated = BiomeScatterAuthoring.Apply(source,
        [
            new BiomeScatterPatch(
                taggedBiome.Id,
                [
                    BiomeScatterPatchField.Density,
                    BiomeScatterPatchField.Resource,
                    BiomeScatterPatchField.Altitude,
                    BiomeScatterPatchField.Slope,
                    BiomeScatterPatchField.Moisture,
                    BiomeScatterPatchField.Scale,
                    BiomeScatterPatchField.Tint,
                    BiomeScatterPatchField.Focus,
                    BiomeScatterPatchField.ExclusionTags,
                    BiomeScatterPatchField.Seed,
                ],
                Density: .72f,
                Resource: "assets/nature/fern.glb",
                Altitude: new(-4, 18),
                Slope: new(2, 31),
                Moisture: new(.35f, .91f),
                Scale: 1.35f,
                Tint: new("#79bd39", .28f),
                Focus: new(4.5f, -7.25f, 42),
                ExclusionTags: ["road", "water", "settlement"],
                Seed: 7821),
        ]);

        var edited = updated.Biomes.Single(item => item.Id == taggedBiome.Id);
        var settings = BiomeScatterAuthoring.Read(edited);
        Assert.Equal(.72f, edited.ScatterDensity);
        Assert.Equal("assets/nature/fern.glb", edited.ScatterResource);
        Assert.Equal((-4f, 18f), (edited.MinimumAltitude, edited.MaximumAltitude));
        Assert.Equal((2f, 31f), (edited.MinimumSlopeDegrees, edited.MaximumSlopeDegrees));
        Assert.Equal((.35f, .91f), (edited.MinimumMoisture, edited.MaximumMoisture));
        Assert.Equal(1.35f, settings.Scale);
        Assert.Equal(new BiomeScatterTint("#79bd39", .28f), settings.Tint);
        Assert.Equal(new BiomeScatterFocus(4.5f, -7.25f, 42), settings.Focus);
        Assert.Equal(7821, settings.Seed);
        Assert.Equal(["road", "settlement", "water"], edited.ExclusionTags);
        Assert.Contains("custom-neighbour:preserved", edited.Tags);

        Assert.Same(source.Terrain, updated.Terrain);
        Assert.Same(source.VolumetricNavigation, updated.VolumetricNavigation);
        Assert.Same(source.Loading, updated.Loading);
        Assert.Equal(source.Features, updated.Features);
        Assert.Equal(source.FlightRoutes, updated.FlightRoutes);
        Assert.Same(source.Biomes[1], updated.Biomes.Single(item => item.Id == source.Biomes[1].Id));
        Assert.Equal(source.Provenance, updated.Provenance);
    }

    [Fact]
    public void OmittedFieldsRemainByteForByteEquivalentAtTheDomainBoundary()
    {
        var areaId = AreaId.From(Guid.Parse("7f3750be-31d8-4eeb-8981-a348a3110102"));
        var source = AreaWorldProfile.CreateDefault(areaId, AreaKind.Exterior, AreaDimensions.Default);
        var biome = source.Biomes[0];

        var updated = BiomeScatterAuthoring.Apply(source,
        [
            new BiomeScatterPatch(biome.Id, [BiomeScatterPatchField.Density], Density: .63f),
        ]);
        var edited = updated.Biomes.Single(item => item.Id == biome.Id);

        Assert.Equal(.63f, edited.ScatterDensity);
        Assert.Equal(biome.ScatterResource, edited.ScatterResource);
        Assert.Equal(biome.MinimumAltitude, edited.MinimumAltitude);
        Assert.Equal(biome.MaximumAltitude, edited.MaximumAltitude);
        Assert.Equal(biome.MinimumSlopeDegrees, edited.MinimumSlopeDegrees);
        Assert.Equal(biome.MaximumSlopeDegrees, edited.MaximumSlopeDegrees);
        Assert.Equal(biome.MinimumMoisture, edited.MinimumMoisture);
        Assert.Equal(biome.MaximumMoisture, edited.MaximumMoisture);
        Assert.Equal(biome.Tags, edited.Tags);
        Assert.Equal(biome.ExclusionTags, edited.ExclusionTags);
        Assert.Equal(biome.Priority, edited.Priority);
        Assert.Equal(biome.Provenance, edited.Provenance);
    }
}
