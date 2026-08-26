using Forge.Application.Gameplay;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Tests.Gameplay;

public sealed class RuntimeNarrativeStateTests
{
    [Fact]
    public void Journal_hides_unaccepted_and_preserves_active_terminal_and_untracked_completed_quests()
    {
        var unaccepted = CreateQuest(1, "Découvrir la piste", "Pas encore acceptée",
            "Découvrir la piste", QuestTerminalKind.None);
        var active = CreateQuest(2, "La lanterne d'Aubeciel", "Active",
            "Rallumer la lanterne", QuestTerminalKind.None);
        var failed = CreateQuest(3, "Le convoi perdu", "Le convoi est perdu",
            "Retrouver le convoi", QuestTerminalKind.Failed);
        var completed = CreateQuest(4, "Le serment tenu", "Le serment est accompli",
            "Tenir le serment", QuestTerminalKind.Completed);
        var area = new AreaDefinition(Id<AreaId>(90), "second-workspace", "Second workspace", []);
        var project = new ProjectDefinition(Id<ProjectId>(91), "Narrative runtime",
            [], [area], quests: [unaccepted.Quest, active.Quest, failed.Quest, completed.Quest]);

        var presentation = RuntimeModulePresentationProjector.Project(project, area.Id,
            new RuntimeModulePresentationContext(
                QuestStates:
                [
                    new(unaccepted.Quest.Id, unaccepted.InitialStateId, IsAccepted: false),
                    new(active.Quest.Id, active.InitialStateId, IsAccepted: true),
                    new(failed.Quest.Id, failed.TerminalStateId, IsAccepted: true),
                    new(completed.Quest.Id, completed.TerminalStateId, IsAccepted: true),
                ],
                TrackedQuestId: active.Quest.Id));

        Assert.Equal(3, presentation.Journal.TotalCount);
        Assert.Equal(3, presentation.Journal.AcceptedCount);
        Assert.Equal(1, presentation.Journal.ActiveCount);
        Assert.Equal(2, presentation.Journal.TerminalCount);
        Assert.Equal(1, presentation.Journal.CompletedCount);
        Assert.Equal("La lanterne d'Aubeciel", presentation.Journal.ActiveEntry?.Title);
        Assert.Equal("Rallumer la lanterne", presentation.Journal.ActiveObjective);
        Assert.NotEqual(presentation.Journal.ActiveEntry?.Title, presentation.Journal.ActiveObjective);

        var entries = presentation.Journal.Entries.ToDictionary(entry => entry.Id);
        Assert.DoesNotContain(unaccepted.Quest.Id.ToString(), entries.Keys);
        Assert.True(entries[active.Quest.Id.ToString()].IsAccepted);
        Assert.True(entries[active.Quest.Id.ToString()].IsTracked);

        Assert.Equal("Le convoi est perdu", entries[failed.Quest.Id.ToString()].State);
        Assert.Equal(QuestTerminalKind.Failed, entries[failed.Quest.Id.ToString()].TerminalKind);
        Assert.False(entries[completed.Quest.Id.ToString()].IsTracked);
        Assert.Equal("Le serment est accompli", entries[completed.Quest.Id.ToString()].State);
        Assert.Equal(QuestTerminalKind.Completed, entries[completed.Quest.Id.ToString()].TerminalKind);

        var terminalTracked = RuntimeModulePresentationProjector.Project(project, area.Id,
            new RuntimeModulePresentationContext(
                QuestStates:
                [
                    new(unaccepted.Quest.Id, unaccepted.InitialStateId, IsAccepted: false),
                    new(active.Quest.Id, active.InitialStateId, IsAccepted: true),
                    new(failed.Quest.Id, failed.TerminalStateId, IsAccepted: true),
                    new(completed.Quest.Id, completed.TerminalStateId, IsAccepted: true),
                ],
                TrackedQuestId: failed.Quest.Id));

        Assert.Null(terminalTracked.Journal.ActiveEntry);
        Assert.Null(terminalTracked.Journal.ActiveObjective);
    }

    [Fact]
    public void Dialogue_resolution_uses_participant_entry_point_and_conditions_in_a_second_workspace()
    {
        var unrelatedSpeaker = Id<BlueprintId>(101);
        var authoredSpeaker = Id<BlueprintId>(102);
        var unrelated = CreateDialogue(110, unrelatedSpeaker, "default", "Dialogue sans rapport");
        var authored = CreateDialogue(120, authoredSpeaker, "interaction", "Entrée authorée",
            includeGuardedChoices: true);
        var area = new AreaDefinition(Id<AreaId>(130), "aubeciel", "Aubeciel", []);
        var project = new ProjectDefinition(Id<ProjectId>(131), "Second workspace",
            [
                new BlueprintDefinition(unrelatedSpeaker, "unrelated", "Sans rapport", BlueprintKind.Creature),
                new BlueprintDefinition(authoredSpeaker, "maelis", "Maélis", BlueprintKind.Creature),
            ], [area], [unrelated.Dialogue, authored.Dialogue]);

        var resolution = RuntimeNarrativeResolver.ResolveDialogue(project, authoredSpeaker, "interaction");

        Assert.NotNull(resolution);
        Assert.Equal(authored.Dialogue.Id, resolution.Dialogue.Id);
        Assert.Equal(authored.EntryNodeId, resolution.EntryNodeId);
        var traversal = new DialogueTraversal(resolution.Dialogue, resolution.EntryNodeId);
        Assert.Equal("Entrée authorée", traversal.Start().Node?.Text.Fallback);
        var available = traversal.GetAvailableChoices(new AllowedConditionEvaluator());
        Assert.Equal("Choix autorisé", Assert.Single(available).Text.Fallback);
    }

    [Fact]
    public void Accept_reports_whether_the_quest_was_newly_accepted_without_mutating_it_twice()
    {
        var quest = CreateBoundQuest(30, "beacon", "dialogue.accept");
        var tracker = new RuntimeQuestStateTracker([quest.Quest]);

        Assert.True(tracker.Accept(quest.Quest.Slug, track: true, out var firstAcceptance));
        var stateAfterFirstAcceptance = tracker.GetCurrentState(quest.Quest.Id)?.Id;
        Assert.True(tracker.Accept(quest.Quest.Slug, track: true, out var repeatedAcceptance));

        Assert.True(firstAcceptance);
        Assert.False(repeatedAcceptance);
        Assert.Equal(stateAfterFirstAcceptance, tracker.GetCurrentState(quest.Quest.Id)?.Id);
        Assert.Equal(quest.Quest.Id, tracker.TrackedQuestId);
    }

    [Fact]
    public void Quest_events_target_the_authored_quest_binding_and_never_the_first_definition()
    {
        var unrelated = CreateBoundQuest(20, "unrelated", "unrelated.event");
        var target = CreateBoundQuest(30, "beacon", "dialogue.accept");
        var tracker = new RuntimeQuestStateTracker([unrelated.Quest, target.Quest]);

        Assert.True(tracker.Accept(target.Quest.Slug, track: true));
        var advanced = tracker.AdvanceByEvent(
            target.Quest.Slug,
            QuestEventKind.Dialogue,
            "dialogue.accept",
            sourceReference: "dialogue:beacon",
            new AlwaysQuestConditionEvaluator());

        Assert.NotNull(advanced);
        Assert.Equal(target.Quest.Id, advanced.Quest.Id);
        Assert.Equal(target.TerminalStateId, advanced.Result.State.Id);
        Assert.Null(tracker.TrackedQuestId);
        var states = tracker.Snapshot().ToDictionary(state => state.QuestId);
        Assert.Equal(unrelated.InitialStateId, states[unrelated.Quest.Id].StateId);
        Assert.False(states[unrelated.Quest.Id].IsAccepted);
        Assert.Equal(target.TerminalStateId, states[target.Quest.Id].StateId);
        Assert.True(states[target.Quest.Id].IsAccepted);
    }

    private static QuestFixture CreateQuest(
        int seed,
        string title,
        string stateLabel,
        string objective,
        QuestTerminalKind terminalKind)
    {
        var questId = Id<QuestId>(seed * 10);
        var initialId = Id<QuestStateId>((seed * 10) + 1);
        var terminalId = Id<QuestStateId>((seed * 10) + 2);
        var initialJournal = new QuestJournalEntry(
            new LocalizedText($"quest.{seed}.summary", stateLabel),
            new LocalizedText($"quest.{seed}.details", $"Situation {seed}"),
            [new LocalizedText($"quest.{seed}.objective", objective)],
            provenance: "test:runtime-narrative");
        var terminalJournal = new QuestJournalEntry(
            new LocalizedText($"quest.{seed}.terminal.summary", stateLabel),
            new LocalizedText($"quest.{seed}.terminal.details", $"Situation terminale {seed}"),
            [new LocalizedText($"quest.{seed}.terminal.objective", "Objectif obsolète")],
            provenance: "test:runtime-narrative");
        var initial = new QuestStateDefinition(initialId, stateLabel, initialJournal);
        var terminal = new QuestStateDefinition(
            terminalId,
            QuestStateDefinition.CurrentSchemaVersion,
            "test:runtime-narrative",
            "terminal",
            stateLabel,
            terminalJournal,
            isTerminal: true,
            displayOrder: 2,
            legacyJournalIndex: 1,
            terminalKind == QuestTerminalKind.None ? QuestTerminalKind.Completed : terminalKind,
            string.Empty,
            QuestReactivationRule.Never);
        var quest = new QuestDefinition(
            questId,
            QuestDefinition.CurrentSchemaVersion,
            "test:runtime-narrative",
            $"quest-{seed}",
            title,
            new LocalizedText($"quest.{seed}.title", title),
            new LocalizedText($"quest.{seed}.description", $"Description {seed}"),
            "journal",
            100 - seed,
            "author:test",
            requiredAtRuntime: false,
            instancePolicy: null,
            initialId,
            [initial, terminal]);
        return new(quest, initialId, terminalId);
    }

    private static DialogueFixture CreateDialogue(
        int seed,
        BlueprintId speaker,
        string entryKey,
        string entryText,
        bool includeGuardedChoices = false)
    {
        var defaultNodeId = Id<DialogueNodeId>(seed + 1);
        var entryNodeId = Id<DialogueNodeId>(seed + 2);
        IReadOnlyList<DialogueChoice> choices = includeGuardedChoices
            ?
            [
                new DialogueChoice(Id<DialogueChoiceId>(seed + 3), new("choice.denied", "Choix refusé"),
                    [new DialogueCondition(DialogueConditionKind.VariableEquals, "denied", "true")]),
                new DialogueChoice(Id<DialogueChoiceId>(seed + 4), new("choice.allowed", "Choix autorisé"),
                    [new DialogueCondition(DialogueConditionKind.VariableEquals, "allowed", "true")]),
            ]
            : [];
        IReadOnlyList<DialogueChoice> defaultChoices = string.Equals(entryKey, "default", StringComparison.Ordinal)
            ? choices
            :
            [
                new DialogueChoice(Id<DialogueChoiceId>(seed + 7),
                    new("choice.enter", "Continuer"), targetNodeId: entryNodeId),
            ];
        IReadOnlyList<DialogueEntryPoint> entryPoints = string.Equals(entryKey, "default", StringComparison.Ordinal)
            ?
            [
                new DialogueEntryPoint(GuidFor(seed + 6), DialogueEntryPoint.CurrentSchemaVersion,
                    "default", defaultNodeId, 0, "test:runtime-narrative"),
            ]
            :
            [
                new DialogueEntryPoint(GuidFor(seed + 6), DialogueEntryPoint.CurrentSchemaVersion,
                    "default", defaultNodeId, 0, "test:runtime-narrative"),
                new DialogueEntryPoint(GuidFor(seed + 8), DialogueEntryPoint.CurrentSchemaVersion,
                    entryKey, entryNodeId, 1, "test:runtime-narrative"),
            ];
        var dialogue = new DialogueDefinition(
            Id<DialogueId>(seed),
            $"dialogue-{seed}",
            $"Dialogue {seed}",
            defaultNodeId,
            [
                new DialogueNode(defaultNodeId, speaker, new("dialogue.default", "Entrée par défaut"),
                    choices: defaultChoices),
                new DialogueNode(entryNodeId, speaker, new("dialogue.authored", entryText), choices: choices),
            ],
            entryPoints: entryPoints);
        return new(dialogue, entryNodeId);
    }

    private static QuestFixture CreateBoundQuest(int seed, string slug, string eventName)
    {
        var fixture = CreateQuest(seed, $"Quest {slug}", "Disponible", $"Faire {slug}",
            QuestTerminalKind.Completed);
        var transition = new QuestTransition(
            Id<QuestTransitionId>(seed + 9),
            QuestTransition.CurrentSchemaVersion,
            "test:runtime-narrative",
            fixture.InitialStateId,
            fixture.TerminalStateId,
            1,
            new QuestEventBinding(QuestEventKind.Dialogue, eventName,
                sourceReference: "dialogue:beacon", provenance: "test:runtime-narrative"),
            80,
            QuestTransitionIdempotence.PerEvent,
            eventName,
            1);
        var source = fixture.Quest;
        var quest = new QuestDefinition(
            source.Id, source.SchemaVersion, source.Provenance, slug, source.DisplayName,
            source.Title, source.Description, source.Category, source.Priority, source.Owner,
            source.RequiredAtRuntime, source.InstancePolicy, source.InitialStateId, source.States,
            [transition], source.Operations, source.Simulation, source.Authoring, source.Experience, source.Quality);
        return fixture with { Quest = quest };
    }

    private static T Id<T>(int seed) where T : class
    {
        var value = GuidFor(seed);
        return typeof(T).Name switch
        {
            nameof(AreaId) => (T)(object)AreaId.From(value),
            nameof(BlueprintId) => (T)(object)BlueprintId.From(value),
            nameof(DialogueChoiceId) => (T)(object)DialogueChoiceId.From(value),
            nameof(DialogueId) => (T)(object)DialogueId.From(value),
            nameof(DialogueNodeId) => (T)(object)DialogueNodeId.From(value),
            nameof(ProjectId) => (T)(object)ProjectId.From(value),
            nameof(QuestId) => (T)(object)QuestId.From(value),
            nameof(QuestStateId) => (T)(object)QuestStateId.From(value),
            nameof(QuestTransitionId) => (T)(object)QuestTransitionId.From(value),
            _ => throw new InvalidOperationException($"Unsupported identifier type {typeof(T).Name}."),
        };
    }

    private static Guid GuidFor(int seed) => Guid.Parse($"d2200000-0000-4000-8000-{seed:D12}");

    private sealed class AllowedConditionEvaluator : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => condition.Key == "allowed";
    }

    private sealed class AlwaysQuestConditionEvaluator : IQuestConditionEvaluator
    {
        public bool IsSatisfied(QuestCondition condition) => true;
    }

    private sealed record QuestFixture(
        QuestDefinition Quest,
        QuestStateId InitialStateId,
        QuestStateId TerminalStateId);

    private sealed record DialogueFixture(DialogueDefinition Dialogue, DialogueNodeId EntryNodeId);
}
