using System.Text.Json;
using Forge.Domain.Areas;

namespace Forge.ModuleFormat.Serialization;

public static class AreaJsonSerializer
{
    public const string CurrentFormatVersion = "1.10.0";
    public const string PreAreaUserEventFormatVersion = "1.9.0";
    public const string PreTerrainPaintFormatVersion = "1.8.0";
    public const string PreAreaExperienceFormatVersion = "1.7.0";
    public const string PreRulesProfileFormatVersion = "1.6.0";
    public const string PreCinematicFormatVersion = "1.5.0";
    public const string PreRuntimePolicyFormatVersion = "1.4.0";
    public const string PreWorldProfileFormatVersion = "1.3.0";
    public const string PreClimateProfileFormatVersion = "1.2.0";
    public const string PreRenderingProfileFormatVersion = "1.1.0";
    public const string PreNavigationFormatVersion = "1.0.0";

    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    public static string Serialize(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        return JsonSerializer.Serialize(new AreaDocument(CurrentFormatVersion, area), Options);
    }

    public static AreaDefinition Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<AreaDocument>(json, Options)
            ?? throw new JsonException("The area document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreAreaUserEventFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreTerrainPaintFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreAreaExperienceFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreRulesProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreCinematicFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreRuntimePolicyFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreWorldProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreClimateProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreRenderingProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreNavigationFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Area format '{document.FormatVersion}' is not supported; expected '{CurrentFormatVersion}'.");
        }

        return document.Area;
    }
}
