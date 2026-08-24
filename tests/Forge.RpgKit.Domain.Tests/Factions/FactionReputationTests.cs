using Forge.Domain.Factions;
using Forge.RpgKit.Domain.Factions;

namespace Forge.RpgKit.Domain.Tests.Factions;

public sealed class FactionReputationTests
{
    [Fact]
    public void Applies_scoped_and_personal_reputation_with_expiration()
    {
        var (catalog, village, glass) = Sample(); var now = DateTimeOffset.Parse("2026-08-03T12:00:00Z");
        var personal = new PersonalReputationOverlay(Guid.NewGuid(), 1, "author:test", "player-1", village, glass, 20, now.AddHours(1), "saved merchant");
        var transaction = new ReputationTransaction(Guid.NewGuid(), 1, "author:test", ReputationScope.Campaign, "demo", village, glass, 15, "lighthouse choice", now);
        var mutation = FactionReputationService.Apply(new([personal], []), transaction);
        var context = new ReactionContext("player-1", village, glass, now, 0, 0, 0, null, null);
        Assert.Equal(-5, FactionReputationService.EffectiveScore(catalog, mutation.State, context, ReputationScope.Campaign, "demo"));
        Assert.Equal(-15, mutation.Inverse.Delta);
        Assert.Equal(-25, FactionReputationService.EffectiveScore(catalog, mutation.State, context with { At = now.AddHours(2) }, ReputationScope.Campaign, "demo"));
    }

    [Fact]
    public void Queries_semantic_tags_and_round_trips_heatmap_history()
    {
        var (catalog, village, glass) = Sample();
        Assert.Equal("village", Assert.Single(FactionReputationService.QueryByTags(catalog, ["civilian", "MERCHANT"])).Slug);
        var before = FactionReputationService.Capture(catalog, "before", DateTimeOffset.UnixEpoch);
        var changed = catalog with { Relationships = catalog.Relationships.Select(item => item.SourceId == village && item.TargetId == glass ? item with { Score = 30, Explanation = "truce", Preset = "custom" } : item).ToArray() };
        var after = FactionReputationService.Capture(changed, "after", DateTimeOffset.UnixEpoch.AddMinutes(1));
        Assert.Equal(70, Assert.Single(FactionReputationService.Compare(before, after)).After - (-40));
        var restored = FactionReputationService.ImportSnapshot(FactionReputationService.ExportSnapshot(after));
        Assert.Equal("after", restored.Label);
        Assert.Equal("hostile", Assert.Single(FactionReputationService.SimulateEncounter(catalog, [village], glass)).Disposition);
    }

    [Fact]
    public void Resolves_reaction_in_stable_override_order()
    {
        var (catalog, village, glass) = Sample(); var now = DateTimeOffset.UnixEpoch;
        var state = new ReputationState([], []);
        var result = FactionReputationService.ResolveReaction(catalog, state, new("player", village, glass, now, 20, -10, 5, 80, -70), ReputationScope.World, "world");
        Assert.Equal(80, result.Score);
        Assert.Equal("allied", result.Disposition);
        Assert.Equal("gm-override:80", result.Explanation[^1]);
    }

    [Fact]
    public void InitialStateUsesCanonicalActorPlayerAndGroupOverlays()
    {
        var (catalog, village, glass) = Sample();
        var overlays = new[]
        {
            new PersonalReputationOverlay(Guid.NewGuid(), 1, "test:r086", "actor", village, glass,
                5, null, "actor-cause", PersonalReputationSubjectKind.Actor),
            new PersonalReputationOverlay(Guid.NewGuid(), 1, "test:r086", "player", village, glass,
                10, null, "player-cause", PersonalReputationSubjectKind.Player),
            new PersonalReputationOverlay(Guid.NewGuid(), 1, "test:r086", "group", village, glass,
                15, null, "group-cause", PersonalReputationSubjectKind.Group),
        };

        var state = FactionReputationService.InitialState(catalog with { PersonalReputation = overlays });

        Assert.Equal(3, state.Personal.Count);
        Assert.Equal([PersonalReputationSubjectKind.Actor, PersonalReputationSubjectKind.Player,
            PersonalReputationSubjectKind.Group], state.Personal.Select(item => item.SubjectKind));
        Assert.All(state.Personal, item => Assert.False(string.IsNullOrWhiteSpace(item.Cause)));
    }

    [Fact]
    public void CanonicalProfileSupportsEveryScopeAndGovernedReactionPolicy()
    {
        var (catalog, village, glass) = Sample();
        var now = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
        var transactions = Enum.GetValues<ReputationScope>().Select((scope, index) =>
            new ReputationTransaction(Guid.NewGuid(), 1, "test:r087", scope,
                scope.ToString().ToLowerInvariant(), village, glass, index + 1, $"cause-{scope}",
                now.AddMinutes(index))).ToArray();
        var policy = new FactionReactionPolicy(Guid.NewGuid(), 1, "test:r087",
            ReputationScope.Campaign, "campaign", false, true, true, false, false);
        var profile = new FactionReputationProfile(Guid.NewGuid(), 1, "test:r087", transactions, [], policy);
        catalog = catalog with { Reputation = profile, RequireReputationRuntime = true };
        var state = FactionReputationService.InitialState(catalog);

        var reaction = FactionReputationService.ResolveReaction(catalog, state,
            new("player", village, glass, now, 50, -10, 5, 100, -100));

        Assert.Equal(5, state.Transactions.Count);
        Assert.Equal(-42, reaction.Score);
        Assert.DoesNotContain(reaction.Explanation, item => item.StartsWith("disguise", StringComparison.Ordinal));
        Assert.DoesNotContain(reaction.Explanation, item => item.StartsWith("zone", StringComparison.Ordinal));
        Assert.DoesNotContain(reaction.Explanation, item => item.StartsWith("gm", StringComparison.Ordinal));
    }

    [Fact]
    public void HeatmapImportRejectsInvalidIdentityAndPreservesCanonicalMetadata()
    {
        var (catalog, _, _) = Sample();
        var captured = FactionReputationService.Capture(catalog, "baseline", DateTimeOffset.UnixEpoch,
            "test:r087");

        var restored = FactionReputationService.ImportSnapshot(
            FactionReputationService.ExportSnapshot(captured));

        Assert.Equal(captured.Id, restored.Id);
        Assert.Equal(captured.Label, restored.Label);
        Assert.Equal(captured.Relationships, restored.Relationships);
        Assert.Equal(1, restored.SchemaVersion);
        Assert.Equal("test:r087", restored.Provenance);
        Assert.Throws<InvalidOperationException>(() => FactionReputationService.ImportSnapshot("{}"));
    }

    private static (FactionCatalog Catalog, Guid Village, Guid Glass) Sample()
    {
        var village = Guid.Parse("11111111-1111-1111-1111-111111111111"); var glass = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var factions = new[] { new FactionDefinition(village, 1, "author:test", "village", "Village", false, [], ["civilian", "merchant"]), new FactionDefinition(glass, 1, "author:test", "glass", "Glass", false, [], ["undead"]) };
        var relations = new[] { new FactionRelationship(village, glass, -40, "fear", "hostile"), new FactionRelationship(glass, village, 10, "curiosity", "neutral") };
        var bands = new[] { new DispositionBand("hostile", -100, -1, 1), new DispositionBand("neutral", 0, 49, 1), new DispositionBand("allied", 50, 100, 1) };
        return (new(factions, relations, bands, []), village, glass);
    }
}
