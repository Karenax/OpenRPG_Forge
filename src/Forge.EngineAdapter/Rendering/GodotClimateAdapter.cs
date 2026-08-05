using Forge.Domain.Areas;
using Forge.Domain.Validation;
using Godot;

namespace Forge.EngineAdapter.Rendering;

public sealed record GodotClimateResult(
    Node3D Root,
    int PrecipitationEmitters,
    int WaterSurfaces,
    string ReverbPreset,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class GodotClimateAdapter
{
    public const string PrecipitationResourceMissingCode = "environment.weather.resource_missing";
    public const string RuntimeAudioBusName = "ForgeEnvironment";

    public static GodotClimateResult Build(AreaClimatePlan plan, bool configureRuntimeAudio)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var diagnostics = plan.Diagnostics.ToList();
        var root = new Node3D { Name = "ForgeClimateEffects" };
        ApplyWindGlobals(plan);
        var precipitation = CreatePrecipitation(plan, diagnostics);
        if (precipitation is not null) root.AddChild(precipitation);
        foreach (var body in plan.WaterBodies)
        {
            root.AddChild(CreateWaterBody(body, plan.EffectiveWaterQuality));
        }

        var reverbPreset = plan.ActiveReverbZone?.Preset ?? plan.Reverb.DefaultPreset;
        if (configureRuntimeAudio)
        {
            ConfigureReverbBus(reverbPreset, plan.ActiveReverbZone?.Wet ?? 0.28f, plan.ActiveReverbZone?.Dry ?? 1);
        }

        return new(root, precipitation is null ? 0 : 1, plan.WaterBodies.Count, reverbPreset, diagnostics);
    }

    private static void ApplyWindGlobals(AreaClimatePlan plan)
    {
        var zone = plan.ActiveWindZone;
        var direction = new Vector2(zone?.DirectionX ?? plan.Wind.DirectionX, zone?.DirectionZ ?? plan.Wind.DirectionZ);
        var speed = zone?.Speed ?? plan.Wind.Speed;
        var strength = zone is null ? plan.Wind.GustStrength : plan.Wind.GustStrength * zone.GustMultiplier;
        RenderingServer.GlobalShaderParameterSet("gnd_wind_direction", direction);
        RenderingServer.GlobalShaderParameterSet("gnd_wind_speed", speed);
        RenderingServer.GlobalShaderParameterSet("gnd_wind_strength", strength);
    }

    private static GpuParticles3D? CreatePrecipitation(
        AreaClimatePlan plan,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (plan.Weather.Kind is not WeatherKind.Rain and not WeatherKind.Storm and not WeatherKind.Snow ||
            plan.Weather.Intensity <= 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(plan.Weather.PrecipitationResource))
        {
            var resourcePath = $"res://{plan.Weather.PrecipitationResource}";
            if (!ResourceLoader.Exists(resourcePath))
            {
                diagnostics.Add(new(
                    PrecipitationResourceMissingCode,
                    $"Precipitation resource '{plan.Weather.PrecipitationResource}' is unavailable; procedural fallback applied.",
                    "environment/weather/precipitationResource"));
            }
        }

        var snow = plan.Weather.Kind == WeatherKind.Snow;
        var color = snow ? new Color(0.92f, 0.96f, 1, 0.85f) : new Color(0.48f, 0.72f, 1, 0.62f);
        var drawMaterial = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        var mesh = new QuadMesh
        {
            Size = snow ? new Vector2(0.12f, 0.12f) : new Vector2(0.035f, 0.65f),
            Material = drawMaterial,
        };
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(24, 1, 24),
            Direction = new Vector3(plan.Wind.DirectionX * 0.2f, -1, plan.Wind.DirectionZ * 0.2f),
            InitialVelocityMin = snow ? 2 : 14,
            InitialVelocityMax = snow ? 4 : 20,
            Gravity = snow ? new Vector3(0, -0.4f, 0) : new Vector3(0, -9.8f, 0),
        };
        return new GpuParticles3D
        {
            Name = $"Weather{plan.Weather.Kind}",
            Position = new Vector3(0, 18, 0),
            Amount = Math.Max(64, (int)(1600 * plan.Weather.Intensity)),
            Lifetime = snow ? 9 : 3,
            VisibilityAabb = new Aabb(new Vector3(-28, -24, -28), new Vector3(56, 30, 56)),
            ProcessMaterial = process,
            DrawPass1 = mesh,
            Emitting = true,
        };
    }

    private static Node3D CreateWaterBody(WaterBodyProfile body, WaterQualityTier quality)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = quality >= WaterQualityTier.High
                ? new Color(0.08f, 0.34f, 0.48f, 0.72f)
                : new Color(0.1f, 0.3f, 0.4f, 0.82f),
            Metallic = quality >= WaterQualityTier.High ? 0.45f : 0.18f,
            Roughness = quality >= WaterQualityTier.High ? 0.12f : 0.3f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var root = new Node3D
        {
            Name = $"Water_{body.Id:N}",
            Position = new Vector3(body.Center.X, body.Center.Y, body.Center.Z),
        };
        root.AddChild(new MeshInstance3D
        {
            Name = "Surface",
            Mesh = new PlaneMesh { Size = new Vector2(body.Size.X, body.Size.Z), Material = material },
        });
        var volume = new Area3D { Name = "WaterVolume", Monitoring = true, Monitorable = true };
        volume.AddToGroup("forge_water_volume");
        volume.SetMeta("forge_water_id", body.Id.ToString());
        volume.SetMeta("forge_swimming_enabled", body.SwimmingEnabled);
        volume.SetMeta("forge_buoyancy", body.Buoyancy);
        volume.SetMeta("forge_navigation_cost", body.NavigationCost);
        volume.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0, -body.MaximumDepth / 2, 0),
            Shape = new BoxShape3D { Size = new Vector3(body.Size.X, body.MaximumDepth, body.Size.Z) },
        });
        root.AddChild(volume);
        return root;
    }

    private static void ConfigureReverbBus(string preset, float wet, float dry)
    {
        var index = AudioServer.GetBusIndex(RuntimeAudioBusName);
        if (index < 0)
        {
            AudioServer.AddBus();
            index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, RuntimeAudioBusName);
        }

        while (AudioServer.GetBusEffectCount(index) > 0)
        {
            AudioServer.RemoveBusEffect(index, 0);
        }

        var (roomSize, damping, hipass) = preset.ToLowerInvariant() switch
        {
            "cave" or "cavern" => (0.92f, 0.28f, 0.12f),
            "hall" => (0.78f, 0.38f, 0.18f),
            "forest" => (0.35f, 0.62f, 0.42f),
            _ => (0.5f, 0.5f, 0.25f),
        };
        AudioServer.AddBusEffect(index, new AudioEffectReverb
        {
            RoomSize = roomSize,
            Damping = damping,
            Hipass = hipass,
            Wet = wet,
            Dry = dry,
        });
    }
}
