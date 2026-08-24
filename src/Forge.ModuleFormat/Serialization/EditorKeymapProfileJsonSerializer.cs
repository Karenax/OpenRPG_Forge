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
        var migrated = AddMissingCatalogCommands(document.Profile);
        EnsureValid(migrated);
        return migrated;
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

    private static EditorKeymapProfile AddMissingCatalogCommands(EditorKeymapProfile profile)
    {
        var known = profile.Bindings.Select(binding => binding.CommandId).ToHashSet(StringComparer.Ordinal);
        var missing = EditorKeymapCatalog.Commands.Where(command => !known.Contains(command.Id)).ToArray();
        if (missing.Length == 0)
        {
            return profile;
        }

        var preset = profile.Preset == EditorKeymapPreset.Custom
            ? EditorKeymapPreset.Standard
            : profile.Preset;
        var defaults = EditorKeymapProfile.CreatePreset(preset).Bindings
            .ToDictionary(binding => binding.CommandId, StringComparer.Ordinal);
        var used = profile.Bindings.Select(binding => binding.Gesture.CanonicalText)
            .ToHashSet(StringComparer.Ordinal);
        var additions = new List<EditorCommandBinding>();
        foreach (var command in missing)
        {
            var preferred = defaults[command.Id].Gesture;
            var gesture = MigrationGestures(preferred).FirstOrDefault(candidate =>
                !used.Contains(candidate.CanonicalText)) ??
                throw new JsonException($"No conflict-free migration shortcut is available for '{command.Id}'.");
            used.Add(gesture.CanonicalText);
            additions.Add(new EditorCommandBinding(command.Id, command.DisplayName, command.Category, gesture));
        }

        return new EditorKeymapProfile(
            profile.Id,
            profile.SchemaVersion,
            profile.Preset,
            [.. profile.Bindings, .. additions],
            profile.Provenance);
    }

    private static IReadOnlyList<EditorKeyGesture> MigrationGestures(EditorKeyGesture preferred) =>
    [
        preferred,
        new(preferred.Key, ctrl: true, shift: true),
        new(preferred.Key, ctrl: true, alt: true),
        new(preferred.Key, alt: true, shift: true),
        new(preferred.Key, meta: true, shift: true),
    ];

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
