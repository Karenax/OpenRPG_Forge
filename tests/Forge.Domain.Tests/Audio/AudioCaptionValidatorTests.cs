using Forge.Domain.Areas;
using Forge.Domain.Audio;

namespace Forge.Domain.Tests.Audio;

public sealed class AudioCaptionValidatorTests
{
    [Fact]
    public void Accepts_governed_localized_caption_and_rejects_invalid_distribution_metadata()
    {
        var valid = Profile(Caption("builtin://audio/dragon", "CC0-1.0", new string('a', 64)));
        var invalid = Profile(Caption("builtin://audio/dragon", "UNLICENSED", "invalid") with
        {
            Localizations = [new("fr", ""), new("FR", "doublon")],
            Importance = "unknown",
        });

        Assert.Empty(AudioCaptionValidator.Validate(valid));
        var diagnostics = AudioCaptionValidator.Validate(invalid);
        Assert.Contains(diagnostics, item => item.Code == AudioCaptionValidator.IdentityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AudioCaptionValidator.LocalizationInvalidCode);
        Assert.All(diagnostics, item => Assert.StartsWith("audio/captions/0", item.Path,
            StringComparison.Ordinal));
    }

    private static SoundCaption Caption(string resource, string license, string hash) => new(resource,
        [new("fr", "[Rugissement à gauche]"), new("en", "[Roar to the left]")], true, "critical",
        Guid.Parse("a0631000-0000-4000-8000-000000000001"), 1, "test:r063",
        "assets/audio-captions.json", license, hash, "effect", 1.5f, 6, true);

    private static AreaAudioProfile Profile(SoundCaption caption) => new("", "", "", "", "neutral", 1,
        [], orchestration: new([], [], [caption]));
}
