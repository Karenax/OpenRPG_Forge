namespace Forge.Domain.Areas;

public enum PbrMaterialWorkflow
{
    MetallicRoughness,
}

public enum ToneMappingProfile
{
    Agx,
    Aces,
    Filmic,
    Linear,
}

public enum RenderQualityTier
{
    Low,
    Medium,
    High,
    Ultra,
}

public enum SkyProfileMode
{
    Physical,
    Procedural,
    Panorama,
}

public sealed record DynamicLightBudgetProfile
{
    public string Platform { get; }

    public int MaximumDynamicLights { get; }

    public int MaximumShadowCastingLights { get; }

    public int ShadowAtlasSize { get; }

    public double WarningThreshold { get; }

    public DynamicLightBudgetProfile(
        string platform,
        int maximumDynamicLights,
        int maximumShadowCastingLights,
        int shadowAtlasSize,
        double warningThreshold)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        if (maximumDynamicLights is < 0 or > 4096 ||
            maximumShadowCastingLights < 0 || maximumShadowCastingLights > maximumDynamicLights ||
            shadowAtlasSize is < 512 or > 16384 ||
            (shadowAtlasSize & (shadowAtlasSize - 1)) != 0 ||
            warningThreshold is < 0.5 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDynamicLights));
        }

        Platform = platform.Trim().ToLowerInvariant();
        MaximumDynamicLights = maximumDynamicLights;
        MaximumShadowCastingLights = maximumShadowCastingLights;
        ShadowAtlasSize = shadowAtlasSize;
        WarningThreshold = warningThreshold;
    }
}

public sealed record AreaVisualTuningProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public double Brightness { get; }

    public double ContrastMultiplier { get; }

    public double SaturationMultiplier { get; }

    public double AmbientOcclusionIntensityMultiplier { get; }

    public double AmbientOcclusionPower { get; }

    public double DirectionalLightPitchDegrees { get; }

    public double DirectionalLightYawDegrees { get; }

    public double DirectionalLightAngularDistance { get; }

    public double ShadowBlur { get; }

    public AreaVisualTuningProfile(
        int schemaVersion,
        double brightness,
        double contrastMultiplier,
        double saturationMultiplier,
        double ambientOcclusionIntensityMultiplier,
        double ambientOcclusionPower,
        double directionalLightPitchDegrees,
        double directionalLightYawDegrees,
        double directionalLightAngularDistance,
        double shadowBlur)
    {
        if (schemaVersion != CurrentSchemaVersion ||
            !double.IsFinite(brightness) || brightness is < 0.1 or > 4 ||
            !double.IsFinite(contrastMultiplier) || contrastMultiplier is < 0.25 or > 4 ||
            !double.IsFinite(saturationMultiplier) || saturationMultiplier is < 0 or > 4 ||
            !double.IsFinite(ambientOcclusionIntensityMultiplier) ||
            ambientOcclusionIntensityMultiplier is < 0 or > 4 ||
            !double.IsFinite(ambientOcclusionPower) || ambientOcclusionPower is < 0.1 or > 8 ||
            !double.IsFinite(directionalLightPitchDegrees) || directionalLightPitchDegrees is < -89 or > 89 ||
            !double.IsFinite(directionalLightYawDegrees) || directionalLightYawDegrees is < -360 or > 360 ||
            !double.IsFinite(directionalLightAngularDistance) || directionalLightAngularDistance is < 0 or > 90 ||
            !double.IsFinite(shadowBlur) || shadowBlur is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        SchemaVersion = schemaVersion;
        Brightness = brightness;
        ContrastMultiplier = contrastMultiplier;
        SaturationMultiplier = saturationMultiplier;
        AmbientOcclusionIntensityMultiplier = ambientOcclusionIntensityMultiplier;
        AmbientOcclusionPower = ambientOcclusionPower;
        DirectionalLightPitchDegrees = directionalLightPitchDegrees;
        DirectionalLightYawDegrees = directionalLightYawDegrees;
        DirectionalLightAngularDistance = directionalLightAngularDistance;
        ShadowBlur = shadowBlur;
    }

    public static AreaVisualTuningProfile CreateDefault() => new(
        CurrentSchemaVersion,
        brightness: 1,
        contrastMultiplier: 1,
        saturationMultiplier: 1,
        ambientOcclusionIntensityMultiplier: 1,
        ambientOcclusionPower: 1.35,
        directionalLightPitchDegrees: -58,
        directionalLightYawDegrees: -32,
        directionalLightAngularDistance: .52,
        shadowBlur: 1.45);
}

public sealed record AreaRenderingProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public PbrMaterialWorkflow MaterialWorkflow { get; }

    public ToneMappingProfile ToneMapping { get; }

    public RenderQualityTier Quality { get; }

    public bool ForwardPlus { get; }

    public bool PerPixelLighting { get; }

    public bool MaterialInstances { get; }

    public bool HdrOutput { get; }

    public double Exposure { get; }

    public double WhitePoint { get; }

    public string LutResource { get; }

    public IReadOnlyList<DynamicLightBudgetProfile> LightBudgets { get; }

    public AreaVisualTuningProfile VisualTuning { get; }

    public AreaRenderingProfile(
        int schemaVersion,
        PbrMaterialWorkflow materialWorkflow,
        ToneMappingProfile toneMapping,
        RenderQualityTier quality,
        bool forwardPlus,
        bool perPixelLighting,
        bool materialInstances,
        bool hdrOutput,
        double exposure,
        double whitePoint,
        string lutResource,
        IReadOnlyList<DynamicLightBudgetProfile> lightBudgets,
        AreaVisualTuningProfile? visualTuning = null)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(lutResource);
        ArgumentNullException.ThrowIfNull(lightBudgets);
        if (exposure is < 0.05 or > 8 || whitePoint is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(exposure));
        }

        var budgets = lightBudgets.OrderBy(item => item.Platform, StringComparer.Ordinal).ToArray();
        if (budgets.Length == 0 ||
            budgets.Select(item => item.Platform).Distinct(StringComparer.Ordinal).Count() != budgets.Length)
        {
            throw new ArgumentException("Rendering profiles require unique platform light budgets.", nameof(lightBudgets));
        }

        if (!string.IsNullOrWhiteSpace(lutResource) && !IsSafeResourcePath(lutResource))
        {
            throw new ArgumentException("The color lookup resource must be a safe relative path.", nameof(lutResource));
        }

        SchemaVersion = schemaVersion;
        MaterialWorkflow = materialWorkflow;
        ToneMapping = toneMapping;
        Quality = quality;
        ForwardPlus = forwardPlus;
        PerPixelLighting = perPixelLighting;
        MaterialInstances = materialInstances;
        HdrOutput = hdrOutput;
        Exposure = exposure;
        WhitePoint = whitePoint;
        LutResource = lutResource.Trim().Replace('\\', '/');
        LightBudgets = budgets;
        VisualTuning = visualTuning ?? AreaVisualTuningProfile.CreateDefault();
    }

    public static AreaRenderingProfile CreateDefault() => new(
        CurrentSchemaVersion,
        PbrMaterialWorkflow.MetallicRoughness,
        ToneMappingProfile.Agx,
        RenderQualityTier.High,
        forwardPlus: true,
        perPixelLighting: true,
        materialInstances: true,
        hdrOutput: true,
        exposure: 1,
        whitePoint: 6,
        lutResource: string.Empty,
        [
            new("desktop", 32, 8, 4096, 0.8),
            new("mobile", 8, 2, 2048, 0.75),
        ]);

    private static bool IsSafeResourcePath(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        return !Path.IsPathRooted(normalized) &&
               !normalized.Split('/').Any(segment => segment is "" or "." or "..");
    }
}

public sealed record SkyProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public SkyProfileMode Mode { get; }

    public string PanoramaResource { get; }

    public string ZenithColor { get; }

    public string HorizonColor { get; }

    public string GroundColor { get; }

    public double StarIntensity { get; }

    public double CloudCoverage { get; }

    public double MoonIntensity { get; }

    public double EnergyMultiplier { get; }

    public double TransitionSeconds { get; }

    public SkyProfile(
        int schemaVersion,
        SkyProfileMode mode,
        string panoramaResource,
        string zenithColor,
        string horizonColor,
        string groundColor,
        double starIntensity,
        double cloudCoverage,
        double moonIntensity,
        double energyMultiplier,
        double transitionSeconds)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(panoramaResource);
        if (!IsHexColor(zenithColor) || !IsHexColor(horizonColor) || !IsHexColor(groundColor) ||
            starIntensity is < 0 or > 4 || cloudCoverage is < 0 or > 1 || moonIntensity is < 0 or > 4 ||
            energyMultiplier is < 0.05 or > 8 || transitionSeconds is < 0 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(starIntensity));
        }

        var panorama = panoramaResource.Trim().Replace('\\', '/');
        if (mode == SkyProfileMode.Panorama && string.IsNullOrWhiteSpace(panorama) ||
            !string.IsNullOrWhiteSpace(panorama) &&
            (Path.IsPathRooted(panorama) || panorama.Split('/').Any(segment => segment is "" or "." or "..")))
        {
            throw new ArgumentException("Panorama skies require a safe relative HDRI resource.", nameof(panoramaResource));
        }

        SchemaVersion = schemaVersion;
        Mode = mode;
        PanoramaResource = panorama;
        ZenithColor = NormalizeColor(zenithColor);
        HorizonColor = NormalizeColor(horizonColor);
        GroundColor = NormalizeColor(groundColor);
        StarIntensity = starIntensity;
        CloudCoverage = cloudCoverage;
        MoonIntensity = moonIntensity;
        EnergyMultiplier = energyMultiplier;
        TransitionSeconds = transitionSeconds;
    }

    public static SkyProfile CreateDefault(AreaEnvironmentClass classification) => classification switch
    {
        AreaEnvironmentClass.Interior or AreaEnvironmentClass.Underground =>
            new(CurrentSchemaVersion, SkyProfileMode.Procedural, string.Empty, "#171225", "#38283f", "#100d17", 0, 0, 0, 0.35, 1),
        _ => new(CurrentSchemaVersion, SkyProfileMode.Physical, string.Empty, "#4c78a8", "#e4b88a", "#38402f", 0.15, 0.2, 0.1, 1, 2),
    };

    private static bool IsHexColor(string value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private static string NormalizeColor(string value) => value.ToLowerInvariant();
}
