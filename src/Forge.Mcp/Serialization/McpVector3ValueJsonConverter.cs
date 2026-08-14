using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Spatial;

namespace Forge.Mcp.Serialization;

public sealed class McpVector3ValueJsonConverter : JsonConverter<Vector3Value>
{
    public override Vector3Value Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Vector3Value must be a JSON object.");
        }

        float x = 0;
        float y = 0;
        float z = 0;
        var hasX = false;
        var hasY = false;
        var hasZ = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (!hasX || !hasY || !hasZ)
                {
                    throw new JsonException("Vector3Value requires exactly the x, y and z components.");
                }

                return new Vector3Value(x, y, z);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Vector3Value property name expected.");
            }

            var name = reader.GetString();
            var component = name?.ToLowerInvariant() switch
            {
                "x" => 0,
                "y" => 1,
                "z" => 2,
                _ => throw new JsonException($"Unknown Vector3Value component '{name}'."),
            };

            if ((component == 0 && hasX) || (component == 1 && hasY) || (component == 2 && hasZ))
            {
                throw new JsonException($"Duplicate Vector3Value component '{name}'.");
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.Number ||
                !reader.TryGetSingle(out var value) || !float.IsFinite(value))
            {
                throw new JsonException($"Vector3Value component '{name}' must be a finite number.");
            }

            switch (component)
            {
                case 0:
                    x = value;
                    hasX = true;
                    break;
                case 1:
                    y = value;
                    hasY = true;
                    break;
                default:
                    z = value;
                    hasZ = true;
                    break;
            }
        }

        throw new JsonException("Vector3Value JSON object is incomplete.");
    }

    public override void Write(Utf8JsonWriter writer, Vector3Value value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("z", value.Z);
        writer.WriteEndObject();
    }
}
