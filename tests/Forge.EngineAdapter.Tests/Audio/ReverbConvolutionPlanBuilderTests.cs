using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Audio;

namespace Forge.EngineAdapter.Tests.Audio;

public sealed class ReverbConvolutionPlanBuilderTests
{
    [Fact]
    public void AppliesConvolutionImpulseAndOrderedPerSourceSends()
    {
        var zone = CreateZone();
        var provider = new CapturingProvider(true);
        var second = Guid.Parse("a3900000-0000-4000-8000-000000000002");
        var first = Guid.Parse("a3900000-0000-4000-8000-000000000001");

        var result = ReverbConvolutionPlanBuilder.BuildAndApply(
            new(AreaReverbProfile.CurrentSchemaVersion, "neutral", [zone]),
            Vector3Value.Zero,
            [new(second, .25f), new(first, .8f)],
            provider);

        Assert.True(result.Applied);
        Assert.Equal(ReverbProcessingMode.Convolution, provider.Plan!.Mode);
        Assert.Equal("audio/impulses/crypt.wav", provider.Plan.ImpulseResource);
        Assert.Equal([first, second], provider.Plan.SourceSends.Select(item => item.SourceId));
        Assert.Equal(2, result.AppliedSourceSends);
    }

    [Fact]
    public void FallsBackToAlgorithmicPresetWhenProviderHasNoConvolutionCapability()
    {
        var plan = ReverbConvolutionPlanBuilder.Build(
            new(AreaReverbProfile.CurrentSchemaVersion, "neutral", [CreateZone()]),
            Vector3Value.Zero,
            [],
            supportsConvolution: false);

        Assert.Equal(ReverbProcessingMode.Algorithmic, plan.Mode);
        Assert.Empty(plan.ImpulseResource);
        Assert.Contains(plan.Diagnostics, item => item.Code == ReverbConvolutionPlanBuilder.ConvolutionUnavailableCode);
    }

    [Fact]
    public void RejectsDuplicateOrOutOfRangeSourceSends()
    {
        var source = Guid.Parse("a3900000-0000-4000-8000-000000000003");
        var profile = new AreaReverbProfile(1, "neutral", [CreateZone()]);
        Assert.Throws<ArgumentException>(() => ReverbConvolutionPlanBuilder.Build(
            profile, Vector3Value.Zero, [new(source, .2f), new(source, .5f)], true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReverbConvolutionPlanBuilder.Build(
            profile, Vector3Value.Zero, [new(source, 1.1f)], true));
    }

    private static ReverbZoneProfile CreateZone() => new(
        Guid.Parse("a3900000-0000-4000-8000-000000000010"), "Crypt", Vector3Value.Zero,
        new(10, 4, 10), "cave", .7f, 1, 1.5f, "audio/impulses/crypt.wav", "CC0-1.0",
        new string('a', 64), "asset-vault://impulses/crypt");

    private sealed class CapturingProvider(bool supportsConvolution) : IReverbRuntimeProvider
    {
        public bool SupportsConvolution { get; } = supportsConvolution;
        public ReverbConvolutionPlan? Plan { get; private set; }

        public ReverbRuntimeApplyResult Apply(ReverbConvolutionPlan plan)
        {
            Plan = plan;
            return new(true, "AUDIO_REVERB_APPLIED", plan.SourceSends.Count);
        }
    }
}
