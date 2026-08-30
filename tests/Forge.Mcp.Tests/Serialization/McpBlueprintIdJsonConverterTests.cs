using System.Text.Json;
using Forge.Domain.Blueprints;
using Forge.Mcp.Serialization;

namespace Forge.Mcp.Tests.Serialization;

public sealed class McpBlueprintIdJsonConverterTests
{
    private static readonly BlueprintId Source =
        BlueprintId.Parse("a0290000-0000-4000-8000-000000000097");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new McpBlueprintIdJsonConverter());
        return options;
    }

    [Fact]
    public void RoundTripsTheObjectShapePublishedByTheMcpSchema()
    {
        var json = JsonSerializer.Serialize(Source, CreateOptions());
        var restored = JsonSerializer.Deserialize<BlueprintId>(json, CreateOptions());

        Assert.Equal("{\"value\":\"a0290000-0000-4000-8000-000000000097\"}", json);
        Assert.Equal(Source, restored);
    }

    [Theory]
    [InlineData("\"a0290000-0000-4000-8000-000000000097\"")]
    [InlineData("{\"Value\":\"a0290000-0000-4000-8000-000000000097\"}")]
    public void ReadsSupportedBoundaryShapes(string json)
    {
        Assert.Equal(Source, JsonSerializer.Deserialize<BlueprintId>(json, CreateOptions()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"value\":\"not-a-uuid\"}")]
    [InlineData("{\"value\":\"a0290000-0000-4000-8000-000000000097\",\"extra\":1}")]
    public void RejectsMalformedOrAmbiguousIdentifiers(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BlueprintId>(json, CreateOptions()));
    }
}
