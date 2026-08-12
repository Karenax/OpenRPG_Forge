using Forge.Domain.Areas;
using Forge.Domain.Audio;

namespace Forge.Application.Areas;

public sealed record AudioListenerContext(
    float Distance,
    float Hour,
    string Weather,
    IReadOnlySet<string> LocationTags,
    float Danger,
    bool Indoors,
    string AreaId = "",
    string ListenerId = "",
    bool IsLocalPlayer = true,
    string PartyId = "",
    string LocalPartyId = "",
    float SourceLoudnessLufs = -18);

public sealed record AreaAudioMix(
    float Attenuation,
    IReadOnlyList<string> ActiveLayers,
    float FadeSeconds,
    AudioAuthority Authority,
    AudioInstancePolicy InstancePolicy,
    float NormalizedGain = 1,
    string Bus = "Ambience",
    float DuckingDb = 0,
    bool AccessibilityCapped = false,
    float MeterLufs = -18,
    bool Personalized = false,
    bool ListenerAllowed = true);

public static class AreaAudioMixer
{
    public static AreaAudioMix Evaluate(AreaAudioProfile profile, AudioListenerContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        var spatial = profile.Spatial;
        var mix = profile.Authoring.Mix;
        var ambient = profile.Authoring.Ambient;
        var listenerAllowed = ListenerAllowed(ambient, context);
        var inScope = string.IsNullOrWhiteSpace(ambient.AreaScope) ||
            ambient.AreaScope.Equals(context.AreaId, StringComparison.OrdinalIgnoreCase);
        var attenuation = !listenerAllowed || !inScope
            ? 0
            : spatial.Mode == AreaSoundMode.Global2D
                ? 1f
                : Attenuate(spatial, context.Distance * mix.DistanceScale);
        var layers = profile.AdaptiveLayers.Where(layer => InHourRange(context.Hour, layer.StartHour, layer.EndHour) &&
            (string.IsNullOrWhiteSpace(layer.Weather) || layer.Weather.Equals(context.Weather, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(layer.LocationTag) || context.LocationTags.Contains(layer.LocationTag)) &&
            context.Danger >= layer.MinimumDanger && (!layer.IndoorOnly || context.Indoors) &&
            (string.IsNullOrWhiteSpace(layer.VolumeId) || context.LocationTags.Contains(layer.VolumeId)))
            .Select(layer => layer.Resource).ToArray();
        var personalized = ambient.ListenerGains.TryGetValue(context.ListenerId, out var listenerGain);
        if (!personalized) listenerGain = 1;
        var loudnessGain = MathF.Pow(10, (mix.TargetLoudnessLufs - context.SourceLoudnessLufs) / 20f);
        var uncapped = attenuation * (1 - spatial.AirAbsorption) * profile.RelativeVolume * loudnessGain * listenerGain;
        var normalized = Math.Clamp(uncapped, 0, mix.AccessibilityVolumeCap);
        var meter = context.SourceLoudnessLufs + (normalized <= 0 ? -80 : 20 * MathF.Log10(normalized));
        return new(attenuation * (1 - spatial.AirAbsorption), layers,
            profile.RuntimeControl.DefaultFadeSeconds, profile.RuntimeControl.Authority,
            profile.RuntimeControl.InstancePolicy, normalized, ambient.Bus, mix.DuckingDb,
            uncapped > mix.AccessibilityVolumeCap, meter, personalized, listenerAllowed && inScope);
    }

    private static bool ListenerAllowed(AreaAmbientScopeProfile ambient, AudioListenerContext context) =>
        ambient.ListenerPolicy switch
        {
            AudioListenerPolicy.LocalPlayer => context.IsLocalPlayer,
            AudioListenerPolicy.Party => !string.IsNullOrWhiteSpace(context.PartyId) &&
                                         context.PartyId.Equals(context.LocalPartyId, StringComparison.Ordinal),
            AudioListenerPolicy.Personalized => ambient.ListenerGains.ContainsKey(context.ListenerId),
            _ => true,
        };

    private static float Attenuate(AudioSpatialProfile profile, float distance)
    {
        if (distance <= profile.InnerRadius) return 1;
        if (distance >= profile.OuterRadius || profile.OuterRadius == profile.InnerRadius) return 0;
        var amount = (distance - profile.InnerRadius) / (profile.OuterRadius - profile.InnerRadius);
        if (profile.AttenuationCurve.Equals("inverse-square", StringComparison.OrdinalIgnoreCase))
            return 1 / (1 + 8 * amount * amount);
        return profile.AttenuationCurve.Equals("inverse", StringComparison.OrdinalIgnoreCase)
            ? 1 / (1 + 4 * amount)
            : 1 - amount;
    }

    private static bool InHourRange(float value, float start, float end) =>
        start <= end ? value >= start && value < end : value >= start || value < end;
}
