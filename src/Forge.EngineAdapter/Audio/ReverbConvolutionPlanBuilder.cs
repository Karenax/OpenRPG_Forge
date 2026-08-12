using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.Audio;

public enum ReverbProcessingMode
{
    Algorithmic,
    Convolution,
}

public sealed record AudioSourceReverbSend(Guid SourceId, float SendLevel)
{
    public AudioSourceReverbSend Normalize()
    {
        if (SourceId == Guid.Empty || SendLevel is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(SendLevel));
        return this;
    }
}

public sealed record ReverbConvolutionPlan(
    Guid? ZoneId,
    string Preset,
    ReverbProcessingMode Mode,
    string ImpulseResource,
    string LicenseSpdx,
    string ContentSha256,
    string Provenance,
    float Wet,
    float Dry,
    float BlendSeconds,
    IReadOnlyList<AudioSourceReverbSend> SourceSends,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public sealed record ReverbRuntimeApplyResult(bool Applied, string Code, int AppliedSourceSends);

public interface IReverbRuntimeProvider
{
    bool SupportsConvolution { get; }

    ReverbRuntimeApplyResult Apply(ReverbConvolutionPlan plan);
}

public static class ReverbConvolutionPlanBuilder
{
    public const string ConvolutionUnavailableCode = "audio.reverb.convolution_unavailable";

    public static ReverbConvolutionPlan Build(AreaReverbProfile profile, Vector3Value listenerPosition,
        IReadOnlyList<AudioSourceReverbSend> sourceSends, bool supportsConvolution)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sourceSends);
        var sends = sourceSends.Select(item => item.Normalize()).OrderBy(item => item.SourceId).ToArray();
        if (sends.Length > 256 || sends.Select(item => item.SourceId).Distinct().Count() != sends.Length)
            throw new ArgumentException("Reverb source sends must be unique and bounded.", nameof(sourceSends));

        var zone = profile.Zones.Where(item => Contains(item, listenerPosition))
            .OrderBy(item => item.Size.X * item.Size.Y * item.Size.Z).ThenBy(item => item.Id).FirstOrDefault();
        var wantsConvolution = !string.IsNullOrEmpty(zone?.ImpulseResource);
        var diagnostics = new List<DomainDiagnostic>();
        if (wantsConvolution && !supportsConvolution)
        {
            diagnostics.Add(new(ConvolutionUnavailableCode,
                "The runtime provider cannot process the selected impulse response; the algorithmic preset is used.",
                $"environment/reverb/zones/{zone!.Id}/impulseResource"));
        }

        return new(
            zone?.Id,
            zone?.Preset ?? profile.DefaultPreset,
            wantsConvolution && supportsConvolution ? ReverbProcessingMode.Convolution : ReverbProcessingMode.Algorithmic,
            wantsConvolution && supportsConvolution ? zone!.ImpulseResource : string.Empty,
            wantsConvolution && supportsConvolution ? zone!.LicenseSpdx : string.Empty,
            wantsConvolution && supportsConvolution ? zone!.ContentSha256 : string.Empty,
            zone?.Provenance ?? "builtin://audio/reverb",
            zone?.Wet ?? .28f,
            zone?.Dry ?? 1,
            zone?.BlendSeconds ?? 0,
            sends,
            diagnostics);
    }

    public static ReverbRuntimeApplyResult BuildAndApply(AreaReverbProfile profile, Vector3Value listenerPosition,
        IReadOnlyList<AudioSourceReverbSend> sourceSends, IReverbRuntimeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Apply(Build(profile, listenerPosition, sourceSends, provider.SupportsConvolution));
    }

    private static bool Contains(ReverbZoneProfile zone, Vector3Value point) =>
        MathF.Abs(point.X - zone.Center.X) <= zone.Size.X / 2 &&
        MathF.Abs(point.Y - zone.Center.Y) <= zone.Size.Y / 2 &&
        MathF.Abs(point.Z - zone.Center.Z) <= zone.Size.Z / 2;
}
