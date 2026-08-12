using System.Security.Cryptography;
using System.Text;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Network.Content;

public sealed record ContentRepositoryResource(string Path, long Size, string Sha256,
    string LicenseSpdx, string Provenance);

public sealed record ContentRepositoryManifest(Guid Id, int SchemaVersion, string ModuleId,
    string ModuleVersion, string? SignerFingerprint, IReadOnlyList<ContentRepositoryResource> Resources,
    string Fingerprint);

public sealed record ContentSyncDiagnostic(string Code, string Path, string Message);

public sealed record ContentSyncItem(string Path, string Sha256, long Size, long ResumeOffset,
    bool CacheHit, string CachePath, int ChunkCount = 0, bool DeltaRequired = false);

public sealed record ContentSyncReport(bool Valid, bool Applied, string Fingerprint,
    IReadOnlyList<ContentSyncItem> Items, IReadOnlyList<ContentSyncDiagnostic> Diagnostics,
    long DownloadedBytes, long ReusedBytes);

public interface IContentRepositoryTransport
{
    Stream OpenRead(string resourcePath, long offset);
}

public static class ContentRepositoryManifestService
{
    public const int CurrentSchemaVersion = 1;

    public static ContentRepositoryManifest Create(Guid id, string moduleId, string moduleVersion,
        string? signerFingerprint, IEnumerable<ContentRepositoryResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(moduleVersion))
            throw new ArgumentException("Content repository manifests require identity and version.");
        var ordered = resources.Select(Normalize).OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0 || ordered.Select(item => item.Path).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException("Content repository paths must be unique and non-empty.", nameof(resources));
        var signer = string.IsNullOrWhiteSpace(signerFingerprint) ? null : signerFingerprint.Trim().ToLowerInvariant();
        if (signer is not null && (signer.Length != 64 || signer.Any(character => !Uri.IsHexDigit(character))))
            throw new ArgumentException("Signer fingerprint must be SHA-256.", nameof(signerFingerprint));
        var canonical = string.Join('\n', new[] { id.ToString("D"), moduleId.Trim(), moduleVersion.Trim(), signer ?? "unsigned" }
            .Concat(ordered.Select(item => $"{item.Path}|{item.Size}|{item.Sha256}|{item.LicenseSpdx}|{item.Provenance}")));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new ContentRepositoryManifest(id, CurrentSchemaVersion, moduleId.Trim(), moduleVersion.Trim(),
            signer, ordered, fingerprint);
    }

    private static ContentRepositoryResource Normalize(ContentRepositoryResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var path = AgentTaskManifest.NormalizePaths([resource.Path], nameof(resource.Path))[0];
        var hash = resource.Sha256.Trim().ToLowerInvariant();
        if (resource.Size < 0 || hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)) ||
            string.IsNullOrWhiteSpace(resource.LicenseSpdx) || string.IsNullOrWhiteSpace(resource.Provenance))
            throw new ArgumentException("Content repository resources require size, SHA-256, license and provenance.");
        return resource with
        {
            Path = path,
            Sha256 = hash,
            LicenseSpdx = resource.LicenseSpdx.Trim().ToUpperInvariant(),
            Provenance = resource.Provenance.Trim(),
        };
    }
}

public sealed class ContentRepositorySyncService
{
    private readonly ContentSynchronizationPolicy _policy;
    private readonly string _cacheRoot;
    private readonly int _chunkBytes;
    private readonly bool _deltaEnabled;

    public ContentRepositorySyncService(ContentSynchronizationPolicy policy, string userStorageRoot,
        int chunkBytes = 64 * 1024, bool deltaEnabled = true)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (chunkBytes is < 16 * 1024 or > 4 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(chunkBytes), "NET_CONTENT_CHUNK_SIZE_INVALID");
        _chunkBytes = chunkBytes;
        _deltaEnabled = deltaEnabled;
        if (!Path.IsPathFullyQualified(userStorageRoot))
            throw new ArgumentException("User storage root must be absolute.", nameof(userStorageRoot));
        var root = Path.GetFullPath(userStorageRoot);
        _cacheRoot = Path.GetFullPath(Path.Combine(root, policy.StorageDirectory));
        if (!_cacheRoot.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Content cache must remain inside user storage.", nameof(userStorageRoot));
    }

    public ContentSyncReport Plan(ContentRepositoryManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var diagnostics = Validate(manifest);
        if (diagnostics.Count != 0)
            return new ContentSyncReport(false, false, manifest.Fingerprint, [], diagnostics, 0, 0);
        var items = manifest.Resources.Select(resource =>
        {
            var cachePath = CachePath(resource.Sha256);
            var cacheHit = File.Exists(cachePath) && Verify(cachePath, resource);
            var partial = PartialPath(resource.Sha256);
            var resume = _policy.Resumable && File.Exists(partial)
                ? Math.Min(new FileInfo(partial).Length, resource.Size) : 0;
            var remaining = Math.Max(0, resource.Size - (cacheHit ? resource.Size : resume));
            return new ContentSyncItem(resource.Path, resource.Sha256, resource.Size,
                cacheHit ? resource.Size : resume, cacheHit, cachePath,
                (int)Math.Ceiling(remaining / (double)_chunkBytes), _deltaEnabled && !cacheHit && resume > 0);
        }).ToArray();
        var reused = items.Where(item => item.CacheHit).Sum(item => item.Size);
        return new ContentSyncReport(diagnostics.Count == 0, false, manifest.Fingerprint, items, diagnostics, 0, reused);
    }

    public ContentSyncReport Synchronize(ContentRepositoryManifest manifest, IContentRepositoryTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var plan = Plan(manifest);
        if (!plan.Valid) return plan;
        Directory.CreateDirectory(Path.Combine(_cacheRoot, "blobs"));
        Directory.CreateDirectory(Path.Combine(_cacheRoot, "partial"));
        var downloaded = 0L;
        foreach (var item in plan.Items.Where(item => !item.CacheHit))
        {
            var resource = manifest.Resources.Single(candidate => candidate.Path == item.Path);
            var partial = PartialPath(resource.Sha256);
            var offset = _policy.Resumable && File.Exists(partial) ? new FileInfo(partial).Length : 0;
            if (offset > resource.Size) { File.Delete(partial); offset = 0; }
            using var source = transport.OpenRead(resource.Path, offset);
            using (var destination = new FileStream(partial, offset == 0 ? FileMode.Create : FileMode.Append,
                       FileAccess.Write, FileShare.None))
            {
                var remaining = resource.Size - offset;
                var buffer = new byte[_chunkBytes];
                while (remaining > 0)
                {
                    var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (read == 0) break;
                    destination.Write(buffer, 0, read);
                    remaining -= read;
                    downloaded += read;
                    if (CurrentCacheBytes() + downloaded > _policy.MaximumCacheBytes)
                        throw new IOException("NET_CONTENT_CACHE_QUOTA_EXCEEDED");
                }
            }
            if (new FileInfo(partial).Length != resource.Size || _policy.VerifyContent && !Verify(partial, resource))
                throw new InvalidDataException($"NET_CONTENT_HASH_MISMATCH:{resource.Path}");
            var cachePath = CachePath(resource.Sha256);
            if (File.Exists(cachePath)) File.Delete(partial);
            else File.Move(partial, cachePath, false);
        }
        var completed = Plan(manifest);
        return completed with { Applied = true, DownloadedBytes = downloaded };
    }

    private IReadOnlyList<ContentSyncDiagnostic> Validate(ContentRepositoryManifest manifest)
    {
        var diagnostics = new List<ContentSyncDiagnostic>();
        if (manifest.SchemaVersion != ContentRepositoryManifestService.CurrentSchemaVersion)
            diagnostics.Add(new("NET_CONTENT_SCHEMA_UNSUPPORTED", "manifest/schemaVersion", "Content repository schema is unsupported."));
        try
        {
            var canonical = ContentRepositoryManifestService.Create(manifest.Id, manifest.ModuleId,
                manifest.ModuleVersion, manifest.SignerFingerprint, manifest.Resources);
            if (!string.Equals(canonical.Fingerprint, manifest.Fingerprint, StringComparison.Ordinal))
                diagnostics.Add(new("NET_CONTENT_MANIFEST_TAMPERED", "manifest/fingerprint",
                    "Content repository manifest fingerprint does not match its canonical payload."));
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(new("NET_CONTENT_MANIFEST_INVALID", "manifest", exception.Message));
            return diagnostics;
        }
        if (_policy.RequireTrustedSigner && (manifest.SignerFingerprint is null ||
            !_policy.TrustedSignerFingerprints.Contains(manifest.SignerFingerprint, StringComparer.Ordinal)))
            diagnostics.Add(new("NET_CONTENT_SIGNER_UNTRUSTED", "manifest/signerFingerprint", "Content signer is not trusted by user policy."));
        foreach (var resource in manifest.Resources.Where(resource =>
                     !_policy.AllowedLicenses.Contains(resource.LicenseSpdx, StringComparer.OrdinalIgnoreCase)))
            diagnostics.Add(new("NET_CONTENT_LICENSE_BLOCKED", $"manifest/resources/{resource.Path}/licenseSpdx",
                $"License '{resource.LicenseSpdx}' is not allowed."));
        var projected = manifest.Resources.Select(resource => resource.Sha256).Distinct(StringComparer.Ordinal)
            .Where(hash => !File.Exists(CachePath(hash))).Sum(hash => manifest.Resources.First(item => item.Sha256 == hash).Size);
        if (projected + CurrentCacheBytes() > _policy.MaximumCacheBytes)
            diagnostics.Add(new("NET_CONTENT_CACHE_QUOTA_EXCEEDED", "policy/maximumCacheBytes", "Content cache quota would be exceeded."));
        return diagnostics;
    }

    private long CurrentCacheBytes()
    {
        var blobs = Path.Combine(_cacheRoot, "blobs");
        return Directory.Exists(blobs) ? Directory.EnumerateFiles(blobs).Sum(path => new FileInfo(path).Length) : 0;
    }

    private string CachePath(string sha256) => Path.Combine(_cacheRoot, "blobs", $"{sha256}.blob");
    private string PartialPath(string sha256) => Path.Combine(_cacheRoot, "partial", $"{sha256}.partial");
    private static bool Verify(string path, ContentRepositoryResource resource)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != resource.Size) return false;
        using var stream = File.OpenRead(path);
        return string.Equals(Convert.ToHexStringLower(SHA256.HashData(stream)), resource.Sha256,
            StringComparison.Ordinal);
    }
}
