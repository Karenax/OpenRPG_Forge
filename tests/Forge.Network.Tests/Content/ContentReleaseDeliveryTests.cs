using System.Security.Cryptography;
using Forge.Domain.Modules;
using Forge.Network.Content;

namespace Forge.Network.Tests.Content;

public sealed class ContentReleaseDeliveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"forge-content-release-{Guid.NewGuid():N}");

    [Fact]
    public void SignedRotationRequiresTrustedOverlapAndProducesNextPolicy()
    {
        var currentKey = new string('a', 64);
        var nextKey = new string('b', 64);
        var policy = new ModuleSigningConfiguration(ModuleSigningMode.Required, "release", [currentKey], [], false);
        var manifest = new ContentTrustRotationManifest(Guid.NewGuid(), 1, currentKey, nextKey,
            DateTimeOffset.Parse("2026-08-12T10:00:00Z"), DateTimeOffset.Parse("2026-08-19T10:00:00Z"),
            true, "publisher:test");

        var accepted = ContentTrustRotationService.Preview(policy, manifest);
        var unsigned = ContentTrustRotationService.Preview(policy, manifest with { SignatureValid = false });
        var noOverlap = ContentTrustRotationService.Preview(policy,
            manifest with { RetiresCurrentAt = manifest.ActivatesAt });

        Assert.True(accepted.Valid);
        Assert.Equal(2, accepted.NextPolicy!.TrustedKeyFingerprints.Count);
        Assert.Equal("NET_CONTENT_KEY_ROTATION_UNTRUSTED", unsigned.DiagnosticCode);
        Assert.Equal("NET_CONTENT_KEY_ROTATION_OVERLAP_REQUIRED", noOverlap.DiagnosticCode);
    }

    [Fact]
    public void RangeDeliveryResumesRetriesAndVerifiesHash()
    {
        Directory.CreateDirectory(_root);
        var bytes = Enumerable.Range(0, 180_000).Select(index => (byte)(index % 251)).ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var resource = new ContentRepositoryResource("packages/module.orpmod", bytes.Length, hash,
            "CC0-1.0", "test:cdn");
        var destination = Path.Combine(_root, "module.partial");
        File.WriteAllBytes(destination, bytes[..32_000]);
        var transport = new MemoryRangeTransport(bytes);

        var result = ContentRangeDeliveryService.Download(resource, destination, transport, 16 * 1024, 3);

        Assert.True(result.Applied);
        Assert.Equal(32_000, result.ResumedBytes);
        Assert.Equal(bytes.Length - 32_000, result.DownloadedBytes);
        Assert.True(result.Requests > (int)Math.Ceiling(result.DownloadedBytes / (16d * 1024)));
        Assert.Equal(bytes, File.ReadAllBytes(destination));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class MemoryRangeTransport(byte[] bytes) : IContentRangeTransport
    {
        private bool _failed;

        public ContentRangeResponse Read(string resourcePath, long offset, int maximumBytes)
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("transient");
            }

            return new(offset, bytes.AsSpan((int)offset, maximumBytes).ToArray(), "release-v1");
        }
    }
}
