using System.Text.Json;
using Forge.Domain.Assistant;

namespace Forge.ModuleFormat.Serialization;

internal static class LoreIndexJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(LoreIndexDefinition index)
    {
        ArgumentNullException.ThrowIfNull(index);
        EnsureValid(index);
        return JsonSerializer.Serialize(new LoreIndexDocument(CurrentFormatVersion, index), Options);
    }

    internal static LoreIndexDefinition Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<LoreIndexDocument>(json, Options)
            ?? throw new JsonException("The lore index document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Lore index format '{document.FormatVersion}' is unsupported; expected '{CurrentFormatVersion}'.");
        }
        if (document.Index is null)
        {
            throw new JsonException("The lore index is missing.");
        }
        EnsureValid(document.Index);
        return document.Index;
    }

    private static void EnsureValid(LoreIndexDefinition index)
    {
        var diagnostics = LoreIndexValidator.Validate(index);
        if (diagnostics.Count > 0)
        {
            throw new JsonException($"Lore index is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}");
        }
    }

    private sealed record LoreIndexDocument(string FormatVersion, LoreIndexDefinition? Index);
}
