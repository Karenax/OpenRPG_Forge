using Forge.Domain.Areas;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaRenderingProfileTests
{
    [Fact]
    public void DefaultsProvidePbrHdrSkyAndPlatformBudgets()
    {
        var exterior = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);
        var interior = AreaEnvironmentProfile.CreateDefault(AreaKind.Interior);

        Assert.Equal(AreaEnvironmentProfile.CurrentSchemaVersion, exterior.SchemaVersion);
        Assert.Equal(PbrMaterialWorkflow.MetallicRoughness, exterior.Rendering.MaterialWorkflow);
        Assert.Equal(ToneMappingProfile.Agx, exterior.Rendering.ToneMapping);
        Assert.True(exterior.Rendering.ForwardPlus);
        Assert.True(exterior.Rendering.PerPixelLighting);
        Assert.True(exterior.Rendering.HdrOutput);
        Assert.Equal(1, exterior.Rendering.VisualTuning.Brightness);
        Assert.Equal(["desktop", "mobile"], exterior.Rendering.LightBudgets.Select(item => item.Platform));
        Assert.Equal(SkyProfileMode.Physical, exterior.Sky.Mode);
        Assert.Equal(SkyProfileMode.Procedural, interior.Sky.Mode);
    }

    [Fact]
    public void RejectsUnsafePanoramaAndInvalidLightBudget()
    {
        Assert.Throws<ArgumentException>(() => new SkyProfile(
            1, SkyProfileMode.Panorama, "../sky.hdr", "#123456", "#123456", "#123456",
            0, 0, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicLightBudgetProfile(
            "desktop", 8, 9, 4096, 0.8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaVisualTuningProfile(
            1, 0, 1, 1, 1, 1, -58, -32, .52, 1.45));
    }
}
