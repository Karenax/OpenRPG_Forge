using Forge.Domain.Areas;
using Forge.EngineAdapter.Rendering;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class EnvironmentVisualQualityPlanBuilderTests
{
    [Fact]
    public void HighQualityHdrProfileEnablesDepthAndBloomEnhancements()
    {
        var profile = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);
        var renderPlan = AreaRenderPlanBuilder.Build(profile, "desktop", 2, 1);

        var visual = EnvironmentVisualQualityPlanBuilder.Build(renderPlan);

        Assert.True(visual.ScreenSpaceAmbientOcclusion);
        Assert.True(visual.ScreenSpaceIndirectLighting);
        Assert.True(visual.Glow);
        Assert.False(visual.VolumetricFog);
        Assert.InRange(visual.Contrast, 1.04f, 1.12f);
        Assert.InRange(visual.Saturation, .97f, 1.01f);
    }

    [Fact]
    public void VolumetricFogProfileBuildsBoundedAtmosphereSettings()
    {
        var source = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);
        var profile = new AreaEnvironmentProfile(
            source.SchemaVersion,
            source.Classification,
            source.Tags,
            source.Lighting,
            new AreaFogProfile(FogMode.Volumetric, .018f, 10, 240, 18),
            source.Audio,
            source.Rendering,
            source.Sky,
            source.Wind,
            source.Weather,
            source.Water,
            source.Reverb,
            source.Authoring);
        var renderPlan = AreaRenderPlanBuilder.Build(profile, "desktop", 2, 1);

        var visual = EnvironmentVisualQualityPlanBuilder.Build(renderPlan);

        Assert.True(visual.VolumetricFog);
        Assert.InRange(visual.VolumetricFogDensity, .001f, .05f);
        Assert.InRange(visual.VolumetricFogLength, 32f, 512f);
        Assert.InRange(visual.FogSkyAffect, 0f, 1f);
    }

    [Fact]
    public void CinematicSkyUsesDefinedSmoothstepBounds()
    {
        Assert.DoesNotContain("smoothstep(0.04, -0.34", GodotEnvironmentAdapter.CinematicSkyShaderCode, StringComparison.Ordinal);
        Assert.Contains("1.0 - smoothstep(-0.34, 0.04, up)", GodotEnvironmentAdapter.CinematicSkyShaderCode, StringComparison.Ordinal);
    }

    [Fact]
    public void CinematicSkyDoesNotInvalidateRadianceEveryFrame()
    {
        Assert.DoesNotContain("TIME", GodotEnvironmentAdapter.CinematicSkyShaderCode, StringComparison.Ordinal);
    }
}
