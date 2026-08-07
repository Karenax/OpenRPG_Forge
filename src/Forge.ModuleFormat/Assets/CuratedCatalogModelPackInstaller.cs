using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Assets;

public static class CuratedCatalogModelPackInstaller
{
    public const string DefaultManifestRelativePath =
        "asset_manifests/packs/forgotten_forest_polyhaven_environment.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static IReadOnlyList<string> ListExternalIds(string repositoryRoot, string? manifestPath = null) =>
        ReadDocument(repositoryRoot, manifestPath).Models
            .Select(model => model.ExternalId)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static CuratedCatalogModelPlan Plan(
        string repositoryRoot,
        string vaultRoot,
        string workspaceRoot,
        string externalId,
        string? manifestPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        var repository = Path.GetFullPath(repositoryRoot);
        var vault = Path.GetFullPath(vaultRoot);
        var workspace = Path.GetFullPath(workspaceRoot);
        var document = ReadDocument(repository, manifestPath);
        var model = document.Models.SingleOrDefault(candidate =>
            string.Equals(candidate.ExternalId, externalId, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Curated model '{externalId}' is absent from pack '{document.PackId}'.");
        var loaded = AssetCatalogLoader.Load(repository, vault);
        if (!loaded.Succeeded || loaded.Catalog is null)
        {
            throw new InvalidDataException("The governed OpenRPG Asset Atlas could not be loaded.");
        }

        var catalogItem = loaded.Catalog.Items.SingleOrDefault(candidate =>
            string.Equals(candidate.ExternalId, externalId, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Curated model '{externalId}' is absent from the governed catalog.");
        if (!catalogItem.LicenseVerified ||
            !string.Equals(catalogItem.LicenseSpdx, document.License, StringComparison.Ordinal) ||
            catalogItem.Availability == AssetAvailability.CatalogOnly ||
            string.IsNullOrWhiteSpace(catalogItem.RelativeModelPath))
        {
            throw new InvalidDataException(
                $"Curated model '{externalId}' requires a locally available, verified '{document.License}' source.");
        }

        var sourceRelativePath = catalogItem.RelativeModelPath;
        var sourcePath = ResolveContained(vault, sourceRelativePath);
        if (!File.Exists(sourcePath) ||
            !string.Equals(Path.GetExtension(sourcePath), ".gltf", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("Curated catalog model GLTF is missing or unsupported.", sourcePath);
        }

        var sourceHash = HashFile(sourcePath);
        var provenance = $"curated-pack:{document.PackId}@{document.Version};source:{externalId}";
        var item = new AssetCatalogItem(
            catalogItem.Id,
            catalogItem.ExternalId,
            catalogItem.DisplayName,
            catalogItem.Source,
            catalogItem.SourceAssetId,
            catalogItem.SourcePage,
            catalogItem.LicenseSpdx,
            catalogItem.LicenseUrl,
            catalogItem.LicenseVerified,
            catalogItem.ContentSha256,
            catalogItem.TotalBytes,
            catalogItem.Categories,
            catalogItem.Tags.Concat(model.Tags).ToArray(),
            catalogItem.CatalogStatus,
            catalogItem.Availability,
            sourceRelativePath,
            catalogItem.RelativePreviewPath,
            catalogItem.ThumbnailUrl,
            provenance,
            author: catalogItem.Author,
            attributionRequired: catalogItem.AttributionRequired,
            attributionText: catalogItem.AttributionText,
            installedPackIds: catalogItem.InstalledPackIds);
        var profile = new AssetProcessingProfile(
            item.Id,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Model,
            "gltf",
            "glb",
            item.LicenseSpdx,
            item.ContentSha256,
            item.Provenance,
            model.MaximumOutputBytes,
            model: new ModelProcessingOptions(false, false, false, [], [1m]));
        var outputRelativePath = $"assets/imported/{Slug(externalId)}/model.glb";
        var baseManifest = AssetPackageManifestFactory.Create(
            item,
            profile,
            new AssetManifestArtifactInput(
                outputRelativePath,
                sourceHash,
                new FileInfo(sourcePath).Length,
                false),
            AssetPackageManifestAuthoringOptions.Default with
            {
                ModificationProvenance = $"author:openrpg-forge:{document.PackId}@{document.Version}",
                RequireGeneratedExtensions = false,
                BaseTriangleBudget = model.TriangleBudget,
            });
        var profiles = AssetProcessingProfileStore.Load(workspace);
        var manifests = AssetPackageManifestStore.Load(workspace);
        var currentProfile = profiles.Profiles.SingleOrDefault(candidate => candidate.AssetId == item.Id);
        var currentManifest = manifests.Manifests.SingleOrDefault(candidate => candidate.AssetId == item.Id);
        if (profiles.Succeeded && manifests.Succeeded &&
            ProfilesEquivalent(currentProfile, profile) &&
            currentManifest is not null && IsReusable(workspace, currentManifest, item.ContentSha256))
        {
            return new(document.PackId, document.Version, document.DisplayName, item, profile, currentManifest,
                null, null, outputRelativePath, model.SourceMeshNames, model.BaseDecimationRatio, true);
        }

        var configuration = new AssetExternalEditConfiguration(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(item.Id),
            item.Id,
            baseManifest.Id,
            provenance,
            item.LicenseSpdx,
            sourceHash,
            sourceRelativePath,
            outputRelativePath,
            AssetExternalTool.Blender,
            [
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.NormalizeModel,
                AssetExternalOperation.ExportGlb,
                AssetExternalOperation.GenerateThumbnail,
                AssetExternalOperation.AuditQuality,
            ],
            true,
            [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux]);
        var request = BlenderWorkerRequestStore.Save(
            workspace,
            configuration,
            new BlenderWorkerOptions(
                true,
                1m,
                "ground_center",
                [],
                false,
                [],
                [],
                null,
                model.SourceMeshNames,
                model.BaseDecimationRatio));
        if (!request.Succeeded)
        {
            throw new InvalidDataException(request.Diagnostics.First().Message);
        }

        return new(document.PackId, document.Version, document.DisplayName, item, profile, baseManifest,
            configuration, request.RequestRelativePath, outputRelativePath, model.SourceMeshNames,
            model.BaseDecimationRatio, false);
    }

    public static CuratedCatalogModelInstallResult Complete(
        string workspaceRoot,
        CuratedCatalogModelPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(plan);
        var workspace = Path.GetFullPath(workspaceRoot);
        var manifest = plan.Manifest;
        if (!plan.Reused)
        {
            var configuration = plan.Configuration ??
                throw new InvalidOperationException("Curated catalog model build configuration is missing.");
            var finalized = AssetRoundTripFinalizer.Finalize(workspace, plan.Item, plan.Manifest, configuration);
            if (!finalized.Succeeded || finalized.Manifest?.ModelMetadata is null)
            {
                throw new InvalidDataException(finalized.Diagnostics.First().Message);
            }

            manifest = finalized.Manifest;
            var profiles = AssetProcessingProfileStore.Load(workspace).Profiles
                .Where(candidate => candidate.AssetId != plan.Item.Id)
                .Append(plan.Profile)
                .OrderBy(candidate => candidate.AssetId.ToString(), StringComparer.Ordinal)
                .ToArray();
            var manifests = AssetPackageManifestStore.Load(workspace).Manifests
                .Where(candidate => candidate.AssetId != plan.Item.Id)
                .Append(manifest)
                .OrderBy(candidate => candidate.AssetId.ToString(), StringComparer.Ordinal)
                .ToArray();
            EnsureSaved(AssetProcessingProfileStore.Save(workspace, profiles));
            EnsureSaved(AssetPackageManifestStore.Save(workspace, manifests));
        }

        WriteCredits(workspace, plan, manifest);
        var chunk = manifest.Chunks.Single(candidate => candidate.Role == AssetChunkRole.Model);
        return new(plan.PackId, plan.Version, plan.Item.Id, chunk.RelativePath,
            manifest.ModelMetadata?.Metrics.TriangleCount ?? 0, chunk.StoredBytes, plan.Reused,
            plan.Item.LicenseSpdx);
    }

    private static CuratedCatalogModelPackDocument ReadDocument(string repositoryRoot, string? manifestPath)
    {
        var repository = Path.GetFullPath(repositoryRoot);
        var path = Path.GetFullPath(manifestPath ?? Path.Combine(
            repository,
            DefaultManifestRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(repository, path, "Curated catalog pack manifest escapes the repository root.");
        var document = JsonSerializer.Deserialize<CuratedCatalogModelPackDocument>(
            File.ReadAllText(path, Encoding.UTF8), JsonOptions)
            ?? throw new InvalidDataException("The curated catalog model pack manifest is empty.");
        if (document.SchemaVersion != 1 || string.IsNullOrWhiteSpace(document.PackId) ||
            string.IsNullOrWhiteSpace(document.Version) || string.IsNullOrWhiteSpace(document.DisplayName) ||
            string.IsNullOrWhiteSpace(document.License) || document.Models.Count == 0 ||
            document.Models.Any(model => string.IsNullOrWhiteSpace(model.ExternalId) ||
                model.SourceMeshNames.Count == 0 || model.SourceMeshNames.Any(string.IsNullOrWhiteSpace) ||
                model.SourceMeshNames.Distinct(StringComparer.Ordinal).Count() != model.SourceMeshNames.Count ||
                model.BaseDecimationRatio is < 0.001m or > 1m ||
                model.MaximumOutputBytes < 1_048_576 || model.Tags.Count == 0) ||
            document.Models.Select(model => model.ExternalId).Distinct(StringComparer.Ordinal).Count() !=
            document.Models.Count)
        {
            throw new InvalidDataException("The curated catalog model pack manifest is invalid.");
        }

        return document;
    }

    private static bool IsReusable(string workspace, AssetPackageManifest manifest, string sourceFingerprint)
    {
        var chunk = manifest.Chunks.SingleOrDefault(candidate => candidate.Role == AssetChunkRole.Model);
        if (chunk is null || manifest.ModelMetadata is null ||
            !string.Equals(manifest.SourceContentSha256, sourceFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        var path = ResolveContained(workspace, chunk.RelativePath);
        return File.Exists(path) && new FileInfo(path).Length == chunk.StoredBytes &&
               string.Equals(HashFile(path), chunk.ContentSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProfilesEquivalent(AssetProcessingProfile? actual, AssetProcessingProfile expected) =>
        actual is not null && actual.AssetId == expected.AssetId &&
        actual.SchemaVersion == expected.SchemaVersion && actual.Kind == expected.Kind &&
        string.Equals(actual.SourceFormat, expected.SourceFormat, StringComparison.Ordinal) &&
        string.Equals(actual.CanonicalFormat, expected.CanonicalFormat, StringComparison.Ordinal) &&
        string.Equals(actual.LicenseSpdx, expected.LicenseSpdx, StringComparison.Ordinal) &&
        string.Equals(actual.ContentSha256, expected.ContentSha256, StringComparison.Ordinal) &&
        string.Equals(actual.Provenance, expected.Provenance, StringComparison.Ordinal) &&
        actual.MaximumOutputBytes == expected.MaximumOutputBytes &&
        actual.Model is not null && expected.Model is not null &&
        actual.Model.ImportRig == expected.Model.ImportRig &&
        actual.Model.ImportAnimations == expected.Model.ImportAnimations &&
        actual.Model.GenerateCollisions == expected.Model.GenerateCollisions &&
        actual.Model.Sockets.SequenceEqual(expected.Model.Sockets, StringComparer.Ordinal) &&
        actual.Model.LodRatios.SequenceEqual(expected.Model.LodRatios);

    private static void WriteCredits(
        string workspace,
        CuratedCatalogModelPlan plan,
        AssetPackageManifest manifest)
    {
        var directory = Path.Combine(workspace, "assets", "credits");
        var path = Path.Combine(directory, $"{Slug(plan.PackId)}-{Slug(plan.Item.SourceAssetId)}.md");
        var chunk = manifest.Chunks.Single(candidate => candidate.Role == AssetChunkRole.Model);
        var content = string.Join(Environment.NewLine,
        new string[]
        {
            $"## {plan.Item.DisplayName}",
            string.Empty,
            $"- Pack: {plan.DisplayName} ({plan.PackId} {plan.Version})",
            $"- Source: Poly Haven ({plan.Item.SourcePage})",
            $"- License: {plan.Item.LicenseSpdx} ({plan.Item.LicenseUrl})",
            "- License verification: official Poly Haven license page and governed OpenRPG Asset Atlas",
            $"- Vault original: `{plan.Item.RelativeModelPath}` — unchanged",
            $"- Runtime derivation: mesh selection `{string.Join(", ", plan.SourceMeshNames)}` and decimation ratio {plan.BaseDecimationRatio:0.#####}",
            $"- Stable identity: `{plan.Item.ExternalId}` — `{plan.Item.Id}`",
            $"- Runtime GLB: `{chunk.RelativePath}` — SHA-256 `{chunk.ContentSha256}`",
        }) + Environment.NewLine;
        WriteTextAtomically(path, content);
        RegenerateCreditsIndex(workspace, directory);
    }

    private static void RegenerateCreditsIndex(string workspace, string creditsDirectory)
    {
        var sections = Directory.EnumerateFiles(creditsDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => File.ReadAllText(path, Encoding.UTF8).Trim())
            .Where(content => content.Length > 0)
            .ToArray();
        WriteTextAtomically(
            Path.Combine(workspace, "assets", "CREDITS_ASSETS.md"),
            "# Asset credits" + Environment.NewLine + Environment.NewLine +
            string.Join(Environment.NewLine + Environment.NewLine, sections) + Environment.NewLine);
    }

    private static void EnsureSaved(ProjectSessionSaveResult result)
    {
        if (!result.Succeeded)
        {
            throw new IOException(string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
    }

    private static string ResolveContained(string root, string relativePath)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
        {
            throw new InvalidDataException($"Unsafe curated model path '{relativePath}'.");
        }

        var resolved = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(root, resolved, $"Curated model path '{relativePath}' escapes its root.");
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

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Slug(string value) => string.Concat(value.ToLowerInvariant().Select(character =>
        char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-');

    private static void WriteTextAtomically(string targetPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporary = Path.Combine(Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private sealed record CuratedCatalogModelPackDocument(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("pack_id")] string PackId,
        string Version,
        [property: JsonPropertyName("display_name")] string DisplayName,
        string License,
        IReadOnlyList<CuratedCatalogModelDocument> Models);

    private sealed record CuratedCatalogModelDocument(
        [property: JsonPropertyName("external_id")] string ExternalId,
        [property: JsonPropertyName("source_mesh_names")] IReadOnlyList<string> SourceMeshNames,
        [property: JsonPropertyName("base_decimation_ratio")] decimal BaseDecimationRatio,
        [property: JsonPropertyName("maximum_output_bytes")] long MaximumOutputBytes,
        IReadOnlyList<string> Tags)
    {
        public int TriangleBudget => Math.Max(1, (int)Math.Round(4_300_000m * BaseDecimationRatio));
    }
}

public sealed record CuratedCatalogModelPlan(
    string PackId,
    string Version,
    string DisplayName,
    AssetCatalogItem Item,
    AssetProcessingProfile Profile,
    AssetPackageManifest Manifest,
    AssetExternalEditConfiguration? Configuration,
    string? RequestRelativePath,
    string OutputRelativePath,
    IReadOnlyList<string> SourceMeshNames,
    decimal BaseDecimationRatio,
    bool Reused);

public sealed record CuratedCatalogModelInstallResult(
    string PackId,
    string Version,
    AssetCatalogItemId AssetId,
    string OutputRelativePath,
    int TriangleCount,
    long OutputBytes,
    bool Reused,
    string LicenseSpdx);
