using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Serialization;

internal static class AssetProcessingProfileJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";

    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(IReadOnlyList<AssetProcessingProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var ordered = profiles
            .OrderBy(profile => profile.AssetId.ToString(), StringComparer.Ordinal)
            .ThenBy(profile => profile.Kind)
            .ToArray();
        EnsureValid(ordered);
        return JsonSerializer.Serialize(
            new AssetProcessingProfileDocument(CurrentFormatVersion, ordered),
            Options);
    }

    internal static IReadOnlyList<AssetProcessingProfile> Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<AssetProcessingProfileDocument>(json, Options)
            ?? throw new JsonException("The asset processing profile document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Asset processing profile format '{document.FormatVersion}' is not supported; expected '{CurrentFormatVersion}'.");
        }

        var profiles = document.Profiles?.ToArray() ?? [];
        EnsureValid(profiles);
        return profiles
            .OrderBy(profile => profile.AssetId.ToString(), StringComparer.Ordinal)
            .ThenBy(profile => profile.Kind)
            .ToArray();
    }

    private static void EnsureValid(IReadOnlyList<AssetProcessingProfile> profiles)
    {
        if (profiles.GroupBy(profile => (profile.AssetId, profile.Kind)).Any(group => group.Count() > 1))
        {
            throw new JsonException("Asset processing profiles must be unique by asset and processing kind.");
        }

        var diagnostics = profiles.SelectMany(AssetProcessingProfileValidator.Validate).ToArray();
        if (diagnostics.Length > 0)
        {
            throw new JsonException($"Asset processing profile is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}");
        }
    }

    private sealed record AssetProcessingProfileDocument(
        string FormatVersion,
        IReadOnlyList<AssetProcessingProfile>? Profiles);
}
