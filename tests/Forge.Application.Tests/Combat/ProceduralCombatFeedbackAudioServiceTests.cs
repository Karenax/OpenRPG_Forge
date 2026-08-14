using System.Buffers.Binary;
using Forge.Application.Combat;

namespace Forge.Application.Tests.Combat;

public sealed class ProceduralCombatFeedbackAudioServiceTests
{
    [Fact]
    public void GeneratesDeterministicDistinctBoundedClipsForEveryFeedbackKind()
    {
        var first = Enum.GetValues<CombatFeedbackAudioKind>()
            .Select(ProceduralCombatFeedbackAudioService.Create).ToArray();
        var second = Enum.GetValues<CombatFeedbackAudioKind>()
            .Select(ProceduralCombatFeedbackAudioService.Create).ToArray();

        Assert.Equal(3, first.Length);
        Assert.Equal(3, first.Select(item => item.ContentSha256).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Select(item => item.ContentSha256), second.Select(item => item.ContentSha256));
        Assert.All(first.Zip(second), pair => Assert.Equal(pair.First.Pcm16Mono, pair.Second.Pcm16Mono));
        Assert.All(first, clip =>
        {
            Assert.Equal(ProceduralCombatFeedbackAudioService.SampleRate, clip.SampleRate);
            Assert.InRange(clip.DurationSeconds, .1, .3);
            Assert.InRange(clip.PeakAmplitude, .01, .72);
            Assert.Equal(64, clip.ContentSha256.Length);
            for (var offset = 0; offset < clip.Pcm16Mono.Length; offset += sizeof(short))
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(
                    clip.Pcm16Mono.AsSpan(offset, sizeof(short)));
                Assert.NotEqual(short.MinValue, sample);
                Assert.NotEqual(short.MaxValue, sample);
            }
        });
    }

    [Fact]
    public void MapsResourcesAndRejectsUnknownAudio()
    {
        Assert.Equal(CombatFeedbackAudioKind.ImpactLight,
            ProceduralCombatFeedbackAudioService.Create(
                ProceduralCombatFeedbackAudioService.ImpactLightResource).Kind);
        Assert.Throws<KeyNotFoundException>(() =>
            ProceduralCombatFeedbackAudioService.Create("builtin://combat/untracked"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProceduralCombatFeedbackAudioService.Create((CombatFeedbackAudioKind)999));
    }
}
