using System.Text.Json;
using Forge.Domain.Areas;

namespace Forge.ModuleFormat.Serialization;

public static class AreaJsonSerializer
{
    public const string CurrentFormatVersion = "1.2.0";
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
            !string.Equals(document.FormatVersion, PreRenderingProfileFormatVersion, StringComparison.Ordinal) &&
            !string.Equals(document.FormatVersion, PreNavigationFormatVersion, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Area format '{document.FormatVersion}' is not supported; expected '{CurrentFormatVersion}'.");
        }

        return document.Area;
    }
}
