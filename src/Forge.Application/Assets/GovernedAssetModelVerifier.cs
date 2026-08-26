using System.Security.Cryptography;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public static class GovernedAssetModelVerifier
{
    public const string CanonicalModelInvalidCode = "asset.model.canonical_invalid";
    public const string PathUnsafeCode = "asset.model.path_unsafe";
    public const string FileMissingCode = "asset.model.file_missing";
    public const string SizeMismatchCode = "asset.model.size_mismatch";
    public const string HashMismatchCode = "asset.model.hash_mismatch";
    public const string ReadFailedCode = "asset.model.read_failed";

    public static GovernedAssetModelVerification Verify(
        string workspaceRoot,
        AssetPackageManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(manifest);

        var models = manifest.Chunks.Where(chunk =>
            chunk.Role == AssetChunkRole.Model && chunk.Required && IsRuntimeModel(chunk.RelativePath)).ToArray();
        if (models.Length != 1)
        {
            return Failure(
                CanonicalModelInvalidCode,
                "Asset installation requires exactly one governed glTF or GLB model chunk.");
        }

        if (manifest.Chunks.Any(chunk => !AssetCatalogValidator.IsSafeRelativePath(chunk.RelativePath)))
        {
            return Failure(PathUnsafeCode, "A governed asset chunk path is not confined to the project workspace.");
        }

        try
        {
            var root = Path.GetFullPath(workspaceRoot);
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var chunk in manifest.Chunks)
            {
                var absolute = Path.GetFullPath(Path.Combine(
                    root,
                    chunk.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!absolute.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(PathUnsafeCode, "A governed asset chunk path escapes the project workspace.");
                }

                if (!File.Exists(absolute))
                {
                    return Failure(FileMissingCode, $"Governed asset chunk '{chunk.RelativePath}' does not exist.");
                }

                var length = new FileInfo(absolute).Length;
                if (length != chunk.StoredBytes)
                {
                    return Failure(
                        SizeMismatchCode,
                        $"Governed asset chunk '{chunk.RelativePath}' has {length} bytes; expected {chunk.StoredBytes}.");
                }

                using var stream = File.OpenRead(absolute);
                var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                if (!string.Equals(actual, chunk.ContentSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(HashMismatchCode,
                        $"Governed asset chunk '{chunk.RelativePath}' failed SHA-256 verification.");
                }
            }

            return new GovernedAssetModelVerification(true, null, null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failure(ReadFailedCode, $"The governed model could not be verified: {exception.Message}");
        }
    }

    private static GovernedAssetModelVerification Failure(string code, string message) =>
        new(false, code, message);

    private static bool IsRuntimeModel(string path) =>
        string.Equals(Path.GetExtension(path), ".glb", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetExtension(path), ".gltf", StringComparison.OrdinalIgnoreCase);
}

public sealed record GovernedAssetModelVerification(
    bool Succeeded,
    string? DiagnosticCode,
    string? DiagnosticMessage);
