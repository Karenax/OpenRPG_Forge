using Forge.Domain.Areas;
using Forge.Domain.Validation;
using Godot;

namespace Forge.EngineAdapter.Rendering;

public sealed record GodotEnvironmentResult(
    Godot.Environment Environment,
    Color DirectionalLightColor,
    float DirectionalLightEnergy,
    int ShadowAtlasSize,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class GodotEnvironmentAdapter
{
    public const string PanoramaMissingCode = "render.sky.panorama_missing";
    public const string ColorCorrectionMissingCode = "render.color.lut_missing";

    public static GodotEnvironmentResult Build(AreaRenderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var diagnostics = plan.Diagnostics.ToList();
        var skyMaterial = BuildSkyMaterial(plan.Sky, diagnostics);
        var sky = new Sky { SkyMaterial = skyMaterial };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = plan.Lighting.AmbientIntensity,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = ConvertToneMapper(plan.Rendering.ToneMapping),
            TonemapExposure = (float)plan.Rendering.Exposure,
            TonemapWhite = (float)plan.Rendering.WhitePoint,
            FogEnabled = plan.Fog.Mode != FogMode.Disabled,
            FogDensity = plan.Fog.Density,
            FogDepthBegin = plan.Fog.StartDistance,
            FogDepthEnd = plan.Fog.EndDistance,
            FogHeight = plan.Fog.Height,
            FogAerialPerspective = plan.Fog.Mode == FogMode.Volumetric ? 0.65f : 0.2f,
        };
        ApplyColorCorrection(environment, plan.Rendering, diagnostics);
        return new(
            environment,
            KelvinToColor(plan.Lighting.ColorTemperatureKelvin),
            plan.Lighting.DirectionalIntensity,
            plan.LightBudget.ShadowAtlasSize,
            diagnostics);
    }

    public static void ApplyPbrDefaults(StandardMaterial3D material, AreaRenderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(plan);
        material.ShadingMode = plan.Rendering.PerPixelLighting
            ? BaseMaterial3D.ShadingModeEnum.PerPixel
            : BaseMaterial3D.ShadingModeEnum.PerVertex;
        material.Metallic = 0.02f;
        material.Roughness = 0.72f;
    }

    private static void ApplyColorCorrection(
        Godot.Environment environment,
        AreaRenderingProfile profile,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(profile.LutResource)) return;
        var resourcePath = $"res://{profile.LutResource}";
        if (ResourceLoader.Exists(resourcePath) && ResourceLoader.Load<Texture>(resourcePath) is { } lut)
        {
            environment.AdjustmentEnabled = true;
            environment.AdjustmentColorCorrection = lut;
            return;
        }

        diagnostics.Add(new(
            ColorCorrectionMissingCode,
            $"Color correction LUT '{profile.LutResource}' is unavailable; neutral grading applied.",
            "environment/rendering/lutResource"));
    }

    private static Material BuildSkyMaterial(SkyProfile profile, ICollection<DomainDiagnostic> diagnostics)
    {
        if (profile.Mode == SkyProfileMode.Panorama)
        {
            var resourcePath = $"res://{profile.PanoramaResource}";
            if (ResourceLoader.Exists(resourcePath))
            {
                return new PanoramaSkyMaterial
                {
                    Panorama = ResourceLoader.Load<Texture2D>(resourcePath),
                    EnergyMultiplier = (float)profile.EnergyMultiplier,
                };
            }

            diagnostics.Add(new(
                PanoramaMissingCode,
                $"Panorama sky resource '{profile.PanoramaResource}' is unavailable; procedural fallback applied.",
                "environment/sky/panoramaResource"));
        }

        if (profile.Mode == SkyProfileMode.Physical)
        {
            return new PhysicalSkyMaterial
            {
                GroundColor = new Color(profile.GroundColor),
                EnergyMultiplier = (float)profile.EnergyMultiplier,
                Turbidity = (float)(2 + profile.CloudCoverage * 8),
                SunDiskScale = (float)(1 + profile.MoonIntensity * 0.05),
                UseDebanding = true,
            };
        }

        return new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(profile.ZenithColor),
            SkyHorizonColor = new Color(profile.HorizonColor),
            GroundBottomColor = new Color(profile.GroundColor),
            GroundHorizonColor = new Color(profile.HorizonColor).Darkened(0.35f),
            SkyEnergyMultiplier = (float)profile.EnergyMultiplier,
            GroundEnergyMultiplier = (float)profile.EnergyMultiplier,
            UseDebanding = true,
        };
    }

    private static Godot.Environment.ToneMapper ConvertToneMapper(ToneMappingProfile profile) => profile switch
    {
        ToneMappingProfile.Agx => Godot.Environment.ToneMapper.Agx,
        ToneMappingProfile.Aces => Godot.Environment.ToneMapper.Aces,
        ToneMappingProfile.Filmic => Godot.Environment.ToneMapper.Filmic,
        _ => Godot.Environment.ToneMapper.Linear,
    };

    private static Color KelvinToColor(float kelvin)
    {
        var normalized = Math.Clamp((kelvin - 1000f) / 19000f, 0, 1);
        return normalized < 0.29f
            ? new Color(1, 0.48f + normalized * 1.65f, 0.24f + normalized * 2.45f)
            : new Color(0.94f - (normalized - 0.29f) * 0.18f, 0.96f, 1);
    }
}
