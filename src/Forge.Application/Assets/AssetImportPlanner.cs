using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.Application.Assets;

public static class AssetImportPlanner
{
    public const string LicenseBlockedCode = "asset.import.license_blocked";
    public const string SourceUnavailableCode = "asset.import.source_unavailable";
    public const string PathUnsafeCode = "asset.import.path_unsafe";

    public static AssetImportPlanResult Plan(AssetCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var diagnostics = new List<DomainDiagnostic>();
        if (!item.LicenseVerified)
        {
            diagnostics.Add(new DomainDiagnostic(
                LicenseBlockedCode,
                $"Import of '{item.ExternalId}' is blocked until its license is verified.",
                $"assets/{item.Id}/license"));
        }

        if (item.Availability == AssetAvailability.CatalogOnly || item.RelativeModelPath is null)
        {
            diagnostics.Add(new DomainDiagnostic(
                SourceUnavailableCode,
                $"Asset '{item.ExternalId}' is catalogued but its model is not installed locally.",
                $"assets/{item.Id}/availability"));
        }
        else if (!AssetCatalogValidator.IsSafeRelativePath(item.RelativeModelPath))
        {
            diagnostics.Add(new DomainDiagnostic(
                PathUnsafeCode,
                $"Asset '{item.ExternalId}' has an unsafe source path.",
                $"assets/{item.Id}/relativeModelPath"));
        }

        var slug = string.Concat(item.ExternalId.ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        var plan = diagnostics.Count == 0
            ? new AssetImportPlan(
                AssetImportPlan.CurrentSchemaVersion,
                item.Id,
                item.RelativeModelPath!,
                $"assets/imported/{slug}",
                item.ContentSha256,
                item.LicenseSpdx,
                item.Provenance,
                true)
            : null;
        return new AssetImportPlanResult(plan, diagnostics);
    }
}

public sealed record AssetImportPlan(
    int SchemaVersion,
    AssetCatalogItemId AssetId,
    string SourceRelativePath,
    string DestinationRelativeDirectory,
    string ContentSha256,
    string LicenseSpdx,
    string Provenance,
    bool DryRun)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AssetImportPlanResult(
    AssetImportPlan? Plan,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Plan is not null && Diagnostics.Count == 0;
}
