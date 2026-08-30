using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Forge.Studio.Core.Assets;

public static class StudioAssetLibraryLoader
{
    private static readonly HashSet<string> ModelExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".glb", ".gltf", ".fbx", ".obj" };

    private static readonly HashSet<string> PreviewExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    public static StudioAssetLibraryIndex Load(string repositoryRoot, string vaultRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        vaultRoot = Path.GetFullPath(vaultRoot);
        var catalogDirectory = Path.Combine(repositoryRoot, "asset_manifests", "catalogs");
        if (!Directory.Exists(catalogDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Le dossier de catalogues est introuvable : {catalogDirectory}");
        }
        if (!Directory.Exists(vaultRoot))
        {
            throw new DirectoryNotFoundException(
                $"Le Vault de modèles est introuvable : {vaultRoot}");
        }

        var collections = LoadPackCollections(repositoryRoot);
        var intelligence = LoadIntelligenceProfiles(repositoryRoot, vaultRoot);
        var items = new Dictionary<string, StudioAssetLibraryItem>(StringComparer.OrdinalIgnoreCase);
        var catalogEntryCount = 0;
        foreach (var catalogPath in Directory.EnumerateFiles(catalogDirectory, "*.json")
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            var root = document.RootElement;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    catalogEntryCount++;
                    var item = MapAsset(asset, vaultRoot, intelligence);
                    if (item is not null) Add(items, item);
                }
            }

            if (root.TryGetProperty("packs", out var packs) && packs.ValueKind == JsonValueKind.Array)
            {
                foreach (var pack in packs.EnumerateArray())
                {
                    catalogEntryCount++;
                    var packId = ReadOptionalString(pack, "pack_id");
                    var collection = !string.IsNullOrWhiteSpace(packId) &&
                                     collections.TryGetValue(packId, out var configured)
                        ? configured
                        : null;
                    foreach (var item in MapPack(pack, vaultRoot, intelligence))
                    {
                        Add(items, collection is null ? item : ApplyCollection(item, collection));
                    }
                }
            }
        }

        return new StudioAssetLibraryIndex(
            repositoryRoot,
            vaultRoot,
            items.Values
                .OrderBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.PackName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ExternalId, StringComparer.Ordinal)
                .ToArray(),
            catalogEntryCount);
    }

    private static IReadOnlyDictionary<string, StudioAssetPackCollection> LoadPackCollections(
        string repositoryRoot)
    {
        var result = new Dictionary<string, StudioAssetPackCollection>(StringComparer.OrdinalIgnoreCase);
        var directory = Path.Combine(repositoryRoot, "asset_manifests", "packs");
        if (!Directory.Exists(directory)) return result;

        foreach (var path in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (!string.Equals(
                    ReadOptionalString(root, "collection_type"),
                    "asset-vault-collection",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!root.TryGetProperty("schema_version", out var schema) || schema.GetInt32() != 1)
            {
                throw new InvalidDataException($"Le pack de collection utilise un schéma non pris en charge : {path}");
            }
            var packId = ReadOptionalString(root, "pack_id");
            var displayName = ReadOptionalString(root, "display_name");
            var sourcePackIds = ReadStrings(root, "source_pack_ids");
            if (string.IsNullOrWhiteSpace(packId) || string.IsNullOrWhiteSpace(displayName) ||
                sourcePackIds.Count == 0)
            {
                throw new InvalidDataException($"Le pack de collection est incomplet : {path}");
            }

            var collection = new StudioAssetPackCollection(packId, displayName, ReadStrings(root, "tags"));
            foreach (var sourcePackId in sourcePackIds)
            {
                if (!result.TryAdd(sourcePackId, collection))
                {
                    throw new InvalidDataException(
                        $"Le pack source '{sourcePackId}' appartient à plusieurs collections Asset Vault.");
                }
            }
        }
        return result;
    }

    private static StudioAssetLibraryItem ApplyCollection(
        StudioAssetLibraryItem item,
        StudioAssetPackCollection collection) => new(
        item.ExternalId,
        item.DisplayName,
        item.Source,
        collection.PackId,
        collection.DisplayName,
        item.Kind,
        item.Format,
        item.RelativeModelPath,
        item.RelativePreviewPath,
        item.SourcePage,
        item.LicenseSpdx,
        item.LicenseUrl,
        item.LicenseVerified,
        item.Author,
        item.AttributionRequired,
        item.AttributionText,
        Normalize(item.Tags.Concat(collection.Tags)),
        item.Categories,
        item.Variants,
        item.FileSize,
        item.AvailableLocally,
        item.Description,
        item.SemanticType,
        item.Family,
        item.AssemblyRole,
        item.PlacementSurface,
        item.PivotClass,
        item.WidthMeters,
        item.HeightMeters,
        item.DepthMeters,
        item.Readiness,
        item.ReviewStatus,
        item.MetadataConfidence,
        item.PivotKind,
        item.PivotXMeters,
        item.PivotYMeters,
        item.PivotZMeters,
        item.PivotRequiresReview);

    private static StudioAssetLibraryItem? MapAsset(
        JsonElement asset,
        string vaultRoot,
        IReadOnlyDictionary<string, StudioAssetIntelligence> intelligence)
    {
        var externalId = ReadOptionalString(asset, "asset_id");
        var source = ReadOptionalString(asset, "source");
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(source)) return null;

        var extractedRoot = ReadOptionalString(asset, "extracted_root");
        var variants = new List<StudioAssetVariant>();
        if (asset.TryGetProperty("model_files", out var modelFiles) &&
            modelFiles.ValueKind == JsonValueKind.Array &&
            !string.IsNullOrWhiteSpace(extractedRoot))
        {
            variants.AddRange(modelFiles.EnumerateArray()
                .Select(value => value.GetString())
                .Where(path => IsModel(path))
                .Select(path => CreateVariant(vaultRoot, CombineRelative(extractedRoot!, path!))));
        }
        if (asset.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            variants.AddRange(files.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Object)
                .Select(value => ReadOptionalString(value, "path"))
                .Where(path => IsModel(path))
                .Select(path => CreateVariant(vaultRoot, path!)));
        }

        variants = DistinctVariants(variants);
        if (variants.Count == 0) return null;
        var selected = SelectVariant(variants);
        var preview = ResolveUnitPreview(asset, extractedRoot, vaultRoot);
        var sourceAssetId = ReadOptionalString(asset, "source_asset_id") ?? externalId.Split(':').Last();
        return CreateItem(
            asset,
            externalId,
            ReadOptionalString(asset, "name") ?? Humanize(sourceAssetId),
            SourceLabel(source),
            null,
            null,
            ReadOptionalString(asset, "kind") ?? "model",
            selected,
            preview,
            ReadStrings(asset, "tags"),
            ReadStrings(asset, "categories"),
            variants,
            vaultRoot,
            intelligence);
    }

    private static IReadOnlyList<StudioAssetLibraryItem> MapPack(
        JsonElement pack,
        string vaultRoot,
        IReadOnlyDictionary<string, StudioAssetIntelligence> intelligence)
    {
        var packId = ReadOptionalString(pack, "pack_id");
        if (string.IsNullOrWhiteSpace(packId)) return [];
        var sourceId = ReadOptionalString(pack, "source") ?? packId.Split(':')[0];
        var packSlug = packId.Contains(':', StringComparison.Ordinal)
            ? packId[(packId.IndexOf(':', StringComparison.Ordinal) + 1)..]
            : packId;
        var packName = ReadOptionalString(pack, "name") ?? Humanize(packSlug);
        var extractedRoot = ReadOptionalString(pack, "extracted_root") ??
                            $"normalized/{sourceId.ToLowerInvariant()}/{packSlug}";
        if (!pack.TryGetProperty("model_files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var grouped = files.EnumerateArray()
            .Select(value => value.GetString())
            .Where(path => IsModel(path))
            .GroupBy(path => LogicalName(path!), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var previews = BuildPreviewMap(pack, extractedRoot, vaultRoot);
        var fallbackPreview = FirstExistingRelative(
            vaultRoot,
            CombineRelative(extractedRoot, "Preview.png"),
            CombineRelative(extractedRoot, "Sample.png"));
        var packTags = ReadStrings(pack, "tags");
        var packCategories = ReadStrings(pack, "categories");
        var result = new List<StudioAssetLibraryItem>();
        foreach (var group in grouped)
        {
            var variants = DistinctVariants(group
                .Select(path => CreateVariant(vaultRoot, CombineRelative(extractedRoot, path!))));
            if (variants.Count == 0) continue;
            var selected = SelectVariant(variants);
            var sidecar = Path.ChangeExtension(selected.RelativePath, null) + ".preview.png";
            var logicalDirectory = Path.GetDirectoryName(selected.RelativePath) ?? string.Empty;
            var logicalSidecar = CombineRelative(logicalDirectory, $"{group.Key}.preview.png");
            var preview = FirstExistingRelative(
                vaultRoot,
                sidecar,
                logicalSidecar,
                previews.GetValueOrDefault(group.Key),
                fallbackPreview);
            var isAnimation = packName.Contains("animation library", StringComparison.OrdinalIgnoreCase) ||
                              selected.RelativePath.Split('/').Any(part =>
                                  part.StartsWith("animation", StringComparison.OrdinalIgnoreCase));
            var logicalName = group.Key;
            var id = $"{packId}:{Slug(logicalName)}";
            result.Add(CreateItem(
                pack,
                id,
                Humanize(logicalName),
                SourceLabel(sourceId),
                packId,
                packName,
                isAnimation ? "animation" : "model",
                selected,
                preview,
                packTags.Concat([packSlug, logicalName]).ToArray(),
                packCategories.Concat([isAnimation ? "animation" : "3d-model"]).ToArray(),
                variants,
                vaultRoot,
                intelligence));
        }
        return result;
    }

    private static StudioAssetLibraryItem CreateItem(
        JsonElement source,
        string externalId,
        string displayName,
        string sourceLabel,
        string? packId,
        string? packName,
        string kind,
        StudioAssetVariant selected,
        string? preview,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> categories,
        IReadOnlyList<StudioAssetVariant> variants,
        string vaultRoot,
        IReadOnlyDictionary<string, StudioAssetIntelligence> intelligence)
    {
        var metadata = intelligence.TryGetValue(externalId, out var enriched)
            ? enriched
            : StudioAssetIntelligence.CreateFallback(externalId, displayName, sourceLabel, tags, categories);
        return new StudioAssetLibraryItem(
            externalId,
            displayName,
            sourceLabel,
            packId,
            packName,
            kind,
            selected.Format,
            selected.RelativePath,
            preview,
            ReadOptionalString(source, "source_page") ?? string.Empty,
            ReadOptionalString(source, "license") ?? "unknown",
            ReadOptionalString(source, "license_url") ??
                ReadOptionalString(source, "source_page") ?? string.Empty,
            ReadOptionalBoolean(source, "license_verified"),
            ReadOptionalString(source, "author") ?? sourceLabel,
            ReadOptionalBoolean(source, "attribution_required"),
            ReadOptionalString(source, "attribution_text") ?? string.Empty,
            Normalize(tags),
            Normalize(categories),
            variants,
            selected.FileSize,
            selected.AvailableLocally,
            metadata.Description,
            metadata.SemanticType,
            metadata.Family,
            metadata.AssemblyRole,
            metadata.PlacementSurface,
            metadata.PivotClass,
            metadata.WidthMeters,
            metadata.HeightMeters,
            metadata.DepthMeters,
            metadata.Readiness,
            metadata.ReviewStatus,
            metadata.Confidence,
            metadata.PivotKind,
            metadata.PivotXMeters,
            metadata.PivotYMeters,
            metadata.PivotZMeters,
            metadata.PivotRequiresReview);
    }

    private static string? ResolveUnitPreview(JsonElement asset, string? extractedRoot, string vaultRoot)
    {
        var declared = ReadOptionalString(asset, "preview_file");
        if (!string.IsNullOrWhiteSpace(declared) && IsPreview(declared))
        {
            var direct = FirstExistingRelative(vaultRoot, declared);
            if (direct is not null) return direct;
        }
        if (!string.IsNullOrWhiteSpace(extractedRoot))
        {
            var sourceAssetId = ReadOptionalString(asset, "source_asset_id") ?? string.Empty;
            var local = FirstExistingRelative(
                vaultRoot,
                CombineRelative(extractedRoot, $"{sourceAssetId}.png"),
                CombineRelative(extractedRoot, "Preview.png"),
                CombineRelative(extractedRoot, "Sample.png"));
            if (local is not null) return local;
        }
        return null;
    }

    private static Dictionary<string, string> BuildPreviewMap(
        JsonElement pack,
        string extractedRoot,
        string vaultRoot)
    {
        var candidates = ReadStrings(pack, "preview_files").ToList();
        if (pack.TryGetProperty("extracted_files", out var extracted) &&
            extracted.ValueKind == JsonValueKind.Array)
        {
            candidates.AddRange(extracted.EnumerateArray()
                .Select(value => value.GetString())
                .Where(value => IsPreview(value) &&
                                value!.Split('/').Any(part =>
                                    string.Equals(part, "Previews", StringComparison.OrdinalIgnoreCase)))
                .Cast<string>());
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var relative = CombineRelative(extractedRoot, candidate);
            if (FirstExistingRelative(vaultRoot, relative) is { } existing)
            {
                result.TryAdd(Path.GetFileNameWithoutExtension(candidate), existing);
            }
        }
        return result;
    }

    private static StudioAssetVariant CreateVariant(string vaultRoot, string relativePath)
    {
        relativePath = NormalizeRelative(relativePath);
        var absolute = ResolveContained(vaultRoot, relativePath);
        var available = absolute is not null && File.Exists(absolute);
        return new StudioAssetVariant(
            Path.GetExtension(relativePath).TrimStart('.').ToLowerInvariant(),
            relativePath,
            available,
            available ? new FileInfo(absolute!).Length : 0);
    }

    private static StudioAssetVariant SelectVariant(IEnumerable<StudioAssetVariant> variants) => variants
        .OrderByDescending(variant => variant.AvailableLocally)
        .ThenBy(variant => FormatPriority(variant.Format))
        .ThenBy(variant => variant.RelativePath, StringComparer.OrdinalIgnoreCase)
        .First();

    private static List<StudioAssetVariant> DistinctVariants(IEnumerable<StudioAssetVariant> variants) => variants
        .DistinctBy(variant => variant.RelativePath, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static int FormatPriority(string format) => format.ToLowerInvariant() switch
    {
        "glb" => 0,
        "gltf" => 1,
        "fbx" => 2,
        "obj" => 3,
        _ => 9,
    };

    private static void Add(IDictionary<string, StudioAssetLibraryItem> items, StudioAssetLibraryItem item)
    {
        if (items.TryAdd(item.ExternalId, item)) return;
        if (items[item.ExternalId].RelativeModelPath.Equals(
                item.RelativeModelPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.RelativeModelPath)))
            .ToLowerInvariant()[..8];
        items[$"{item.ExternalId}--{digest}"] = item with { ExternalId = $"{item.ExternalId}--{digest}" };
    }

    private static string? FirstExistingRelative(string vaultRoot, params string?[] candidates)
    {
        foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var normalized = NormalizeRelative(candidate!);
            var absolute = ResolveContained(vaultRoot, normalized);
            if (absolute is not null && File.Exists(absolute)) return normalized;
        }
        return null;
    }

    private static string? ResolveContained(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;
        var normalized = NormalizeRelative(relativePath);
        if (normalized.Split('/').Any(part => part is ".." or "." or "")) return null;
        var absoluteRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        return candidate.StartsWith(absoluteRoot, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }

    private static string CombineRelative(string left, string right) =>
        NormalizeRelative($"{left.TrimEnd('/', '\\')}/{right.TrimStart('/', '\\')}");

    private static string NormalizeRelative(string value) => value.Replace('\\', '/').Trim('/');

    private static bool IsModel(string? path) =>
        !string.IsNullOrWhiteSpace(path) && ModelExtensions.Contains(Path.GetExtension(path));

    private static bool IsPreview(string? path) =>
        !string.IsNullOrWhiteSpace(path) && PreviewExtensions.Contains(Path.GetExtension(path));

    private static string? ReadOptionalString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool ReadOptionalBoolean(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> ReadStrings(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray()
            : [];

    private static IReadOnlyList<string> Normalize(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static string SourceLabel(string source) => source.ToLowerInvariant() switch
    {
        "ambientcg" => "ambientCG",
        "kaykit" => "KayKit",
        "kenney" => "Kenney",
        "polyhaven" => "Poly Haven",
        "quaternius" => "Quaternius",
        _ => Humanize(source),
    };

    private static string Humanize(string value) => string.Join(' ',
        value.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Length == 0
                ? part
                : char.ToUpperInvariant(part[0]) + part[1..]));

    private static string Slug(string value)
    {
        var characters = value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        return string.Concat(characters).Trim('-');
    }

    private static string LogicalName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        while (ModelExtensions.Contains(Path.GetExtension(name)))
        {
            name = Path.GetFileNameWithoutExtension(name);
        }
        return name;
    }

    private static IReadOnlyDictionary<string, StudioAssetIntelligence> LoadIntelligenceProfiles(
        string repositoryRoot,
        string vaultRoot)
    {
        var result = new Dictionary<string, StudioAssetIntelligence>(StringComparer.OrdinalIgnoreCase);
        var paths = new List<string>();
        var overlayDirectory = Path.Combine(repositoryRoot, "asset_manifests", "intelligence");
        if (Directory.Exists(overlayDirectory))
        {
            paths.AddRange(Directory.EnumerateFiles(overlayDirectory, "*.json"));
        }
        var vaultIndex = Path.Combine(vaultRoot, "metadata", "asset-intelligence.v1.json");
        if (File.Exists(vaultIndex)) paths.Add(vaultIndex);
        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("schema_version", out var schema) ||
                !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1 ||
                !document.RootElement.TryGetProperty("profiles", out var profiles) ||
                profiles.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Le profil d’intelligence d’assets est invalide : {path}");
            }
            foreach (var profile in profiles.EnumerateArray()) AddIntelligence(result, profile);
        }

        var installationState = Path.Combine(vaultRoot, "metadata", "asset-vault-installations.v1.json");
        if (File.Exists(installationState))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(installationState));
            if (document.RootElement.TryGetProperty("packages", out var packages) &&
                packages.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in packages.EnumerateArray()
                             .Where(package => package.TryGetProperty("assets", out _))
                             .SelectMany(package => package.GetProperty("assets").EnumerateArray()))
                {
                    if (asset.TryGetProperty("intelligence", out var profile) &&
                        profile.ValueKind == JsonValueKind.Object)
                    {
                        AddIntelligence(result, profile);
                    }
                }
            }
        }
        return result;
    }

    private static void AddIntelligence(
        IDictionary<string, StudioAssetIntelligence> result,
        JsonElement profile)
    {
        var externalId = ReadOptionalString(profile, "external_id");
        if (string.IsNullOrWhiteSpace(externalId)) return;
        var descriptions = profile.TryGetProperty("descriptions", out var values) &&
                           values.ValueKind == JsonValueKind.Object
            ? values
            : default;
        var description = descriptions.ValueKind == JsonValueKind.Object &&
                          descriptions.TryGetProperty("fr", out var french) &&
                          french.ValueKind == JsonValueKind.String
            ? french.GetString() ?? string.Empty
            : descriptions.ValueKind == JsonValueKind.Object &&
              descriptions.TryGetProperty("en", out var english) &&
              english.ValueKind == JsonValueKind.String
                ? english.GetString() ?? string.Empty
                : string.Empty;
        JsonElement geometry = default;
        JsonElement dimensions = default;
        var hasGeometry = profile.TryGetProperty("geometry", out geometry) &&
                          geometry.ValueKind == JsonValueKind.Object;
        var hasDimensions = hasGeometry && geometry.TryGetProperty("dimensions_meters", out dimensions) &&
                            dimensions.ValueKind == JsonValueKind.Object;
        JsonElement placement = default;
        var hasPlacement = profile.TryGetProperty("placement", out placement) &&
                           placement.ValueKind == JsonValueKind.Object;
        JsonElement suggestedPivot = default;
        var hasSuggestedPivot = hasPlacement &&
                                placement.TryGetProperty("suggested_pivot", out suggestedPivot) &&
                                suggestedPivot.ValueKind == JsonValueKind.Object;
        JsonElement pivotPosition = default;
        var hasPivotPosition = hasSuggestedPivot &&
                               suggestedPivot.TryGetProperty("position_meters", out pivotPosition) &&
                               pivotPosition.ValueKind == JsonValueKind.Object;
        var candidate = new StudioAssetIntelligence(
            description,
            ReadOptionalString(profile, "semantic_type") ?? "unclassified",
            ReadOptionalString(profile, "family") ?? "unclassified",
            ReadOptionalString(profile, "assembly_role") ?? "unspecified",
            hasPlacement ? ReadOptionalString(placement, "surface") ?? "unspecified" : "unspecified",
            hasGeometry ? ReadOptionalString(geometry, "pivot_class") ?? "unknown" : "unknown",
            hasDimensions ? ReadOptionalDouble(dimensions, "x") : null,
            hasDimensions ? ReadOptionalDouble(dimensions, "y") : null,
            hasDimensions ? ReadOptionalDouble(dimensions, "z") : null,
            ReadOptionalString(profile, "readiness") ?? "catalogued",
            ReadOptionalString(profile, "review_status") ?? "generated-review-required",
            ReadOptionalDouble(profile, "confidence") ?? 0.1,
            hasSuggestedPivot ? ReadOptionalString(suggestedPivot, "kind") ?? "unknown" : "unknown",
            hasPivotPosition ? ReadOptionalDouble(pivotPosition, "x") : null,
            hasPivotPosition ? ReadOptionalDouble(pivotPosition, "y") : null,
            hasPivotPosition ? ReadOptionalDouble(pivotPosition, "z") : null,
            hasSuggestedPivot && (ReadOptionalBooleanValue(suggestedPivot, "requires_review") ?? true),
            ReadOptionalString(profile, "generated_by") ?? string.Empty,
            hasGeometry && geometry.TryGetProperty("bounds", out var bounds) &&
            bounds.ValueKind == JsonValueKind.Object);
        if (!result.TryGetValue(externalId, out var existing) || candidate.Score > existing.Score)
        {
            result[externalId] = candidate;
        }
    }

    private static double? ReadOptionalDouble(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.TryGetDouble(out var parsed)
            ? parsed
            : null;

    private static bool? ReadOptionalBooleanValue(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;

    private sealed record StudioAssetIntelligence(
        string Description,
        string SemanticType,
        string Family,
        string AssemblyRole,
        string PlacementSurface,
        string PivotClass,
        double? WidthMeters,
        double? HeightMeters,
        double? DepthMeters,
        string Readiness,
        string ReviewStatus,
        double Confidence,
        string PivotKind,
        double? PivotXMeters,
        double? PivotYMeters,
        double? PivotZMeters,
        bool PivotRequiresReview,
        string GeneratedBy,
        bool HasBounds)
    {
        internal int Score =>
            (ReviewStatus == "human-reviewed" ? 100 : 0) +
            (HasBounds ? 25 : 0) +
            (GeneratedBy.Contains("blender", StringComparison.OrdinalIgnoreCase) ||
             GeneratedBy.Contains("inventory", StringComparison.OrdinalIgnoreCase) ? 20 : 0) +
            (int)Math.Round(Math.Clamp(Confidence, 0, 1) * 20);

        internal static StudioAssetIntelligence CreateFallback(
            string externalId,
            string displayName,
            string source,
            IEnumerable<string> tags,
            IEnumerable<string> categories)
        {
            var text = string.Join(' ', new[] { externalId, displayName }.Concat(tags).Concat(categories))
                .ToLowerInvariant();
            var tokens = text.Split(
                    text.Where(character => !char.IsLetterOrDigit(character)).Distinct().ToArray(),
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);
            var semantic = tokens.Contains("door") ? "door" :
                tokens.Contains("character") || tokens.Contains("creature") ? "character" :
                tokens.Contains("wall") || tokens.Contains("floor") || tokens.Contains("roof") ? "architecture" :
                tokens.Contains("tree") || tokens.Contains("plant") ? "vegetation" : "prop";
            var role = semantic switch
            {
                "door" => "interactive-leaf",
                "architecture" => "modular-structure",
                "vegetation" => "terrain-scatter",
                "character" => "actor",
                _ => "standalone-prop",
            };
            return new StudioAssetIntelligence(
                $"{displayName} est un asset de type {semantic} provenant de {source}.",
                semantic,
                categories.FirstOrDefault(value => !string.Equals(value, "3d-model", StringComparison.OrdinalIgnoreCase))
                    ?.ToLowerInvariant() ?? source.ToLowerInvariant(),
                role,
                semantic == "door" ? "opening" : "ground",
                "unknown",
                null,
                null,
                null,
                "semantic-enriched",
                "generated-review-required",
                0.4,
                "unknown",
                null,
                null,
                null,
                true,
                "openrpg-studio-catalog-fallback/1",
                false);
        }
    }

    private sealed record StudioAssetPackCollection(
        string PackId,
        string DisplayName,
        IReadOnlyList<string> Tags);
}
