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
                    var item = MapAsset(asset, vaultRoot);
                    if (item is not null) Add(items, item);
                }
            }

            if (root.TryGetProperty("packs", out var packs) && packs.ValueKind == JsonValueKind.Array)
            {
                foreach (var pack in packs.EnumerateArray())
                {
                    catalogEntryCount++;
                    foreach (var item in MapPack(pack, vaultRoot)) Add(items, item);
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

    private static StudioAssetLibraryItem? MapAsset(JsonElement asset, string vaultRoot)
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
            vaultRoot);
    }

    private static IReadOnlyList<StudioAssetLibraryItem> MapPack(JsonElement pack, string vaultRoot)
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
                vaultRoot));
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
        string vaultRoot)
    {
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
            selected.AvailableLocally);
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
}
