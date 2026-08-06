using System.Text.Json;
using Forge.Domain.Assistant;

namespace Forge.ModuleFormat.Serialization;

internal static class AssistantWorkspaceProfileJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(AssistantWorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureValid(profile);
        return JsonSerializer.Serialize(new AssistantWorkspaceProfileDocument(CurrentFormatVersion, profile), Options);
    }

    internal static AssistantWorkspaceProfile Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<AssistantWorkspaceProfileDocument>(json, Options)
            ?? throw new JsonException("The assistant workspace profile document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Assistant workspace profile format '{document.FormatVersion}' is unsupported; expected '{CurrentFormatVersion}'.");
        }
        if (document.Profile is null)
        {
            throw new JsonException("The assistant workspace profile is missing.");
        }
        EnsureValid(document.Profile);
        return document.Profile;
    }

    private static void EnsureValid(AssistantWorkspaceProfile profile)
    {
        var diagnostics = AssistantWorkspaceProfileValidator.Validate(profile);
        if (diagnostics.Count > 0)
        {
            throw new JsonException($"Assistant workspace profile is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}");
        }
    }

    private sealed record AssistantWorkspaceProfileDocument(
        string FormatVersion,
        AssistantWorkspaceProfile? Profile);
}
