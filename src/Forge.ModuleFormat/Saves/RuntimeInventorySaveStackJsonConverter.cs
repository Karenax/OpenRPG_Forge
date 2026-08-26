using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.ModuleFormat.Saves;

public sealed class RuntimeInventorySaveStackJsonConverter : JsonConverter<RuntimeInventorySaveStack>
{
    public override RuntimeInventorySaveStack Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new RuntimeInventorySaveStack(reader.GetString() ?? string.Empty, 1);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("An inventory stack must be a legacy slug or an object.");
        }

        string? slug = null;
        int? quantity = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("An inventory stack contains an invalid JSON token.");
            }

            var propertyName = reader.GetString();
            if (!reader.Read()) throw new JsonException("An inventory stack property has no value.");
            if (string.Equals(propertyName, "slug", StringComparison.OrdinalIgnoreCase))
            {
                slug = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            }
            else if (string.Equals(propertyName, "quantity", StringComparison.OrdinalIgnoreCase))
            {
                quantity = reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value)
                    ? value
                    : null;
            }
            else
            {
                reader.Skip();
            }
        }

        return new RuntimeInventorySaveStack(slug ?? string.Empty, quantity ?? 0);
    }

    public override void Write(
        Utf8JsonWriter writer,
        RuntimeInventorySaveStack value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(options.PropertyNamingPolicy?.ConvertName(nameof(value.Slug)) ?? nameof(value.Slug),
            value.Slug);
        writer.WriteNumber(options.PropertyNamingPolicy?.ConvertName(nameof(value.Quantity)) ?? nameof(value.Quantity),
            value.Quantity);
        writer.WriteEndObject();
    }
}
