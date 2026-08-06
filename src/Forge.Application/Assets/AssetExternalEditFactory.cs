using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public static class AssetExternalEditFactory
{
    public static AssetExternalEditConfiguration CreateBlenderRoundTrip(
        AssetCatalogItem item,
        AssetPackageManifest manifest,
        string sourceFileSha256,
        bool watchForChanges = true)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(manifest);
        if (item.Id != manifest.AssetId)
        {
            throw new ArgumentException("Asset and package manifest IDs do not match.", nameof(manifest));
        }

        var source = item.RelativeModelPath
            ?? throw new ArgumentException("Asset has no model source path.", nameof(item));
        var modelChunk = manifest.Chunks
            .Where(chunk => chunk.Role == AssetChunkRole.Model)
            .OrderByDescending(chunk => chunk.Required)
            .ThenBy(chunk => chunk.RelativePath, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new ArgumentException("Asset package manifest has no model output chunk.", nameof(manifest));
        var platforms = modelChunk.Platforms
            .Concat(manifest.PlatformBudgets.Keys)
            .Distinct()
            .Order()
            .ToArray();
        var outputPath = modelChunk.RelativePath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            ? modelChunk.RelativePath
            : $"{modelChunk.RelativePath.Split("/source/", StringSplitOptions.None)[0]}/model.glb";
        var operations = new List<AssetExternalOperation>
        {
            AssetExternalOperation.ValidateModel,
            AssetExternalOperation.NormalizeModel,
        };
        if (manifest.Collision is not null)
        {
            operations.Add(AssetExternalOperation.GenerateCollision);
        }

        if (manifest.Lod is not null)
        {
            operations.Add(AssetExternalOperation.GenerateLods);
        }

        operations.Add(AssetExternalOperation.ExportGlb);
        operations.Add(AssetExternalOperation.GenerateThumbnail);
        operations.Add(AssetExternalOperation.AuditQuality);
        var configuration = new AssetExternalEditConfiguration(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(item.Id),
            item.Id,
            manifest.Id,
            $"{manifest.ModificationProvenance}:external-edit",
            item.LicenseSpdx,
            sourceFileSha256.Trim().ToLowerInvariant(),
            source,
            outputPath,
            AssetExternalTool.Blender,
            operations,
            watchForChanges,
            platforms);
        var diagnostics = AssetExternalEditValidator.Validate(configuration);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Generated external edit configuration is invalid: {diagnostics[0].Code} — {diagnostics[0].Message}");
        }

        return configuration;
    }
}
