using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Assets;

public static class CuratedEmbeddedAnimatedCharacterInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static CuratedEmbeddedAnimatedCharacterPlan Plan(
        string repositoryRoot,
        string vaultRoot,
        string workspaceRoot,
        string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var repository = Path.GetFullPath(repositoryRoot);
        var vault = Path.GetFullPath(vaultRoot);
        var workspace = Path.GetFullPath(workspaceRoot);
        var documentPath = Path.GetFullPath(manifestPath);
        EnsureContained(repository, documentPath, "Embedded animated character manifest escapes the repository root.");
        var document = JsonSerializer.Deserialize<EmbeddedAnimatedCharacterDocument>(
            File.ReadAllText(documentPath, Encoding.UTF8),
            JsonOptions) ?? throw new InvalidDataException("The embedded animated character manifest is empty.");
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
        EnsureModelFile(modelPath);
        var modelRelativePath = Normalize(Path.GetRelativePath(vault, modelPath));
        var modelHash = HashFile(modelPath);
        var sourceFormat = Path.GetExtension(modelPath).TrimStart('.').ToLowerInvariant();
        var expectedArmatureCount = document.ExpectedArmatureCount <= 0 ? 1 : document.ExpectedArmatureCount;
        var sourceFingerprint = HashText($"{sourcePack.ArchiveSha256}|{modelHash}");
        var externalId = $"{document.SourcePackId}:{document.Key}";
        var assetId = AssetCatalogItemId.ForExternalId(externalId);
        var outputRelativePath = $"assets/imported/{Slug(externalId)}/model.glb";
        var provenance =
            $"curated-pack:{document.PackId}@{document.Version};source:{document.SourcePackId};" +
            $"archive-sha256:{sourcePack.ArchiveSha256};model:{document.ModelPath}";
        var sourceBytes = new FileInfo(modelPath).Length;
        var requiredClips = document.Clips.Required().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var item = new AssetCatalogItem(
            assetId,
            externalId,
            document.DisplayName,
            sourcePack.Source,
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
            sourceFormat,
            "glb",
            document.License,
            sourceFingerprint,
            provenance,
            Math.Max(sourceBytes * 2, 1_048_576),
            model: new ModelProcessingOptions(true, true, false, [], [1m]));
        var baseManifest = AssetPackageManifestFactory.Create(
            item,
            profile,
            new AssetManifestArtifactInput(outputRelativePath, modelHash, sourceBytes, false),
            AssetPackageManifestAuthoringOptions.Default with
            {
                ModificationProvenance = $"author:openrpg-forge:{document.PackId}@{document.Version}",
                RequireGeneratedExtensions = false,
                Rig = document.Rig,
                PreviewClipName = document.Clips.Idle,
                PreviewClipDurationSeconds = 1m,
                IncludeVfx = false,
            });
        var existingProfiles = AssetProcessingProfileStore.Load(workspace);
        var existingManifests = AssetPackageManifestStore.Load(workspace);
        var currentManifest = existingManifests.Manifests.SingleOrDefault(candidate => candidate.AssetId == assetId);
        var currentProfile = existingProfiles.Profiles.SingleOrDefault(candidate => candidate.AssetId == assetId);
        if (existingProfiles.Succeeded && existingManifests.Succeeded && currentManifest is not null &&
            ProfilesEquivalent(currentProfile, profile) &&
            IsReusable(workspace, currentManifest, sourceFingerprint, document.Rig, requiredClips))
        {
            return new CuratedEmbeddedAnimatedCharacterPlan(
                document.PackId,
                document.Version,
                sourcePack.Name,
                sourcePack.SourcePage,
                sourcePack.LicenseUrl,
                item,
                profile,
                currentManifest,
                null,
                null,
                outputRelativePath,
                document.Rig,
                expectedArmatureCount,
                document.ExpectedBoneCount,
                document.AnimationProfile,
                document.Clips,
                requiredClips,
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
                [],
                []));
        if (!request.Succeeded)
        {
            throw new InvalidDataException(AssertSingle(request.Diagnostics).Message);
        }

        return new CuratedEmbeddedAnimatedCharacterPlan(
            document.PackId,
            document.Version,
            sourcePack.Name,
            sourcePack.SourcePage,
            sourcePack.LicenseUrl,
            item,
            profile,
            baseManifest,
            configuration,
            request.RequestRelativePath,
            outputRelativePath,
            document.Rig,
            expectedArmatureCount,
            document.ExpectedBoneCount,
            document.AnimationProfile,
            document.Clips,
            requiredClips,
            false);
    }

    public static CuratedEmbeddedAnimatedCharacterInstallResult Complete(
        string workspaceRoot,
        CuratedEmbeddedAnimatedCharacterPlan plan)
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
            throw new InvalidOperationException("Embedded animated character build configuration is missing.");
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
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingClips = plan.RequiredClips.Except(clipNames, StringComparer.Ordinal).ToArray();
        if (missingClips.Length > 0 ||
            metrics.GetProperty("armatureCount").GetInt32() != plan.ExpectedArmatureCount ||
            metrics.GetProperty("boneCount").GetInt32() != plan.ExpectedBoneCount)
        {
            throw new InvalidDataException(
                $"Embedded animated character quality report does not match rig '{plan.Rig}', " +
                $"{plan.ExpectedArmatureCount} armatures, {plan.ExpectedBoneCount} bones and required clips. " +
                $"Missing: {string.Join(", ", missingClips)}.");
        }

        var durations = metrics.GetProperty("animationDurationsSeconds");
        var clips = clipNames
            .Where(name => durations.GetProperty(name).GetDecimal() > 0)
            .Select(name => new AssetAnimationClipManifest(
            name,
            durations.GetProperty(name).GetDecimal(),
            false,
            AssetAnimationCompression.Balanced,
            [])).ToArray();
        var manifest = finalized.Manifest with
        {
            Animation = new AssetAnimationManifest(true, modelChunk.Id, plan.Rig, null, clips),
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
                $"Embedded animated character manifest is invalid: {diagnostics[0].Code} - {diagnostics[0].Message}");
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

    private static CuratedEmbeddedAnimatedCharacterInstallResult Result(
        CuratedEmbeddedAnimatedCharacterPlan plan,
        AssetPackageManifest manifest,
        bool reused) =>
        new(
            plan.PackId,
            plan.Version,
            plan.Item.Id,
            manifest.Chunks.Single(chunk => chunk.Role == AssetChunkRole.Model).RelativePath,
            manifest.Animation?.Clips.Select(clip => clip.Name).Order(StringComparer.Ordinal).ToArray() ?? [],
            manifest.ModelMetadata?.Metrics.TriangleCount ?? 0,
            plan.AnimationProfile,
            plan.Clips,
            reused,
            plan.Item.LicenseSpdx);

    private static bool IsReusable(
        string workspace,
        AssetPackageManifest manifest,
        string sourceFingerprint,
        string rig,
        IReadOnlyList<string> requiredClips)
    {
        var chunk = manifest.Chunks.SingleOrDefault(candidate => candidate.Role == AssetChunkRole.Model);
        if (chunk is null || manifest.ModelMetadata is null || manifest.Animation is null ||
            !string.Equals(manifest.SourceContentSha256, sourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(manifest.Animation.Rig, rig, StringComparison.Ordinal) ||
            requiredClips.Except(manifest.Animation.Clips.Select(clip => clip.Name), StringComparer.Ordinal).Any())
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
            actual.MaximumOutputBytes != expected.MaximumOutputBytes || actual.Texture is not null || expected.Texture is not null)
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

    private static void ValidateDocument(EmbeddedAnimatedCharacterDocument document)
    {
        if (document.SchemaVersion != 1 || string.IsNullOrWhiteSpace(document.PackId) ||
            string.IsNullOrWhiteSpace(document.Version) || string.IsNullOrWhiteSpace(document.DisplayName) ||
            string.IsNullOrWhiteSpace(document.SourceCatalog) || string.IsNullOrWhiteSpace(document.SourcePackId) ||
            string.IsNullOrWhiteSpace(document.License) || string.IsNullOrWhiteSpace(document.Key) ||
            string.IsNullOrWhiteSpace(document.ModelPath) || document.ScaleMeters is < 0.001m or > 1000m ||
            string.IsNullOrWhiteSpace(document.Rig) || document.ExpectedArmatureCount < 0 ||
            document.ExpectedBoneCount <= 0 ||
            string.IsNullOrWhiteSpace(document.AnimationProfile) || document.Tags.Count == 0 ||
            document.Clips.Required().Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("The curated embedded animated character manifest is invalid.");
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
                pack.GetProperty("name").GetString() ?? string.Empty,
                pack.GetProperty("source").GetString() ?? string.Empty,
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
        CuratedEmbeddedAnimatedCharacterPlan plan,
        AssetPackageManifest manifest)
    {
        var path = Path.Combine(workspace, "assets", "CREDITS_ASSETS.md");
        var existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8).TrimEnd() : "# Asset credits";
        var heading = $"## Animated character — {plan.Item.DisplayName}";
        existing = RemoveSection(existing, heading);
        var chunk = manifest.Chunks.Single(candidate => candidate.Role == AssetChunkRole.Model);
        var section = string.Join(Environment.NewLine,
        new string[]
        {
            heading,
            string.Empty,
            $"- Pack: {plan.PackId} {plan.Version}",
            $"- Source: {plan.SourcePackName} ({plan.SourcePage})",
            $"- License: {plan.Item.LicenseSpdx} ({plan.LicenseUrl})",
            "- License verification: official source repository and governed local catalog",
            "- Modification: runtime-ready GLB normalized, audited and re-exported; Vault original unchanged",
            $"- Stable identity: `{plan.Item.ExternalId}` — `{plan.Item.Id}`",
            $"- Runtime GLB: `{chunk.RelativePath}` — SHA-256 `{chunk.ContentSha256}`",
            $"- Rig: {plan.Rig} ({plan.ExpectedArmatureCount} armatures, {plan.ExpectedBoneCount} bones)",
            $"- Semantic clips: idle={plan.Clips.Idle}, run={plan.Clips.Run}, jump={plan.Clips.Jump}, " +
            $"attack={plan.Clips.Attack}, hit={plan.Clips.Hit}, death={plan.Clips.Death}",
            $"- Available clips: {manifest.Animation!.Clips.Count}",
        });
        WriteTextAtomically(path, $"{existing}{Environment.NewLine}{Environment.NewLine}{section}{Environment.NewLine}");
    }

    private static string RemoveSection(string content, string heading)
    {
        var start = content.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0) return content;
        var next = content.IndexOf($"{Environment.NewLine}## ", start + heading.Length, StringComparison.Ordinal);
        return (next < 0 ? content[..start] : content.Remove(start, next - start)).TrimEnd();
    }

    private static void EnsureModelFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!File.Exists(path) || extension is not (".glb" or ".fbx"))
        {
            throw new FileNotFoundException(
                "Embedded animated character model is missing or has an unsupported format.", path);
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

    private sealed record EmbeddedAnimatedCharacterDocument(
        [property: JsonPropertyName("schema_version")] int SchemaVersion,
        [property: JsonPropertyName("pack_id")] string PackId,
        string Version,
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("source_catalog")] string SourceCatalog,
        [property: JsonPropertyName("source_pack_id")] string SourcePackId,
        string License,
        string Key,
        [property: JsonPropertyName("model_path")] string ModelPath,
        [property: JsonPropertyName("scale_meters")] decimal ScaleMeters,
        string Rig,
        [property: JsonPropertyName("expected_armature_count")] int ExpectedArmatureCount,
        [property: JsonPropertyName("expected_bone_count")] int ExpectedBoneCount,
        [property: JsonPropertyName("animation_profile")] string AnimationProfile,
        EmbeddedAnimationClips Clips,
        IReadOnlyList<string> Tags);

    private sealed record SourcePack(
        string Name,
        string Source,
        string ExtractedRoot,
        string License,
        string LicenseUrl,
        bool LicenseVerified,
        string SourcePage,
        string ArchiveSha256);
}

public sealed record EmbeddedAnimationClips(
    string Idle,
    string Run,
    string Jump,
    string Attack,
    string Hit,
    string Death)
{
    public IReadOnlyList<string> Required() => [Idle, Run, Jump, Attack, Hit, Death];
}

public sealed record CuratedEmbeddedAnimatedCharacterPlan(
    string PackId,
    string Version,
    string SourcePackName,
    string SourcePage,
    string LicenseUrl,
    AssetCatalogItem Item,
    AssetProcessingProfile Profile,
    AssetPackageManifest Manifest,
    AssetExternalEditConfiguration? Configuration,
    string? RequestRelativePath,
    string OutputRelativePath,
    string Rig,
    int ExpectedArmatureCount,
    int ExpectedBoneCount,
    string AnimationProfile,
    EmbeddedAnimationClips Clips,
    IReadOnlyList<string> RequiredClips,
    bool Reused);

public sealed record CuratedEmbeddedAnimatedCharacterInstallResult(
    string PackId,
    string Version,
    AssetCatalogItemId AssetId,
    string OutputRelativePath,
    IReadOnlyList<string> Clips,
    int TriangleCount,
    string AnimationProfile,
    EmbeddedAnimationClips SemanticClips,
    bool Reused,
    string LicenseSpdx);
