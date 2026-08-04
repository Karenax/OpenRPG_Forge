using System.Text.Json;
using Forge.Domain.Assets;

namespace Forge.ModuleFormat.Serialization;

internal static class AssetExternalEditJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";

    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(IReadOnlyList<AssetExternalEditConfiguration> configurations)
    {
        ArgumentNullException.ThrowIfNull(configurations);
        var ordered = configurations.OrderBy(item => item.AssetId.ToString(), StringComparer.Ordinal).ToArray();
        EnsureValid(ordered);
        return JsonSerializer.Serialize(new Document(CurrentFormatVersion, ordered), Options);
    }

    internal static IReadOnlyList<AssetExternalEditConfiguration> Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options)
            ?? throw new JsonException("The external edit document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"External edit format '{document.FormatVersion}' is unsupported.");
        }

        var configurations = document.Configurations?.ToArray() ?? [];
        EnsureValid(configurations);
        return configurations.OrderBy(item => item.AssetId.ToString(), StringComparer.Ordinal).ToArray();
    }

    private static void EnsureValid(IReadOnlyList<AssetExternalEditConfiguration> configurations)
    {
        if (configurations.GroupBy(item => item.AssetId).Any(group => group.Count() > 1))
        {
            throw new JsonException("External edit configurations must be unique by asset.");
        }

        var diagnostic = configurations.SelectMany(AssetExternalEditValidator.Validate).FirstOrDefault();
        if (diagnostic is not null)
        {
            throw new JsonException($"External edit configuration is invalid: {diagnostic.Code} — {diagnostic.Message}");
        }
    }

    private sealed record Document(
        string FormatVersion,
        IReadOnlyList<AssetExternalEditConfiguration>? Configurations);
}
