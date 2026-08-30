using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public static class AssetCatalogLoader
{
    public const string CatalogMissingCode = "asset.catalog.missing";
    public const string CatalogInvalidCode = "asset.catalog.invalid";

    private static readonly HashSet<string> SupportedModelExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gltf", ".glb", ".obj", ".fbx" };
    private static readonly HashSet<string> SupportedPreviewExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    public static AssetCatalogLoadResult Load(string repositoryRoot, string? vaultRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        vaultRoot = ResolveVaultRoot(repositoryRoot, vaultRoot);
        var unitCatalogPath = Path.Combine(repositoryRoot, "asset_manifests", "catalogs", "cc0_3d_all.json");
        var packCatalogPath = Path.Combine(repositoryRoot, "asset_manifests", "catalogs", "kenney_3d_packs.json");
        var catalogDirectory = Path.GetDirectoryName(unitCatalogPath)!;
        var diagnostics = new List<DomainDiagnostic>();
        if (!File.Exists(unitCatalogPath) || !File.Exists(packCatalogPath))
        {
            diagnostics.Add(new DomainDiagnostic(
                CatalogMissingCode,
                "The versioned CC0 asset catalogs are missing from asset_manifests/catalogs.",
                "asset_manifests/catalogs"));
            return new AssetCatalogLoadResult(
                null,
                repositoryRoot,
                vaultRoot,
                new AssetVaultInstallationState(),
                0,
                diagnostics);
        }

        try
        {
            var items = new List<AssetCatalogItem>();
            var catalogDates = new List<string>();
            var licensePolicies = new List<string>();
            var catalogEntryCount = 0;
            foreach (var catalogPath in Directory.EnumerateFiles(catalogDirectory, "*.json")
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
                var root = document.RootElement;
                EnsureSchema(root, catalogPath);
                catalogDates.Add(ReadString(root, "catalog_date"));
                licensePolicies.Add(ReadString(root, "license_policy"));
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    catalogEntryCount += assets.GetArrayLength();
                    items.AddRange(assets.EnumerateArray().Select(item => MapUnitAsset(item, vaultRoot)));
                }

                if (root.TryGetProperty("packs", out var packs) && packs.ValueKind == JsonValueKind.Array)
                {
                    catalogEntryCount += packs.GetArrayLength();
                    items.AddRange(packs.EnumerateArray().SelectMany(item => MapPack(item, vaultRoot)));
                }
            }

            if (licensePolicies.Any(policy => !string.Equals(policy, "cc0-only", StringComparison.Ordinal)))
            {
                throw new InvalidDataException("Every distributed asset catalog must declare license_policy 'cc0-only'.");
            }

            var installationState = new AssetVaultPackageStore(vaultRoot).LoadState();
            var repositoryIntelligence = AssetIntelligenceIndexStore.LoadDirectory(Path.Combine(
                repositoryRoot,
                "asset_manifests",
                "intelligence"));
            var vaultIntelligence = AssetIntelligenceIndexStore.Load(vaultRoot);
            diagnostics.AddRange(repositoryIntelligence.Diagnostics);
            diagnostics.AddRange(vaultIntelligence.Diagnostics);
            var repositoryProfiles = repositoryIntelligence.Profiles.ToDictionary(
                profile => profile.ExternalId,
                StringComparer.OrdinalIgnoreCase);
            var vaultProfiles = vaultIntelligence.Profiles.ToDictionary(
                profile => profile.ExternalId,
                StringComparer.OrdinalIgnoreCase);
            var installedProfiles = installationState.Packages
                .SelectMany(package => package.Assets)
                .Where(asset => asset.Intelligence is not null)
                .GroupBy(asset => asset.ExternalId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(asset => asset.Intelligence!)
                        .Aggregate(AssetIntelligenceProfileAnalyzer.PreferProfile),
                    StringComparer.OrdinalIgnoreCase);
            items = items.Select(item =>
                {
                    var profile = item.Intelligence;
                    if (vaultProfiles.TryGetValue(item.ExternalId, out var indexed))
                    {
                        profile = AssetIntelligenceProfileAnalyzer.PreferProfile(profile, indexed);
                    }
                    if (repositoryProfiles.TryGetValue(item.ExternalId, out var curated))
                    {
                        profile = AssetIntelligenceProfileAnalyzer.PreferProfile(profile, curated);
                    }
                    if (installedProfiles.TryGetValue(item.ExternalId, out var installed))
                    {
                        profile = AssetIntelligenceProfileAnalyzer.PreferProfile(profile, installed);
                    }
                    return item.WithIntelligence(profile).WithInstalledPacks(
                        installationState.FindPackageIds(item.Id));
                })
                .ToList();
            var catalogDate = catalogDates
                .OrderByDescending(value => value, StringComparer.Ordinal)
                .First();
            var catalog = new AssetCatalog(
                catalogDate,
                "cc0-only",
                vaultRoot,
                items,
                "asset_manifests/packs/cc0_3d_all.json");
            diagnostics.AddRange(AssetCatalogValidator.Validate(catalog));
            return new AssetCatalogLoadResult(
                catalog,
                repositoryRoot,
                vaultRoot,
                installationState,
                catalogEntryCount,
                diagnostics);
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException)
        {
            diagnostics.Add(new DomainDiagnostic(
                CatalogInvalidCode,
                exception.Message,
                "asset_manifests/catalogs"));
            return new AssetCatalogLoadResult(
                null,
                repositoryRoot,
                vaultRoot,
                new AssetVaultInstallationState(),
                0,
                diagnostics);
        }
    }

    public static string? FindRepositoryRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "asset_manifests", "catalogs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public static string ResolveVaultRoot(string repositoryRoot, string? configuredVaultRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var configured = string.IsNullOrWhiteSpace(configuredVaultRoot)
            ? Environment.GetEnvironmentVariable("OPENRPG_ASSET_VAULT")
            : configuredVaultRoot;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        repositoryRoot = Path.GetFullPath(repositoryRoot);
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(repositoryRoot, "..", "..", "OpenRPG_AssetVault")),
            Path.GetFullPath(Path.Combine(repositoryRoot, "..", "OpenRPG_AssetVault")),
            Path.Combine(Path.GetPathRoot(repositoryRoot) ?? repositoryRoot, "OpenRPG_AssetVault"),
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static AssetCatalogItem MapUnitAsset(JsonElement item, string vaultRoot)
    {
        var externalId = ReadString(item, "asset_id");
        var extractedRoot = ReadOptionalString(item, "extracted_root");
        var modelPath = SelectModelPath(item, extractedRoot);
        var previewPath = SelectPreviewPath(item, extractedRoot);
        var availability = ResolveAvailability(vaultRoot, modelPath);
        return CreateItem(
            item,
            externalId,
            ReadString(item, "source_asset_id"),
            ReadStrings(item, "categories"),
            ReadStrings(item, "tags"),
            modelPath,
            previewPath,
            availability,
            ReadOptionalString(item, "thumbnail_url"));
    }

    private static IReadOnlyList<AssetCatalogItem> MapPack(JsonElement item, string vaultRoot)
    {
        var packId = ReadString(item, "pack_id");
        var name = ReadString(item, "name");
        var source = ReadString(item, "source");
        var declaredTags = ReadStrings(item, "tags");
        var tags = declaredTags.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(value => value.Trim().ToLowerInvariant())
            .Append(source)
            .Append("pack")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var declaredCategories = ReadStrings(item, "categories");
        var categories = declaredCategories.Count > 0
            ? declaredCategories.Concat(["3d-model", "pack"]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : ["3d-model", "pack"];
        var extractedRoot = ReadOptionalString(item, "extracted_root");
        if (string.IsNullOrWhiteSpace(extractedRoot) ||
            !item.TryGetProperty("model_files", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return models.EnumerateArray()
            .Select(value => value.GetString())
            .Where(IsSupportedModel)
            .Cast<string>()
            .GroupBy(LogicalName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var relativeModel = group
                    .OrderBy(ModelScore)
                    .ThenBy(value => value, StringComparer.Ordinal)
                    .First();
                var modelPath = CombineRelative(extractedRoot, relativeModel);
                var logicalName = group.Key;
                var externalId = $"{packId}:{Slug(logicalName)}";
                return CreateItem(
                    item,
                    externalId,
                    logicalName,
                    categories,
                    tags.Concat([logicalName]).ToArray(),
                    modelPath,
                    null,
                    ResolveAvailability(vaultRoot, modelPath),
                    null,
                    Humanize(logicalName));
            })
            .ToArray();
    }

    private static AssetCatalogItem CreateItem(
        JsonElement item,
        string externalId,
        string sourceAssetId,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> tags,
        string? modelPath,
        string? previewPath,
        AssetAvailability availability,
        string? thumbnailUrl,
        string? displayName = null)
    {
        var result = new AssetCatalogItem(
            AssetCatalogItemId.ForExternalId(externalId),
            externalId,
            displayName ?? ReadString(item, "name"),
            ReadString(item, "source"),
            sourceAssetId,
            ReadString(item, "source_page"),
            ReadString(item, "license"),
            ReadString(item, "license_url"),
            ReadBoolean(item, "license_verified"),
            FingerprintFiles(item),
            ReadInt64(item, "total_bytes"),
            categories,
            tags,
            ReadString(item, "status"),
            availability,
            modelPath,
            previewPath,
            thumbnailUrl,
            $"asset-catalog:{externalId}",
            author: ReadOptionalString(item, "author"),
            attributionRequired: ReadBoolean(item, "attribution_required"),
            attributionText: ReadOptionalString(item, "attribution_text"),
            technicalMetadata: ReadTechnicalMetadata(item));
        return result.WithIntelligence(AssetIntelligenceProfileAnalyzer.CreateCatalogProfile(result));
    }

    private static AssetTechnicalMetadata ReadTechnicalMetadata(JsonElement item)
    {
        AssetDimensions? dimensions = null;
        if (item.TryGetProperty("dimensions", out var dimensionsValue) &&
            dimensionsValue.ValueKind == JsonValueKind.Object &&
            TryReadDouble(dimensionsValue, "width", out var width) &&
            TryReadDouble(dimensionsValue, "height", out var height) &&
            TryReadDouble(dimensionsValue, "depth", out var depth))
        {
            dimensions = new AssetDimensions(width, height, depth);
        }

        var extracted = ReadStrings(item, "extracted_files");
        var materials = ReadStrings(item, "materials");
        if (materials.Count == 0)
        {
            materials = extracted.Where(path => Path.GetExtension(path).Equals(".mtl", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileNameWithoutExtension).Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>().ToArray();
        }
        var style = ReadOptionalString(item, "style") ?? ReadStrings(item, "tags").FirstOrDefault(tag =>
            tag.Contains("styl", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("realistic", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("low-poly", StringComparison.OrdinalIgnoreCase));
        var variant = ReadOptionalString(item, "variant");
        var variants = ReadStrings(item, "variants").Concat(string.IsNullOrWhiteSpace(variant) ? [] : [variant])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new AssetTechnicalMetadata(
            dimensions: dimensions,
            style: style,
            skeleton: ReadOptionalString(item, "skeleton"),
            materials: materials,
            triangleCount: ReadOptionalInt64(item, "triangle_count"),
            variants: variants,
            animations: ReadStrings(item, "animations"),
            compatibility: ReadStrings(item, "compatibility"));
    }

    private static string? SelectModelPath(JsonElement item, string? extractedRoot)
    {
        if (!string.IsNullOrWhiteSpace(extractedRoot) && item.TryGetProperty("model_files", out var models))
        {
            var model = models.EnumerateArray().Select(value => value.GetString())
                .Where(IsSupportedModel)
                .OrderBy(ModelScore)
                .ThenBy(value => value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(model))
            {
                return CombineRelative(extractedRoot, model);
            }
        }

        if (!item.TryGetProperty("files", out var files))
        {
            return null;
        }

        return files.EnumerateArray()
            .Select(file => ReadOptionalString(file, "path"))
            .Where(IsSupportedModel)
            .OrderBy(ModelScore)
            .ThenBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int ModelScore(string? path) => Path.GetExtension(path ?? string.Empty).ToLowerInvariant() switch
    {
        ".glb" => 0,
        ".gltf" => 1,
        ".fbx" => 2,
        ".obj" => 3,
        _ => 9,
    };

    private static string? SelectPreviewPath(JsonElement item, string? extractedRoot)
    {
        var declaredPreview = ReadOptionalString(item, "preview_file");
        if (IsSupportedPreview(declaredPreview))
        {
            return declaredPreview!.Replace('\\', '/');
        }

        if (!string.IsNullOrWhiteSpace(extractedRoot) && item.TryGetProperty("extracted_files", out var extracted))
        {
            var preview = extracted.EnumerateArray().Select(value => value.GetString())
                .Where(IsSupportedPreview)
                .OrderBy(PreviewScore)
                .ThenBy(value => value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(preview))
            {
                return CombineRelative(extractedRoot, preview);
            }
        }

        if (!item.TryGetProperty("files", out var files))
        {
            return null;
        }

        return files.EnumerateArray()
            .Select(file => ReadOptionalString(file, "path"))
            .Where(IsSupportedPreview)
            .OrderBy(PreviewScore)
            .ThenBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int PreviewScore(string? path)
    {
        var name = Path.GetFileNameWithoutExtension(path ?? string.Empty).ToLowerInvariant();
        if (!name.Contains('_')) return 0;
        if (name.Contains("diff") || name.Contains("color") || name.Contains("albedo")) return 1;
        if (name.Contains("normal") || name.Contains("nor_") || name.Contains("rough") || name.Contains("arm")) return 5;
        return 3;
    }

    private static AssetAvailability ResolveAvailability(string vaultRoot, string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !AssetCatalogValidator.IsSafeRelativePath(modelPath))
        {
            return AssetAvailability.CatalogOnly;
        }

        var absolute = Path.GetFullPath(Path.Combine(vaultRoot, modelPath.Replace('/', Path.DirectorySeparatorChar)));
        var canonicalRoot = Path.GetFullPath(vaultRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute))
        {
            return AssetAvailability.CatalogOnly;
        }

        return modelPath.StartsWith("normalized/", StringComparison.OrdinalIgnoreCase)
            ? AssetAvailability.ExtractedAvailable
            : AssetAvailability.OriginalAvailable;
    }

    private static string FingerprintFiles(JsonElement item)
    {
        var values = item.GetProperty("files").EnumerateArray()
            .Select(file => $"{ReadString(file, "path")}:{ReadString(file, "sha256").ToLowerInvariant()}")
            .OrderBy(value => value, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values))))
            .ToLowerInvariant();
    }

    private static void EnsureSchema(JsonElement root, string path)
    {
        var schema = root.GetProperty("schema_version").GetInt32();
        if (schema != AssetCatalog.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Asset catalog '{path}' uses schema {schema}; expected {AssetCatalog.CurrentSchemaVersion}.");
        }
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
            : [];

    private static string ReadString(JsonElement element, string property) =>
        ReadOptionalString(element, property) ?? throw new InvalidDataException($"Missing string '{property}'.");

    private static string? ReadOptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBoolean(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static long ReadInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var result) ? result : 0;

    private static long? ReadOptionalInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var result) ? result : null;

    private static bool TryReadDouble(JsonElement element, string property, out double result)
    {
        result = 0;
        return element.TryGetProperty(property, out var value) && value.TryGetDouble(out result);
    }

    private static bool IsSupportedModel(string? path) => !string.IsNullOrWhiteSpace(path) &&
        SupportedModelExtensions.Contains(Path.GetExtension(path));

    private static bool IsSupportedPreview(string? path) => !string.IsNullOrWhiteSpace(path) &&
        SupportedPreviewExtensions.Contains(Path.GetExtension(path));

    private static string CombineRelative(string left, string right) =>
        $"{left.TrimEnd('/', '\\')}/{right.TrimStart('/', '\\')}".Replace('\\', '/');

    private static string LogicalName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        while (SupportedModelExtensions.Contains(Path.GetExtension(name)))
        {
            name = Path.GetFileNameWithoutExtension(name);
        }

        return name;
    }

    private static string Humanize(string value) => string.Join(' ',
        value.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private static string Slug(string value)
    {
        var characters = value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        return string.Concat(characters).Trim('-');
    }
}

public sealed record AssetCatalogLoadResult(
    AssetCatalog? Catalog,
    string RepositoryRoot,
    string VaultRoot,
    AssetVaultInstallationState InstallationState,
    int CatalogEntryCount,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Catalog is not null && Diagnostics.Count == 0;
}
