using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record AssetPreviewMetric(string Label, string Value);

public sealed record AssetPreviewPlan(
    AssetCatalogItemId AssetId,
    bool CanRender3D,
    string ThumbnailRelativePath,
    IReadOnlyList<int> TurntableAngles,
    IReadOnlyList<string> Variants,
    IReadOnlyList<string> Animations,
    IReadOnlyList<AssetPreviewMetric> Metrics,
    string LicenseSummary,
    string Provenance,
    IReadOnlyList<string> Diagnostics);

public static class AssetPreviewPlanBuilder
{
    public const string RequiredModelMissingCode = "asset.preview.model_required_missing";

    public static AssetPreviewPlan Build(AssetCatalogItem item, bool modelRequired = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        var canRender = !string.IsNullOrWhiteSpace(item.RelativeModelPath) &&
                        item.Availability != AssetAvailability.CatalogOnly;
        var diagnostics = modelRequired && !canRender
            ? [$"{RequiredModelMissingCode}: assets/{item.Id}/relativeModelPath"]
            : Array.Empty<string>();
        var dimensions = item.TechnicalMetadata.Dimensions;
        var metrics = new List<AssetPreviewMetric>
        {
            new("Taille", FormatBytes(item.TotalBytes)),
            new("Triangles", item.TechnicalMetadata.TriangleCount?.ToString() ?? "non mesuré"),
            new("Dimensions", dimensions is null
                ? "non mesurées"
                : $"{dimensions.Width:0.##} × {dimensions.Height:0.##} × {dimensions.Depth:0.##} m"),
            new("Matériaux", item.TechnicalMetadata.Materials.Count.ToString()),
        };
        return new AssetPreviewPlan(item.Id, canRender,
            $".forge/cache/thumbnails/{item.Id}.png",
            canRender ? Enumerable.Range(0, 8).Select(index => index * 45).ToArray() : [],
            item.TechnicalMetadata.Variants,
            item.TechnicalMetadata.Animations,
            metrics,
            $"{item.LicenseSpdx} · {(item.LicenseVerified ? "vérifiée" : "non vérifiée")} · {item.Author}",
            item.Provenance,
            diagnostics);
    }

    private static string FormatBytes(long bytes) => bytes >= 1_048_576
        ? $"{bytes / 1_048_576d:0.0} MiB"
        : $"{bytes / 1024d:0.0} KiB";
}
