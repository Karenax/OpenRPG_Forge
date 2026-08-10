using System.Security.Cryptography;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssetGovernanceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-governance-tests", Guid.NewGuid().ToString("N"));

    public AssetGovernanceStoreTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void StoreIsUtf8NoBomAtomicDeterministicAndMissingIsOptional()
    {
        Assert.True(AssetGovernanceStore.Load(_root).FileMissing);
        var record = ValidRecord();
        var saved = AssetGovernanceStore.Save(_root, [record]);
        Assert.True(saved.Succeeded, string.Join("; ", saved.Diagnostics.Select(item => item.Message)));
        var path = Path.Combine(_root, AssetGovernanceStore.RelativePath);
        var first = File.ReadAllBytes(path);
        var loaded = AssetGovernanceStore.Load(_root);
        Assert.True(loaded.Succeeded);
        Assert.Equal(record, Assert.Single(loaded.Records));
        Assert.True(AssetGovernanceStore.Save(_root, loaded.Records).Succeeded);
        Assert.Equal(first, File.ReadAllBytes(path));
        Assert.False(first.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void QuarantineRejectsTraversalTypeSizeHashParserAndMalwareBeforeCanonicalImport()
    {
        var sourceRoot = Path.Combine(_root, "vault");
        Directory.CreateDirectory(sourceRoot);
        File.WriteAllBytes(Path.Combine(sourceRoot, "bad.png"), [1, 2, 3, 4]);
        var request = new AssetQuarantineStageRequest(ValidRecord().AssetId, sourceRoot, "bad.png", "image/png", 3,
            new string('0', 64));

        var result = AssetImportQuarantine.Stage(_root, request, new Scanner(clean: false));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineSizeExceededCode);
        Assert.Contains(result.Diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineTypeMismatchCode);
        Assert.Contains(result.Diagnostics, item => item.Code == AssetGovernanceValidator.HashInvalidCode);
        Assert.Contains(result.Diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineParserFailedCode);
        Assert.False(Directory.Exists(Path.Combine(_root, "assets", "imported")));

        var traversal = AssetImportQuarantine.Stage(_root, request with { SourceRelativePath = "../escape.glb" }, new Scanner(true));
        Assert.Contains(traversal.Diagnostics, item => item.Code == AssetGovernanceValidator.QuarantineUnsafePathCode);
    }

    [Fact]
    public void CleanGltfStagesOnlyInQuarantineAndNeedsExplicitHumanDecision()
    {
        var sourceRoot = Path.Combine(_root, "vault");
        Directory.CreateDirectory(sourceRoot);
        var source = Path.Combine(sourceRoot, "model.glb");
        File.WriteAllBytes(source, [.. "glTF"u8.ToArray(), 2, 0, 0, 0, 12, 0, 0, 0]);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
        var request = new AssetQuarantineStageRequest(ValidRecord().AssetId, sourceRoot, "model.glb",
            "model/gltf-binary", 1024, hash);

        var result = AssetImportQuarantine.Stage(_root, request, new Scanner(true));

        Assert.True(result.Succeeded);
        Assert.Equal(AssetQuarantineStatus.Pending, result.Review.Status);
        Assert.True(File.Exists(Path.Combine(_root, result.QuarantineRelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(Directory.Exists(Path.Combine(_root, "assets", "imported")));
        var approved = AssetImportQuarantine.Decide(result.Review, true, "human-reviewer", DateTimeOffset.UnixEpoch);
        Assert.Equal(AssetQuarantineStatus.Approved, approved.Status);
        Assert.Equal("human-reviewer", approved.Reviewer);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static AssetGovernanceRecord ValidRecord()
    {
        var assetId = AssetCatalogItemId.ForExternalId("test:persistence-governance");
        return new(1, AssetGovernanceId.ForAsset(assetId), assetId, AssetPackageManifestId.ForAsset(assetId),
            "CC0-1.0", new string('c', 64), "Author", "https://example.invalid/model", "permission:fixture",
            "None.", false, string.Empty, string.Empty, 4096,
            new("incoming/model.glb", "model/gltf-binary", "model/gltf-binary", 12, 4096, "scanner", "clean",
                "valid", AssetQuarantineStatus.Approved, "reviewer", DateTimeOffset.UnixEpoch),
            new(AssetOriginKind.OpenLicensed, true, false, string.Empty));
    }

    private sealed class Scanner(bool clean) : IAssetMalwareScanner
    {
        public string AdapterName => "test-scanner";
        public AssetMalwareScanResult Scan(string confinedFilePath) => new(clean, clean ? "clean" : "malware");
    }
}
