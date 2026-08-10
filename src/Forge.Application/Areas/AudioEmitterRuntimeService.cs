using Forge.Domain.Audio;
namespace Forge.Application.Areas;

public static class AudioEmitterRuntimeService
{
    public static IReadOnlyList<AudioEmitterPlayback> CreateInitialPlaybacks(
        AudioEmitterCatalog? catalog,
        int deterministicSeed = 0)
    {
        if (catalog is null) return [];
        var definitions = catalog.Definitions.ToDictionary(item => item.Id);
        var results = new List<AudioEmitterPlayback>();
        foreach (var instance in catalog.Instances.Where(item => item.Enabled).OrderBy(item => item.Id))
        {
            if (!definitions.TryGetValue(instance.DefinitionId, out var definition) || definition.ClipSet.Clips.Count == 0)
                continue;
            var clip = SelectClip(definition.ClipSet, deterministicSeed, instance.Id);
            results.Add(new AudioEmitterPlayback(
                new AudioEmitterHandle(instance.Id), definition.Id, clip.Id, clip.ResourcePath,
                instance.Transform, definition.Loop, definition.Spatial, definition.MixBusId,
                definition.StartDelaySeconds, definition.Loop.Mode != AudioLoopMode.EventDriven,
                definition.ActivatedScript, definition.CompletedScript));
        }
        return results;
    }

    public static AudioClipDefinition SelectClip(AudioClipSet clipSet, int deterministicSeed, Guid instanceId)
    {
        ArgumentNullException.ThrowIfNull(clipSet);
        if (clipSet.Clips.Count == 0) throw new ArgumentException("Audio clip set is empty.", nameof(clipSet));
        if (clipSet.Selection == AudioClipSelectionMode.Ordered) return clipSet.Clips[0];
        var bytes = instanceId.ToByteArray();
        var seed = deterministicSeed;
        foreach (var value in bytes) seed = unchecked(seed * 31 + value);
        var random = new Random(seed);
        var total = clipSet.Clips.Sum(item => Math.Max(0, item.Weight));
        if (total <= 0) return clipSet.Clips[0];
        var cursor = random.NextDouble() * total;
        foreach (var clip in clipSet.Clips)
        {
            cursor -= Math.Max(0, clip.Weight);
            if (cursor <= 0) return clip;
        }
        return clipSet.Clips[^1];
    }
}
