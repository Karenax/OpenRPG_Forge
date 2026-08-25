[CmdletBinding()]
param(
    [string]$OutputPath = 'apps/Forge.Runtime/content/solaren/branding/solaren-aubeciel-calm-v1.wav'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot $OutputPath
}
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutputPath)
if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw 'The launcher music output has no parent directory.'
}
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$generatorSource = @'
using System;
using System.IO;
using System.Text;

public static class SolarenLauncherMusicGenerator
{
    private const int SampleRate = 44100;
    private const int Channels = 2;
    private const int BitsPerSample = 16;
    private const double BeatsPerMinute = 60.0;
    private const double BeatSeconds = 60.0 / BeatsPerMinute;
    private const int Bars = 16;
    private const double DurationSeconds = Bars * 4 * BeatSeconds;

    private static readonly int[][] Chords =
    {
        new[] { 48, 55, 59, 64 },
        new[] { 47, 55, 62, 64 },
        new[] { 45, 52, 55, 60 },
        new[] { 41, 48, 52, 55 },
        new[] { 40, 48, 55, 59 },
        new[] { 50, 57, 60, 65 },
        new[] { 43, 50, 55, 60 },
        new[] { 43, 50, 59, 62 },
        new[] { 48, 55, 59, 64 },
        new[] { 47, 55, 62, 67 },
        new[] { 45, 52, 60, 64 },
        new[] { 41, 48, 55, 60 },
        new[] { 40, 48, 55, 64 },
        new[] { 50, 57, 60, 65 },
        new[] { 43, 50, 55, 60 },
        new[] { 43, 50, 59, 62 },
    };

    private sealed class NoteEvent
    {
        public NoteEvent(double beat, int midi, double beats)
        {
            Beat = beat;
            Midi = midi;
            Beats = beats;
        }

        public double Beat { get; private set; }
        public int Midi { get; private set; }
        public double Beats { get; private set; }
    }

    private static readonly NoteEvent[] Melody =
    {
        new NoteEvent(2, 67, 1.5), new NoteEvent(4, 69, 1.5), new NoteEvent(6, 72, 2.5),
        new NoteEvent(10, 71, 1.5), new NoteEvent(12, 69, 2.5), new NoteEvent(15, 67, 1.0),
        new NoteEvent(18, 64, 2.0), new NoteEvent(21, 67, 1.5), new NoteEvent(23, 69, 2.5),
        new NoteEvent(27, 65, 2.0), new NoteEvent(30, 64, 1.5),
        new NoteEvent(34, 67, 1.5), new NoteEvent(36, 72, 2.0), new NoteEvent(39, 74, 1.5),
        new NoteEvent(42, 72, 2.0), new NoteEvent(45, 69, 2.0), new NoteEvent(48, 67, 2.5),
        new NoteEvent(52, 65, 1.5), new NoteEvent(54, 64, 1.5), new NoteEvent(56, 62, 2.0),
        new NoteEvent(59, 64, 1.5), new NoteEvent(61, 67, 2.0),
    };

    public static void Generate(string path)
    {
        var sampleCount = checked((int)Math.Round(DurationSeconds * SampleRate));
        var left = new float[sampleCount];
        var right = new float[sampleCount];

        for (var bar = 0; bar < Chords.Length; bar++)
        {
            var start = bar * 4 * BeatSeconds;
            var chord = Chords[bar];
            for (var voice = 0; voice < chord.Length; voice++)
            {
                var pan = -0.32 + voice * 0.21;
                AddPad(left, right, start, 4.35, MidiToFrequency(chord[voice]), 0.034, pan);
            }

            var pattern = new[] { 0, 1, 2, 3, 2, 1, 3, 1 };
            for (var step = 0; step < pattern.Length; step++)
            {
                var note = chord[pattern[step]] + 12;
                var pan = step % 2 == 0 ? -0.38 : 0.38;
                AddPluck(left, right, start + step * 0.5 * BeatSeconds, 1.45,
                    MidiToFrequency(note), 0.078, pan);
            }

            if (bar % 4 == 0)
            {
                AddBell(left, right, start + 0.12, 3.2, MidiToFrequency(chord[2] + 24), 0.035, 0.2);
            }
        }

        foreach (var note in Melody)
        {
            AddFlute(left, right, note.Beat * BeatSeconds, note.Beats * BeatSeconds,
                MidiToFrequency(note.Midi), 0.048, 0.08);
        }

        WriteWave(path, left, right);
    }

    private static void AddPad(float[] left, float[] right, double startSeconds, double durationSeconds,
        double frequency, double amplitude, double pan)
    {
        AddVoice(left, right, startSeconds, durationSeconds, pan, (time, duration) =>
        {
            var envelope = SmoothEnvelope(time, duration, 1.25, 1.45);
            var phase = 2 * Math.PI * frequency * time;
            var shimmer = 0.18 * Math.Sin(phase * 2.0 + 0.5) + 0.08 * Math.Sin(phase * 3.0 + 1.1);
            return amplitude * envelope * (Math.Sin(phase) + shimmer);
        });
    }

    private static void AddPluck(float[] left, float[] right, double startSeconds, double durationSeconds,
        double frequency, double amplitude, double pan)
    {
        AddVoice(left, right, startSeconds, durationSeconds, pan, (time, _) =>
        {
            var attack = Math.Min(1.0, time / 0.012);
            var decay = Math.Exp(-3.15 * time);
            var phase = 2 * Math.PI * frequency * time;
            var tone = Math.Sin(phase) + 0.34 * Math.Sin(phase * 2.0) + 0.12 * Math.Sin(phase * 3.0);
            return amplitude * attack * decay * tone;
        });
    }

    private static void AddBell(float[] left, float[] right, double startSeconds, double durationSeconds,
        double frequency, double amplitude, double pan)
    {
        AddVoice(left, right, startSeconds, durationSeconds, pan, (time, _) =>
        {
            var envelope = Math.Min(1.0, time / 0.02) * Math.Exp(-1.5 * time);
            var phase = 2 * Math.PI * frequency * time;
            var tone = Math.Sin(phase) + 0.28 * Math.Sin(phase * 2.01) + 0.15 * Math.Sin(phase * 3.97);
            return amplitude * envelope * tone;
        });
    }

    private static void AddFlute(float[] left, float[] right, double startSeconds, double durationSeconds,
        double frequency, double amplitude, double pan)
    {
        AddVoice(left, right, startSeconds, durationSeconds, pan, (time, duration) =>
        {
            var envelope = SmoothEnvelope(time, duration, 0.38, 0.62);
            var vibrato = 1.0 + 0.0032 * Math.Sin(2 * Math.PI * 4.7 * time);
            var phase = 2 * Math.PI * frequency * vibrato * time;
            var breath = 0.06 * Math.Sin(2 * Math.PI * (frequency * 0.5) * time + 0.8);
            return amplitude * envelope * (Math.Sin(phase) + 0.16 * Math.Sin(phase * 2.0) + breath);
        });
    }

    private static void AddVoice(float[] left, float[] right, double startSeconds, double durationSeconds,
        double pan, Func<double, double, double> sample)
    {
        var first = Math.Max(0, (int)Math.Round(startSeconds * SampleRate));
        var last = Math.Min(left.Length, (int)Math.Round((startSeconds + durationSeconds) * SampleRate));
        var leftGain = Math.Sqrt((1.0 - pan) * 0.5);
        var rightGain = Math.Sqrt((1.0 + pan) * 0.5);
        for (var index = first; index < last; index++)
        {
            var time = (index - first) / (double)SampleRate;
            var value = sample(time, durationSeconds);
            left[index] += (float)(value * leftGain);
            right[index] += (float)(value * rightGain);
        }
    }

    private static double SmoothEnvelope(double time, double duration, double attack, double release)
    {
        var attackGain = Math.Min(1.0, Math.Max(0.0, time / attack));
        var releaseGain = Math.Min(1.0, Math.Max(0.0, (duration - time) / release));
        return SmoothStep(attackGain) * SmoothStep(releaseGain);
    }

    private static double SmoothStep(double value)
    {
        return value * value * (3.0 - 2.0 * value);
    }

    private static double MidiToFrequency(int midi)
    {
        return 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
    }

    private static void WriteWave(string path, float[] left, float[] right)
    {
        var peak = 0.0;
        for (var index = 0; index < left.Length; index++)
        {
            peak = Math.Max(peak, Math.Abs(left[index]));
            peak = Math.Max(peak, Math.Abs(right[index]));
        }
        var gain = peak > 0 ? 0.78 / peak : 1.0;
        var fadeSamples = (int)(1.2 * SampleRate);
        var dataBytes = checked(left.Length * Channels * (BitsPerSample / 8));

        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, false))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)Channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * Channels * (BitsPerSample / 8));
            writer.Write((short)(Channels * (BitsPerSample / 8)));
            writer.Write((short)BitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);

            for (var index = 0; index < left.Length; index++)
            {
                var edgeGain = 1.0;
                if (index < fadeSamples) edgeGain = SmoothStep(index / (double)fadeSamples);
                if (index >= left.Length - fadeSamples)
                    edgeGain = Math.Min(edgeGain, SmoothStep((left.Length - 1 - index) / (double)fadeSamples));
                writer.Write(ToPcm16(left[index] * gain * edgeGain));
                writer.Write(ToPcm16(right[index] * gain * edgeGain));
            }
        }
    }

    private static short ToPcm16(double value)
    {
        var limited = Math.Tanh(value * 1.08);
        return (short)Math.Round(Math.Max(-1.0, Math.Min(1.0, limited)) * short.MaxValue);
    }
}
'@

Add-Type -TypeDefinition $generatorSource -Language CSharp
[SolarenLauncherMusicGenerator]::Generate($resolvedOutputPath)

$outputFile = Get-Item -LiteralPath $resolvedOutputPath
$sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedOutputPath).Hash.ToLowerInvariant()
Write-Output "OPENRPG_FORGE_SOLAREN_LAUNCHER_MUSIC_READY path=$resolvedOutputPath bytes=$($outputFile.Length) duration_seconds=64 sample_rate=44100 channels=2 sha256=$sha256"
