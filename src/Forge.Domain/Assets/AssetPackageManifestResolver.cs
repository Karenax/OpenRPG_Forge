using Forge.Domain.Validation;

namespace Forge.Domain.Assets;

public static class AssetPackageManifestResolver
{
    public const string RequiredChunkMissingCode = "asset.manifest.runtime_chunk_missing";
    public const string ChunkHashMismatchCode = "asset.manifest.runtime_chunk_hash_mismatch";

    public static AssetPackageManifestResolution Resolve(
        AssetPackageManifest manifest,
        AssetTargetPlatform platform,
        IReadOnlyDictionary<string, string> availableContentHashes)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(availableContentHashes);
        var diagnostics = AssetPackageManifestValidator.Validate(manifest).ToList();
        var chunks = manifest.Chunks
            .Where(chunk => chunk.Platforms.Contains(AssetTargetPlatform.Any) ||
                chunk.Platforms.Contains(platform))
            .OrderBy(chunk => chunk.Role)
            .ThenBy(chunk => chunk.RelativePath, StringComparer.Ordinal)
            .ToArray();
        foreach (var chunk in chunks.Where(chunk => chunk.Required))
        {
            if (!availableContentHashes.ContainsKey(chunk.RelativePath))
            {
                diagnostics.Add(new(
                    RequiredChunkMissingCode,
                    $"Required asset chunk '{chunk.RelativePath}' is unavailable for {platform}.",
                    $"assets/manifests/{manifest.Id}/chunks/{chunk.Id}"));
            }
        }

        foreach (var chunk in chunks.Where(chunk => availableContentHashes.ContainsKey(chunk.RelativePath)))
        {
            if (!string.Equals(
                    availableContentHashes[chunk.RelativePath],
                    chunk.ContentSha256,
                    StringComparison.Ordinal))
            {
                diagnostics.Add(new(
                    ChunkHashMismatchCode,
                    $"Asset chunk '{chunk.RelativePath}' does not match its manifest SHA-256.",
                    $"assets/manifests/{manifest.Id}/chunks/{chunk.Id}/contentSha256"));
            }
        }

        return new AssetPackageManifestResolution(chunks, diagnostics);
    }
}

public sealed record AssetPackageManifestResolution(
    IReadOnlyList<AssetManifestChunk> Chunks,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
