using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaEnvironmentAuthoringProfileTests
{
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("a0540000-0000-4000-8000-000000000001"));

    [Fact]
    public void R054ClassificationLightingFogAndRuntimeContractValidateTogether()
    {
        var environment = Environment();
        var area = new AreaDefinition(AreaId, "r054", "R054", [], AreaKind.Exterior,
            environment: environment);

        Assert.Empty(AreaEnvironmentAuthoringValidator.Validate(area, environment));
        Assert.Equal("custom:moonlit-forest", environment.Authoring.Classification.Key);
        Assert.True(environment.Authoring.Classification.DrivesNavigation);
        Assert.Equal(3, environment.Authoring.Lighting.Curves.Count);
        Assert.Single(environment.Authoring.Lighting.Events);
        Assert.Single(environment.Authoring.Lighting.Probes);
        Assert.Single(environment.Authoring.Lighting.VolumeOverrides);
        Assert.NotNull(environment.Authoring.Fog.Noise);
        Assert.Single(environment.Authoring.Fog.LocalVolumes);
        Assert.Equal(AreaFogQualityTier.High, environment.Authoring.Fog.QualityProfiles["desktop"]);

        var optional = AreaEnvironmentRuntimeResolver.Resolve(area.Id, null, required: false);
        var required = AreaEnvironmentRuntimeResolver.Resolve(area.Id, null, required: true);
        var resolved = AreaEnvironmentRuntimeResolver.Resolve(area.Id, environment, required: true);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal(AreaEnvironmentRuntimeResolver.RequiredMissingCode, Assert.Single(required.Diagnostics).Code);
        Assert.Equal("physical-moonlit", resolved.Handle!.LightingPresetKey);
        Assert.Equal(1, resolved.Handle.FogVolumes);
    }

    [Fact]
    public void R054InvalidEnvironmentReturnsStableLocalizedDiagnostics()
    {
        var source = Environment();
        var invalidAuthoring = new AreaEnvironmentAuthoringProfile(Guid.Empty, 99, string.Empty,
            new(AreaEnvironmentClass.Interior, "invalid key", string.Empty,
                false, false, false, false, false, false),
            new(string.Empty, "bad", "#ffffff", false,
                [new(-1, -1, 0, -120)],
                [new(Guid.Empty, 25, string.Empty, "../outside.forge", true)],
                [new(Guid.Empty, string.Empty, TransformValue.Identity, string.Empty, null, 0, false,
                    new Dictionary<string, string>())],
                []),
            new("bad", "#ffffff", new(string.Empty, 0, 0, 0, -1),
                [new(Guid.Empty, string.Empty, TransformValue.Identity, FogMode.Volumetric, 2, -1, "bad", -1,
                    new Dictionary<string, string>())], new Dictionary<string, AreaFogQualityTier>()), true);
        var invalid = new AreaEnvironmentProfile(source.SchemaVersion, source.Classification, source.Tags,
            source.Lighting, source.Fog, source.Audio, source.Rendering, source.Sky, source.Wind, source.Weather,
            source.Water, source.Reverb, invalidAuthoring);
        var area = new AreaDefinition(AreaId, "r054-invalid", "R054 invalid", [], AreaKind.Exterior,
            environment: invalid);

        var diagnostics = AreaEnvironmentAuthoringValidator.Validate(area, invalid);

        Assert.Contains(diagnostics, item => item.Code == AreaEnvironmentAuthoringValidator.IdentityCode);
        Assert.Contains(diagnostics, item => item.Code == AreaEnvironmentAuthoringValidator.SchemaCode &&
            item.Path.EndsWith("schemaVersion", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == AreaEnvironmentAuthoringValidator.ClassificationCode);
        Assert.Contains(diagnostics, item => item.Code == AreaEnvironmentAuthoringValidator.LightingCode);
        Assert.Contains(diagnostics, item => item.Code == AreaEnvironmentAuthoringValidator.EventCode);
        Assert.Contains(diagnostics, item => item.Code == AreaEnvironmentAuthoringValidator.ProbeCode);
        Assert.Contains(diagnostics, item => item.Code == AreaEnvironmentAuthoringValidator.FogCode);
    }

    [Fact]
    public void R054CloneCreatesNewCanonicalEnvironmentAndChildUuidsWhileSharingAssets()
    {
        var source = Environment();
        var destination = AreaId.From(Guid.Parse("a0540000-0000-4000-8000-000000000099"));

        var clone = source.CloneForArea(destination);

        Assert.NotEqual(source.Authoring.Id, clone.Authoring.Id);
        Assert.NotEqual(source.Authoring.Lighting.Events[0].Id, clone.Authoring.Lighting.Events[0].Id);
        Assert.NotEqual(source.Authoring.Lighting.Probes[0].Id, clone.Authoring.Lighting.Probes[0].Id);
        Assert.NotEqual(source.Authoring.Lighting.VolumeOverrides[0].Id,
            clone.Authoring.Lighting.VolumeOverrides[0].Id);
        Assert.NotEqual(source.Authoring.Fog.LocalVolumes[0].Id, clone.Authoring.Fog.LocalVolumes[0].Id);
        Assert.Equal(source.Authoring.Lighting.Probes[0].AssetId, clone.Authoring.Lighting.Probes[0].AssetId);
        Assert.Equal("area-clone", clone.Authoring.Provenance);
    }

    internal static AreaEnvironmentProfile Environment()
    {
        var baseline = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);
        var authoring = new AreaEnvironmentAuthoringProfile(
            Guid.Parse("a0540000-0000-4000-8000-000000000010"), 1, "test:r054",
            new(AreaEnvironmentClass.Exterior, "custom:moonlit-forest", "forest-pack",
                true, true, true, true, true, true),
            new("physical-moonlit", "#30405f", "#ffe2b5", false,
                [new(0, 0, .8f, -45), new(12, 1.4f, 0, 65), new(24, 0, .8f, -45)],
                [new(Guid.Parse("a0540000-0000-4000-8000-000000000011"), 18,
                    "world.sunset", "scripts/sunset.forge", true)],
                [new(Guid.Parse("a0540000-0000-4000-8000-000000000012"), "Forest probe",
                    TransformValue.Identity, "asset:probe-forest", BlueprintId.From(
                        Guid.Parse("a0540000-0000-4000-8000-000000000020")), 24, false,
                    new Dictionary<string, string> { ["priority"] = "high" })],
                [new(Guid.Parse("a0540000-0000-4000-8000-000000000013"), "Moon shrine",
                    TransformValue.Identity, "physical-shrine", FogMode.Volumetric, 3,
                    new Dictionary<string, string> { ["moon.boost"] = "1.5" })]),
            new("#b7c9dd", "#263248", new("asset:fog-noise", 12, .1f, .05f, 1.2f),
                [new(Guid.Parse("a0540000-0000-4000-8000-000000000014"), "Low mist",
                    TransformValue.Identity, FogMode.Volumetric, .3f, .2f, "#9fb4c8", 2,
                    new Dictionary<string, string> { ["quality"] = "desktop" })],
                new Dictionary<string, AreaFogQualityTier>(StringComparer.Ordinal)
                {
                    ["desktop"] = AreaFogQualityTier.High,
                    ["low"] = AreaFogQualityTier.Low,
                }), true);
        return new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion,
            AreaEnvironmentClass.Exterior, ["forest", "moonlit"],
            new(DayNightMode.Clock, 30, .4f, 1.4f, 5400, true, true),
            new(FogMode.Volumetric, .3f, 4, 90, 1.5f), baseline.Audio, baseline.Rendering,
            baseline.Sky, baseline.Wind, baseline.Weather, baseline.Water, baseline.Reverb, authoring);
    }
}
