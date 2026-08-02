using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Spatial;

namespace Forge.ModuleFormat.Serialization;

internal sealed class Vector3ValueJsonConverter : JsonConverter<Vector3Value>
{
    public override Vector3Value Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return new Vector3Value(
            root.GetProperty("x").GetSingle(),
            root.GetProperty("y").GetSingle(),
            root.GetProperty("z").GetSingle());
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

internal sealed class QuaternionValueJsonConverter : JsonConverter<QuaternionValue>
{
    public override QuaternionValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        return new QuaternionValue(
            root.GetProperty("x").GetSingle(),
            root.GetProperty("y").GetSingle(),
            root.GetProperty("z").GetSingle(),
            root.GetProperty("w").GetSingle());
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

internal sealed class TransformValueJsonConverter : JsonConverter<TransformValue>
{
    public override TransformValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var position = root.GetProperty("position").Deserialize<Vector3Value>(options);
        var rotation = root.GetProperty("rotation").Deserialize<QuaternionValue>(options);
        var scale = root.GetProperty("scale").Deserialize<Vector3Value>(options);
        return new TransformValue(position, rotation, scale);
    }

    public override void Write(Utf8JsonWriter writer, TransformValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("position");
        JsonSerializer.Serialize(writer, value.Position, options);
        writer.WritePropertyName("rotation");
        JsonSerializer.Serialize(writer, value.Rotation, options);
        writer.WritePropertyName("scale");
        JsonSerializer.Serialize(writer, value.Scale, options);
        writer.WriteEndObject();
    }
}
