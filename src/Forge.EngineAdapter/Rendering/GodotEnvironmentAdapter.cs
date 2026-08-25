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

    public const string CinematicSkyShaderCode = """
        shader_type sky;

        uniform vec4 zenith_color : source_color = vec4(0.08, 0.22, 0.42, 1.0);
        uniform vec4 horizon_color : source_color = vec4(0.86, 0.63, 0.40, 1.0);
        uniform vec4 ground_color : source_color = vec4(0.08, 0.11, 0.09, 1.0);
        uniform float cloud_coverage : hint_range(0.0, 1.0) = 0.42;
        uniform float cloud_brightness : hint_range(0.0, 2.0) = 1.0;

        float forge_sky_hash(vec2 value) {
            return fract(sin(dot(value, vec2(127.1, 311.7))) * 43758.5453123);
        }

        float forge_sky_noise(vec2 value) {
            vec2 cell = floor(value);
            vec2 local = fract(value);
            vec2 blend = local * local * (3.0 - 2.0 * local);
            float a = forge_sky_hash(cell);
            float b = forge_sky_hash(cell + vec2(1.0, 0.0));
            float c = forge_sky_hash(cell + vec2(0.0, 1.0));
            float d = forge_sky_hash(cell + vec2(1.0, 1.0));
            return mix(mix(a, b, blend.x), mix(c, d, blend.x), blend.y);
        }

        float forge_sky_fbm(vec2 value) {
            float result = 0.0;
            float amplitude = 0.55;
            mat2 rotation = mat2(vec2(0.82, -0.57), vec2(0.57, 0.82));
            for (int octave = 0; octave < 5; octave++) {
                result += forge_sky_noise(value) * amplitude;
                value = rotation * value * 2.04 + vec2(13.7, 8.3);
                amplitude *= 0.48;
            }
            return result;
        }

        void sky() {
            float up = EYEDIR.y;
            float sky_gradient = pow(clamp(up * 0.78 + 0.22, 0.0, 1.0), 0.52);
            vec3 color = mix(horizon_color.rgb, zenith_color.rgb, sky_gradient);
            float below_horizon = 1.0 - smoothstep(-0.34, 0.04, up);
            color = mix(color, mix(horizon_color.rgb, ground_color.rgb, clamp(-up, 0.0, 1.0)), below_horizon);

            vec2 cloud_offset = vec2(0.37, -0.19);
            vec2 cloud_uv = EYEDIR.xz / max(up + 0.24, 0.11);
            float cloud_noise = forge_sky_fbm(cloud_uv * 1.35 + cloud_offset);
            float threshold = 0.94 - cloud_coverage * 0.62;
            float cloud_mask = smoothstep(threshold, threshold + 0.17, cloud_noise);
            cloud_mask *= smoothstep(-0.01, 0.18, up) * (1.0 - smoothstep(0.72, 0.99, up));
            vec2 wispy_uv = vec2(
                cloud_uv.x * 0.34 + cloud_uv.y * 0.08,
                cloud_uv.y * 2.45 - cloud_uv.x * 0.04);
            float wispy_body = forge_sky_fbm(wispy_uv + cloud_offset * 3.2);
            float wispy_bands = forge_sky_noise(vec2(
                cloud_uv.x * 0.12 - cloud_uv.y * 0.05,
                cloud_uv.y * 5.8 + cloud_offset.y * 4.0));
            float wispy_mask = smoothstep(
                0.58,
                0.82,
                wispy_body * 0.74 + wispy_bands * 0.26 + cloud_coverage * 0.18);
            wispy_mask *= smoothstep(0.04, 0.20, up) * (1.0 - smoothstep(0.78, 0.99, up));
            cloud_mask = max(cloud_mask, wispy_mask * cloud_coverage * 0.92);
            vec3 cloud_shadow = mix(horizon_color.rgb, vec3(0.52, 0.62, 0.72), 0.34);
            vec3 cloud_light = mix(vec3(1.05, 1.07, 1.10), horizon_color.rgb, 0.10) * cloud_brightness;
            float cloud_detail = forge_sky_noise(cloud_uv * 8.0 - cloud_offset * 5.0);
            vec3 cloud_color = mix(cloud_shadow, cloud_light, 0.44 + cloud_detail * 0.36);
            color = mix(color, cloud_color, cloud_mask * 0.82);

            vec3 sun_direction = normalize(vec3(-0.36, 0.80, 0.48));
            float sun_dot = max(dot(EYEDIR, sun_direction), 0.0);
            float sun_disk = pow(sun_dot, 820.0);
            float sun_halo = pow(sun_dot, 18.0) * 0.26;
            vec3 sun_color = mix(vec3(1.0, 0.70, 0.38), vec3(1.0, 0.94, 0.78), smoothstep(0.0, 0.35, up));
            color += sun_color * (sun_disk * 4.2 + sun_halo) * (1.0 - cloud_mask * 0.72);
            COLOR = max(color, vec3(0.0));
        }
        """;

    public static GodotEnvironmentResult Build(AreaRenderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var diagnostics = plan.Diagnostics.ToList();
        var skyMaterial = BuildSkyMaterial(plan.Sky, diagnostics);
        var sky = new Sky { SkyMaterial = skyMaterial };
        var visual = EnvironmentVisualQualityPlanBuilder.Build(plan);
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            BackgroundEnergyMultiplier = (float)plan.Sky.EnergyMultiplier,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = plan.Lighting.AmbientIntensity,
            AmbientLightSkyContribution = .82f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = ConvertToneMapper(plan.Rendering.ToneMapping),
            TonemapExposure = (float)plan.Rendering.Exposure,
            TonemapWhite = (float)plan.Rendering.WhitePoint,
            FogEnabled = plan.Fog.Mode != FogMode.Disabled,
            FogDensity = plan.Fog.Density,
            FogDepthBegin = plan.Fog.StartDistance,
            FogDepthEnd = plan.Fog.EndDistance,
            FogHeight = plan.Fog.Height,
            FogAerialPerspective = plan.Fog.Mode == FogMode.Volumetric ? .72f : .28f,
            FogSkyAffect = visual.FogSkyAffect,
            FogLightColor = new Color(plan.Sky.HorizonColor),
            FogLightEnergy = .9f,
            SsaoEnabled = visual.ScreenSpaceAmbientOcclusion,
            SsaoRadius = visual.AmbientOcclusionRadius,
            SsaoIntensity = visual.AmbientOcclusionIntensity,
            SsaoPower = (float)plan.Rendering.VisualTuning.AmbientOcclusionPower,
            SsaoDetail = .65f,
            SsaoSharpness = .86f,
            SsaoLightAffect = .28f,
            SsilEnabled = visual.ScreenSpaceIndirectLighting,
            SsilRadius = visual.IndirectLightingRadius,
            SsilIntensity = visual.IndirectLightingIntensity,
            SsilSharpness = .9f,
            GlowEnabled = visual.Glow,
            GlowNormalized = true,
            GlowIntensity = visual.GlowIntensity,
            GlowStrength = visual.GlowStrength,
            GlowBloom = .08f,
            GlowHdrThreshold = 1.15f,
            GlowHdrScale = 1.55f,
            VolumetricFogEnabled = visual.VolumetricFog,
            VolumetricFogDensity = visual.VolumetricFogDensity,
            VolumetricFogLength = visual.VolumetricFogLength,
            VolumetricFogAlbedo = new Color(plan.Sky.HorizonColor).Lightened(.18f),
            VolumetricFogAmbientInject = .62f,
            VolumetricFogSkyAffect = visual.FogSkyAffect,
            VolumetricFogTemporalReprojectionEnabled = true,
            AdjustmentEnabled = true,
            AdjustmentBrightness = (float)plan.Rendering.VisualTuning.Brightness,
            AdjustmentContrast = visual.Contrast,
            AdjustmentSaturation = visual.Saturation,
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
        environment.AdjustmentEnabled = false;
        environment.AdjustmentColorCorrection = null;
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

        var shader = new Shader
        {
            ResourceName = "ForgeCinematicProceduralSky",
            Code = CinematicSkyShaderCode,
        };
        var material = new ShaderMaterial
        {
            ResourceName = "ForgeCinematicProceduralSkyMaterial",
            Shader = shader,
        };
        material.SetShaderParameter("zenith_color", new Color(profile.ZenithColor));
        material.SetShaderParameter("horizon_color", new Color(profile.HorizonColor));
        material.SetShaderParameter("ground_color", new Color(profile.GroundColor));
        material.SetShaderParameter("cloud_coverage", (float)profile.CloudCoverage);
        material.SetShaderParameter("cloud_brightness", (float)Math.Clamp(.72 + profile.EnergyMultiplier * .32, .55, 1.25));
        return material;
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
