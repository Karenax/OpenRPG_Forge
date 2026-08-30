using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Blueprints;

namespace Forge.Mcp.Serialization;

public sealed class McpBlueprintIdJsonConverter : JsonConverter<BlueprintId>
{
    public override BlueprintId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return Parse(reader.GetString());
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("BlueprintId must be a UUID string or an object containing 'value'.");

        string? value = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return Parse(value);
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("BlueprintId JSON object is malformed.");

            var propertyName = reader.GetString();
            if (!reader.Read())
                throw new JsonException("BlueprintId JSON object is incomplete.");
            if (!string.Equals(propertyName, "value", StringComparison.OrdinalIgnoreCase) ||
                reader.TokenType != JsonTokenType.String || value is not null)
                throw new JsonException("BlueprintId JSON object must contain one UUID 'value' property.");
            value = reader.GetString();
        }

        throw new JsonException("BlueprintId JSON object is incomplete.");
    }

    public override void Write(Utf8JsonWriter writer, BlueprintId value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("value", value.Value);
        writer.WriteEndObject();
    }

    private static BlueprintId Parse(string? value)
    {
        try
        {
            return BlueprintId.Parse(value ?? string.Empty);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new JsonException("BlueprintId value must be a non-empty UUID.", exception);
        }
    }
}
