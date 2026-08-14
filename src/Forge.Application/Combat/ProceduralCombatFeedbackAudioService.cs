using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Forge.Application.Combat;

public enum CombatFeedbackAudioKind
{
    ImpactLight,
    Healing,
    AbilityReady,
}

public sealed record ProceduralCombatFeedbackAudioClip(
    CombatFeedbackAudioKind Kind,
    string Resource,
    int SampleRate,
    double DurationSeconds,
    byte[] Pcm16Mono,
    double PeakAmplitude,
    string ContentSha256);

public static class ProceduralCombatFeedbackAudioService
{
    public const string ImpactLightResource = "builtin://combat/impact-light";
    public const string HealingResource = "builtin://combat/healing";
    public const string AbilityReadyResource = "builtin://combat/ability-ready";
    public const int SampleRate = 22050;

    public static IReadOnlyList<string> SupportedResources { get; } =
        [ImpactLightResource, HealingResource, AbilityReadyResource];

    public static ProceduralCombatFeedbackAudioClip Create(CombatFeedbackAudioKind kind) =>
        Create(ResourceFor(kind));

    public static ProceduralCombatFeedbackAudioClip Create(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        var kind = resource switch
        {
            ImpactLightResource => CombatFeedbackAudioKind.ImpactLight,
            HealingResource => CombatFeedbackAudioKind.Healing,
            AbilityReadyResource => CombatFeedbackAudioKind.AbilityReady,
            _ => throw new KeyNotFoundException($"Unknown procedural combat feedback resource '{resource}'."),
        };
        var durationSeconds = kind switch
        {
            CombatFeedbackAudioKind.ImpactLight => .14,
            CombatFeedbackAudioKind.Healing => .28,
            CombatFeedbackAudioKind.AbilityReady => .22,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var sampleCount = (int)Math.Round(SampleRate * durationSeconds, MidpointRounding.AwayFromZero);
        var data = new byte[sampleCount * sizeof(short)];
        var noiseState = Seed(resource);
        var peak = 0d;
        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / (double)SampleRate;
            var progress = time / durationSeconds;
            var fadeIn = Math.Clamp(time / .012, 0, 1);
            var fadeOut = Math.Clamp((durationSeconds - time) / .055, 0, 1);
            var envelope = fadeIn * fadeOut;
            var value = kind switch
            {
                CombatFeedbackAudioKind.ImpactLight =>
                    (NextNoise(ref noiseState) * .24 + Math.Sin(Math.Tau * (118 - 52 * progress) * time) * .42) *
                    Math.Exp(-8 * progress),
                CombatFeedbackAudioKind.Healing =>
                    (Math.Sin(Math.Tau * 523.25 * time) * .22 +
                     Math.Sin(Math.Tau * 659.25 * time) * .18 +
                     Math.Sin(Math.Tau * 783.99 * time) * .12) * Math.Sin(Math.PI * progress),
                CombatFeedbackAudioKind.AbilityReady =>
                    (Math.Sin(Math.Tau * 659.25 * time) * .24 +
                     Math.Sin(Math.Tau * 987.77 * time) * .19) *
                    (progress < .48 ? Math.Sin(Math.PI * progress / .48) :
                        Math.Sin(Math.PI * (progress - .48) / .52) * .82),
                _ => 0,
            };
            var bounded = Math.Clamp(value * envelope, -.72, .72);
            peak = Math.Max(peak, Math.Abs(bounded));
            var pcm = (short)Math.Round(bounded * short.MaxValue, MidpointRounding.AwayFromZero);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index * sizeof(short), sizeof(short)), pcm);
        }

        return new ProceduralCombatFeedbackAudioClip(
            kind,
            resource,
            SampleRate,
            sampleCount / (double)SampleRate,
            data,
            peak,
            Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant());
    }

    private static string ResourceFor(CombatFeedbackAudioKind kind) => kind switch
    {
        CombatFeedbackAudioKind.ImpactLight => ImpactLightResource,
        CombatFeedbackAudioKind.Healing => HealingResource,
        CombatFeedbackAudioKind.AbilityReady => AbilityReadyResource,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static uint Seed(string resource)
    {
        var state = 2166136261u;
        foreach (var character in resource)
        {
            state ^= character;
            state *= 16777619u;
        }
        return state == 0 ? 1u : state;
    }

    private static double NextNoise(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return ((state & 0x00ffffff) / 8388607.5) - 1;
    }
}
