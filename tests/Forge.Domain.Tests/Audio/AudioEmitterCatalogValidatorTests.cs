using Forge.Domain.Audio;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Audio;

public sealed class AudioEmitterCatalogValidatorTests
{
    [Fact]
    public void Validates_canonical_emitter_clip_loop_spatial_identity_and_budgets()
    {
        var busId = Guid.Parse("a0040000-0000-4000-8000-000000000001");
        var catalog = CreateValid(busId);

        Assert.Empty(AudioEmitterCatalogValidator.Validate(catalog, new HashSet<Guid> { busId }));
        Assert.Equal(AudioLoopMode.SeamlessRegion, catalog.Definitions.Single().Loop.Mode);
        Assert.Equal(12, catalog.Instances.Single().Transform.Position.X);
    }

    [Fact]
    public void Rejects_unlicensed_missing_duplicate_and_over_budget_audio_with_stable_paths()
    {
        var valid = CreateValid(Guid.Parse("a0040000-0000-4000-8000-000000000001"));
        var definition = valid.Definitions.Single();
        var invalidClip = definition.ClipSet.Clips.Single() with
        {
            LicenseSpdx = "UNLICENSED",
            ContentSha256 = "bad",
            MemoryBytes = 512,
        };
        var invalidDefinition = definition with
        {
            Id = Guid.Empty,
            MixBusId = Guid.NewGuid(),
            ClipSet = definition.ClipSet with { Clips = [invalidClip, invalidClip] },
            Loop = definition.Loop with { RegionEndSeconds = .1f, CrossfadeSeconds = 1 },
            Spatial = definition.Spatial with { OuterRadius = 0 },
        };
        var invalid = new AudioEmitterCatalog(99, [invalidDefinition, invalidDefinition],
        [
            valid.Instances.Single() with { DefinitionId = Guid.NewGuid() },
            valid.Instances.Single() with { DefinitionId = Guid.NewGuid() },
        ]);

        var diagnostics = AudioEmitterCatalogValidator.Validate(invalid, new HashSet<Guid>(), maximumMemoryBytes: 1,
            path: "areas/demo/audio");

        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.SchemaUnsupportedCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.DefinitionInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.DefinitionDuplicateCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.ClipInvalidCode &&
            item.Path.StartsWith("areas/demo/audio/definitions/", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.ClipDuplicateCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.LoopInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.SpatialInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.InstanceDuplicateCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.DefinitionMissingCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.MixBusMissingCode);
        Assert.Contains(diagnostics, item => item.Code == AudioEmitterCatalogValidator.MemoryBudgetCode);
    }

    internal static AudioEmitterCatalog CreateValid(Guid? busId = null)
    {
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000002");
        return new AudioEmitterCatalog(AudioEmitterCatalog.CurrentSchemaVersion,
        [
            new AudioEmitterDefinition(
                definitionId, AudioEmitterDefinition.CurrentSchemaVersion, "Cascade ancienne",
                new AudioClipSet(Guid.Parse("a0040000-0000-4000-8000-000000000003"),
                    AudioClipSet.CurrentSchemaVersion, "Cascade", AudioClipSelectionMode.Weighted,
                    [new(Guid.Parse("a0040000-0000-4000-8000-000000000004"), "audio/waterfall.ogg", 1,
                        ["water", "ambient"], "close", "fr", -18, "audio/waterfall.preview.ogg",
                        "audio/ogg", 1024, "CC0-1.0", "asset-vault:waterfall", new string('a', 64))],
                    "author:audio-test"),
                new(AudioLoopMode.SeamlessRegion, 0, 1, 8, .25f, string.Empty),
                new(2, 40, "inverse-square", 180, -12, .35f, 1.5f, true),
                busId, .5f, "scripts/waterfall-on.forge", "scripts/waterfall-off.forge", true,
                "author:audio-test", "assets/asset-manifests.json", "CC0-1.0", new string('b', 64)),
        ],
        [
            new AudioEmitterInstance(Guid.Parse("a0040000-0000-4000-8000-000000000005"), definitionId,
                new TransformValue(new Vector3Value(12, 3, -4), QuaternionValue.Identity, Vector3Value.One),
                true, "author:audio-test"),
        ]);
    }
}
