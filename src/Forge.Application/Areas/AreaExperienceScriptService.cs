using System.Collections.ObjectModel;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Areas;

public sealed record AreaExperienceScriptHandle(Guid Id, int SchemaVersion);

public sealed record AreaExperienceScriptSnapshot(
    AreaExperienceScriptHandle Handle,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// Exposes copied authoring values and typed runtime-only mutations to ForgeScript.
/// Persistent authoring mutations remain governed by SetAreaExperienceProfileHandler.
/// </summary>
public sealed class AreaExperienceScriptService
{
    private readonly AreaExperienceProfile _profile;

    public AreaExperienceScriptService(AreaExperienceProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public AreaExperienceScriptHandle Handle => new(_profile.Id, _profile.SchemaVersion);

    public AreaAudioRuntimeState State { get; private set; } = AreaAudioRuntimeState.Empty;

    public AreaExperienceScriptSnapshot Resolve(AreaExperienceScriptHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle != Handle)
            throw new KeyNotFoundException($"Unknown area experience handle '{handle.Id}'.");
        var values = new Dictionary<string, object?>
        {
            ["audioCueIds"] = _profile.AudioCues.Select(item => item.CueId).ToArray(),
            ["scatterEmitterCount"] = _profile.ScatterEmitters.Count,
            ["voiceProfileCount"] = _profile.Voices.Count,
            ["crossAreaPatrolCount"] = _profile.PatrolRoutes.Count,
            ["loadingTitle"] = _profile.Loading.Title,
            ["loadingShowsProgress"] = _profile.Loading.ShowProgress,
            ["loadingPrefetchesResources"] = _profile.Loading.PrefetchRequiredResources,
        };
        return new AreaExperienceScriptSnapshot(handle, new ReadOnlyDictionary<string, object?>(values));
    }

    public AudioCuePlayback PlayCue(string cueId, DateTimeOffset now, Func<Guid>? idSource = null)
    {
        var result = AreaExperienceRuntimeService.PlayCue(_profile, State, cueId, now, idSource);
        State = result.State;
        return result.Playback;
    }

    public void CompleteCue(Guid playbackId, DateTimeOffset now, Func<Guid>? idSource = null) =>
        State = AreaExperienceRuntimeService.Complete(State, playbackId, now, idSource);

    public void InterruptCue(Guid playbackId, string reason, DateTimeOffset now, Func<Guid>? idSource = null) =>
        State = AreaExperienceRuntimeService.Interrupt(State, playbackId, reason, now, idSource);

    public void SetMusicState(string musicState, DateTimeOffset now, Func<Guid>? idSource = null) =>
        State = AreaExperienceRuntimeService.SetMusicState(State, musicState, now, idSource);

    public void PushSnapshot(string snapshot, DateTimeOffset now, Func<Guid>? idSource = null) =>
        State = AreaExperienceRuntimeService.PushSnapshot(State, snapshot, now, idSource);
}
