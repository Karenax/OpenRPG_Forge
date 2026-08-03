using Forge.Domain.Areas;
namespace Forge.Application.Areas;

public sealed record PlannedAudioCue(string Resource, float DelaySeconds, float RelativeVolume);
public static class AreaAudioSequencePlanner
{
    public static IReadOnlyList<PlannedAudioCue> Plan(AreaAudioProfile profile, int count, int seed)
    {
        ArgumentNullException.ThrowIfNull(profile); if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (profile.Playlist.Count == 0 || count == 0) return [];
        var random = new Random(seed); var result = new List<PlannedAudioCue>(count);
        for (var index = 0; index < count; index++)
        {
            var selected = profile.OrderMode == AudioOrderMode.Sequential ? index % profile.Playlist.Count : random.Next(profile.Playlist.Count);
            var variation = profile.IntervalVariationSeconds == 0 ? 0 : ((float)random.NextDouble() * 2 - 1) * profile.IntervalVariationSeconds;
            result.Add(new(profile.Playlist[selected], MathF.Max(0, profile.IntervalSeconds + variation), profile.RelativeVolume));
        }
        return result;
    }
}
