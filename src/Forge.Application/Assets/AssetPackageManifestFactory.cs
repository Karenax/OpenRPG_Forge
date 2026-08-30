using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public static class AssetPackageManifestFactory
{
    public static AssetPackageManifest Create(
        AssetCatalogItem item,
        AssetProcessingProfile profile,
        AssetManifestArtifactInput artifact,
        AssetPackageManifestAuthoringOptions? authoring = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(artifact);
        if (item.Id != profile.AssetId)
        {
            throw new ArgumentException("Asset catalog item and processing profile IDs do not match.", nameof(profile));
        }

        authoring ??= AssetPackageManifestAuthoringOptions.Default;
        var chunkId = StableGuid($"{item.Id}|{artifact.OutputRelativePath}");
        var role = profile.Kind == AssetProcessingKind.Model ? AssetChunkRole.Model : AssetChunkRole.Texture;
        var chunk = new AssetManifestChunk(
            chunkId,
            role,
            true,
            artifact.OutputRelativePath,
            artifact.OutputContentSha256,
            artifact.OutputBytes,
            artifact.OutputBytes,
            AssetChunkCompression.None,
            authoring.Platforms);
        var canonicalReady = artifact.CanonicalReady;
        AssetCollisionManifest? collision = null;
        AssetAnimationManifest? animation = null;
        AssetLodManifest? lod = null;
        if (profile.Model is { } model)
        {
            if (model.GenerateCollisions)
            {
                collision = new AssetCollisionManifest(
                    authoring.RequireGeneratedExtensions,
                    canonicalReady ? chunkId : null,
                    canonicalReady
                        ? AssetCollisionGeneration.Embedded
                        : authoring.CollisionGeneration,
                    authoring.CollisionMaterialTags,
                    authoring.Platforms);
            }

            if (model.ImportAnimations)
            {
                animation = new AssetAnimationManifest(
                    authoring.RequireGeneratedExtensions,
                    canonicalReady ? chunkId : null,
                    authoring.Rig,
                    authoring.RetargetMap,
                    [new AssetAnimationClipManifest(
                        authoring.PreviewClipName,
                        authoring.PreviewClipDurationSeconds,
                        authoring.RootMotion,
                        authoring.AnimationCompression,
                        authoring.PreviewClipEvents)]);
            }

            lod = new AssetLodManifest(
                false,
                authoring.LodCrossFadeSeconds,
                model.LodRatios
                    .Select((ratio, index) => new AssetLodLevelManifest(
                        index,
                        index == 0 ? chunkId : null,
                        ratio,
                        Math.Max(1, (int)Math.Round(authoring.BaseTriangleBudget * ratio))))
                    .ToArray());
        }

        var vfx = authoring.IncludeVfx
            ? new AssetVfxManifest(
                false,
                null,
                authoring.VfxProvider,
                authoring.MaximumParticles,
                authoring.MaximumDrawCalls,
                authoring.VfxCullDistance)
            : null;
        var platformBudgets = authoring.Platforms
            .Where(platform => platform != AssetTargetPlatform.Any)
            .DefaultIfEmpty(AssetTargetPlatform.Any)
            .Distinct()
            .Order()
            .ToDictionary(platform => platform, _ => profile.MaximumOutputBytes);
        var manifest = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(item.Id),
            item.Id,
            authoring.ModificationProvenance,
            item.LicenseSpdx,
            item.ContentSha256,
            platformBudgets,
            [chunk],
            collision,
            animation,
            vfx,
            lod,
            Intelligence: item.Intelligence);
        var diagnostics = AssetPackageManifestValidator.Validate(manifest);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Generated asset package manifest is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}",
                nameof(authoring));
        }

        return manifest;
    }

    public static AssetPackageManifest AttachContentExtensions(
        AssetPackageManifest manifest,
        AssetCollisionAuthoringInput collision,
        AssetNavigationAuthoringInput navigation,
        AssetFontAuthoringInput font)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(collision);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(font);
        var chunks = manifest.Chunks.ToList();
        var collisionChunk = AddChunk(chunks, AssetChunkRole.Collision, collision.Artifact, collision.Required);
        var navigationChunk = AddChunk(chunks, AssetChunkRole.Navigation, navigation.Artifact, navigation.Required);
        var primaryFontChunk = AddChunk(chunks, AssetChunkRole.Font, font.PrimaryArtifact, font.Required);
        var fallbackChunks = font.Fallbacks.Select(fallback => new AssetFontFallbackManifest(
            fallback.Family,
            AddChunk(chunks, AssetChunkRole.Font, fallback.Artifact, required: true).Id)).ToArray();
        var updated = manifest with
        {
            Chunks = chunks.OrderBy(chunk => chunk.Role).ThenBy(chunk => chunk.RelativePath, StringComparer.Ordinal).ToArray(),
            Collision = new AssetCollisionManifest(
                collision.Required, collisionChunk.Id, collision.Generation,
                collision.MaterialTags, collision.CookPlatforms),
            Navigation = new AssetNavigationManifest(
                navigation.Required, navigationChunk.Id, navigation.Generation,
                navigation.MaterialTags, navigation.CookPlatforms),
            Font = new AssetFontManifest(
                font.Required, font.PrimaryFamily, primaryFontChunk.Id, fallbackChunks,
                font.ShapingScripts, font.LocalizationTestLocales, font.DpiBuckets,
                font.MinimumTextScale, font.MaximumTextScale),
        };
        var diagnostics = AssetPackageManifestValidator.Validate(updated);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Authored content extensions are invalid: {diagnostics[0].Code} — {diagnostics[0].Message}.",
                nameof(manifest));
        }

        return updated;
    }

    private static AssetManifestChunk AddChunk(
        ICollection<AssetManifestChunk> chunks,
        AssetChunkRole role,
        AssetExtensionArtifactInput artifact,
        bool required)
    {
        var chunk = new AssetManifestChunk(
            StableGuid($"{role}|{artifact.RelativePath}|{artifact.ContentSha256}"),
            role,
            required,
            artifact.RelativePath,
            artifact.ContentSha256,
            artifact.UncompressedBytes,
            artifact.StoredBytes,
            artifact.Compression,
            artifact.Platforms);
        chunks.Add(chunk);
        return chunk;
    }

    private static Guid StableGuid(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:asset-chunk:{input}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}

public sealed record AssetManifestArtifactInput(
    string OutputRelativePath,
    string OutputContentSha256,
    long OutputBytes,
    bool CanonicalReady);

public sealed record AssetExtensionArtifactInput(
    string RelativePath,
    string ContentSha256,
    long UncompressedBytes,
    long StoredBytes,
    AssetChunkCompression Compression,
    IReadOnlyList<AssetTargetPlatform> Platforms);

public sealed record AssetCollisionAuthoringInput(
    AssetExtensionArtifactInput Artifact,
    bool Required,
    AssetCollisionGeneration Generation,
    IReadOnlyList<string> MaterialTags,
    IReadOnlyList<AssetTargetPlatform> CookPlatforms);

public sealed record AssetNavigationAuthoringInput(
    AssetExtensionArtifactInput Artifact,
    bool Required,
    AssetNavigationGeneration Generation,
    IReadOnlyList<string> MaterialTags,
    IReadOnlyList<AssetTargetPlatform> CookPlatforms);

public sealed record AssetFontFallbackAuthoringInput(
    string Family,
    AssetExtensionArtifactInput Artifact);

public sealed record AssetFontAuthoringInput(
    string PrimaryFamily,
    AssetExtensionArtifactInput PrimaryArtifact,
    IReadOnlyList<AssetFontFallbackAuthoringInput> Fallbacks,
    bool Required,
    IReadOnlyList<string> ShapingScripts,
    IReadOnlyList<string> LocalizationTestLocales,
    IReadOnlyList<int> DpiBuckets,
    decimal MinimumTextScale,
    decimal MaximumTextScale);

public sealed record AssetPackageManifestAuthoringOptions(
    string ModificationProvenance,
    IReadOnlyList<AssetTargetPlatform> Platforms,
    bool RequireGeneratedExtensions,
    AssetCollisionGeneration CollisionGeneration,
    IReadOnlyList<string> CollisionMaterialTags,
    string Rig,
    string? RetargetMap,
    string PreviewClipName,
    decimal PreviewClipDurationSeconds,
    bool RootMotion,
    AssetAnimationCompression AnimationCompression,
    IReadOnlyList<string> PreviewClipEvents,
    bool IncludeVfx,
    string VfxProvider,
    int MaximumParticles,
    int MaximumDrawCalls,
    decimal VfxCullDistance,
    decimal LodCrossFadeSeconds,
    int BaseTriangleBudget)
{
    public static AssetPackageManifestAuthoringOptions Default { get; } = new(
        "author:openrpg-forge-content-browser",
        [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux],
        false,
        AssetCollisionGeneration.ConvexDecomposition,
        ["default"],
        "humanoid",
        null,
        "preview",
        1m,
        false,
        AssetAnimationCompression.Balanced,
        [],
        false,
        "forge.vfxgraph",
        2048,
        16,
        80m,
        0.15m,
        100_000);
}
