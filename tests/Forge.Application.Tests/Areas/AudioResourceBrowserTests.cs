using Forge.Application.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Areas;

public sealed class AudioResourceBrowserTests
{
    [Fact]
    public void Searches_transcript_and_tags_then_builds_waveform_loudness_batch_preview()
    {
        var definitionId = Guid.Parse("a0613000-0000-4000-8000-000000000001");
        var catalog = new AudioEmitterCatalog(1,
        [
            new(definitionId, 1, "Pluie de forêt",
                new AudioClipSet(Guid.Parse("a0613000-0000-4000-8000-000000000002"), 1,
                    "Pluie", AudioClipSelectionMode.Weighted,
                    [new(Guid.Parse("a0613000-0000-4000-8000-000000000003"), "audio/rain.ogg", 3,
                        ["rain", "forest"], "soft", "fr", -24, "audio/rain.preview.ogg",
                        "audio/ogg", 2048, "CC0-1.0", "vault:r061", new string('a', 64),
                        "gouttes sur les feuilles", [.1f, .6f, .9f, .2f], 42)], "test:r061"),
                new(AudioLoopMode.Loop, 0, 0, 0, .1f, ""),
                new(1, 30, "linear", 360, 0, 0, 0, true), null, 0, "", "", false,
                "test:r061", "assets/audio.json", "CC0-1.0", new string('b', 64)),
        ],
        [new(Guid.NewGuid(), definitionId, TransformValue.Identity, true, "test:r061")]);

        var results = AudioResourceBrowser.Search(catalog,
            new AudioResourceSearch("feuilles", new HashSet<string> { "forest" }, "fr", "CC0-1.0"));
        var preview = AudioResourceBrowser.PlanAudition(results,
            new(-18, "Ambience", -6, 1, 4, AudioMeterMode.Loudness), batch: true);

        var resource = Assert.Single(results);
        Assert.Equal([.1f, .6f, .9f, .2f], resource.WaveformPeaks);
        Assert.True(resource.DistributionReady);
        Assert.True(preview.Batch);
        Assert.Equal("Ambience", preview.Bus);
        Assert.Empty(preview.Diagnostics);
        Assert.InRange(preview.NormalizedGain, 1.99f, 2.01f);
    }
}
