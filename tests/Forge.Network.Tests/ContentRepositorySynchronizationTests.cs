using System.Security.Cryptography;
using Forge.Domain.RuntimeExtensions;
using Forge.Network.Content;

namespace Forge.Network.Tests;

public sealed class ContentRepositorySynchronizationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Synchronization_resumes_verifies_and_reuses_content_addressed_cache()
    {
        Directory.CreateDirectory(_root);
        var content = System.Text.Encoding.UTF8.GetBytes("resumable-openrpg-content");
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        var signer = new string('b', 64);
        var policy = new ContentSynchronizationPolicy(Guid.NewGuid(), 1, true, true, true, true,
            "cache", 1_048_576, ["CC0-1.0"], [signer], "test");
        var resource = new ContentRepositoryResource("modules/crypt/data.bin", content.Length, hash,
            "CC0-1.0", "test-fixture");
        var manifest = ContentRepositoryManifestService.Create(Guid.NewGuid(), "crypt", "1.0.0", signer, [resource]);
        var partialDirectory = Path.Combine(_root, "cache", "partial");
        Directory.CreateDirectory(partialDirectory);
        File.WriteAllBytes(Path.Combine(partialDirectory, $"{hash}.partial"), content[..8]);
        var transport = new MemoryTransport(resource.Path, content);
        var service = new ContentRepositorySyncService(policy, _root);

        var applied = service.Synchronize(manifest, transport);
        var reused = service.Synchronize(manifest, transport);

        Assert.True(applied.Valid);
        Assert.True(applied.Applied);
        Assert.Equal(8, transport.LastOffset);
        Assert.Equal(content.Length - 8, applied.DownloadedBytes);
        Assert.True(Assert.Single(reused.Items).CacheHit);
        Assert.Equal(0, reused.DownloadedBytes);
        Assert.Equal(content.Length, reused.ReusedBytes);
    }

    [Fact]
    public void Plan_blocks_untrusted_signer_and_license_before_download()
    {
        Directory.CreateDirectory(_root);
        var policy = new ContentSynchronizationPolicy(Guid.NewGuid(), 1, true, true, true, true,
            "cache", 1_048_576, ["CC0-1.0"], [new string('a', 64)], "test");
        var resource = new ContentRepositoryResource("module.bin", 1, new string('c', 64), "MIT", "fixture");
        var manifest = ContentRepositoryManifestService.Create(Guid.NewGuid(), "module", "1.0.0",
            new string('b', 64), [resource]);

        var report = new ContentRepositorySyncService(policy, _root).Plan(manifest);

        Assert.False(report.Valid);
        Assert.Contains(report.Diagnostics, item => item.Code == "NET_CONTENT_SIGNER_UNTRUSTED");
        Assert.Contains(report.Diagnostics, item => item.Code == "NET_CONTENT_LICENSE_BLOCKED");
    }

    [Fact]
    public void Plan_rejects_a_tampered_manifest_before_resolving_cache_paths()
    {
        Directory.CreateDirectory(_root);
        var signer = new string('a', 64);
        var policy = new ContentSynchronizationPolicy(Guid.NewGuid(), 1, true, true, true, true,
            "cache", 1_048_576, ["CC0-1.0"], [signer], "test");
        var resource = new ContentRepositoryResource("module.bin", 1, new string('c', 64), "CC0-1.0", "fixture");
        var manifest = ContentRepositoryManifestService.Create(Guid.NewGuid(), "module", "1.0.0", signer, [resource])
            with
        { Fingerprint = new string('d', 64) };

        var report = new ContentRepositorySyncService(policy, _root).Plan(manifest);

        Assert.False(report.Valid);
        Assert.Empty(report.Items);
        Assert.Contains(report.Diagnostics, item => item.Code == "NET_CONTENT_MANIFEST_TAMPERED");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class MemoryTransport(string path, byte[] content) : IContentRepositoryTransport
    {
        public long LastOffset { get; private set; }
        public Stream OpenRead(string resourcePath, long offset)
        {
            Assert.Equal(path, resourcePath);
            LastOffset = offset;
            return new MemoryStream(content[(int)offset..], writable: false);
        }
    }
}
