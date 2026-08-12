using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Quests;

public sealed class QuestExperienceProfileTests
{
    [Fact]
    public void Profile_models_five_resource_kinds_editable_steps_visibility_history_and_four_marker_bindings()
    {
        var quest = QuestAuthoringProfileTests.Quest();
        quest = quest.WithAuthoring(QuestAuthoringProfileTests.Profile(quest, [QuestObjectiveKind.Destination]));
        var profile = Profile(quest);

        Assert.Empty(QuestExperienceProfileValidator.Validate(quest.WithExperience(profile)));
        Assert.Equal(Enum.GetValues<QuestExperienceResourceKind>(), profile.Resources.Select(item => item.Kind));
        Assert.Equal(Enum.GetValues<QuestExperienceMarkerBindingKind>(), profile.Markers.Select(item => item.BindingKind));
        Assert.True(profile.Steps.Single().Editable);
        Assert.Equal(QuestExperienceVisibility.Hidden, profile.Visibility.Initial);
    }

    [Fact]
    public void Step_instantiation_and_visibility_reveal_are_deterministic_and_condition_driven()
    {
        var quest = QuestAuthoringProfileTests.Quest();
        quest = quest.WithAuthoring(QuestAuthoringProfileTests.Profile(quest, [QuestObjectiveKind.Destination]));
        var template = new QuestExperienceStepTemplate(Guid.NewGuid(), 1, "test:r072", "investigation", 2,
            new LocalizedText("quest.r072.step", "Inspecter le brasier"),
            [quest.Authoring!.Objectives[0].Id], [quest.Transitions[0].Id],
            new Dictionary<string, string> { ["pace"] = "deliberate" });
        var first = QuestExperienceRuntimeService.Instantiate(template, quest.Id, 1);
        var repeated = QuestExperienceRuntimeService.Instantiate(template, quest.Id, 1);
        Assert.Equal(first.Id, repeated.Id);
        Assert.True(first.Editable);

        var policy = Profile(quest).Visibility;
        Assert.Equal(QuestExperienceVisibility.Discovered,
            QuestExperienceRuntimeService.Reveal(policy, QuestExperienceVisibility.Hidden,
                new HashSet<string> { "heard", "seen" }));
        Assert.Equal(QuestExperienceVisibility.Hidden,
            QuestExperienceRuntimeService.Reveal(policy, QuestExperienceVisibility.Hidden, new HashSet<string>()));
    }

    [Fact]
    public void History_exposes_localized_player_view_and_complete_debug_timeline()
    {
        var quest = QuestAuthoringProfileTests.Quest();
        quest = quest.WithAuthoring(QuestAuthoringProfileTests.Profile(quest, [QuestObjectiveKind.Destination]));
        var profile = Profile(quest);
        var player = QuestExperienceRuntimeService.ReadHistory(profile, QuestExperienceHistoryView.Player);
        var debug = QuestExperienceRuntimeService.ReadHistory(profile, QuestExperienceHistoryView.Debug);
        Assert.Equal(2, player.Count);
        Assert.All(player, item => Assert.Null(item.Cause));
        Assert.Equal("dialogue", debug[1].Cause);
        Assert.Equal("player:smoke", debug[1].ActorId);
    }

    [Fact]
    public void Marker_tracking_respects_visibility_and_active_objective()
    {
        var quest = QuestAuthoringProfileTests.Quest();
        quest = quest.WithAuthoring(QuestAuthoringProfileTests.Profile(quest, [QuestObjectiveKind.Destination]));
        var marker = Profile(quest).Markers[0];
        Assert.True(QuestExperienceRuntimeService.ResolveMarker(marker, QuestExperienceVisibility.Tracked, true).Tracked);
        Assert.False(QuestExperienceRuntimeService.ResolveMarker(marker, QuestExperienceVisibility.Hidden, true).Tracked);
        Assert.False(QuestExperienceRuntimeService.ResolveMarker(marker, QuestExperienceVisibility.Tracked, false).Tracked);
    }

    [Fact]
    public void Invalid_contracts_have_stable_localized_diagnostics_and_optional_absence_is_safe()
    {
        var quest = QuestAuthoringProfileTests.Quest();
        Assert.Empty(QuestExperienceProfileValidator.Validate(quest));
        quest = quest.WithAuthoring(QuestAuthoringProfileTests.Profile(quest, [QuestObjectiveKind.Destination]));
        var profile = Profile(quest);
        var invalid = profile with
        {
            Resources = [profile.Resources[0] with { Id = Guid.Empty }],
            Steps = [profile.Steps[0] with { Editable = false }],
            Visibility = profile.Visibility with { RevealConditions = new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>() },
            History = [profile.History[0] with { ActorId = string.Empty }],
            Markers = [profile.Markers[0] with { PrecisionRadius = -1 }],
        };
        var diagnostics = QuestExperienceProfileValidator.Validate(quest.WithExperience(invalid));
        Assert.Contains(diagnostics, item => item.Code == QuestExperienceProfileValidator.ResourceInvalidCode && item.Path.EndsWith("/resources"));
        Assert.Contains(diagnostics, item => item.Code == QuestExperienceProfileValidator.StepInvalidCode && item.Path.EndsWith("/steps"));
        Assert.Contains(diagnostics, item => item.Code == QuestExperienceProfileValidator.VisibilityInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == QuestExperienceProfileValidator.HistoryInvalidCode);
        Assert.Contains(diagnostics, item => item.Code == QuestExperienceProfileValidator.MarkerInvalidCode);
        Assert.Contains(QuestExperienceProfileValidator.Validate(quest.WithExperience(profile with { Enabled = false, RequiredAtRuntime = true })),
            item => item.Code == QuestExperienceProfileValidator.RequiredMissingCode);
    }

    internal static QuestExperienceProfile Profile(QuestDefinition quest)
    {
        var objectiveId = quest.Authoring!.Objectives[0].Id;
        var resources = Enum.GetValues<QuestExperienceResourceKind>().Select((kind, index) =>
            new QuestExperienceResource(Guid.Parse($"c7120000-0000-4000-8000-{(100 + index):D12}"), 1,
                "test:r072", index + 1, kind, $"resource:{kind.ToString().ToLowerInvariant()}", "owner:module", false,
                ["quest:active"])).ToArray();
        var step = new QuestExperienceStep(Guid.Parse("c7120000-0000-4000-8000-000000000201"), 1,
            "template:r072:investigation:v2", 1, new LocalizedText("quest.r072.step", "Inspecter le brasier"),
            [objectiveId], [quest.Transitions[0].Id], "investigation", 2, true);
        var visibility = new QuestExperienceVisibilityPolicy(
            Guid.Parse("c7120000-0000-4000-8000-000000000202"), 1, "test:r072",
            QuestExperienceVisibility.Hidden,
            new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>
            {
                [QuestExperienceVisibility.Rumor] = ["heard"],
                [QuestExperienceVisibility.Discovered] = ["heard", "seen"],
                [QuestExperienceVisibility.Tracked] = ["accepted"],
                [QuestExperienceVisibility.SecretCompleted] = ["secret:complete"],
            }, true);
        var history = new[]
        {
            new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000203"), 1,
                "test:r072", quest.Id, "none", "hidden", "module-load", "server",
                DateTimeOffset.Parse("2026-08-11T08:00:00Z"), new Dictionary<string, string>(),
                new LocalizedText("quest.r072.history.hidden", "Un murmure se lève.")),
            new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000204"), 1,
                "test:r072", quest.Id, "hidden", "discovered", "dialogue", "player:smoke",
                DateTimeOffset.Parse("2026-08-11T08:01:00Z"), new Dictionary<string, string> { ["seed"] = "72" },
                new LocalizedText("quest.r072.history.discovered", "La Crypte est révélée.")),
        };
        var markers = Enum.GetValues<QuestExperienceMarkerBindingKind>().Select((kind, index) =>
            new QuestExperienceMarker(Guid.Parse($"c7120000-0000-4000-8000-{(300 + index):D12}"), 1,
                "test:r072", index + 1, objectiveId, kind,
                kind switch
                {
                    QuestExperienceMarkerBindingKind.Zone => "crypt-of-ashes",
                    QuestExperienceMarkerBindingKind.Entity => "entity:guardian",
                    QuestExperienceMarkerBindingKind.Query => "query:nearest-ember",
                    _ => "waypoint:crypt-entry",
                }, QuestExperienceVisibility.Discovered, 4 + index, true,
                new LocalizedText($"quest.r072.marker.{index}", kind.ToString()),
                kind == QuestExperienceMarkerBindingKind.Waypoint ? new Vector3Value(1, 0, 2) : null)).ToArray();
        return new QuestExperienceProfile(Guid.Parse("c7120000-0000-4000-8000-000000000400"), 1,
            "test:r072", true, true, resources, [step], visibility, history, markers);
    }
}
