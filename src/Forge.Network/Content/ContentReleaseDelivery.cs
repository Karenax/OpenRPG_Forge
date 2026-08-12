using System.Security.Cryptography;
using Forge.Domain.Modules;

namespace Forge.Network.Content;

public sealed record ContentTrustRotationManifest(
    Guid Id,
    int SchemaVersion,
    string CurrentFingerprint,
    string NextFingerprint,
    DateTimeOffset ActivatesAt,
    DateTimeOffset RetiresCurrentAt,
    bool SignatureValid,
    string Provenance);

public sealed record ContentTrustRotationResult(
    bool Valid,
    string DiagnosticCode,
    ModuleSigningConfiguration? NextPolicy,
    string AuditSummary);

public static class ContentTrustRotationService
{
    public static ContentTrustRotationResult Preview(
        ModuleSigningConfiguration current,
        ContentTrustRotationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Id == Guid.Empty || manifest.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(manifest.Provenance) || !IsSha256(manifest.CurrentFingerprint) ||
            !IsSha256(manifest.NextFingerprint))
            return Reject("NET_CONTENT_KEY_ROTATION_INVALID");
        if (!manifest.SignatureValid ||
            !current.TrustedKeyFingerprints.Contains(manifest.CurrentFingerprint, StringComparer.Ordinal))
            return Reject("NET_CONTENT_KEY_ROTATION_UNTRUSTED");
        if (current.RevokedKeyFingerprints.Contains(manifest.NextFingerprint, StringComparer.Ordinal) ||
            manifest.CurrentFingerprint == manifest.NextFingerprint)
            return Reject("NET_CONTENT_KEY_ROTATION_CONFLICT");
        if (manifest.ActivatesAt >= manifest.RetiresCurrentAt)
            return Reject("NET_CONTENT_KEY_ROTATION_OVERLAP_REQUIRED");

        var trusted = current.TrustedKeyFingerprints.Append(manifest.NextFingerprint)
            .Distinct(StringComparer.Ordinal).ToArray();
        var next = new ModuleSigningConfiguration(current.Mode, current.SigningProfile, trusted,
            current.RevokedKeyFingerprints, current.AllowUnsignedDevelopment, current.SchemaVersion);
        return new(true, "NET_CONTENT_KEY_ROTATION_READY", next,
            $"rotation={manifest.Id:D};activate={manifest.ActivatesAt:O};retire={manifest.RetiresCurrentAt:O};keys={trusted.Length}");
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit) && value == value.ToLowerInvariant();

    private static ContentTrustRotationResult Reject(string code) => new(false, code, null, $"rejected={code}");
}

public sealed record ContentRangeResponse(long Offset, byte[] Bytes, string EntityTag);

public interface IContentRangeTransport
{
    ContentRangeResponse Read(string resourcePath, long offset, int maximumBytes);
}

public sealed record ContentRangeTransferResult(
    bool Applied,
    string DiagnosticCode,
    long ResumedBytes,
    long DownloadedBytes,
    int Requests,
    string Sha256);

public static class ContentRangeDeliveryService
{
    public static ContentRangeTransferResult Download(
        ContentRepositoryResource resource,
        string destinationPath,
        IContentRangeTransport transport,
        int chunkBytes = 64 * 1024,
        int maximumAttempts = 3)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(transport);
        if (chunkBytes is < 1024 or > 4 * 1024 * 1024 || maximumAttempts is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(chunkBytes), "NET_CONTENT_RANGE_POLICY_INVALID");
        var destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("Destination requires a parent directory.", nameof(destinationPath)));
        var resumed = File.Exists(destination) ? Math.Min(new FileInfo(destination).Length, resource.Size) : 0;
        if (File.Exists(destination) && new FileInfo(destination).Length > resource.Size)
        {
            File.Delete(destination);
            resumed = 0;
        }

        var offset = resumed;
        var downloaded = 0L;
        var requests = 0;
        string? entityTag = null;
        using (var output = new FileStream(destination, offset == 0 ? FileMode.Create : FileMode.Append,
                   FileAccess.Write, FileShare.None))
        {
            while (offset < resource.Size)
            {
                ContentRangeResponse? response = null;
                for (var attempt = 1; attempt <= maximumAttempts; attempt++)
                {
                    requests++;
                    try
                    {
                        response = transport.Read(resource.Path, offset,
                            (int)Math.Min(chunkBytes, resource.Size - offset));
                        break;
                    }
                    catch (IOException) when (attempt < maximumAttempts)
                    {
                    }
                }

                if (response is null || response.Offset != offset || response.Bytes.Length == 0 ||
                    response.Bytes.Length > resource.Size - offset ||
                    (entityTag is not null && response.EntityTag != entityTag))
                    return new(false, "NET_CONTENT_RANGE_INVALID", resumed, downloaded, requests, string.Empty);
                entityTag ??= response.EntityTag;
                output.Write(response.Bytes);
                offset += response.Bytes.Length;
                downloaded += response.Bytes.Length;
            }
        }

        using var input = File.OpenRead(destination);
        var hash = Convert.ToHexStringLower(SHA256.HashData(input));
        if (!string.Equals(hash, resource.Sha256, StringComparison.Ordinal))
            return new(false, "NET_CONTENT_RANGE_HASH_MISMATCH", resumed, downloaded, requests, hash);
        return new(true, "NET_CONTENT_RANGE_APPLIED", resumed, downloaded, requests, hash);
    }
}
