using Forge.Application.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class ProceduralAmbientAudioServiceTests
{
    [Fact]
    public void ProducesDeterministicDistinctGovernedPcmForEveryBuiltinForestCue()
    {
        var firstPass = ProceduralAmbientAudioService.SupportedResources
            .Select(ProceduralAmbientAudioService.Create)
            .ToArray();
        var secondPass = ProceduralAmbientAudioService.SupportedResources
            .Select(ProceduralAmbientAudioService.Create)
            .ToArray();

        Assert.Equal(3, firstPass.Length);
        Assert.Equal(3, firstPass.Select(clip => clip.ContentSha256).Distinct(StringComparer.Ordinal).Count());
        Assert.All(firstPass, clip =>
        {
            Assert.Equal(ProceduralAmbientAudioService.SampleRate, clip.SampleRate);
            Assert.True(clip.Pcm16Mono.Length >= ProceduralAmbientAudioService.SampleRate * sizeof(short) * 2);
            Assert.Equal(64, clip.ContentSha256.Length);
        });
        Assert.Equal(
            firstPass.Select(clip => clip.ContentSha256),
            secondPass.Select(clip => clip.ContentSha256));
        Assert.All(firstPass.Zip(secondPass), pair => Assert.Equal(pair.First.Pcm16Mono, pair.Second.Pcm16Mono));
    }

    [Fact]
    public void RejectsAnUnknownBuiltinInsteadOfGeneratingUntrackedAudio()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            ProceduralAmbientAudioService.Create("builtin://forest/untracked"));
    }
}
