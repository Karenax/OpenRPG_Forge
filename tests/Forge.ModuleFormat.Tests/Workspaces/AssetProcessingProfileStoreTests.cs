using Forge.Domain.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssetProcessingProfileStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-asset-profile-tests",
        Guid.NewGuid().ToString("N"));
    private static readonly AssetCatalogItemId AssetId =
        AssetCatalogItemId.From(Guid.Parse("a5200000-0000-4000-8000-000000000001"));

    public AssetProcessingProfileStoreTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
    }

    [Fact]
    public void MissingDocumentReturnsEmptyCanonicalCollection()
    {
        var result = AssetProcessingProfileStore.Load(_temporaryRoot);

        Assert.True(result.Succeeded);
        Assert.True(result.FileMissing);
        Assert.Empty(result.Profiles);
    }

    [Fact]
    public void SaveAndReloadRoundTripsTextureAndModelProfilesDeterministically()
    {
        var profiles = new[] { TextureProfile(), ModelProfile() };

        var saved = AssetProcessingProfileStore.Save(_temporaryRoot, profiles);
        var firstJson = File.ReadAllText(Path.Combine(_temporaryRoot, "assets", "import-profiles.json"));
        var restored = AssetProcessingProfileStore.Load(_temporaryRoot);
        var savedAgain = AssetProcessingProfileStore.Save(_temporaryRoot, restored.Profiles);
        var secondJson = File.ReadAllText(Path.Combine(_temporaryRoot, "assets", "import-profiles.json"));

        Assert.True(saved.Succeeded);
        Assert.True(savedAgain.Succeeded);
        Assert.True(restored.Succeeded);
        Assert.False(restored.FileMissing);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(2, restored.Profiles.Count);
        Assert.Equal(AssetProcessingKind.Texture, restored.Profiles[0].Kind);
        Assert.Equal(AssetProcessingKind.Model, restored.Profiles[1].Kind);
    }

    [Fact]
    public void InvalidDocumentReturnsStableDiagnosticWithoutPartialProfiles()
    {
        var directory = Path.Combine(_temporaryRoot, "assets");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "import-profiles.json"), "{ invalid }");

        var result = AssetProcessingProfileStore.Load(_temporaryRoot);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Profiles);
        Assert.Equal(AssetProcessingProfileStore.ReadFailedCode, Assert.Single(result.Diagnostics).Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private static AssetProcessingProfile ModelProfile() =>
        new(
            AssetId,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Model,
            "glb",
            "glb",
            "CC0-1.0",
            new string('b', 64),
            "asset-catalog:test:model",
            8 * 1024 * 1024,
            model: new(false, true, true, ["hand.r"], [1m, 0.5m]));

    private static AssetProcessingProfile TextureProfile() =>
        new(
            AssetId,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Texture,
            "png",
            "ktx2",
            "CC0-1.0",
            new string('b', 64),
            "asset-catalog:test:texture",
            4 * 1024 * 1024,
            texture: new(
                TextureColorSpace.Linear,
                TextureCompressionProfile.Universal,
                1024,
                5.12m,
                new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.Normal] = "textures/model-normal.png",
                }));
}
