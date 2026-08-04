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
        var diagnostics = new List<DomainDiagnostic>();
        if (!File.Exists(unitCatalogPath) || !File.Exists(packCatalogPath))
        {
            diagnostics.Add(new DomainDiagnostic(
                CatalogMissingCode,
                "The versioned CC0 asset catalogs are missing from asset_manifests/catalogs.",
                "asset_manifests/catalogs"));
            return new AssetCatalogLoadResult(null, repositoryRoot, vaultRoot, diagnostics);
        }

        try
        {
            using var unitDocument = JsonDocument.Parse(File.ReadAllText(unitCatalogPath));
            using var packDocument = JsonDocument.Parse(File.ReadAllText(packCatalogPath));
            var unitRoot = unitDocument.RootElement;
            var packRoot = packDocument.RootElement;
            EnsureSchema(unitRoot, unitCatalogPath);
            EnsureSchema(packRoot, packCatalogPath);
            var items = new List<AssetCatalogItem>();
            items.AddRange(unitRoot.GetProperty("assets").EnumerateArray()
                .Select(item => MapUnitAsset(item, vaultRoot)));
            items.AddRange(packRoot.GetProperty("packs").EnumerateArray()
                .Select(item => MapPack(item, vaultRoot)));
            var catalogDate = new[] { ReadString(unitRoot, "catalog_date"), ReadString(packRoot, "catalog_date") }
                .OrderByDescending(value => value, StringComparer.Ordinal)
                .First();
            var catalog = new AssetCatalog(
                catalogDate,
                ReadString(unitRoot, "license_policy"),
                vaultRoot,
                items,
                "asset_manifests/packs/cc0_3d_all.json");
            diagnostics.AddRange(AssetCatalogValidator.Validate(catalog));
            return new AssetCatalogLoadResult(catalog, repositoryRoot, vaultRoot, diagnostics);
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException)
        {
            diagnostics.Add(new DomainDiagnostic(
                CatalogInvalidCode,
                exception.Message,
                "asset_manifests/catalogs"));
            return new AssetCatalogLoadResult(null, repositoryRoot, vaultRoot, diagnostics);
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

    private static AssetCatalogItem MapPack(JsonElement item, string vaultRoot)
    {
        var externalId = ReadString(item, "pack_id");
        var name = ReadString(item, "name");
        var tags = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim().ToLowerInvariant())
            .Append("kenney")
            .Append("pack")
            .ToArray();
        var modelPath = SelectModelPath(item, ReadOptionalString(item, "extracted_root"));
        return CreateItem(
            item,
            externalId,
            externalId.Split(':').Last(),
            ["3d-model", "pack"],
            tags,
            modelPath,
            null,
            ResolveAvailability(vaultRoot, modelPath),
            null);
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
        string? thumbnailUrl) => new(
        AssetCatalogItemId.ForExternalId(externalId),
        externalId,
        ReadString(item, "name"),
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
        $"asset-catalog:{externalId}");

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
        ".obj" => 2,
        ".fbx" => 3,
        _ => 9,
    };

    private static string? SelectPreviewPath(JsonElement item, string? extractedRoot)
    {
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

    private static bool IsSupportedModel(string? path) => !string.IsNullOrWhiteSpace(path) &&
        SupportedModelExtensions.Contains(Path.GetExtension(path));

    private static bool IsSupportedPreview(string? path) => !string.IsNullOrWhiteSpace(path) &&
        SupportedPreviewExtensions.Contains(Path.GetExtension(path));

    private static string CombineRelative(string left, string right) =>
        $"{left.TrimEnd('/', '\\')}/{right.TrimStart('/', '\\')}".Replace('\\', '/');
}

public sealed record AssetCatalogLoadResult(
    AssetCatalog? Catalog,
    string RepositoryRoot,
    string VaultRoot,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Catalog is not null && Diagnostics.Count == 0;
}
