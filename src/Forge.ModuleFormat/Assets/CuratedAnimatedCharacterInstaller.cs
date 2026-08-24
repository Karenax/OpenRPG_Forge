using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Assets;

public static class CuratedAnimatedCharacterInstaller
{
    public const string DefaultManifestRelativePath =
        "asset_manifests/packs/asset_vault_animated_character.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static CuratedAnimatedCharacterPlan Plan(
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
        EnsureContained(repository, documentPath, "Animated character manifest escapes the repository root.");
        var document = JsonSerializer.Deserialize<AnimatedCharacterDocument>(
            File.ReadAllText(documentPath, Encoding.UTF8),
            JsonOptions) ?? throw new InvalidDataException("The animated character manifest is empty.");
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
        var modelPath = ResolveContained(extractedRoot, document.ModelPath);
        var skinPath = ResolveContained(extractedRoot, document.SkinPath);
        EnsureFile(modelPath, ".fbx", "Animated character model");
        EnsureFile(skinPath, ".png", "Animated character skin");
        var modelRelativePath = Normalize(Path.GetRelativePath(vault, modelPath));
        var skinRelativePath = Normalize(Path.GetRelativePath(vault, skinPath));
        var modelHash = HashFile(modelPath);
        var skinHash = HashFile(skinPath);
        var animations = document.Animations
            .OrderBy(animation => animation.Name, StringComparer.Ordinal)
            .Select(animation =>
            {
                var path = ResolveContained(extractedRoot, animation.Path);
                EnsureFile(path, ".fbx", $"Animation '{animation.Name}'");
                return new CuratedAnimationSource(
                    animation.Name,
                    Normalize(Path.GetRelativePath(vault, path)),
                    HashFile(path));
            })
            .ToArray();
        var sourceFingerprint = HashText(string.Join('|',
            new[] { sourcePack.ArchiveSha256, modelHash, skinHash }.Concat(
                animations.Select(animation => animation.ContentSha256))));
        var externalId = $"{document.SourcePackId}:{document.Key}";
        var assetId = AssetCatalogItemId.ForExternalId(externalId);
        var outputRelativePath = $"assets/imported/{Slug(externalId)}/model.glb";
        var provenance =
            $"curated-pack:{document.PackId}@{document.Version};source:{document.SourcePackId};" +
            $"archive-sha256:{sourcePack.ArchiveSha256};skin:{document.SkinPath}";
        var sourceBytes = new FileInfo(modelPath).Length + new FileInfo(skinPath).Length +
                          animations.Sum(animation => new FileInfo(ResolveContained(vault, animation.RelativePath)).Length);
        var item = new AssetCatalogItem(
            assetId,
            externalId,
            document.DisplayName,
            "kenney",
            externalId,
            sourcePack.SourcePage,
            document.License,
            sourcePack.LicenseUrl,
            true,
            sourceFingerprint,
            sourceBytes,
            ["3d-model", "animated-character"],
            document.Tags,
            "curated-and-extracted",
            AssetAvailability.ExtractedAvailable,
            modelRelativePath,
            null,
            null,
            provenance);
        var profile = new AssetProcessingProfile(
            assetId,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Model,
            "fbx",
            "glb",
            document.License,
            sourceFingerprint,
            provenance,
            Math.Max(sourceBytes * 2, 1_048_576),
            model: new ModelProcessingOptions(true, true, false, [], [1m]));
        var baseManifest = AssetPackageManifestFactory.Create(
            item,
            profile,
            new AssetManifestArtifactInput(outputRelativePath, modelHash, new FileInfo(modelPath).Length, false),
            AssetPackageManifestAuthoringOptions.Default with
            {
                ModificationProvenance = $"author:openrpg-forge:{document.PackId}@{document.Version}",
                RequireGeneratedExtensions = false,
                Rig = "kenney-humanoid-58",
                PreviewClipName = "idle",
                PreviewClipDurationSeconds = 1m,
                IncludeVfx = false,
            });
        var existingProfiles = AssetProcessingProfileStore.Load(workspace);
        var existingManifests = AssetPackageManifestStore.Load(workspace);
        var currentManifest = existingManifests.Manifests.SingleOrDefault(candidate => candidate.AssetId == assetId);
        var currentProfile = existingProfiles.Profiles.SingleOrDefault(candidate => candidate.AssetId == assetId);
        if (existingProfiles.Succeeded && existingManifests.Succeeded && currentManifest is not null &&
            ProfilesEquivalent(currentProfile, profile) &&
            IsReusable(workspace, currentManifest, sourceFingerprint, animations))
        {
            return new CuratedAnimatedCharacterPlan(
                document.PackId,
                document.Version,
                sourcePack.SourcePage,
                sourcePack.LicenseUrl,
                item,
                profile,
                currentManifest,
                null,
                null,
                outputRelativePath,
                animations,
                true);
        }

        var configuration = new AssetExternalEditConfiguration(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(assetId),
            assetId,
            baseManifest.Id,
            provenance,
            document.License,
            modelHash,
            modelRelativePath,
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
                document.ScaleMeters,
                "ground_center",
                [],
                false,
                animations.Select(animation => new BlenderWorkerAnimationSource(
                    animation.Name,
                    animation.RelativePath,
                    animation.ContentSha256)).ToArray(),
                [new BlenderWorkerMaterialTextureSource(
                    document.SkinMaterial,
                    "base_color",
                    skinRelativePath,
                    skinHash)]));
        if (!request.Succeeded)
        {
            throw new InvalidDataException(AssertSingle(request.Diagnostics).Message);
        }

        return new CuratedAnimatedCharacterPlan(
            document.PackId,
            document.Version,
            sourcePack.SourcePage,
            sourcePack.LicenseUrl,
            item,
            profile,
            baseManifest,
            configuration,
            request.RequestRelativePath,
            outputRelativePath,
            animations,
            false);
    }

    public static CuratedAnimatedCharacterInstallResult Complete(
        string workspaceRoot,
        CuratedAnimatedCharacterPlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(plan);
        var workspace = Path.GetFullPath(workspaceRoot);
        if (plan.Reused)
        {
            WriteCredits(workspace, plan, plan.Manifest);
            return Result(plan, plan.Manifest, true);
        }

        var configuration = plan.Configuration ??
            throw new InvalidOperationException("Animated character build configuration is missing.");
        var finalized = AssetRoundTripFinalizer.Finalize(
            workspace,
            plan.Item,
            plan.Manifest,
            configuration);
        if (!finalized.Succeeded || finalized.Manifest?.ModelMetadata is null)
        {
            throw new InvalidDataException(AssertSingle(finalized.Diagnostics).Message);
        }

        var modelChunk = finalized.Manifest.Chunks.Single(chunk => chunk.Role == AssetChunkRole.Model);
        var qualityPath = ResolveContained(workspace, finalized.Manifest.ModelMetadata.QualityReportRelativePath);
        using var quality = JsonDocument.Parse(File.ReadAllText(qualityPath, Encoding.UTF8));
        var metrics = quality.RootElement.GetProperty("metrics");
        var clipNames = metrics.GetProperty("animationClips").EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expectedNames = plan.Animations.Select(animation => animation.Name).Order(StringComparer.Ordinal).ToArray();
        if (!clipNames.SequenceEqual(expectedNames, StringComparer.Ordinal) ||
            metrics.GetProperty("armatureCount").GetInt32() != 1 ||
            metrics.GetProperty("boneCount").GetInt32() != 58)
        {
            throw new InvalidDataException("Animated character quality report does not match the governed 58-bone rig and clips.");
        }

        var durations = metrics.GetProperty("animationDurationsSeconds");
        var clips = expectedNames.Select(name => new AssetAnimationClipManifest(
            name,
            durations.GetProperty(name).GetDecimal(),
            false,
            AssetAnimationCompression.Balanced,
            [])).ToArray();
        var manifest = finalized.Manifest with
        {
            Animation = new AssetAnimationManifest(true, modelChunk.Id, "kenney-humanoid-58", null, clips),
            ModelMetadata = finalized.Manifest.ModelMetadata with
            {
                Compatibility = finalized.Manifest.ModelMetadata.Compatibility with
                {
                    Realism = ArtisticRealism.Stylized,
                    TargetTexelDensity = 512,
                },
            },
        };
        var diagnostics = AssetPackageManifestValidator.Validate(manifest);
        if (diagnostics.Count > 0)
        {
            throw new InvalidDataException(
                $"Animated character manifest is invalid: {diagnostics[0].Code} - {diagnostics[0].Message}");
        }

        var profiles = AssetProcessingProfileStore.Load(workspace).Profiles
            .Where(profile => profile.AssetId != plan.Item.Id)
            .Append(plan.Profile)
            .OrderBy(profile => profile.AssetId.ToString(), StringComparer.Ordinal)
            .ToArray();
        var manifests = AssetPackageManifestStore.Load(workspace).Manifests
            .Where(candidate => candidate.AssetId != plan.Item.Id)
            .Append(manifest)
            .OrderBy(candidate => candidate.AssetId.ToString(), StringComparer.Ordinal)
            .ToArray();
        EnsureSaved(AssetProcessingProfileStore.Save(workspace, profiles));
        EnsureSaved(AssetPackageManifestStore.Save(workspace, manifests));
        WriteCredits(workspace, plan, manifest);
        return Result(plan, manifest, false);
    }

    private static CuratedAnimatedCharacterInstallResult Result(
        CuratedAnimatedCharacterPlan plan,
        AssetPackageManifest manifest,
        bool reused) =>
        new(
            plan.PackId,
            plan.Version,
            plan.Item.Id,
            manifest.Chunks.Single(chunk => chunk.Role == AssetChunkRole.Model).RelativePath,
            manifest.Animation?.Clips.Select(clip => clip.Name).Order(StringComparer.Ordinal).ToArray() ?? [],
            manifest.ModelMetadata?.Metrics.TriangleCount ?? 0,
            reused,
            plan.Item.LicenseSpdx);

    private static bool IsReusable(
        string workspace,
        AssetPackageManifest manifest,
        string sourceFingerprint,
        IReadOnlyList<CuratedAnimationSource> animations)
    {
        var chunk = manifest.Chunks.SingleOrDefault(candidate => candidate.Role == AssetChunkRole.Model);
        if (chunk is null || manifest.ModelMetadata is null || manifest.Animation is null ||
            !string.Equals(manifest.SourceContentSha256, sourceFingerprint, StringComparison.Ordinal) ||
            !manifest.Animation.Clips.Select(clip => clip.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(animations.Select(animation => animation.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return false;
        }

        var path = ResolveContained(workspace, chunk.RelativePath);
        return File.Exists(path) && new FileInfo(path).Length == chunk.StoredBytes &&
               string.Equals(HashFile(path), chunk.ContentSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProfilesEquivalent(AssetProcessingProfile? actual, AssetProcessingProfile expected)
    {
        if (actual is null || actual.AssetId != expected.AssetId || actual.SchemaVersion != expected.SchemaVersion ||
            actual.Kind != expected.Kind || !string.Equals(actual.SourceFormat, expected.SourceFormat, StringComparison.Ordinal) ||
            !string.Equals(actual.CanonicalFormat, expected.CanonicalFormat, StringComparison.Ordinal) ||
            !string.Equals(actual.LicenseSpdx, expected.LicenseSpdx, StringComparison.Ordinal) ||
            !string.Equals(actual.ContentSha256, expected.ContentSha256, StringComparison.Ordinal) ||
            !string.Equals(actual.Provenance, expected.Provenance, StringComparison.Ordinal) ||
            actual.MaximumOutputBytes != expected.MaximumOutputBytes)
        {
            return false;
        }

        if (actual.Texture is not null || expected.Texture is not null)
        {
            return false;
        }

        if (actual.Model is null || expected.Model is null)
        {
            return actual.Model is null && expected.Model is null;
        }

        return actual.Model.ImportRig == expected.Model.ImportRig &&
               actual.Model.ImportAnimations == expected.Model.ImportAnimations &&
               actual.Model.GenerateCollisions == expected.Model.GenerateCollisions &&
               actual.Model.Sockets.SequenceEqual(expected.Model.Sockets, StringComparer.Ordinal) &&
               actual.Model.LodRatios.SequenceEqual(expected.Model.LodRatios);
    }

    private static void ValidateDocument(AnimatedCharacterDocument document)
    {
        if (document.SchemaVersion != 1 || string.IsNullOrWhiteSpace(document.PackId) ||
            string.IsNullOrWhiteSpace(document.Version) || string.IsNullOrWhiteSpace(document.DisplayName) ||
            string.IsNullOrWhiteSpace(document.SourceCatalog) || string.IsNullOrWhiteSpace(document.SourcePackId) ||
            string.IsNullOrWhiteSpace(document.License) || string.IsNullOrWhiteSpace(document.Key) ||
            string.IsNullOrWhiteSpace(document.ModelPath) || string.IsNullOrWhiteSpace(document.SkinPath) ||
            string.IsNullOrWhiteSpace(document.SkinMaterial) || document.ScaleMeters is < 0.001m or > 1000m ||
            document.Animations.Count == 0 || document.Animations.Any(animation =>
                string.IsNullOrWhiteSpace(animation.Name) || string.IsNullOrWhiteSpace(animation.Path)) ||
            document.Animations.Select(animation => animation.Name).Distinct(StringComparer.Ordinal).Count() !=
            document.Animations.Count)
        {
            throw new InvalidDataException("The curated animated character manifest is invalid.");
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

            var archive = pack.GetProperty("files")[0];
            return new SourcePack(
                pack.GetProperty("extracted_root").GetString() ?? string.Empty,
                pack.GetProperty("license").GetString() ?? string.Empty,
                pack.GetProperty("license_url").GetString() ?? string.Empty,
                pack.GetProperty("license_verified").GetBoolean(),
                pack.GetProperty("source_page").GetString() ?? string.Empty,
                archive.GetProperty("sha256").GetString() ?? string.Empty);
        }

        throw new InvalidDataException($"Source pack '{sourcePackId}' is absent from the governed catalog.");
    }

    private static void WriteCredits(
        string workspace,
        CuratedAnimatedCharacterPlan plan,
        AssetPackageManifest manifest)
    {
        var path = Path.Combine(workspace, "assets", "CREDITS_ASSETS.md");
        var existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).TrimEnd() : "# Asset credits";
        var heading = $"## Animated character — {plan.Item.DisplayName}";
        var headingIndex = existing.IndexOf(heading, StringComparison.Ordinal);
        if (headingIndex >= 0)
        {
            var nextHeading = existing.IndexOf(
                $"{Environment.NewLine}## ",
                headingIndex + heading.Length,
                StringComparison.Ordinal);
            existing = (nextHeading < 0
                ? existing[..headingIndex]
                : existing.Remove(headingIndex, nextHeading - headingIndex)).TrimEnd();
        }

        var chunk = manifest.Chunks.Single(candidate => candidate.Role == AssetChunkRole.Model);
        var section = string.Join(Environment.NewLine,
        new string[]
        {
            heading,
            string.Empty,
            $"- Pack: {plan.PackId} {plan.Version}",
            $"- Source: Kenney Animated Characters Protagonists ({plan.SourcePage})",
            $"- License: {plan.Item.LicenseSpdx} ({plan.LicenseUrl})",
            "- License verification: official Kenney page and governed local catalog",
            "- Modification: model, skin and compatible animation FBX files merged into a runtime GLB; Vault originals unchanged",
            $"- Stable identity: `{plan.Item.ExternalId}` — `{plan.Item.Id}`",
            $"- Runtime GLB: `{chunk.RelativePath}` — SHA-256 `{chunk.ContentSha256}`",
            $"- Clips: {string.Join(", ", manifest.Animation!.Clips.Select(clip => $"{clip.Name} ({clip.DurationSeconds:0.###} s)"))}",
        });
        WriteTextAtomically(path, $"{existing}{Environment.NewLine}{Environment.NewLine}{section}{Environment.NewLine}");
    }

    private static void EnsureFile(string path, string extension, string label)
    {
        if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException($"{label} is missing or has an unsupported format.", path);
        }
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values) =>
        values.Count > 0 ? values[0] : throw new InvalidDataException("Expected a diagnostic.");

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

    private static string HashText(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Slug(string value) => string.Concat(value.ToLowerInvariant().Select(character =>
        char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-');

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

    private static void WriteTextAtomically(string targetPath, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(targetPath)!,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
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

    private sealed record AnimatedCharacterDocument(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("pack_id")] string PackId,
        string Version,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("source_catalog")] string SourceCatalog,
        [property: JsonPropertyName("source_pack_id")] string SourcePackId,
        string License,
        string Key,
        [property: JsonPropertyName("model_path")] string ModelPath,
        [property: JsonPropertyName("skin_path")] string SkinPath,
        [property: JsonPropertyName("skin_material")] string SkinMaterial,
        [property: JsonPropertyName("scale_meters")] decimal ScaleMeters,
        IReadOnlyList<string> Tags,
        IReadOnlyList<AnimationDocument> Animations);

    private sealed record AnimationDocument(string Name, string Path);
    private sealed record SourcePack(
        string ExtractedRoot,
        string License,
        string LicenseUrl,
        bool LicenseVerified,
        string SourcePage,
        string ArchiveSha256);
}

public sealed record CuratedAnimationSource(string Name, string RelativePath, string ContentSha256);

public sealed record CuratedAnimatedCharacterPlan(
    string PackId,
    string Version,
    string SourcePage,
    string LicenseUrl,
    AssetCatalogItem Item,
    AssetProcessingProfile Profile,
    AssetPackageManifest Manifest,
    AssetExternalEditConfiguration? Configuration,
    string? RequestRelativePath,
    string OutputRelativePath,
    IReadOnlyList<CuratedAnimationSource> Animations,
    bool Reused);

public sealed record CuratedAnimatedCharacterInstallResult(
    string PackId,
    string Version,
    AssetCatalogItemId AssetId,
    string OutputRelativePath,
    IReadOnlyList<string> Clips,
    int TriangleCount,
    bool Reused,
    string LicenseSpdx);
