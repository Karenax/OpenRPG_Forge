using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Forge.Application.Areas;

public sealed record ProceduralAmbientAudioClip(
    string Resource,
    int SampleRate,
    byte[] Pcm16Mono,
    string ContentSha256);

public static class ProceduralAmbientAudioService
{
    public const string CanopyWindResource = "builtin://forest/canopy-wind";
    public const string BirdsongResource = "builtin://forest/birdsong";
    public const string StreamResource = "builtin://forest/stream";
    public const int SampleRate = 22050;

    public static IReadOnlyList<string> SupportedResources { get; } =
        [CanopyWindResource, BirdsongResource, StreamResource];

    public static ProceduralAmbientAudioClip Create(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        if (!SupportedResources.Contains(resource, StringComparer.Ordinal))
            throw new KeyNotFoundException($"Unknown procedural ambient audio resource '{resource}'.");

        var durationSeconds = resource switch
        {
            CanopyWindResource => 2.4,
            BirdsongResource => 2.0,
            StreamResource => 2.2,
            _ => throw new InvalidOperationException("Unsupported procedural ambient resource."),
        };
        var sampleCount = (int)(SampleRate * durationSeconds);
        var data = new byte[sampleCount * sizeof(short)];
        var noiseState = Seed(resource);
        var filteredNoise = 0d;
        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / (double)SampleRate;
            var noise = NextNoise(ref noiseState);
            filteredNoise = (filteredNoise * .985) + (noise * .015);
            var sample = resource switch
            {
                CanopyWindResource =>
                    (filteredNoise * .72) + (Math.Sin(time * Math.Tau * .19) * .10) +
                    (Math.Sin(time * Math.Tau * .43) * .05),
                BirdsongResource =>
                    (filteredNoise * .035) + Chirp(time, .18, .24, 1850, 950) +
                    (Chirp(time, .72, .18, 2450, -420) * .78) +
                    (Chirp(time, 1.28, .30, 1550, 1350) * .86),
                StreamResource =>
                    (noise * .16) + (filteredNoise * .30) +
                    (Math.Sin(time * Math.Tau * 7.1) * .035) +
                    (Math.Sin(time * Math.Tau * 11.7) * .025),
                _ => 0,
            };
            var gain = resource == BirdsongResource ? .72 : .58;
            var fade = Math.Min(1, Math.Min(time / .08, (durationSeconds - time) / .12));
            var normalized = Math.Clamp(sample * gain * Math.Max(0, fade), -1, 1);
            var pcm = (short)Math.Round(normalized * short.MaxValue, MidpointRounding.AwayFromZero);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index * sizeof(short), sizeof(short)), pcm);
        }

        var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        return new ProceduralAmbientAudioClip(resource, SampleRate, data, hash);
    }

    private static double Chirp(double time, double start, double duration, double baseFrequency, double sweep)
    {
        var elapsed = time - start;
        if (elapsed < 0 || elapsed > duration) return 0;
        var progress = elapsed / duration;
        var envelope = Math.Pow(Math.Sin(progress * Math.PI), 2);
        var phase = Math.Tau * ((baseFrequency * elapsed) + ((sweep * elapsed * elapsed) / (2 * duration)));
        return Math.Sin(phase) * envelope * .42;
    }

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
