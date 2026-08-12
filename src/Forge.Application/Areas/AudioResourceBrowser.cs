using Forge.Domain.Audio;

namespace Forge.Application.Areas;

public sealed record AudioResourceSearch(
    string Text,
    IReadOnlySet<string>? Tags = null,
    string? Locale = null,
    string? LicenseSpdx = null,
    int Limit = 100);

public sealed record AudioResourcePreview(
    Guid EmitterId,
    Guid ClipId,
    string Name,
    string ResourcePath,
    string PreviewResourcePath,
    IReadOnlyList<string> Tags,
    string Locale,
    string Transcript,
    string LicenseSpdx,
    string Provenance,
    string ContentSha256,
    float LoudnessLufs,
    float DurationSeconds,
    IReadOnlyList<float> WaveformPeaks,
    bool DistributionReady);

public sealed record AudioAuditionPlan(
    IReadOnlyList<AudioResourcePreview> Resources,
    string Bus,
    float NormalizedGain,
    bool Batch,
    IReadOnlyList<string> Diagnostics);

public static class AudioResourceBrowser
{
    public static IReadOnlyList<AudioResourcePreview> Search(
        AudioEmitterCatalog catalog,
        AudioResourceSearch query)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(query.Limit));
        var text = query.Text?.Trim() ?? string.Empty;
        return catalog.Definitions.SelectMany(definition => definition.ClipSet.Clips.Select(clip => new
        {
            Definition = definition,
            Clip = clip,
        }))
            .Where(item => string.IsNullOrWhiteSpace(text) ||
                item.Definition.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                item.Clip.ResourcePath.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                item.Clip.Transcript.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                item.Clip.Tags.Any(tag => tag.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .Where(item => query.Tags is null || query.Tags.Count == 0 ||
                query.Tags.All(tag => item.Clip.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .Where(item => string.IsNullOrWhiteSpace(query.Locale) ||
                item.Clip.Locale.Equals(query.Locale, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(query.LicenseSpdx) ||
                item.Clip.LicenseSpdx.Equals(query.LicenseSpdx, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Definition.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Clip.ResourcePath, StringComparer.Ordinal)
            .Take(query.Limit)
            .Select(item => new AudioResourcePreview(item.Definition.Id, item.Clip.Id,
                item.Definition.Name, item.Clip.ResourcePath, item.Clip.PreviewResourcePath,
                item.Clip.Tags, item.Clip.Locale, item.Clip.Transcript, item.Clip.LicenseSpdx,
                item.Clip.Provenance, item.Clip.ContentSha256, item.Clip.LoudnessLufs,
                item.Clip.DurationSeconds, MaterializeWaveform(item.Clip),
                AudioEmitterCatalogValidator.Validate(new AudioEmitterCatalog(
                    AudioEmitterCatalog.CurrentSchemaVersion, [item.Definition], [])).Count == 0))
            .ToArray();
    }

    public static AudioAuditionPlan PlanAudition(
        IReadOnlyList<AudioResourcePreview> resources,
        AudioLoudnessMixProfile mix,
        bool batch)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(mix);
        var selected = batch ? resources.Take(32).ToArray() : resources.Take(1).ToArray();
        if (selected.Length == 0)
            return new([], mix.Bus, 0, batch, ["audio.preview.empty"]);
        var gain = selected.Average(item => MathF.Pow(10, (mix.TargetLoudnessLufs - item.LoudnessLufs) / 20f));
        var diagnostics = selected.Where(item => !item.DistributionReady)
            .Select(item => $"audio.preview.governance_invalid:{item.ClipId:D}").ToArray();
        return new(selected, mix.Bus, Math.Clamp(gain, 0, mix.AccessibilityVolumeCap), batch, diagnostics);
    }

    private static IReadOnlyList<float> MaterializeWaveform(AudioClipDefinition clip)
    {
        if (clip.WaveformPeaks is { Count: > 0 }) return clip.WaveformPeaks.ToArray();
        var bytes = Convert.FromHexString(clip.ContentSha256);
        return Enumerable.Range(0, 32).Select(index => bytes[index % bytes.Length] / 255f).ToArray();
    }
}
