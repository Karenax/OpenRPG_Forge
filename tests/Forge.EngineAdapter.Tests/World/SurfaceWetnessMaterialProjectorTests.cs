using Forge.Domain.Areas;
using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class SurfaceWetnessMaterialProjectorTests
{
    [Fact]
    public void Projection_is_bounded_reversible_and_preserves_alpha()
    {
        var dry = new PbrMaterialState(.8f, .6f, .4f, .35f, .74f, .2f);
        var coefficients = SurfaceWetnessMaterialProfile.CreateDefault();

        var unchanged = SurfaceWetnessMaterialProjector.Project(dry, 0, coefficients);
        var wet = SurfaceWetnessMaterialProjector.Project(dry, .8f, coefficients);
        var repeatedFromBaseline = SurfaceWetnessMaterialProjector.Project(dry, .8f, coefficients);
        var restored = SurfaceWetnessMaterialProjector.Project(dry, 0, coefficients);

        Assert.Equal(dry, unchanged);
        Assert.Equal(dry, restored);
        Assert.Equal(wet, repeatedFromBaseline);
        Assert.InRange(wet.AlbedoR, dry.AlbedoR * .65f, dry.AlbedoR - .001f);
        Assert.InRange(wet.AlbedoG, dry.AlbedoG * .65f, dry.AlbedoG - .001f);
        Assert.InRange(wet.AlbedoB, dry.AlbedoB * .65f, dry.AlbedoB - .001f);
        Assert.Equal(dry.Alpha, wet.Alpha);
        Assert.InRange(wet.Roughness, .08f, dry.Roughness - .001f);
        Assert.InRange(wet.Specular, dry.Specular + .001f, 1f);
    }

    [Fact]
    public void Transition_reaches_rain_and_then_restores_dryness_without_overshoot()
    {
        var rainTransition = SurfaceWetnessTransition.Start(0, .8f, 2);
        var rainStep = SurfaceWetnessTransition.Advance(rainTransition, .5);
        var rain = SurfaceWetnessTransition.Advance(rainStep.State, 1.5);
        var dryingTransition = SurfaceWetnessTransition.Start(rain.Value, 0, 2);
        var dryingStep = SurfaceWetnessTransition.Advance(dryingTransition, .5);
        var dry = SurfaceWetnessTransition.Advance(dryingStep.State, 2);

        Assert.Equal(.2f, rainStep.Value, 4);
        Assert.Equal(.8f, rain.Value, 4);
        Assert.Equal(.6f, dryingStep.Value, 4);
        Assert.Equal(0, dry.Value);
    }
}
