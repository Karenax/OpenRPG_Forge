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
            lod);
        var diagnostics = AssetPackageManifestValidator.Validate(manifest);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Generated asset package manifest is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}",
                nameof(authoring));
        }

        return manifest;
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
