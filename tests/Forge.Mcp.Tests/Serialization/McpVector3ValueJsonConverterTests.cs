using System.Text.Json;
using Forge.Domain.Spatial;
using Forge.Mcp.Serialization;

namespace Forge.Mcp.Tests.Serialization;

public sealed class McpVector3ValueJsonConverterTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new McpVector3ValueJsonConverter());
        return options;
    }

    [Fact]
    public void RoundTripsAllThreeComponentsWithCanonicalNames()
    {
        var options = CreateOptions();
        var source = new Vector3Value(12.5f, -2, 3.75f);

        var json = JsonSerializer.Serialize(source, options);
        var restored = JsonSerializer.Deserialize<Vector3Value>(json, options);

        Assert.Equal("{\"x\":12.5,\"y\":-2,\"z\":3.75}", json);
        Assert.Equal(source, restored);
    }

    [Fact]
    public void ReadsComponentNamesCaseInsensitively()
    {
        var restored = JsonSerializer.Deserialize<Vector3Value>("{\"X\":1,\"y\":2,\"Z\":3}", CreateOptions());

        Assert.Equal(new Vector3Value(1, 2, 3), restored);
    }

    [Theory]
    [InlineData("{\"x\":1,\"y\":2}")]
    [InlineData("{\"x\":1,\"y\":2,\"z\":3,\"xx\":4}")]
    [InlineData("{\"x\":1,\"x\":2,\"y\":3,\"z\":4}")]
    [InlineData("{\"x\":\"1\",\"y\":2,\"z\":3}")]
    [InlineData("{\"x\":1e100,\"y\":2,\"z\":3}")]
    public void RejectsMalformedOrAmbiguousVectors(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Vector3Value>(json, CreateOptions()));
    }
}
