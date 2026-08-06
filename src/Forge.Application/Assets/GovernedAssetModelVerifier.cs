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
            chunk.Role == AssetChunkRole.Model && chunk.Required &&
            string.Equals(Path.GetExtension(chunk.RelativePath), ".glb", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (models.Length != 1)
        {
            return Failure(
                CanonicalModelInvalidCode,
                "Asset installation requires exactly one governed canonical GLB model chunk.");
        }

        var model = models[0];
        if (!AssetCatalogValidator.IsSafeRelativePath(model.RelativePath))
        {
            return Failure(PathUnsafeCode, "The governed model path is not confined to the project workspace.");
        }

        try
        {
            var root = Path.GetFullPath(workspaceRoot);
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(
                root,
                model.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(PathUnsafeCode, "The governed model path escapes the project workspace.");
            }

            if (!File.Exists(absolute))
            {
                return Failure(FileMissingCode, $"Governed model '{model.RelativePath}' does not exist.");
            }

            var length = new FileInfo(absolute).Length;
            if (length != model.StoredBytes)
            {
                return Failure(
                    SizeMismatchCode,
                    $"Governed model '{model.RelativePath}' has {length} bytes; expected {model.StoredBytes}.");
            }

            using var stream = File.OpenRead(absolute);
            var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            return string.Equals(actual, model.ContentSha256, StringComparison.OrdinalIgnoreCase)
                ? new GovernedAssetModelVerification(true, null, null)
                : Failure(HashMismatchCode, $"Governed model '{model.RelativePath}' failed SHA-256 verification.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failure(ReadFailedCode, $"The governed model could not be verified: {exception.Message}");
        }
    }

    private static GovernedAssetModelVerification Failure(string code, string message) =>
        new(false, code, message);
}

public sealed record GovernedAssetModelVerification(
    bool Succeeded,
    string? DiagnosticCode,
    string? DiagnosticMessage);
