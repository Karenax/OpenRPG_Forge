using Forge.Domain.Areas;

namespace Forge.EngineAdapter.Rendering;

public sealed record EnvironmentVisualQualityPlan(
    bool ScreenSpaceAmbientOcclusion,
    float AmbientOcclusionRadius,
    float AmbientOcclusionIntensity,
    bool ScreenSpaceIndirectLighting,
    float IndirectLightingRadius,
    float IndirectLightingIntensity,
    bool Glow,
    float GlowIntensity,
    float GlowStrength,
    bool VolumetricFog,
    float VolumetricFogDensity,
    float VolumetricFogLength,
    float FogSkyAffect,
    float Contrast,
    float Saturation);

public static class EnvironmentVisualQualityPlanBuilder
{
    public static EnvironmentVisualQualityPlan Build(AreaRenderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var highQuality = plan.Rendering.Quality is RenderQualityTier.High or RenderQualityTier.Ultra;
        var forwardPlus = plan.Rendering.ForwardPlus;
        var tuning = plan.Rendering.VisualTuning;
        var volumetricFog = plan.Fog.Mode == FogMode.Volumetric && forwardPlus;
        var fogLength = Math.Clamp(plan.Fog.EndDistance - plan.Fog.StartDistance, 32f, 512f);
        var fogDensity = Math.Clamp(plan.Fog.Density, .001f, .05f);

        return new EnvironmentVisualQualityPlan(
            ScreenSpaceAmbientOcclusion: highQuality && forwardPlus,
            AmbientOcclusionRadius: plan.Rendering.Quality == RenderQualityTier.Ultra ? 2.2f : 1.75f,
            AmbientOcclusionIntensity: (float)((plan.Rendering.Quality == RenderQualityTier.Ultra ? 2.35 : 1.9) *
                                               tuning.AmbientOcclusionIntensityMultiplier),
            ScreenSpaceIndirectLighting: highQuality && forwardPlus,
            IndirectLightingRadius: plan.Rendering.Quality == RenderQualityTier.Ultra ? 4.5f : 3.25f,
            IndirectLightingIntensity: plan.Rendering.Quality == RenderQualityTier.Ultra ? 1.25f : 1.05f,
            Glow: plan.Rendering.HdrOutput && highQuality,
            GlowIntensity: .82f,
            GlowStrength: .9f,
            VolumetricFog: volumetricFog,
            VolumetricFogDensity: fogDensity,
            VolumetricFogLength: fogLength,
            FogSkyAffect: volumetricFog ? .48f : .24f,
            Contrast: (float)((highQuality ? 1.06 : 1.02) * tuning.ContrastMultiplier),
            Saturation: (float)((highQuality ? .99 : .985) * tuning.SaturationMultiplier));
    }
}
