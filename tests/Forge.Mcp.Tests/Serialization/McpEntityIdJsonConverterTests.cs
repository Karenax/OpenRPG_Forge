using System.Text.Json;
using Forge.Domain.Entities;
using Forge.Mcp.Serialization;

namespace Forge.Mcp.Tests.Serialization;

public sealed class McpEntityIdJsonConverterTests
{
    private static readonly Guid Value = Guid.Parse("d6aa3b57-0dd9-45cc-a271-0ae53979198a");

    [Theory]
    [InlineData("\"d6aa3b57-0dd9-45cc-a271-0ae53979198a\"")]
    [InlineData("{\"value\":\"d6aa3b57-0dd9-45cc-a271-0ae53979198a\"}")]
    public void Reads_string_and_object_boundaries(string json)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new McpEntityIdJsonConverter());

        var result = JsonSerializer.Deserialize<EntityId>(json, options);

        Assert.Equal(Value, result!.Value);
    }

    [Fact]
    public void Writes_object_boundary()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new McpEntityIdJsonConverter());

        var json = JsonSerializer.Serialize(EntityId.From(Value), options);

        Assert.Equal("{\"value\":\"d6aa3b57-0dd9-45cc-a271-0ae53979198a\"}", json);
    }
}
