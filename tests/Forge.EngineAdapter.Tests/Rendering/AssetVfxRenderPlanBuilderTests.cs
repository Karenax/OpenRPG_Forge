using Forge.Domain.Assets;
using Forge.EngineAdapter.Rendering;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class AssetVfxRenderPlanBuilderTests
{
    private sealed class Provider : IAssetVfxRenderProvider
    {
        public VfxProviderCapabilities Capabilities { get; } = new("forge-vfx", true, true, true, true);
    }

    [Fact]
    public void ProviderBuildsParticlesDecalLightAndAudioWithinBudget()
    {
        var manifest = new AssetVfxManifest(true, null, "forge-vfx", 2048, 8, 50);
        var plan = AssetVfxRenderPlanBuilder.Build(
            new(manifest, 12, 512, 4, true, true, true), new Provider());
        Assert.True(plan.Valid);
        Assert.Equal("asset.vfx.render_ready", plan.DiagnosticCode);
        Assert.True(plan.Decal && plan.Light && plan.AudioCue);
    }

    [Fact]
    public void ProviderCullsAndRejectsRequiredAbsenceOrBudgetOverflow()
    {
        var manifest = new AssetVfxManifest(true, null, "forge-vfx", 100, 2, 10);
        Assert.True(AssetVfxRenderPlanBuilder.Build(new(manifest, 11, 100, 2, false, false, false),
            new Provider()).Culled);
        Assert.Equal("asset.vfx.provider_required",
            AssetVfxRenderPlanBuilder.Build(new(manifest, 1, 1, 1, false, false, false), null).DiagnosticCode);
        Assert.Equal("asset.vfx.budget_exceeded",
            AssetVfxRenderPlanBuilder.Build(new(manifest, 1, 101, 2, false, false, false),
                new Provider()).DiagnosticCode);
    }
}
