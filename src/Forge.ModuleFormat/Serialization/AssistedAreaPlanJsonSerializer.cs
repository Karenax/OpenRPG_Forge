using System.Text.Json;
using Forge.Domain.Assistant;

namespace Forge.ModuleFormat.Serialization;

internal static class AssistedAreaPlanJsonSerializer
{
    internal const string CurrentFormatVersion = "1.0.0";
    private static readonly JsonSerializerOptions Options = ProjectJsonSerializer.CreateOptions();

    internal static string Serialize(AssistedAreaPlanCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        EnsureValid(catalog);
        return JsonSerializer.Serialize(new AssistedAreaPlanDocument(CurrentFormatVersion, catalog), Options);
    }

    internal static AssistedAreaPlanCatalog Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<AssistedAreaPlanDocument>(json, Options)
            ?? throw new JsonException("The assisted area-plan document is empty.");
        if (!string.Equals(document.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Assisted area-plan format '{document.FormatVersion}' is unsupported; expected '{CurrentFormatVersion}'.");
        if (document.Catalog is null) throw new JsonException("The assisted area-plan catalog is missing.");
        EnsureValid(document.Catalog);
        return document.Catalog;
    }

    private static void EnsureValid(AssistedAreaPlanCatalog catalog)
    {
        var diagnostics = AssistedAreaPlanValidator.Validate(catalog);
        if (diagnostics.Count > 0)
            throw new JsonException($"Assisted area-plan catalog is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}");
    }

    private sealed record AssistedAreaPlanDocument(string FormatVersion, AssistedAreaPlanCatalog? Catalog);
}
