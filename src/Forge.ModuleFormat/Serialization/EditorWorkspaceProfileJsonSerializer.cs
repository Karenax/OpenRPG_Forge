using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Forge.Domain.Authoring;

namespace Forge.ModuleFormat.Serialization;

public static class EditorWorkspaceProfileJsonSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(EditorWorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, Options);
    }

    public static EditorWorkspaceProfile Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Editor workspace profile is empty.");
        var version = root["schemaVersion"]?.GetValue<int>() ?? 1;
        if (version == 1)
        {
            root["uiScale"] = 1;
            root["textScale"] = 1;
            root["primaryFontFamily"] = "Noto Sans";
            root["fallbackFontFamilies"] = new JsonArray("Segoe UI", "Arial");
            root["personalFavoriteBlueprintIds"] = new JsonArray();
            root["recentBlueprintIds"] = new JsonArray();
        }
        if (version < EditorWorkspaceProfile.CurrentSchemaVersion)
            root["schemaVersion"] = EditorWorkspaceProfile.CurrentSchemaVersion;

        return JsonSerializer.Deserialize<EditorWorkspaceProfile>(root.ToJsonString(), Options) ??
            throw new InvalidDataException("Editor workspace profile is empty.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new EditorWorkspaceProfileIdConverter());
        options.Converters.Add(new AreaIdJsonConverter());
        options.Converters.Add(new BlueprintIdJsonConverter());
        return options;
    }

    private sealed class EditorWorkspaceProfileIdConverter : JsonConverter<EditorWorkspaceProfileId>
    {
        public override EditorWorkspaceProfileId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            EditorWorkspaceProfileId.From(Guid.Parse(reader.GetString() ?? throw new JsonException("Missing profile ID.")));

        public override void Write(Utf8JsonWriter writer, EditorWorkspaceProfileId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
