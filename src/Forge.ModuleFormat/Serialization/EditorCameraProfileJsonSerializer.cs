using System.Text.Json;
using Forge.Domain.Authoring;

namespace Forge.ModuleFormat.Serialization;

internal static class EditorCameraProfileJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";

    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(EditorCameraProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(
            new EditorCameraProfileDocument(CurrentFormatVersion, profile),
            Options);
    }

    internal static EditorCameraProfile Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<EditorCameraProfileDocument>(json, Options)
            ?? throw new JsonException("The editor camera profile is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Editor camera profile format '{document.FormatVersion}' is not supported; expected '{CurrentFormatVersion}'.");
        }

        return document.Profile;
    }

    private sealed record EditorCameraProfileDocument(
        string FormatVersion,
        EditorCameraProfile Profile);
}
