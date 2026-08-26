using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class BiomeScatterAuthoringPersistenceTests
{
    [Fact]
    public void AuthoredScatterSettingsRoundTripWithoutChangingNeighbouringAreaSections()
    {
        var areaId = AreaId.From(Guid.Parse("54934975-2ba0-4810-a879-a168cabda801"));
        var source = new AreaDefinition(areaId, "generic-hills", "Generic Hills", [], AreaKind.Exterior);
        var biome = source.World.Biomes[0];
        var world = BiomeScatterAuthoring.Apply(source.World,
        [
            new BiomeScatterPatch(biome.Id,
                [BiomeScatterPatchField.Scale, BiomeScatterPatchField.Tint, BiomeScatterPatchField.Focus,
                    BiomeScatterPatchField.Seed],
                Scale: .84f,
                Tint: new("#aabbcc", .42f),
                Focus: new(8, -3, 55),
                Seed: -912),
        ]);
        var authored = source.WithWorld(world);

        var restored = AreaJsonSerializer.Deserialize(AreaJsonSerializer.Serialize(authored));
        var restoredBiome = restored.World.Biomes.Single(item => item.Id == biome.Id);

        Assert.Equal(new BiomeScatterSettings(.84f, new("#aabbcc", .42f), new(8, -3, 55), -912),
            BiomeScatterAuthoring.Read(restoredBiome));
        var expectedWorld = new AreaWorldProfile(source.World.Id, source.World.SchemaVersion, source.World.Terrain,
            restored.World.Biomes, source.World.Features, source.World.VolumetricNavigation,
            source.World.FlightRoutes, source.World.Loading, source.World.Provenance);
        Assert.Equal(AreaJsonSerializer.Serialize(source.WithWorld(expectedWorld)),
            AreaJsonSerializer.Serialize(restored));
    }
}
