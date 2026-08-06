using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Authoring;

namespace Forge.ModuleFormat.Serialization;

internal static class EditorKeymapProfileJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = CreateOptions();

    internal static string Serialize(EditorKeymapProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureValid(profile);
        return JsonSerializer.Serialize(new Document(CurrentFormatVersion, profile), Options);
    }

    internal static EditorKeymapProfile Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new JsonException("The editor keymap document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
            throw new NotSupportedException($"Editor keymap format '{document.FormatVersion}' is unsupported.");
        if (document.Profile is null) throw new JsonException("The editor keymap profile is missing.");
        EnsureValid(document.Profile);
        return document.Profile;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = ProjectJsonSerializer.CreateOptions();
        options.Converters.Add(new EditorKeymapProfileIdConverter());
        return options;
    }

    private static void EnsureValid(EditorKeymapProfile profile)
    {
        var diagnostic = EditorKeymapProfileValidator.Validate(profile).FirstOrDefault();
        if (diagnostic is not null)
            throw new JsonException($"Editor keymap is invalid: {diagnostic.Code} - {diagnostic.Message}");
    }

    private sealed record Document(string FormatVersion, EditorKeymapProfile? Profile);

    private sealed class EditorKeymapProfileIdConverter : JsonConverter<EditorKeymapProfileId>
    {
        public override EditorKeymapProfileId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            EditorKeymapProfileId.From(Guid.Parse(reader.GetString() ?? throw new JsonException("Missing keymap ID.")));

        public override void Write(Utf8JsonWriter writer, EditorKeymapProfileId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
