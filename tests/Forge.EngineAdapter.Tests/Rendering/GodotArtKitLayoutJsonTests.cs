using Forge.EngineAdapter.Rendering;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class GodotArtKitLayoutJsonTests
{
    [Fact]
    public void DeserializesGenericLayoutWithAllNodeCategories()
    {
        const string json = """
                            {
                              "schemaVersion": 1,
                              "id": "sample-kit",
                              "rootName": "SampleKit",
                              "zones": 2,
                              "floorFills": [
                                { "model": "floor.glb", "minimumX": 0, "maximumX": 4, "minimumZ": 0, "maximumZ": 4 }
                              ],
                              "wallLines": [
                                { "model": "wall.glb", "axis": "x", "fixed": 5, "minimum": 0, "maximum": 4, "yawDegrees": 0 }
                              ],
                              "instances": [
                                { "model": "prop.glb", "position": { "x": 1, "y": 0, "z": 2 } }
                              ],
                              "collisionBoxes": [
                                { "name": "Barrier", "position": { "x": 0, "y": 1, "z": 0 }, "size": { "x": 1, "y": 2, "z": 3 } }
                              ],
                              "lights": [
                                { "name": "Lamp", "position": { "x": 0, "y": 2, "z": 0 }, "color": "ffffff", "energy": 1, "range": 4, "shadows": false }
                              ],
                              "discs": [
                                { "name": "Rune", "position": { "x": 0, "y": 0, "z": 0 }, "radius": 1, "height": 0.1, "color": "ff0000", "emission": true }
                              ]
                            }
                            """;

        var layout = GodotArtKitLayoutJson.Deserialize(json);

        Assert.Equal("sample-kit", layout.Id);
        Assert.Equal(2, layout.Zones);
        Assert.Single(layout.FloorFills);
        Assert.Single(layout.WallLines);
        Assert.Single(layout.Instances);
        Assert.Single(layout.CollisionBoxes);
        Assert.Single(layout.Lights);
        Assert.Single(layout.Discs);
    }

    [Theory]
    [InlineData("../outside.glb")]
    [InlineData("folder/model.glb")]
    [InlineData("model.gltf")]
    public void RejectsUnsafeOrUnsupportedModelPaths(string model)
    {
        var json = $$"""
                     {
                       "schemaVersion": 1,
                       "id": "sample-kit",
                       "rootName": "SampleKit",
                       "zones": 1,
                       "floorFills": [],
                       "wallLines": [],
                       "instances": [
                         { "model": "{{model}}", "position": { "x": 0, "y": 0, "z": 0 } }
                       ],
                       "collisionBoxes": [],
                       "lights": [],
                       "discs": []
                     }
                     """;

        Assert.Throws<InvalidDataException>(() => GodotArtKitLayoutJson.Deserialize(json));
    }
}
