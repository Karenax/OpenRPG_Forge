using System.Text.Json;
using Forge.Domain.Spatial;
using Forge.Mcp.Serialization;

namespace Forge.Mcp.Tests.Serialization;

public sealed class McpQuaternionValueJsonConverterTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new McpQuaternionValueJsonConverter());
        return options;
    }

    [Fact]
    public void RoundTripsAllFourComponentsWithCanonicalNames()
    {
        var source = new QuaternionValue(0, .70710677f, 0, .70710677f);

        var json = JsonSerializer.Serialize(source, CreateOptions());
        var restored = JsonSerializer.Deserialize<QuaternionValue>(json, CreateOptions());

        Assert.Equal("{\"x\":0,\"y\":0.70710677,\"z\":0,\"w\":0.70710677}", json);
        Assert.Equal(source, restored);
    }

    [Fact]
    public void ReadsComponentNamesCaseInsensitively()
    {
        var restored = JsonSerializer.Deserialize<QuaternionValue>(
            "{\"X\":0,\"y\":0,\"Z\":0,\"W\":1}", CreateOptions());

        Assert.Equal(QuaternionValue.Identity, restored);
    }

    [Theory]
    [InlineData("{\"x\":0,\"y\":0,\"z\":0}")]
    [InlineData("{\"x\":0,\"y\":0,\"z\":0,\"w\":0}")]
    [InlineData("{\"x\":0,\"y\":0,\"z\":0,\"w\":1,\"xx\":2}")]
    [InlineData("{\"x\":0,\"x\":1,\"y\":0,\"z\":0,\"w\":1}")]
    [InlineData("{\"x\":\"0\",\"y\":0,\"z\":0,\"w\":1}")]
    public void RejectsMalformedAmbiguousOrZeroLengthQuaternions(string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<QuaternionValue>(json, CreateOptions()));
    }
}
