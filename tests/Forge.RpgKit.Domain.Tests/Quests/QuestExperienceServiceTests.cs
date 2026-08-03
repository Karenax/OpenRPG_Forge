using Forge.RpgKit.Domain.Quests;

namespace Forge.RpgKit.Domain.Tests.Quests;

public sealed class QuestExperienceServiceTests
{
    [Fact]
    public void Resources_cover_items_placeables_doors_zones_assets_references_and_ownership()
    {
        Assert.Equal(5, Enum.GetValues<QuestResourceKind>().Length); var fixture = Fixture(); Assert.Empty(QuestExperienceService.Validate(fixture.Quest, fixture.Experience, fixture.References, fixture.Owners, fixture.Transitions));
        var invalid = fixture.Experience with { Resources = [new("duplicate", QuestResourceKind.Item, "missing", "missing", true, new HashSet<string>()), new("duplicate", QuestResourceKind.Asset, "missing", "missing", true, new HashSet<string>())] };
        Assert.Contains(QuestExperienceService.Validate(fixture.Quest, invalid, fixture.References, fixture.Owners, fixture.Transitions), item => item.Code == "RPG_QUEST_RESOURCE_INVALID");
    }

    [Fact]
    public void Step_template_becomes_deterministic_editable_objectives_and_transitions_with_provenance()
    {
        var template = Template(); var first = QuestExperienceService.Instantiate(template, "vigil"); var repeated = QuestExperienceService.Instantiate(template, "vigil"); Assert.Equal(first.Id, repeated.Id); Assert.Equal(first.Objectives[0].Id, repeated.Objectives[0].Id); Assert.True(first.Editable); Assert.Equal("template:vigil", first.Provenance);
        var edited = first with { Label = "Edited by author" }; Assert.Equal("Edited by author", edited.Label);
    }

    [Fact]
    public void Visibility_progresses_hidden_rumor_discovered_tracked_secret_completed_by_conditions()
    {
        Assert.Equal(5, Enum.GetValues<QuestVisibility>().Length); var policy = new QuestVisibilityPolicy(QuestVisibility.Hidden, new Dictionary<QuestVisibility, IReadOnlySet<string>> { [QuestVisibility.Rumor] = new HashSet<string> { "heard" }, [QuestVisibility.Discovered] = new HashSet<string> { "heard", "seen" }, [QuestVisibility.Tracked] = new HashSet<string> { "accepted" } }, true);
        Assert.Equal(QuestVisibility.Discovered, QuestExperienceService.Reveal(policy, QuestVisibility.Hidden, new HashSet<string> { "heard", "seen" })); Assert.Equal(QuestVisibility.Hidden, QuestExperienceService.Reveal(policy, QuestVisibility.Hidden, new HashSet<string>()));
    }

    [Fact]
    public void History_is_timestamped_with_transitions_causes_actors_and_player_or_debug_views()
    {
        var quest = Guid.NewGuid(); var at = DateTimeOffset.UnixEpoch; var events = new[] { new QuestHistoryEvent(Guid.NewGuid(), quest, "hidden", "active", "dialogue", "player", at.AddSeconds(2), new Dictionary<string, string> { ["seed"] = "4" }, "La quête commence."), new QuestHistoryEvent(Guid.NewGuid(), quest, "none", "hidden", "module-load", "server", at, new Dictionary<string, string>(), "") };
        var player = QuestExperienceService.History(events, QuestHistoryView.Player); var debug = QuestExperienceService.History(events, QuestHistoryView.Debug); Assert.Equal(2, player.Count); Assert.IsType<QuestHistoryEvent>(debug[0]); Assert.Equal("none", ((QuestHistoryEvent)debug[0]).FromState);
    }

    [Fact]
    public void Markers_bind_zone_entity_query_waypoint_with_visibility_precision_and_tracking()
    {
        Assert.Equal(4, Enum.GetValues<ObjectiveMarkerBindingKind>().Length); var fixture = Fixture(); var marker = fixture.Experience.Markers.Single(); Assert.True(QuestExperienceService.UpdateMarker(marker, QuestVisibility.Tracked, true).Tracked); Assert.False(QuestExperienceService.UpdateMarker(marker, QuestVisibility.Hidden, true).Tracked);
        var invalid = fixture.Experience with { Markers = [marker with { PrecisionRadius = -1, BindingValue = "missing" }] }; Assert.Contains(QuestExperienceService.Validate(fixture.Quest, invalid, fixture.References, fixture.Owners, fixture.Transitions), item => item.Code == "RPG_QUEST_MARKER_INVALID");
    }

    private static QuestStepTemplate Template() => new("investigation", 1, "Inspect the prism", [new(Guid.Empty, QuestObjectiveKind.Boolean, "Prism inspected", 1, new HashSet<string>(), null, TimeSpan.Zero, new HashSet<string>(), null, false)], ["transition:inspect"], new Dictionary<string, string>(), "template:vigil");
    private static (QuestDefinition Quest, QuestExperienceDefinition Experience, HashSet<string> References, HashSet<string> Owners, HashSet<string> Transitions) Fixture()
    {
        var objective = new QuestObjective(Guid.NewGuid(), QuestObjectiveKind.Destination, "Reach observatory", 1, new HashSet<string>(), "zone:observatory", TimeSpan.Zero, new HashSet<string>(), null, false); var quest = new QuestDefinition(Guid.NewGuid(), 1, "author:test", "vigil", "Vigil", ["zone:observatory"], [], [], [], [objective], ["transition:inspect"], new(0, new Dictionary<Guid, int>(), new Dictionary<string, decimal>(), new Dictionary<string, double>(), new HashSet<string>(), []), new("design", "canon", "owner", QuestEditorialStatus.Review, [], [], ["source"], ["review"]));
        var step = QuestExperienceService.Instantiate(Template(), quest.Slug); var resources = Enum.GetValues<QuestResourceKind>().Select((kind, index) => new QuestResource($"resource:{index}", kind, $"ref:{index}", "owner:module", true, new HashSet<string>())).ToArray(); var marker = new ObjectiveMarker(Guid.NewGuid(), objective.Id, ObjectiveMarkerBindingKind.Waypoint, "ref:waypoint", QuestVisibility.Discovered, 4, true, "Observatory", null);
        return (quest, new(resources, [step], new(QuestVisibility.Hidden, new Dictionary<QuestVisibility, IReadOnlySet<string>> { [QuestVisibility.Rumor] = new HashSet<string> { "heard" } }, true), [marker]), resources.Select(item => item.ReferenceId).Append("ref:waypoint").ToHashSet(), new HashSet<string> { "owner:module" }, new HashSet<string> { "transition:inspect" });
    }
}
