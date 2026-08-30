using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public static class AssetIntelligenceCatalogEnricher
{
    public const string AssetMissingCode = "asset.intelligence.asset_missing";

    public static AssetIntelligenceCatalogPlan Plan(
        AssetCatalog catalog,
        IReadOnlyList<string>? externalIds = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var requested = (externalIds ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var diagnostics = new List<DomainDiagnostic>();
        var items = requested.Length == 0
            ? catalog.Items
            : requested.Select(id =>
                {
                    var item = catalog.Items.SingleOrDefault(candidate =>
                        string.Equals(candidate.ExternalId, id, StringComparison.OrdinalIgnoreCase));
                    if (item is null)
                    {
                        diagnostics.Add(new(
                            AssetMissingCode,
                            $"Catalog asset '{id}' does not exist.",
                            $"assets/intelligence/{id}"));
                    }
                    return item;
                })
                .Where(item => item is not null)
                .Cast<AssetCatalogItem>()
                .ToArray();
        var hashCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var profiles = new List<AssetIntelligenceProfile>(items.Count);
        var analyzed = 0;
        var geometry = 0;
        foreach (var item in items.OrderBy(value => value.ExternalId, StringComparer.Ordinal))
        {
            var profile = item.Intelligence;
            if (!string.IsNullOrWhiteSpace(item.RelativeModelPath) &&
                AssetCatalogValidator.IsSafeRelativePath(item.RelativeModelPath))
            {
                var modelPath = Path.GetFullPath(Path.Combine(
                    catalog.VaultRoot,
                    item.RelativeModelPath.Replace('/', Path.DirectorySeparatorChar)));
                var relative = Path.GetRelativePath(Path.GetFullPath(catalog.VaultRoot), modelPath);
                if (!relative.Equals("..", StringComparison.Ordinal) &&
                    !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !Path.IsPathRooted(relative) && File.Exists(modelPath))
                {
                    profile = AssetIntelligenceProfileAnalyzer.Analyze(item, modelPath, hashCache: hashCache);
                    analyzed++;
                }
            }

            if (profile.Geometry?.Bounds is not null) geometry++;
            diagnostics.AddRange(AssetIntelligenceProfileValidator.Validate(
                profile,
                $"assets/intelligence/{profile.ExternalId}"));
            profiles.Add(profile);
        }

        return new AssetIntelligenceCatalogPlan(
            profiles.OrderBy(item => item.ExternalId, StringComparer.Ordinal).ToArray(),
            items.Count,
            analyzed,
            geometry,
            diagnostics);
    }
}

public sealed record AssetIntelligenceCatalogPlan(
    IReadOnlyList<AssetIntelligenceProfile> Profiles,
    int ProfileCount,
    int ModelAnalyzedCount,
    int GeometryProfileCount,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool CanApply => Diagnostics.Count == 0;
}
