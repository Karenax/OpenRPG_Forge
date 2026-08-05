using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Application.Areas;

public sealed record AudioCuePlayback(Guid PlaybackId, Guid CueId, string CueKey, string Resource, string Bus,
    float Volume, bool Loop, DateTimeOffset StartedAt);
public sealed record AudioCueStarted(Guid EventId, Guid PlaybackId, string CueId, DateTimeOffset At);
public sealed record AudioCueCompleted(Guid EventId, Guid PlaybackId, string CueId, DateTimeOffset At);
public sealed record AudioCueInterrupted(Guid EventId, Guid PlaybackId, string CueId, string Reason,
    DateTimeOffset At);
public sealed record MusicStateChanged(Guid EventId, string Previous, string Current, DateTimeOffset At);
public sealed record AudioSnapshotPushed(Guid EventId, string Snapshot, DateTimeOffset At);

public sealed record AreaAudioRuntimeState(
    IReadOnlyDictionary<Guid, AudioCuePlayback> Playing,
    string MusicState,
    IReadOnlyList<string> Snapshots,
    IReadOnlyList<object> Events)
{
    public static AreaAudioRuntimeState Empty { get; } =
        new(new Dictionary<Guid, AudioCuePlayback>(), "exploration", [], []);
}

public sealed record ScatterEmission(Guid EmitterId, string CueId, Vector3Value Position, float Volume,
    float DelaySeconds, int Sequence);

public sealed record ResolvedVoiceTake(string Locale, string Resource, string Subtitle, string PhonemeResource,
    bool UsesTts, string TtsPluginId, VoiceConsentMetadata Consent);

public sealed record LoadingPresentationFrame(LoadingMediaKind MediaKind, string MediaResource, string Title,
    string Tip, string AccessibleText, double Progress, bool ShowProgress);

public static class AreaExperienceRuntimeService
{
    public static (AreaAudioRuntimeState State, AudioCuePlayback Playback) PlayCue(
        AreaExperienceProfile profile, AreaAudioRuntimeState state, string cueId, DateTimeOffset now,
        Func<Guid>? idSource = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(cueId);
        var cue = profile.AudioCues.SingleOrDefault(item => item.CueId == cueId) ??
                  throw new KeyNotFoundException($"Unknown audio cue '{cueId}'.");
        var playbackId = (idSource ?? Guid.NewGuid)();
        var playback = new AudioCuePlayback(playbackId, cue.Id, cue.CueId, cue.Resource, cue.Bus, cue.Volume,
            cue.Loop, now);
        var playing = state.Playing.ToDictionary();
        playing[playbackId] = playback;
        var started = new AudioCueStarted((idSource ?? Guid.NewGuid)(), playbackId, cue.CueId, now);
        return (state with { Playing = playing, Events = [.. state.Events, started] }, playback);
    }

    public static AreaAudioRuntimeState Complete(AreaAudioRuntimeState state, Guid playbackId, DateTimeOffset now,
        Func<Guid>? idSource = null)
    {
        if (!state.Playing.TryGetValue(playbackId, out var playback)) return state;
        var playing = state.Playing.ToDictionary();
        playing.Remove(playbackId);
        return state with
        {
            Playing = playing,
            Events = [.. state.Events,
                new AudioCueCompleted((idSource ?? Guid.NewGuid)(), playbackId, playback.CueKey, now)],
        };
    }

    public static AreaAudioRuntimeState Interrupt(AreaAudioRuntimeState state, Guid playbackId, string reason,
        DateTimeOffset now, Func<Guid>? idSource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!state.Playing.TryGetValue(playbackId, out var playback)) return state;
        var playing = state.Playing.ToDictionary();
        playing.Remove(playbackId);
        return state with
        {
            Playing = playing,
            Events = [.. state.Events,
                new AudioCueInterrupted((idSource ?? Guid.NewGuid)(), playbackId, playback.CueKey, reason, now)],
        };
    }

    public static AreaAudioRuntimeState SetMusicState(AreaAudioRuntimeState state, string musicState,
        DateTimeOffset now, Func<Guid>? idSource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(musicState);
        if (state.MusicState == musicState) return state;
        return state with
        {
            MusicState = musicState.Trim(),
            Events = [.. state.Events,
                new MusicStateChanged((idSource ?? Guid.NewGuid)(), state.MusicState, musicState.Trim(), now)],
        };
    }

    public static AreaAudioRuntimeState PushSnapshot(AreaAudioRuntimeState state, string snapshot,
        DateTimeOffset now, Func<Guid>? idSource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot);
        return state with
        {
            Snapshots = [.. state.Snapshots, snapshot.Trim()],
            Events = [.. state.Events,
                new AudioSnapshotPushed((idSource ?? Guid.NewGuid)(), snapshot.Trim(), now)],
        };
    }

    public static ScatterEmission? ResolveScatter(AreaExperienceProfile profile, Guid emitterId, int sequence,
        IReadOnlySet<string> activeBiomeTags, IReadOnlySet<string> activeExclusionTags, bool navigable)
    {
        var emitter = profile.ScatterEmitters.SingleOrDefault(item => item.Id == emitterId) ??
                      throw new KeyNotFoundException($"Unknown scatter emitter '{emitterId}'.");
        if (emitter.RequireNavigable && !navigable ||
            emitter.BiomeTags.Count > 0 && !emitter.BiomeTags.Any(activeBiomeTags.Contains) ||
            emitter.ExclusionTags.Any(activeExclusionTags.Contains)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{emitter.DeterministicSeed}:{sequence}"));
        static float Unit(byte[] value, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(value.AsSpan(offset, sizeof(uint))) / (float)uint.MaxValue;
        var x = emitter.Center.X + ((Unit(bytes, 0) - .5f) * emitter.Size.X);
        var y = emitter.SurfaceOnly ? emitter.Center.Y :
            emitter.Center.Y + ((Unit(bytes, 4) - .5f) * emitter.Size.Y);
        var z = emitter.Center.Z + ((Unit(bytes, 8) - .5f) * emitter.Size.Z);
        var delay = emitter.MinimumIntervalSeconds +
                    (Unit(bytes, 12) * (emitter.MaximumIntervalSeconds - emitter.MinimumIntervalSeconds));
        return new ScatterEmission(emitter.Id, emitter.CueId, new Vector3Value(x, y, z), emitter.Volume, delay,
            sequence);
    }

    public static ResolvedVoiceTake ResolveVoice(CreatureVoiceProfile profile, string locale,
        string fallbackLocale)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackLocale);
        var take = profile.Takes.FirstOrDefault(item => item.Locale.Equals(locale, StringComparison.OrdinalIgnoreCase))
                   ?? profile.Takes.FirstOrDefault(item => item.Locale.Equals(fallbackLocale,
                       StringComparison.OrdinalIgnoreCase))
                   ?? profile.Takes.FirstOrDefault(item => item.Locale.Equals(profile.DefaultLocale,
                       StringComparison.OrdinalIgnoreCase))
                   ?? profile.Takes[0];
        var usesTts = string.IsNullOrWhiteSpace(take.Resource);
        if (usesTts && (!profile.Consent.TtsUseAllowed || string.IsNullOrWhiteSpace(profile.TtsPluginId)))
            throw new InvalidOperationException("AUDIO_VOICE_TTS_CONSENT_REQUIRED");
        return new ResolvedVoiceTake(take.Locale, take.Resource, take.Subtitle, take.PhonemeResource, usesTts,
            usesTts ? profile.TtsPluginId : string.Empty, profile.Consent);
    }

    public static LoadingPresentationFrame ResolveLoading(AreaLoadingPresentation profile, string locale,
        int completed, int total, int tipIndex, bool reduceMotion, bool mediaAvailable)
        => ResolveLoading(profile, locale, completed, total, tipIndex, reduceMotion, mediaAvailable,
            mediaAvailable);

    public static LoadingPresentationFrame ResolveLoading(AreaLoadingPresentation profile, string locale,
        int completed, int total, int tipIndex, bool reduceMotion, bool imageAvailable, bool videoAvailable)
    {
        if (completed < 0 || total < 0 || completed > total)
            throw new ArgumentOutOfRangeException(nameof(completed));
        var tips = profile.LocalizedTips.GetValueOrDefault(locale)
                   ?? profile.LocalizedTips.GetValueOrDefault("en")
                   ?? profile.LocalizedTips.Values.First();
        var tip = tips[(int)(Math.Abs((long)tipIndex) % tips.Count)];
        var mediaKind = LoadingMediaKind.AccessibleFallback;
        var resource = string.Empty;
        if (videoAvailable && !reduceMotion && !string.IsNullOrWhiteSpace(profile.VideoResource))
        {
            mediaKind = LoadingMediaKind.Video;
            resource = profile.VideoResource;
        }
        else if (imageAvailable && !string.IsNullOrWhiteSpace(profile.ImageResource))
        {
            mediaKind = LoadingMediaKind.Image;
            resource = profile.ImageResource;
        }
        return new LoadingPresentationFrame(mediaKind, resource, profile.Title, tip, profile.AccessibleFallback,
            total == 0 ? 1 : completed / (double)total, profile.ShowProgress);
    }
}
