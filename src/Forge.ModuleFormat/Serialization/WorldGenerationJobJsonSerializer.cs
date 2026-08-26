using System.Text.Json;
using Forge.Application.WorldGeneration;

namespace Forge.ModuleFormat.Serialization;

internal static class WorldGenerationJobJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(WorldGenerationJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        EnsureValid(job);
        return JsonSerializer.Serialize(new WorldGenerationJobDocument(CurrentFormatVersion, job), Options);
    }

    internal static WorldGenerationJob Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<WorldGenerationJobDocument>(json, Options)
            ?? throw new JsonException("The world-generation job document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"World-generation job format '{document.FormatVersion}' is unsupported; expected '{CurrentFormatVersion}'.");
        if (document.Job is null) throw new JsonException("The world-generation job is missing.");
        EnsureValid(document.Job);
        return document.Job;
    }

    private static void EnsureValid(WorldGenerationJob job)
    {
        var diagnostics = WorldGenerationJobValidator.Validate(job);
        if (diagnostics.Count > 0)
            throw new JsonException($"World-generation job is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}");
    }

    private sealed record WorldGenerationJobDocument(string FormatVersion, WorldGenerationJob? Job);
}
