using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public static class AssetCatalogValidator
{
    public const string LicenseUnverifiedCode = "asset.license_unverified";
    public const string HashInvalidCode = "asset.hash_invalid";
    public const string PathUnsafeCode = "asset.path_unsafe";
    public const string ModelMissingCode = "asset.model_path_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(AssetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var item in catalog.Items)
        {
            var path = $"assets/{item.Id}";
            if (!item.LicenseVerified)
            {
                diagnostics.Add(new DomainDiagnostic(
                    LicenseUnverifiedCode,
                    $"Asset '{item.ExternalId}' has no verified license.",
                    $"{path}/license"));
            }

            if (item.ContentSha256.Length != 64 || item.ContentSha256.Any(character => !Uri.IsHexDigit(character)))
            {
                diagnostics.Add(new DomainDiagnostic(
                    HashInvalidCode,
                    $"Asset '{item.ExternalId}' does not have a valid SHA-256 fingerprint.",
                    $"{path}/contentSha256"));
            }

            foreach (var relativePath in new[] { item.RelativeModelPath, item.RelativePreviewPath })
            {
                if (relativePath is not null && !IsSafeRelativePath(relativePath))
                {
                    diagnostics.Add(new DomainDiagnostic(
                        PathUnsafeCode,
                        $"Asset '{item.ExternalId}' contains an unsafe relative path.",
                        $"{path}/path"));
                }
            }

            if (item.Availability != AssetAvailability.CatalogOnly && item.RelativeModelPath is null)
            {
                diagnostics.Add(new DomainDiagnostic(
                    ModelMissingCode,
                    $"Installed asset '{item.ExternalId}' has no supported model path.",
                    $"{path}/relativeModelPath"));
            }
        }

        return diagnostics;
    }

    public static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            return false;
        }

        return !path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment == "..");
    }
}
