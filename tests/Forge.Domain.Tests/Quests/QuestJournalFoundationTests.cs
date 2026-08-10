using Forge.Domain.Dialogues;
using Forge.Domain.Quests;

namespace Forge.Domain.Tests.Quests;

public sealed class QuestJournalFoundationTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Definition_migrates_legacy_states_to_stable_numeric_order()
    {
        var quest = LegacyQuest();

        Assert.Equal([1, 2], quest.States.Select(item => item.DisplayOrder));
        Assert.Equal([0, 1], quest.States.Select(item => item.LegacyJournalIndex));
        Assert.Equal("journal", quest.Category);
        Assert.Equal(QuestProgressScope.Player, quest.InstancePolicy.DefaultScope);
        Assert.Empty(QuestValidator.Validate(quest));
    }

    [Fact]
    public void Rich_journal_preserves_localized_sections_and_typed_links()
    {
        var quest = RichQuest();
        var journal = quest.States[0].Journal;

        Assert.Equal("Find the ember key.", journal.Summary.Resolve("en"));
        Assert.Equal("Trouvez la clé de braise.", journal.Summary.Resolve("de"));
        Assert.Equal("Inspectez l'autel puis ouvrez la porte.", journal.Details.Fallback);
        Assert.Equal(2, journal.Objectives.Count);
        Assert.Equal([QuestJournalLinkKind.Location, QuestJournalLinkKind.Custom],
            journal.ContextLinks.Select(item => item.Kind));
        Assert.Empty(QuestValidator.Validate(quest));
    }

    [Fact]
    public void Validator_reports_duplicate_order_and_invalid_custom_terminal()
    {
        var valid = RichQuest();
        var first = valid.States[0];
        var invalidTerminal = State(
            valid.States[1].Id, "broken", "Broken", first.DisplayOrder, 1,
            QuestTerminalKind.Custom, QuestReactivationRule.Manual, string.Empty);
        var invalid = Rebuild(valid, [first, invalidTerminal]);

        var codes = QuestValidator.Validate(invalid).Select(item => item.Code).ToHashSet();

        Assert.Contains(QuestValidator.StateOrderDuplicateCode, codes);
        Assert.Contains(QuestValidator.TerminalConfigurationCode, codes);
    }

    [Fact]
    public void Validator_traverses_past_duplicate_incoming_states()
    {
        var initial = State(Id(1), "initial", "Initial", 1, 0);
        var left = State(Id(2), "left", "Left", 2, 1);
        var right = State(Id(3), "right", "Right", 3, 2);
        var end = State(Id(4), "end", "End", 4, 3, QuestTerminalKind.Completed);
        var quest = Definition([initial, left, right, end],
        [
            Transition(1, initial.Id, left.Id),
            Transition(2, initial.Id, right.Id),
            Transition(3, left.Id, right.Id),
            Transition(4, right.Id, end.Id),
        ]);

        Assert.DoesNotContain(QuestValidator.Validate(quest), item => item.Code == QuestValidator.UnreachableStateCode);
    }

    [Fact]
    public void Instance_tracks_scope_history_variables_timestamps_and_terminal_kind()
    {
        var quest = RichQuest();
        var created = QuestInstanceService.Create(quest, QuestProgressScope.Party, "party:ashes", Start,
            id: Guid.Parse("c7000000-0000-4000-8000-000000000001"));
        var advanced = QuestInstanceService.Advance(quest, created, quest.Transitions[0].Id,
            new AlwaysEvaluator(), Start.AddMinutes(2), "player:elyra",
            new Dictionary<string, string> { ["ember-key"] = "found" });

        Assert.Equal(1, advanced.Revision);
        Assert.Equal(2, advanced.History.Count);
        Assert.Equal("found", advanced.Variables["ember-key"]);
        Assert.Equal(Start.AddMinutes(2), advanced.LastSimulatedAt);
        Assert.Equal(QuestTerminalKind.Completed,
            quest.States.Single(item => item.Id == advanced.CurrentStateId).TerminalKind);
    }

    [Fact]
    public void Instance_rejects_forbidden_scope_and_missing_authority_without_mutation()
    {
        var quest = RichQuest();

        Assert.Throws<InvalidOperationException>(() =>
            QuestInstanceService.Create(quest, QuestProgressScope.Player, "player:one", Start));
        Assert.Throws<InvalidOperationException>(() =>
            QuestInstanceService.Create(quest, QuestProgressScope.Party, "party:one", Start,
                authorityGranted: false));
    }

    [Fact]
    public void Terminal_instance_reactivates_only_under_explicit_rule()
    {
        var quest = RichQuest();
        var created = QuestInstanceService.Create(quest, QuestProgressScope.Party, "party:ashes", Start);
        var completed = QuestInstanceService.Advance(quest, created, quest.Transitions[0].Id,
            new AlwaysEvaluator(), Start.AddMinutes(1), "player:one");
        var reactivated = QuestInstanceService.Reactivate(quest, completed, Start.AddMinutes(5), "gm:one");
        var forbidden = Rebuild(quest,
        [
            quest.States[0],
            State(quest.States[1].Id, "completed", "Completed", 2, 1, QuestTerminalKind.Completed,
                QuestReactivationRule.Never),
        ]);

        Assert.Equal(quest.InitialStateId, reactivated.CurrentStateId);
        Assert.Equal("reactivated", reactivated.History[^1].Reason);
        Assert.Throws<InvalidOperationException>(() =>
            QuestInstanceService.Reactivate(forbidden, completed, Start.AddMinutes(6), "gm:one"));
    }

    [Fact]
    public void Invalid_schema_has_stable_code_and_path()
    {
        var quest = RichQuest();
        var invalid = new QuestDefinition(quest.Id, 99, quest.Provenance, quest.Slug, quest.DisplayName,
            quest.Title, quest.Description, quest.Category, quest.Priority, quest.Owner, quest.RequiredAtRuntime,
            quest.InstancePolicy, quest.InitialStateId, quest.States, quest.Transitions, quest.Operations);

        var diagnostic = Assert.Single(QuestValidator.Validate(invalid), item =>
            item.Code == QuestValidator.SchemaUnsupportedCode);

        Assert.EndsWith("/schemaVersion", diagnostic.Path, StringComparison.Ordinal);
    }

    internal static QuestDefinition RichQuest()
    {
        var initial = State(Id(1), "search", "Search", 1, 10, journal: new QuestJournalEntry(
            new LocalizedText("quest.ashes.search.summary", "Trouvez la clé de braise.",
            [
                LocalizedTextVariant.Create("quest.ashes.search.summary", "en", "Find the ember key."),
            ], "test:r007"),
            new LocalizedText("quest.ashes.search.details", "Inspectez l'autel puis ouvrez la porte.",
                provenance: "test:r007"),
            [
                new LocalizedText("quest.ashes.search.objective.altar", "Inspecter l'autel"),
                new LocalizedText("quest.ashes.search.objective.door", "Ouvrir la porte"),
            ],
            [
                new QuestJournalLink(QuestJournalLinkKind.Location, "crypt-of-ashes", provenance: "test:r007"),
                new QuestJournalLink(QuestJournalLinkKind.Custom, "lore:ember-key", provenance: "test:r007"),
            ], "test:r007"));
        var completed = State(Id(2), "completed", "Completed", 2, 11, QuestTerminalKind.Completed,
            QuestReactivationRule.Manual);
        return new QuestDefinition(
            QuestId.From(Guid.Parse("c7100000-0000-4000-8000-000000000001")), 1, "test:r007",
            "ashes-journal", "Ashes journal",
            new LocalizedText("quest.ashes.title", "La clé de braise"),
            new LocalizedText("quest.ashes.description", "Libérez la crypte."),
            "main", 90, "author:karen", true,
            new QuestInstancePolicy(QuestProgressScope.Party, [QuestProgressScope.Party], true),
            initial.Id, [initial, completed], [Transition(1, initial.Id, completed.Id)]);
    }

    private static QuestDefinition LegacyQuest()
    {
        var initial = new QuestStateDefinition(Id(1), "Initial", new("quest.legacy.initial", "Start"));
        var completed = new QuestStateDefinition(Id(2), "Completed", new("quest.legacy.done", "Done"), true);
        return Definition([initial, completed], [Transition(1, initial.Id, completed.Id)]);
    }

    private static QuestDefinition Definition(
        IReadOnlyList<QuestStateDefinition> states,
        IReadOnlyList<QuestTransition> transitions) => new(
        QuestId.From(Guid.Parse("c7200000-0000-4000-8000-000000000001")), "quest-test", "Quest test",
        states[0].Id, states, transitions);

    private static QuestDefinition Rebuild(QuestDefinition source, IReadOnlyList<QuestStateDefinition> states) =>
        new(source.Id, source.SchemaVersion, source.Provenance, source.Slug, source.DisplayName, source.Title,
            source.Description, source.Category, source.Priority, source.Owner, source.RequiredAtRuntime,
            source.InstancePolicy, source.InitialStateId, states, source.Transitions, source.Operations);

    private static QuestStateDefinition State(
        QuestStateId id,
        string slug,
        string name,
        int order,
        int legacyIndex,
        QuestTerminalKind terminal = QuestTerminalKind.None,
        QuestReactivationRule reactivation = QuestReactivationRule.Never,
        string customCode = "",
        QuestJournalEntry? journal = null) => new(
        id, 1, "test:r007", slug, name,
        journal ?? new QuestJournalEntry(new LocalizedText($"quest.{slug}.summary", name),
            new LocalizedText($"quest.{slug}.details", $"{name} details"), provenance: "test:r007"),
        terminal != QuestTerminalKind.None, order, legacyIndex, terminal, customCode, reactivation);

    private static QuestStateId Id(int value) => QuestStateId.From(Guid.Parse(
        $"c7300000-0000-4000-8000-{value.ToString().PadLeft(12, '0')}"));

    private static QuestTransition Transition(int value, QuestStateId from, QuestStateId to) => new(
        QuestTransitionId.From(Guid.Parse($"c7400000-0000-4000-8000-{value.ToString().PadLeft(12, '0')}")),
        from, to);

    private sealed class AlwaysEvaluator : IQuestConditionEvaluator
    {
        public bool IsSatisfied(QuestCondition condition) => true;
    }
}
