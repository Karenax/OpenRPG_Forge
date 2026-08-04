using Forge.Domain.Assets;

namespace Forge.Domain.Tests.Assets;

public sealed class AssetExternalEditTests
{
    [Fact]
    public void BlenderRoundTripIsTypedGovernedAndPlatformCompatible()
    {
        var configuration = ValidConfiguration();

        var diagnostics = AssetExternalEditValidator.Validate(configuration);
        var parity = AssetPlatformParityValidator.Validate(
            AssetPackageManifestValidatorTests.ValidManifest(),
            [AssetTargetPlatform.Windows]);

        Assert.Empty(diagnostics);
        Assert.Empty(parity);
        Assert.Equal(configuration.Id, AssetExternalEditId.ForAsset(configuration.AssetId));
    }

    [Fact]
    public void ArbitraryToolOperationAndUnsafePathAreRejected()
    {
        var configuration = ValidConfiguration() with
        {
            OutputRelativePath = "../outside.glb",
            Operations = [AssetExternalOperation.EditAudio],
        };

        var codes = AssetExternalEditValidator.Validate(configuration).Select(item => item.Code).ToArray();

        Assert.Contains(AssetExternalEditValidator.PathUnsafeCode, codes);
        Assert.Contains(AssetExternalEditValidator.OperationInvalidCode, codes);
    }

    [Fact]
    public void PlatformParityRequiresFallbackOrExactVariant()
    {
        var manifest = AssetPackageManifestValidatorTests.ValidManifest();

        var diagnostic = Assert.Single(AssetPlatformParityValidator.Validate(
            manifest,
            [AssetTargetPlatform.Android]));

        Assert.Equal(AssetPlatformParityValidator.RequiredRoleMissingCode, diagnostic.Code);
    }

    private static AssetExternalEditConfiguration ValidConfiguration() =>
        new(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(AssetPackageManifestValidatorTests.AssetId),
            AssetPackageManifestValidatorTests.AssetId,
            AssetPackageManifestId.ForAsset(AssetPackageManifestValidatorTests.AssetId),
            "author:test",
            "CC0-1.0",
            new string('a', 64),
            "normalized/polyhaven/tree/tree.obj",
            "assets/imported/tree/model.glb",
            AssetExternalTool.Blender,
            [
                AssetExternalOperation.ValidateModel,
                AssetExternalOperation.NormalizeModel,
                AssetExternalOperation.ExportGlb,
            ],
            true,
            [AssetTargetPlatform.Windows]);
}
