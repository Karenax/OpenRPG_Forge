using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Entities;

namespace Forge.Mcp.Serialization;

public sealed class McpEntityIdJsonConverter : JsonConverter<EntityId>
{
    public override EntityId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return Parse(reader.GetString());
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("EntityId must be a UUID string or an object containing 'value'.");

        string? value = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return Parse(value);
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("EntityId JSON object is malformed.");

            var propertyName = reader.GetString();
            if (!reader.Read())
                throw new JsonException("EntityId JSON object is incomplete.");
            if (!string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase) ||
                reader.TokenType != JsonTokenType.String || value is not null)
                throw new JsonException("EntityId JSON object must contain one UUID 'value' property.");
            value = reader.GetString();
        }

        throw new JsonException("EntityId JSON object is incomplete.");
    }

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("value", value.Value);
        writer.WriteEndObject();
    }

    private static EntityId Parse(string? value)
    {
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
            throw new JsonException("EntityId value must be a non-empty UUID.");
        return EntityId.From(parsed);
    }
}
