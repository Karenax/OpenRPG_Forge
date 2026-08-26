using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Spatial;

namespace Forge.Mcp.Serialization;

public sealed class McpQuaternionValueJsonConverter : JsonConverter<QuaternionValue>
{
    public override QuaternionValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("QuaternionValue must be a JSON object.");

        var values = new float[4];
        var present = new bool[4];
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (present.Any(value => !value))
                    throw new JsonException("QuaternionValue requires exactly the x, y, z and w components.");
                try
                {
                    return new QuaternionValue(values[0], values[1], values[2], values[3]);
                }
                catch (ArgumentException exception)
                {
                    throw new JsonException("QuaternionValue must have finite non-zero length.", exception);
                }
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("QuaternionValue property name expected.");
            var name = reader.GetString();
            var component = name?.ToLowerInvariant() switch
            {
                "x" => 0,
                "y" => 1,
                "z" => 2,
                "w" => 3,
                _ => throw new JsonException($"Unknown QuaternionValue component '{name}'."),
            };
            if (present[component])
                throw new JsonException($"Duplicate QuaternionValue component '{name}'.");
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number ||
                !reader.TryGetSingle(out var value) || !float.IsFinite(value))
                throw new JsonException($"QuaternionValue component '{name}' must be a finite number.");
            values[component] = value;
            present[component] = true;
        }

        throw new JsonException("QuaternionValue JSON object is incomplete.");
    }

    public override void Write(Utf8JsonWriter writer, QuaternionValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("z", value.Z);
        writer.WriteNumber("w", value.W);
        writer.WriteEndObject();
    }
}
