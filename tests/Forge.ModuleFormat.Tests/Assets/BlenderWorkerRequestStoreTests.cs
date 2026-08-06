using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class BlenderWorkerRequestStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-worker-request", Guid.NewGuid().ToString("N"));
    private static readonly AssetCatalogItemId AssetId = AssetCatalogItemId.From(
        Guid.Parse("c1400000-0000-4000-8000-000000000001"));

    public BlenderWorkerRequestStoreTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void WritesDeterministicTypedRequestWithoutExecutableCode()
    {
        var configuration = Configuration();

        var first = BlenderWorkerRequestStore.Save(_root, configuration);
        var path = Path.Combine(_root, first.RequestRelativePath!.Replace('/', Path.DirectorySeparatorChar));
        var firstJson = File.ReadAllText(path);
        var second = BlenderWorkerRequestStore.Save(_root, configuration);
        var secondJson = File.ReadAllText(path);
        using var document = JsonDocument.Parse(secondJson);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("normalize_model", document.RootElement.GetProperty("operations")[1].GetString());
        Assert.Contains("generate_thumbnail", secondJson, StringComparison.Ordinal);
        Assert.Contains("audit_quality", secondJson, StringComparison.Ordinal);
        Assert.Empty(document.RootElement.GetProperty("options").GetProperty("animationSources").EnumerateArray());
        Assert.Empty(document.RootElement.GetProperty("options").GetProperty("materialTextures").EnumerateArray());
        Assert.False(secondJson.Contains("script", StringComparison.OrdinalIgnoreCase));
        Assert.True(document.RootElement.GetProperty("securityPolicy").GetProperty("denyFreeFormPython").GetBoolean());
        Assert.True(document.RootElement.GetProperty("securityPolicy").GetProperty("denyShell").GetBoolean());
        Assert.True(document.RootElement.GetProperty("securityPolicy").GetProperty("denyEval").GetBoolean());
        Assert.False(document.RootElement.GetProperty("securityPolicy").GetProperty("networkAllowed").GetBoolean());
        Assert.Equal(300, document.RootElement.GetProperty("securityPolicy").GetProperty("maximumJobSeconds").GetInt32());
    }

    [Fact]
    public void RejectsOperationDisabledByCanonicalPolicy()
    {
        var policy = ExternalWorkerSecurityPolicy.CreateDefault() with
        {
            AllowedBlenderOperations =
            [
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.NormalizeModel,
                AssetExternalOperation.ExportGlb,
            ],
        };

        var result = BlenderWorkerRequestStore.Save(_root, Configuration(), securityPolicy: policy);

        Assert.False(result.Succeeded);
        Assert.Contains("GenerateThumbnail", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
        Assert.Equal("assets/worker-security/allowedBlenderOperations", result.Diagnostics[0].Path);
    }

    [Fact]
    public void LoadsCanonicalWorkspacePolicyIntoEveryWorkerRequest()
    {
        var policy = ExternalWorkerSecurityPolicy.CreateDefault() with
        {
            MaximumJobSeconds = 77,
            MaximumOutputBytes = 64L * 1024 * 1024,
            Provenance = "author:g12-l:request",
        };
        Assert.True(ExternalWorkerSecurityPolicyStore.Save(_root, policy).Succeeded);

        var result = BlenderWorkerRequestStore.Save(_root, Configuration());
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            _root, result.RequestRelativePath!.Replace('/', Path.DirectorySeparatorChar))));
        var embedded = document.RootElement.GetProperty("securityPolicy");

        Assert.True(result.Succeeded);
        Assert.Equal(77, embedded.GetProperty("maximumJobSeconds").GetInt32());
        Assert.Equal(64L * 1024 * 1024, embedded.GetProperty("maximumOutputBytes").GetInt64());
        Assert.Equal("author:g12-l:request", embedded.GetProperty("provenance").GetString());
    }

    [Fact]
    public void WritesOnlyGovernedConfinedAnimationAndTextureSources()
    {
        var options = BlenderWorkerOptions.Default with
        {
            AnimationSources =
            [
                new("idle", "normalized/kenney/characters/idle.fbx", new string('a', 64)),
                new("run", "normalized/kenney/characters/run.fbx", new string('b', 64)),
            ],
            MaterialTextures =
            [
                new("skin", "base_color", "normalized/kenney/characters/skin.png", new string('c', 64)),
            ],
        };

        var result = BlenderWorkerRequestStore.Save(_root, Configuration(), options);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            _root,
            result.RequestRelativePath!.Replace('/', Path.DirectorySeparatorChar))));

        Assert.True(result.Succeeded);
        Assert.Equal("idle", document.RootElement.GetProperty("options").GetProperty("animationSources")[0]
            .GetProperty("name").GetString());
        Assert.Equal("base_color", document.RootElement.GetProperty("options").GetProperty("materialTextures")[0]
            .GetProperty("channel").GetString());

        var unsafeResult = BlenderWorkerRequestStore.Save(_root, Configuration(), options with
        {
            AnimationSources = [new("idle", "../idle.fbx", new string('a', 64))],
        });
        Assert.False(unsafeResult.Succeeded);
        Assert.Equal(BlenderWorkerRequestStore.WriteFailedCode, Assert.Single(unsafeResult.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static AssetExternalEditConfiguration Configuration() =>
        new(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(AssetId),
            AssetId,
            AssetPackageManifestId.ForAsset(AssetId),
            "author:test",
            "CC0-1.0",
            new string('e', 64),
            "normalized/test/tree.obj",
            "assets/imported/tree/model.glb",
            AssetExternalTool.Blender,
            [
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.NormalizeModel,
                AssetExternalOperation.ExportGlb,
                AssetExternalOperation.GenerateThumbnail,
                AssetExternalOperation.AuditQuality,
            ],
            true,
            [AssetTargetPlatform.Windows]);
}
