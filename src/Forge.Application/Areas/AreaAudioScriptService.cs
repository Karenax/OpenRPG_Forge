using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Spatial;

namespace Forge.Application.Areas;

public sealed record AreaAudioScriptSnapshot(
    string AreaId,
    Guid ProfileId,
    int SchemaVersion,
    string Provenance,
    AudioOrderMode SelectionMode,
    int NoRepeatWindow,
    AudioModulationDistribution Distribution,
    bool TempoSynchronized,
    string MixBus,
    float TargetLoudnessLufs,
    AudioListenerPolicy ListenerPolicy,
    bool RequiredAtRuntime);

public sealed record AreaAudioCaptionScriptSnapshot(Guid Id, string Resource, string Text, string Locale,
    string CueKind, string Importance, bool ShowDirection);

public static class AreaAudioScriptService
{
    public static AreaAudioScriptSnapshot Read(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var audio = area.Environment.Audio;
        return new(area.Id.ToString(), audio.Authoring.Id, audio.Authoring.SchemaVersion,
            audio.Authoring.Provenance, audio.OrderMode, audio.Authoring.Selection.NoRepeatWindow,
            audio.Authoring.Modulation.Distribution, audio.Authoring.Modulation.SyncToTempo,
            audio.Authoring.Mix.Bus, audio.Authoring.Mix.TargetLoudnessLufs,
            audio.Authoring.Ambient.ListenerPolicy, audio.Authoring.RequiredAtRuntime);
    }

    public static AreaAudioCaptionScriptSnapshot? ReadCaption(AreaDefinition area, string resource,
        string locale, string fallbackLocale = "fr")
    {
        ArgumentNullException.ThrowIfNull(area);
        var resolution = AudioCaptionRuntimeService.Resolve(area.Environment.Audio, resource, locale,
            fallbackLocale, Vector3Value.Zero, new(0, 0, -1), Vector3Value.Zero);
        var caption = resolution.Presentation;
        return caption is null ? null : new(caption.Id, caption.Resource, caption.Text, caption.Locale,
            caption.CueKind, caption.Importance, caption.ShowDirection);
    }
}
