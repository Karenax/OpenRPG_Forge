using System.IO.Compression;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Diagnostics;

namespace Forge.ModuleFormat.Tests.Diagnostics;

public sealed class CrashArtifactBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "OpenRPGForgeTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Capture_sanitizes_logs_and_packages_version_fingerprint_reproduction_and_dump_adapter()
    {
        Directory.CreateDirectory(_root);
        var policy = OperationalReadinessProfile.CreateDefault(Guid.NewGuid()).CrashArtifacts;
        var builder = new CrashArtifactBuilder(policy, new StubDumpAdapter());
        var request = new CrashArtifactRequest(Guid.Parse("e0250000-0000-4000-8000-000000000001"),
            new DateTimeOffset(2026, 8, 5, 19, 0, 0, TimeSpan.Zero), "0.6.19", new string('a', 64),
            ["boot ok", "token=do-not-ship", "{\"password\":\"hidden\"}"], _root);

        var result = builder.Capture(request);

        Assert.True(result.DumpIncluded);
        Assert.Equal("managed-test", result.DumpAdapter);
        Assert.Equal(64, result.Sha256.Length);
        using var archive = ZipFile.OpenRead(result.ArtifactPath);
        var log = Read(archive, "sanitized.log");
        var manifest = Read(archive, "manifest.json");
        Assert.DoesNotContain("do-not-ship", log);
        Assert.DoesNotContain("hidden", log);
        Assert.Contains("[REDACTED]", log);
        Assert.Contains(request.ReproductionId.ToString(), manifest);
        Assert.Contains(request.PackageFingerprint, manifest);
        Assert.NotNull(archive.GetEntry("diagnostic.dump"));
    }

    private static string Read(ZipArchive archive, string path)
    {
        using var reader = new StreamReader(archive.GetEntry(path)!.Open());
        return reader.ReadToEnd();
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class StubDumpAdapter : ICrashDumpAdapter
    {
        public string Name => "managed-test";
        public byte[] Capture() => [1, 2, 3, 4];
    }
}
