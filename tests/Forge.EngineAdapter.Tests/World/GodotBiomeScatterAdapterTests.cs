using Forge.EngineAdapter.World;

namespace Forge.EngineAdapter.Tests.World;

public sealed class GodotBiomeScatterAdapterTests
{
    [Fact]
    public void Wind_shader_uses_slow_non_emissive_vegetation_motion()
    {
        var shader = GodotBiomeScatterAdapter.WindVegetationShaderCode;

        Assert.Contains("TIME * speed * 0.36", shader, StringComparison.Ordinal);
        Assert.Contains("TIME * speed * 0.62", shader, StringComparison.Ordinal);
        Assert.Contains("TIME * speed * 1.45", shader, StringComparison.Ordinal);
        Assert.Contains("EMISSION = vegetation_color * 0.025", shader, StringComparison.Ordinal);
        Assert.Contains("source_luminance * 1.12 + 0.05", shader, StringComparison.Ordinal);
    }
}
