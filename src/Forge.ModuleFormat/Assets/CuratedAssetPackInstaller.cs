using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Assets;

public static class CuratedAssetPackInstaller
{
    public const string DefaultManifestRelativePath = "asset_manifests/packs/forgotten_forest_core.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static CuratedAssetPackInstallResult InstallForgottenForest(
        string repositoryRoot,
        string vaultRoot,
        string workspaceRoot,
        string? manifestPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var repository = Path.GetFullPath(repositoryRoot);
        var vault = Path.GetFullPath(vaultRoot);
        var workspace = Path.GetFullPath(workspaceRoot);
        var documentPath = Path.GetFullPath(manifestPath ?? Path.Combine(
            repository,
            DefaultManifestRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(repository, documentPath, "Curated pack manifest escapes the repository root.");

        var document = JsonSerializer.Deserialize<CuratedPackDocument>(
            File.ReadAllText(documentPath, Encoding.UTF8),
            JsonOptions) ?? throw new InvalidDataException("The curated asset pack manifest is empty.");
        ValidateDocument(document);

        var catalogPath = ResolveContained(repository, document.SourceCatalog);
        var sourcePack = ReadSourcePack(catalogPath, document.SourcePackId);
        if (!sourcePack.LicenseVerified ||
            !string.Equals(sourcePack.License, document.License, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Source pack '{document.SourcePackId}' does not carry the verified '{document.License}' license.");
        }

        var extractedRoot = ResolveContained(vault, sourcePack.ExtractedRoot);
        var sharedTextureRelativePath = document.SharedTexturePath;
        if (string.IsNullOrWhiteSpace(sharedTextureRelativePath) &&
            string.Equals(document.SourcePackId, "kenney:mini-forest", StringComparison.Ordinal))
        {
            sharedTextureRelativePath = "Models/GLB format/Textures/colormap.png";
        }

        string? sharedTexturePath = null;
        string? sharedTextureSha256 = null;
        var sharedTextureBytes = 0L;
        if (!string.IsNullOrWhiteSpace(sharedTextureRelativePath))
        {
            sharedTexturePath = ResolveContained(extractedRoot, sharedTextureRelativePath);
            if (!File.Exists(sharedTexturePath))
            {
                throw new FileNotFoundException("The curated pack shared texture is missing.", sharedTexturePath);
            }

            sharedTextureSha256 = HashFile(sharedTexturePath);
            sharedTextureBytes = new FileInfo(sharedTexturePath).Length;
        }
        var profiles = AssetProcessingProfileStore.Load(workspace);
        var manifests = AssetPackageManifestStore.Load(workspace);
        if (!profiles.Succeeded || !manifests.Succeeded)
        {
            throw new InvalidDataException("Existing asset governance files could not be loaded.");
        }

        var profileMap = profiles.Profiles.ToDictionary(profile => profile.AssetId);
        var manifestMap = manifests.Manifests.ToDictionary(manifest => manifest.AssetId);
        var installed = new List<CuratedAssetInstallEntry>();
        foreach (var model in document.Models.OrderBy(model => model.Key, StringComparer.Ordinal))
        {
            var sourcePath = ResolveContained(extractedRoot, model.Path);
            if (!File.Exists(sourcePath) || !string.Equals(Path.GetExtension(sourcePath), ".glb", StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException($"Curated GLB source '{model.Path}' is missing or invalid.", sourcePath);
            }

            var relativeSourcePath = Normalize(Path.GetRelativePath(vault, sourcePath));
            var hash = HashFile(sourcePath);
            var bytes = new FileInfo(sourcePath).Length;
            var externalId = $"{document.SourcePackId}:{model.Key}";
            var assetId = AssetCatalogItemId.ForExternalId(externalId);
            var provenance = $"curated-pack:{document.PackId}@{document.Version};source:{document.SourcePackId};path:{model.Path}";
            var item = new AssetCatalogItem(
                assetId,
                externalId,
                Humanize(model.Key),
                "kenney",
                externalId,
                sourcePack.SourcePage,
                document.License,
                sourcePack.LicenseUrl,
                true,
                hash,
                bytes,
                ["3d-model", "environment"],
                model.Tags,
                "curated-and-extracted",
                AssetAvailability.ExtractedAvailable,
                relativeSourcePath,
                null,
                null,
                provenance);
            var defaultProfile = AssetImportPlanner.CreateDefaultProfile(item, AssetProcessingKind.Model);
            var profile = new AssetProcessingProfile(
                defaultProfile.AssetId,
                defaultProfile.SchemaVersion,
                defaultProfile.Kind,
                defaultProfile.SourceFormat,
                defaultProfile.CanonicalFormat,
                defaultProfile.LicenseSpdx,
                defaultProfile.ContentSha256,
                defaultProfile.Provenance,
                Math.Max(defaultProfile.MaximumOutputBytes, (bytes + sharedTextureBytes) * 2),
                defaultProfile.Texture,
                defaultProfile.Model);
            var plan = AssetImportPlanner.Plan(item, profile);
            if (!plan.Succeeded || plan.Plan is null)
            {
                throw new InvalidDataException($"Asset import plan failed for '{externalId}'.");
            }

            var execution = AssetImportExecutor.Execute(plan.Plan, vault, workspace);
            if (!execution.Succeeded || execution.Execution is null)
            {
                throw new InvalidDataException(
                    $"Asset import failed for '{externalId}': {string.Join("; ", execution.Diagnostics.Select(diagnostic => diagnostic.Message))}");
            }

            var output = execution.Execution;
            var outputDirectory = Path.GetDirectoryName(Path.Combine(
                workspace,
                output.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar)))!;
            var baseManifest = AssetPackageManifestFactory.Create(
                item,
                profile,
                new AssetManifestArtifactInput(
                    output.OutputRelativePath,
                    output.OutputContentSha256,
                    output.OutputBytes,
                    output.Status == AssetImportExecutionStatus.CanonicalReady),
                AssetPackageManifestAuthoringOptions.Default with
                {
                    ModificationProvenance = $"author:openrpg-forge:{document.PackId}@{document.Version}",
                    RequireGeneratedExtensions = false,
                });
            var manifest = baseManifest;
            if (sharedTexturePath is not null && sharedTextureSha256 is not null)
            {
                var texturePath = Path.Combine(outputDirectory, "Textures", "colormap.png");
                CopyAtomically(sharedTexturePath, texturePath);
                var textureRelativePath = Normalize(Path.GetRelativePath(workspace, texturePath));
                var textureChunk = new AssetManifestChunk(
                    StableGuid($"{assetId}:texture:colormap"),
                    AssetChunkRole.Texture,
                    true,
                    textureRelativePath,
                    sharedTextureSha256,
                    sharedTextureBytes,
                    sharedTextureBytes,
                    AssetChunkCompression.None,
                    AssetPackageManifestAuthoringOptions.Default.Platforms);
                manifest = baseManifest with { Chunks = [.. baseManifest.Chunks, textureChunk] };
            }
            profileMap[assetId] = profile;
            manifestMap[assetId] = manifest;
            installed.Add(new CuratedAssetInstallEntry(
                model.Key,
                externalId,
                assetId,
                output.OutputRelativePath,
                output.Reused,
                output.OutputContentSha256,
                output.OutputBytes + sharedTextureBytes));
        }

        EnsureSaved(AssetProcessingProfileStore.Save(
            workspace,
            profileMap.Values.OrderBy(profile => profile.AssetId.ToString(), StringComparer.Ordinal).ToArray()));
        EnsureSaved(AssetPackageManifestStore.Save(
            workspace,
            manifestMap.Values.OrderBy(manifest => manifest.AssetId.ToString(), StringComparer.Ordinal).ToArray()));
        WriteCredits(workspace, document, sourcePack, installed);
        return new CuratedAssetPackInstallResult(
            document.PackId,
            document.Version,
            document.License,
            installed,
            installed.Count(entry => entry.Reused),
            installed.Sum(entry => entry.OutputBytes));
    }

    private static void ValidateDocument(CuratedPackDocument document)
    {
        if (document.SchemaVersion != 1 || string.IsNullOrWhiteSpace(document.PackId) ||
            string.IsNullOrWhiteSpace(document.Version) || string.IsNullOrWhiteSpace(document.SourceCatalog) ||
            string.IsNullOrWhiteSpace(document.SourcePackId) || string.IsNullOrWhiteSpace(document.License) ||
            document.Models.Count == 0 || document.Models.Any(model => string.IsNullOrWhiteSpace(model.Key) ||
                string.IsNullOrWhiteSpace(model.Path)) ||
            document.Models.Select(model => model.Key).Distinct(StringComparer.Ordinal).Count() != document.Models.Count)
        {
            throw new InvalidDataException("The curated asset pack manifest is invalid.");
        }
    }

    private static SourcePack ReadSourcePack(string catalogPath, string sourcePackId)
    {
        using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath, Encoding.UTF8));
        foreach (var pack in catalog.RootElement.GetProperty("packs").EnumerateArray())
        {
            if (!string.Equals(pack.GetProperty("pack_id").GetString(), sourcePackId, StringComparison.Ordinal))
            {
                continue;
            }

            return new SourcePack(
                pack.TryGetProperty("name", out var name) ? name.GetString() ?? sourcePackId : sourcePackId,
                pack.GetProperty("extracted_root").GetString() ?? string.Empty,
                pack.GetProperty("license").GetString() ?? string.Empty,
                pack.GetProperty("license_url").GetString() ?? string.Empty,
                pack.GetProperty("license_verified").GetBoolean(),
                pack.GetProperty("source_page").GetString() ?? string.Empty);
        }

        throw new InvalidDataException($"Source pack '{sourcePackId}' is absent from the governed catalog.");
    }

    private static void WriteCredits(
        string workspace,
        CuratedPackDocument document,
        SourcePack sourcePack,
        IReadOnlyList<CuratedAssetInstallEntry> entries)
    {
        var creditsDirectory = Path.Combine(workspace, "assets", "credits");
        var path = Path.Combine(creditsDirectory, $"{SafeFileName(document.PackId)}.md");
        var lines = new List<string>
        {
            $"## {document.DisplayName}",
            string.Empty,
            $"- Pack: {document.DisplayName} ({document.PackId} {document.Version})",
            $"- Source: {sourcePack.Name} ({sourcePack.SourcePage})",
            $"- License: {document.License} ({sourcePack.LicenseUrl})",
            "- License verification: verified in the OpenRPG Forge CC0 catalog",
            "- Modification: canonical GLB copied into the module workspace without altering the Vault original",
            string.Empty,
            "## Stable asset identities",
            string.Empty,
        };
        lines.AddRange(entries.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"- `{entry.ExternalId}` — `{entry.AssetId}` — `{entry.OutputRelativePath}` — SHA-256 `{entry.OutputSha256}`"));
        WriteTextAtomically(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        RegenerateCreditsIndex(workspace, creditsDirectory);
    }

    private static void RegenerateCreditsIndex(string workspace, string creditsDirectory)
    {
        var sections = Directory.EnumerateFiles(creditsDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => File.ReadAllText(path, Encoding.UTF8).Trim())
            .Where(content => content.Length > 0)
            .ToArray();
        var content = "# Asset credits" + Environment.NewLine + Environment.NewLine +
                      string.Join(Environment.NewLine + Environment.NewLine, sections) + Environment.NewLine;
        WriteTextAtomically(Path.Combine(workspace, "assets", "CREDITS_ASSETS.md"), content);
    }

    private static void EnsureSaved(ProjectSessionSaveResult result)
    {
        if (!result.Succeeded)
        {
            throw new IOException(string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static Guid StableGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:curated-pack:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }

    private static void CopyAtomically(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (File.Exists(targetPath) && string.Equals(HashFile(targetPath), HashFile(sourcePath), StringComparison.Ordinal))
        {
            return;
        }

        var temporaryPath = Path.Combine(Path.GetDirectoryName(targetPath)!, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: false);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static string ResolveContained(string root, string relativePath)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(Normalize(relativePath)))
        {
            throw new InvalidDataException($"Unsafe curated asset path '{relativePath}'.");
        }

        var resolved = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(root, resolved, $"Curated asset path '{relativePath}' escapes its root.");
        return resolved;
    }

    private static void EnsureContained(string root, string candidate, string message)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        if (relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new InvalidDataException(message);
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string Humanize(string key) => string.Join(' ', key.Split('-', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private static string SafeFileName(string value) => string.Concat(value.Select(character =>
        char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')).Trim('-');

    private static void WriteTextAtomically(string targetPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = Path.Combine(Path.GetDirectoryName(targetPath)!, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private sealed record CuratedPackDocument(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("pack_id")] string PackId,
        string Version,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("source_catalog")] string SourceCatalog,
        [property: JsonPropertyName("source_pack_id")] string SourcePackId,
        [property: JsonPropertyName("shared_texture_path")] string? SharedTexturePath,
        string License,
        IReadOnlyList<CuratedModelDocument> Models);

    private sealed record CuratedModelDocument(string Key, string Path, IReadOnlyList<string> Tags);
    private sealed record SourcePack(string Name, string ExtractedRoot, string License, string LicenseUrl, bool LicenseVerified, string SourcePage);
}

public sealed record CuratedAssetInstallEntry(
    string Key,
    string ExternalId,
    AssetCatalogItemId AssetId,
    string OutputRelativePath,
    bool Reused,
    string OutputSha256,
    long OutputBytes);

public sealed record CuratedAssetPackInstallResult(
    string PackId,
    string Version,
    string LicenseSpdx,
    IReadOnlyList<CuratedAssetInstallEntry> Entries,
    int ReusedCount,
    long TotalBytes);
