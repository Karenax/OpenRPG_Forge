using System.Security.Cryptography;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssetImportExecutorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-import-executor-tests",
        Guid.NewGuid().ToString("N"));

    private string VaultRoot => Path.Combine(_root, "vault");

    private string WorkspaceRoot => Path.Combine(_root, "workspace");

    public AssetImportExecutorTests()
    {
        Directory.CreateDirectory(VaultRoot);
        Directory.CreateDirectory(WorkspaceRoot);
    }

    [Fact]
    public void CanonicalGlbIsCopiedWithReceiptAndReusedWithoutChangingSource()
    {
        var sourceRelativePath = "normalized/kenney/castle/tree.glb";
        var sourcePath = WriteSource(sourceRelativePath, [0x67, 0x6c, 0x54, 0x46, 2, 0, 0, 0]);
        var item = CreateItem("kenney:castle-tree", sourceRelativePath, previewPath: null);
        var plan = Assert.IsType<AssetImportPlan>(AssetImportPlanner.Plan(item).Plan);
        var sourceHashBefore = Hash(sourcePath);

        var first = AssetImportExecutor.Execute(plan, VaultRoot, WorkspaceRoot);
        var second = AssetImportExecutor.Execute(plan, VaultRoot, WorkspaceRoot);

        Assert.True(first.Succeeded);
        Assert.Equal(AssetImportExecutionStatus.CanonicalReady, first.Execution!.Status);
        Assert.False(first.Execution.Reused);
        Assert.True(second.Execution!.Reused);
        Assert.Equal(sourceHashBefore, Hash(sourcePath));
        Assert.Equal(
            sourceHashBefore,
            Hash(Path.Combine(WorkspaceRoot, first.Execution.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(
            WorkspaceRoot,
            first.Execution.ReceiptRelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void NonCanonicalTextureIsStagedWithDeterministicConversionTasks()
    {
        var modelRelativePath = "normalized/ambientcg/stone/model.glb";
        WriteSource(modelRelativePath, [0x67, 0x6c, 0x54, 0x46]);
        var previewRelativePath = "normalized/ambientcg/stone/base-color.png";
        var texturePath = WriteSource(previewRelativePath, [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        var item = CreateItem("ambientcg:stone", modelRelativePath, previewRelativePath);
        var profile = AssetImportPlanner.CreateDefaultProfile(item, AssetProcessingKind.Texture);
        var plan = Assert.IsType<AssetImportPlan>(AssetImportPlanner.Plan(item, profile).Plan);

        var result = AssetImportExecutor.Execute(plan, VaultRoot, WorkspaceRoot);

        Assert.True(result.Succeeded);
        Assert.Equal(AssetImportExecutionStatus.Staged, result.Execution!.Status);
        Assert.EndsWith(".png", result.Execution.OutputRelativePath, StringComparison.Ordinal);
        var receipt = File.ReadAllText(Path.Combine(
            WorkspaceRoot,
            result.Execution.ReceiptRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("texture.process", receipt, StringComparison.Ordinal);
        Assert.Contains("staged", receipt, StringComparison.Ordinal);
        Assert.Equal(Hash(texturePath), result.Execution.SourceFileSha256);
    }

    [Fact]
    public void UnsafeDestinationIsRejectedBeforeAnyWrite()
    {
        var sourceRelativePath = "normalized/model.glb";
        WriteSource(sourceRelativePath, [0x67, 0x6c, 0x54, 0x46]);
        var item = CreateItem("kenney:model", sourceRelativePath, null);
        var plan = Assert.IsType<AssetImportPlan>(AssetImportPlanner.Plan(item).Plan) with
        {
            DestinationRelativeDirectory = "../escape",
        };

        var result = AssetImportExecutor.Execute(plan, VaultRoot, WorkspaceRoot);

        Assert.False(result.Succeeded);
        Assert.Equal(AssetImportExecutor.PathUnsafeCode, Assert.Single(result.Diagnostics).Code);
        Assert.False(Directory.Exists(Path.Combine(_root, "escape")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteSource(string relativePath, byte[] content)
    {
        var path = Path.Combine(VaultRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static AssetCatalogItem CreateItem(
        string externalId,
        string modelPath,
        string? previewPath) =>
        new(
            AssetCatalogItemId.ForExternalId(externalId),
            externalId,
            externalId,
            externalId.Split(':')[0],
            externalId.Split(':')[1],
            "https://example.invalid/source",
            "CC0-1.0",
            "https://example.invalid/license",
            true,
            new string('c', 64),
            4096,
            ["3d-model"],
            [],
            "installed",
            AssetAvailability.ExtractedAvailable,
            modelPath,
            previewPath,
            null,
            $"asset-catalog:{externalId}");
}
