using Forge.Domain.Areas;
namespace Forge.Application.Areas;

public sealed record AudioListenerContext(float Distance, float Hour, string Weather, IReadOnlySet<string> LocationTags,
    float Danger, bool Indoors);
public sealed record AreaAudioMix(float Attenuation, IReadOnlyList<string> ActiveLayers, float FadeSeconds,
    AudioAuthority Authority, AudioInstancePolicy InstancePolicy);
public static class AreaAudioMixer
{
    public static AreaAudioMix Evaluate(AreaAudioProfile profile, AudioListenerContext context)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(context);
        var spatial = profile.Spatial; var attenuation = spatial.Mode == AreaSoundMode.Global2D ? 1f : Attenuate(spatial, context.Distance);
        var layers = profile.AdaptiveLayers.Where(layer => InHourRange(context.Hour, layer.StartHour, layer.EndHour) &&
            (string.IsNullOrWhiteSpace(layer.Weather) || layer.Weather.Equals(context.Weather, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(layer.LocationTag) || context.LocationTags.Contains(layer.LocationTag)) &&
            context.Danger >= layer.MinimumDanger && (!layer.IndoorOnly || context.Indoors)).Select(layer => layer.Resource).ToArray();
        return new(attenuation * (1 - spatial.AirAbsorption), layers, profile.RuntimeControl.DefaultFadeSeconds,
            profile.RuntimeControl.Authority, profile.RuntimeControl.InstancePolicy);
    }
    private static float Attenuate(AudioSpatialProfile p, float distance)
    {
        if (distance <= p.InnerRadius) return 1; if (distance >= p.OuterRadius || p.OuterRadius == p.InnerRadius) return 0;
        var t = (distance - p.InnerRadius) / (p.OuterRadius - p.InnerRadius);
        return p.AttenuationCurve.Equals("inverse", StringComparison.OrdinalIgnoreCase) ? 1 / (1 + 4 * t) : 1 - t;
    }
    private static bool InHourRange(float value, float start, float end) => start <= end ? value >= start && value < end : value >= start || value < end;
}
