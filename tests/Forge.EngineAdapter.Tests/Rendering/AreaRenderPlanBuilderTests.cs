using Forge.Domain.Areas;
using Forge.EngineAdapter.Rendering;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class AreaRenderPlanBuilderTests
{
    [Fact]
    public void SelectsPlatformBudgetAndReportsThresholdsDeterministically()
    {
        var profile = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);

        var healthy = AreaRenderPlanBuilder.Build(profile, "desktop", 12, 3);
        var warning = AreaRenderPlanBuilder.Build(profile, "desktop", 26, 7);
        var exceeded = AreaRenderPlanBuilder.Build(profile, "mobile", 9, 3);

        Assert.Equal(32, healthy.LightBudget.MaximumDynamicLights);
        Assert.Empty(healthy.Diagnostics);
        Assert.Contains(warning.Diagnostics, item => item.Code == AreaRenderPlanBuilder.DynamicLightWarningCode);
        Assert.Contains(warning.Diagnostics, item => item.Code == AreaRenderPlanBuilder.ShadowLightWarningCode);
        Assert.Contains(exceeded.Diagnostics, item => item.Code == AreaRenderPlanBuilder.DynamicLightBudgetCode);
        Assert.Contains(exceeded.Diagnostics, item => item.Code == AreaRenderPlanBuilder.ShadowLightBudgetCode);
    }
}
