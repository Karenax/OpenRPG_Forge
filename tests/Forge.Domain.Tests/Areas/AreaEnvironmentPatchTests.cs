using Forge.Domain.Areas;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaEnvironmentPatchTests
{
    [Fact]
    public void LightingOnlyPatchPreservesEveryOmittedEnvironmentSection()
    {
        var areaId = AreaId.From(Guid.Parse("dc6d4b55-f0bd-4a02-ad5c-8b40cbf2b101"));
        var source = AreaEnvironmentProfile.CreateDefault(areaId, AreaKind.Exterior);
        var lighting = new AreaLightingProfile(DayNightMode.AlwaysNight, 42, .21f, .64f, 4100, true, false);

        var updated = AreaEnvironmentPatch.Apply(source,
            new([AreaEnvironmentPatchField.Lighting], Lighting: lighting));

        Assert.Same(lighting, updated.Lighting);
        Assert.Equal(source.Classification, updated.Classification);
        Assert.Equal(source.Tags, updated.Tags);
        Assert.Same(source.Fog, updated.Fog);
        Assert.Same(source.Audio, updated.Audio);
        Assert.Same(source.Rendering, updated.Rendering);
        Assert.Same(source.Sky, updated.Sky);
        Assert.Same(source.Wind, updated.Wind);
        Assert.Same(source.Weather, updated.Weather);
        Assert.Same(source.Water, updated.Water);
        Assert.Same(source.Reverb, updated.Reverb);
        Assert.Same(source.Authoring, updated.Authoring);
        Assert.Equal(source.RequireAuthoringRuntimeMetadata, updated.RequireAuthoringRuntimeMetadata);
    }

    [Fact]
    public void TypedPatchCanChangeRenderingPrecipitationAndAudioTogether()
    {
        var source = AreaEnvironmentProfile.CreateDefault(AreaKind.Interior);
        var rendering = AreaRenderingProfile.CreateDefault();
        var precipitation = WeatherProfile.CreateDefault(AreaEnvironmentClass.Exterior);
        var audio = new AreaAudioProfile("day.ogg", "night.ogg", "combat.ogg", "rain.ogg", "forest", 1.5f);
        var patch = new AreaEnvironmentPatch(
            [AreaEnvironmentPatchField.Rendering, AreaEnvironmentPatchField.Precipitation,
                AreaEnvironmentPatchField.Audio],
            Rendering: rendering,
            Precipitation: precipitation,
            Audio: audio);

        var updated = AreaEnvironmentPatch.Apply(source, patch);

        Assert.Same(rendering, updated.Rendering);
        Assert.Same(precipitation, updated.Weather);
        Assert.Same(audio, updated.Audio);
        Assert.Same(source.Lighting, updated.Lighting);
        Assert.Equal(["environment/rendering", "environment/precipitation", "environment/audio"],
            patch.RelativePaths);
    }
}
