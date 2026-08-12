using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public enum AudioPlaybackStatus
{
    Stopped,
    Playing,
    Paused,
    Fading,
}

public sealed record AudioPlaybackControlHandle(Guid Id, int Generation);

public sealed record AudioPlaybackControlState(
    AudioPlaybackControlHandle Handle,
    AudioPlaybackStatus Status,
    float Gain,
    int ActiveInstances,
    IReadOnlyList<string> Events,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record AudioAttenuationSample(float Distance, float Gain);
public sealed record AudioDebugSphere(float Radius, string Kind);
public sealed record AudioAttenuationPreview(
    IReadOnlyList<AudioAttenuationSample> Samples,
    IReadOnlyList<AudioDebugSphere> DebugSpheres);

public sealed record AdaptiveAmbienceMixEntry(string Resource, float FromGain, float ToGain,
    float CrossfadeSeconds, string VolumeId);
public sealed record AdaptiveAmbienceCrossfade(IReadOnlyList<AdaptiveAmbienceMixEntry> Entries);

public sealed record AudioBusMeterSample(string Bus, float PeakDb, float RmsDb, float LoudnessLufs,
    float EffectiveGain, string Snapshot, string Platform, bool AccessibilityEnhanced);

public static class AudioOrchestrationRuntimeService
{
    public const string AuthorityDeniedCode = "audio.runtime.authority_denied";
    public const string HandleInvalidCode = "audio.runtime.handle_invalid";
    public const string InstanceRejectedCode = "audio.runtime.instance_rejected";

    public static AudioPlaybackControlState Create(Guid id) => new(
        new(id == Guid.Empty ? Guid.NewGuid() : id, 1), AudioPlaybackStatus.Stopped, 1, 0, [], []);

    public static AudioPlaybackControlState Start(AudioPlaybackControlState state,
        AudioRuntimeControl control, AudioAuthority callerAuthority)
    {
        if (!Authorized(control, callerAuthority)) return Denied(state);
        if (state.Handle.Id == Guid.Empty || state.Handle.Generation <= 0)
            return Diagnose(state, HandleInvalidCode, "Audio playback handle is invalid.");
        if (control.InstancePolicy == AudioInstancePolicy.Single && state.ActiveInstances > 0)
            return Diagnose(state, InstanceRejectedCode, "Single-instance audio is already playing.");
        var instances = control.InstancePolicy == AudioInstancePolicy.Restart
            ? 1
            : checked(state.ActiveInstances + 1);
        return state with
        {
            Status = AudioPlaybackStatus.Playing,
            ActiveInstances = instances,
            Events = [.. state.Events, control.StartedEvent],
            Diagnostics = [],
        };
    }

    public static AudioPlaybackControlState Stop(AudioPlaybackControlState state,
        AudioRuntimeControl control, AudioAuthority callerAuthority)
    {
        if (!Authorized(control, callerAuthority)) return Denied(state);
        return state with
        {
            Status = AudioPlaybackStatus.Stopped,
            ActiveInstances = 0,
            Events = [.. state.Events, control.StoppedEvent],
            Diagnostics = [],
        };
    }

    public static AudioPlaybackControlState Pause(AudioPlaybackControlState state,
        AudioRuntimeControl control, AudioAuthority callerAuthority)
    {
        if (!Authorized(control, callerAuthority)) return Denied(state);
        if (state.Status is not (AudioPlaybackStatus.Playing or AudioPlaybackStatus.Fading)) return state;
        return state with
        {
            Status = AudioPlaybackStatus.Paused,
            Events = [.. state.Events, control.PausedEvent],
            Diagnostics = [],
        };
    }

    public static AudioPlaybackControlState Resume(AudioPlaybackControlState state,
        AudioRuntimeControl control, AudioAuthority callerAuthority)
    {
        if (!Authorized(control, callerAuthority)) return Denied(state);
        if (state.Status != AudioPlaybackStatus.Paused) return state;
        return state with
        {
            Status = AudioPlaybackStatus.Playing,
            Events = [.. state.Events, $"{control.StartedEvent}:resume"],
            Diagnostics = [],
        };
    }

    public static AudioPlaybackControlState Fade(AudioPlaybackControlState state,
        AudioRuntimeControl control, AudioAuthority callerAuthority, float targetGain, float seconds)
    {
        if (!Authorized(control, callerAuthority)) return Denied(state);
        if (!float.IsFinite(targetGain) || targetGain is < 0 or > 4 ||
            !float.IsFinite(seconds) || seconds is < 0 or > 120)
            return Diagnose(state, HandleInvalidCode, "Audio fade target or duration is outside supported ranges.");
        return state with
        {
            Status = targetGain == 0 ? AudioPlaybackStatus.Stopped : AudioPlaybackStatus.Fading,
            Gain = targetGain,
            ActiveInstances = targetGain == 0 ? 0 : state.ActiveInstances,
            Events = [.. state.Events, FormattableString.Invariant(
                $"{control.FadedEvent}:{targetGain:0.###}:{seconds:0.###}")],
            Diagnostics = [],
        };
    }

    public static AudioAttenuationPreview PreviewAttenuation(AudioSpatialProfile profile, int sampleCount = 17)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (sampleCount is < 2 or > 4096) throw new ArgumentOutOfRangeException(nameof(sampleCount));
        var extent = Math.Max(profile.OuterRadius, profile.InnerRadius + .001f);
        var samples = Enumerable.Range(0, sampleCount).Select(index =>
        {
            var distance = extent * index / (sampleCount - 1f);
            return new AudioAttenuationSample(distance, Attenuate(profile, distance));
        }).ToArray();
        IReadOnlyList<AudioDebugSphere> spheres = profile.DebugSpheresVisible
            ? [new(profile.InnerRadius, "inner"), new(profile.OuterRadius, "outer")]
            : [];
        return new(samples, spheres);
    }

    public static AdaptiveAmbienceCrossfade PlanAdaptiveCrossfade(AreaAudioProfile profile,
        AudioListenerContext previous, AudioListenerContext next)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var before = ActiveLayers(profile, previous).ToDictionary(item => item.Resource, StringComparer.Ordinal);
        var after = ActiveLayers(profile, next).ToDictionary(item => item.Resource, StringComparer.Ordinal);
        var resources = before.Keys.Union(after.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return new(resources.Select(resource =>
        {
            before.TryGetValue(resource, out var oldLayer);
            after.TryGetValue(resource, out var newLayer);
            var layer = newLayer ?? oldLayer!;
            return new AdaptiveAmbienceMixEntry(resource, oldLayer?.Volume ?? 0, newLayer?.Volume ?? 0,
                layer.CrossfadeSeconds, layer.VolumeId);
        }).ToArray());
    }

    public static IReadOnlyList<AudioBusMeterSample> MonitorMix(AreaAudioOrchestration orchestration,
        IReadOnlyDictionary<string, (float PeakDb, float RmsDb, float LoudnessLufs)> readings,
        string platform, string snapshot)
    {
        ArgumentNullException.ThrowIfNull(orchestration);
        ArgumentNullException.ThrowIfNull(readings);
        var buses = orchestration.MixBuses.ToDictionary(item => item.Id);
        float EffectiveGain(AudioMixBus bus, HashSet<Guid> visited)
        {
            if (!visited.Add(bus.Id)) return 0;
            var gain = bus.Volume;
            if (bus.ParentId is { } parentId && buses.TryGetValue(parentId, out var parent))
                gain *= EffectiveGain(parent, visited);
            return gain;
        }
        return orchestration.MixBuses.Where(bus => bus.MonitorEnabled &&
                (bus.Platform.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                 bus.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)) &&
                (bus.Snapshot.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                 bus.Snapshot.Equals(snapshot, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(bus => bus.Name, StringComparer.Ordinal)
            .Select(bus =>
            {
                var reading = readings.GetValueOrDefault(bus.Name, (-80, -80, -80));
                return new AudioBusMeterSample(bus.Name, reading.PeakDb, reading.RmsDb,
                    reading.LoudnessLufs, EffectiveGain(bus, []), bus.Snapshot, bus.Platform,
                    bus.EnhanceImportantSounds);
            }).ToArray();
    }

    private static IReadOnlyList<AdaptiveAmbienceLayer> ActiveLayers(AreaAudioProfile profile,
        AudioListenerContext context) => profile.AdaptiveLayers.Where(layer =>
        InHourRange(context.Hour, layer.StartHour, layer.EndHour) &&
        (string.IsNullOrWhiteSpace(layer.Weather) ||
         layer.Weather.Equals(context.Weather, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(layer.LocationTag) || context.LocationTags.Contains(layer.LocationTag)) &&
        context.Danger >= layer.MinimumDanger && (!layer.IndoorOnly || context.Indoors) &&
        (string.IsNullOrWhiteSpace(layer.VolumeId) ||
         context.LocationTags.Contains(layer.VolumeId))).ToArray();

    private static float Attenuate(AudioSpatialProfile profile, float distance)
    {
        if (profile.Mode == AreaSoundMode.Global2D || distance <= profile.InnerRadius) return 1;
        if (distance >= profile.OuterRadius || profile.OuterRadius == profile.InnerRadius) return 0;
        var amount = (distance - profile.InnerRadius) / (profile.OuterRadius - profile.InnerRadius);
        if (profile.AttenuationCurve.Equals("inverse-square", StringComparison.OrdinalIgnoreCase))
            return 1 / (1 + 8 * amount * amount);
        if (profile.AttenuationCurve.Equals("inverse", StringComparison.OrdinalIgnoreCase))
            return 1 / (1 + 4 * amount);
        return 1 - amount;
    }

    private static bool InHourRange(float value, float start, float end) =>
        start <= end ? value >= start && value < end : value >= start || value < end;

    private static bool Authorized(AudioRuntimeControl control, AudioAuthority callerAuthority) =>
        control.Authority == AudioAuthority.Local || callerAuthority == AudioAuthority.Server;

    private static AudioPlaybackControlState Denied(AudioPlaybackControlState state) =>
        Diagnose(state, AuthorityDeniedCode, "Server-authoritative audio control rejected a local caller.");

    private static AudioPlaybackControlState Diagnose(AudioPlaybackControlState state, string code, string message) =>
        state with { Diagnostics = [new(code, message, $"audio/runtime/{state.Handle.Id:D}")] };
}
