using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public static class QuestValidator
{
    public const string MissingInitialStateCode = "quest.reference.initial_state_missing";
    public const string MissingTransitionStateCode = "quest.reference.transition_state_missing";
    public const string UnreachableStateCode = "quest.graph.state_unreachable";
    public const string TerminalStateTransitionCode = "quest.graph.terminal_state_has_transition";
    public const string SchemaUnsupportedCode = "quest.schema.unsupported";
    public const string ProvenanceMissingCode = "quest.provenance.missing";
    public const string MetadataInvalidCode = "quest.metadata.invalid";
    public const string InstancePolicyInvalidCode = "quest.instance.policy_invalid";
    public const string StateSchemaUnsupportedCode = "quest.state.schema.unsupported";
    public const string StateIdentityDuplicateCode = "quest.state.identity_duplicate";
    public const string StateOrderDuplicateCode = "quest.state.display_order_duplicate";
    public const string LegacyIndexDuplicateCode = "quest.state.legacy_index_duplicate";
    public const string TerminalConfigurationCode = "quest.state.terminal_configuration_invalid";
    public const string JournalSchemaUnsupportedCode = "quest.journal.schema.unsupported";
    public const string JournalIdentityDuplicateCode = "quest.journal.identity_duplicate";
    public const string JournalLocalizationInvalidCode = "quest.journal.localization_invalid";
    public const string JournalLinkInvalidCode = "quest.journal.link_invalid";
    public const string JournalLinkIdentityDuplicateCode = "quest.journal.link_identity_duplicate";
    public const string TransitionSchemaUnsupportedCode = "quest.transition.schema.unsupported";
    public const string TransitionIdentityInvalidCode = "quest.transition.identity_invalid";
    public const string TransitionOrderDuplicateCode = "quest.transition.display_order_duplicate";
    public const string TransitionBindingInvalidCode = "quest.transition.event_binding_invalid";
    public const string TransitionPolicyInvalidCode = "quest.transition.policy_invalid";
    public const string TransitionConditionInvalidCode = "quest.transition.condition_invalid";
    public const string TransitionActionInvalidCode = "quest.transition.action_invalid";
    public const string SimulationProfileInvalidCode = "quest.simulation.profile_invalid";
    public const string SimulationEventInvalidCode = "quest.simulation.event_invalid";
    public const string SimulationEventIdentityDuplicateCode = "quest.simulation.event_identity_duplicate";

    public static IReadOnlyList<DomainDiagnostic> Validate(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var diagnostics = new List<DomainDiagnostic>();
        var root = $"quests/{quest.Id}";
        if (quest.SchemaVersion != QuestDefinition.CurrentSchemaVersion)
            diagnostics.Add(new DomainDiagnostic(SchemaUnsupportedCode,
                $"Quest '{quest.Id}' uses unsupported schema {quest.SchemaVersion}.", $"{root}/schemaVersion"));
        if (string.IsNullOrWhiteSpace(quest.Provenance))
            diagnostics.Add(new DomainDiagnostic(ProvenanceMissingCode,
                $"Quest '{quest.Id}' requires provenance.", $"{root}/provenance"));
        if (string.IsNullOrWhiteSpace(quest.Category) || string.IsNullOrWhiteSpace(quest.Owner) ||
            quest.Priority is < 1 or > 100)
            diagnostics.Add(new DomainDiagnostic(MetadataInvalidCode,
                $"Quest '{quest.Id}' requires a category, owner and priority from 1 to 100.", root));
        if (!quest.InstancePolicy.AllowedScopes.Contains(quest.InstancePolicy.DefaultScope) ||
            quest.InstancePolicy.AllowedScopes.Count == 0)
            diagnostics.Add(new DomainDiagnostic(InstancePolicyInvalidCode,
                $"Quest '{quest.Id}' has an invalid instance policy.", $"{root}/instancePolicy"));

        ValidateLocalizedText(quest.Title, $"{root}/title", diagnostics);
        ValidateLocalizedText(quest.Description, $"{root}/description", diagnostics);

        var duplicateSlugs = quest.States.GroupBy(state => state.Slug, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var duplicateOrders = quest.States.GroupBy(state => state.DisplayOrder)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var duplicateLegacyIndices = quest.States.Where(state => state.LegacyJournalIndex is not null)
            .GroupBy(state => state.LegacyJournalIndex!.Value).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        var duplicateJournalIds = quest.States.GroupBy(state => state.Journal.Id)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var duplicateLinkIds = quest.States.SelectMany(state => state.Journal.ContextLinks)
            .GroupBy(link => link.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        foreach (var state in quest.States)
        {
            var statePath = $"{root}/states/{state.Id}";
            if (state.SchemaVersion != QuestStateDefinition.CurrentSchemaVersion)
                diagnostics.Add(new DomainDiagnostic(StateSchemaUnsupportedCode,
                    $"Quest state '{state.Id}' uses unsupported schema {state.SchemaVersion}.",
                    $"{statePath}/schemaVersion"));
            if (string.IsNullOrWhiteSpace(state.Provenance) || duplicateSlugs.Contains(state.Slug))
                diagnostics.Add(new DomainDiagnostic(StateIdentityDuplicateCode,
                    $"Quest state '{state.Id}' requires provenance and a unique slug.", statePath));
            if (state.DisplayOrder < 1 || duplicateOrders.Contains(state.DisplayOrder))
                diagnostics.Add(new DomainDiagnostic(StateOrderDuplicateCode,
                    $"Quest state '{state.Id}' requires a unique positive display order.",
                    $"{statePath}/displayOrder"));
            if (state.LegacyJournalIndex is < 0 ||
                state.LegacyJournalIndex is { } legacyIndex && duplicateLegacyIndices.Contains(legacyIndex))
                diagnostics.Add(new DomainDiagnostic(LegacyIndexDuplicateCode,
                    $"Quest state '{state.Id}' has an invalid or duplicate legacy journal index.",
                    $"{statePath}/legacyJournalIndex"));
            if (state.IsTerminal != (state.TerminalKind != QuestTerminalKind.None) ||
                state.TerminalKind == QuestTerminalKind.Custom && string.IsNullOrWhiteSpace(state.CustomTerminalCode) ||
                state.TerminalKind == QuestTerminalKind.None && state.Reactivation != QuestReactivationRule.Never)
                diagnostics.Add(new DomainDiagnostic(TerminalConfigurationCode,
                    $"Quest state '{state.Id}' has an invalid terminal or reactivation configuration.",
                    $"{statePath}/terminalKind"));

            var journalPath = $"{statePath}/journal";
            if (state.Journal.SchemaVersion != QuestJournalEntry.CurrentSchemaVersion)
                diagnostics.Add(new DomainDiagnostic(JournalSchemaUnsupportedCode,
                    $"Journal entry '{state.Journal.Id}' uses unsupported schema {state.Journal.SchemaVersion}.",
                    $"{journalPath}/schemaVersion"));
            if (string.IsNullOrWhiteSpace(state.Journal.Provenance) || duplicateJournalIds.Contains(state.Journal.Id))
                diagnostics.Add(new DomainDiagnostic(JournalIdentityDuplicateCode,
                    $"Journal entry '{state.Journal.Id}' requires provenance and a unique UUID.", journalPath));
            ValidateLocalizedText(state.Journal.Summary, $"{journalPath}/summary", diagnostics);
            ValidateLocalizedText(state.Journal.Details, $"{journalPath}/details", diagnostics);
            for (var objectiveIndex = 0; objectiveIndex < state.Journal.Objectives.Count; objectiveIndex++)
                ValidateLocalizedText(state.Journal.Objectives[objectiveIndex],
                    $"{journalPath}/objectives/{objectiveIndex}", diagnostics);
            foreach (var link in state.Journal.ContextLinks)
            {
                var linkPath = $"{journalPath}/contextLinks/{link.Id}";
                if (link.SchemaVersion != QuestJournalLink.CurrentSchemaVersion ||
                    string.IsNullOrWhiteSpace(link.Provenance) || string.IsNullOrWhiteSpace(link.ReferenceId) ||
                    !Enum.IsDefined(link.Kind))
                    diagnostics.Add(new DomainDiagnostic(JournalLinkInvalidCode,
                        $"Journal link '{link.Id}' has invalid schema, provenance, kind or reference.", linkPath));
                if (duplicateLinkIds.Contains(link.Id))
                    diagnostics.Add(new DomainDiagnostic(JournalLinkIdentityDuplicateCode,
                        $"Journal link UUID '{link.Id}' is used more than once.", $"{linkPath}/id"));
                if (link.Label is not null) ValidateLocalizedText(link.Label, $"{linkPath}/label", diagnostics);
            }
        }

        var states = quest.States.ToDictionary(state => state.Id);
        if (!states.ContainsKey(quest.InitialStateId))
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingInitialStateCode,
                $"Quest '{quest.Id}' references missing initial state '{quest.InitialStateId}'.",
                $"quests/{quest.Id}/initialStateId"));
            return diagnostics;
        }

        var duplicateTransitionOrders = quest.Transitions.GroupBy(item => item.DisplayOrder)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        foreach (var transition in quest.Transitions)
        {
            var transitionPath = $"quests/{quest.Id}/transitions/{transition.Id}";
            if (transition.SchemaVersion != QuestTransition.CurrentSchemaVersion)
                diagnostics.Add(new DomainDiagnostic(TransitionSchemaUnsupportedCode,
                    $"Quest transition '{transition.Id}' uses unsupported schema {transition.SchemaVersion}.",
                    $"{transitionPath}/schemaVersion"));
            if (string.IsNullOrWhiteSpace(transition.Provenance))
                diagnostics.Add(new DomainDiagnostic(TransitionIdentityInvalidCode,
                    $"Quest transition '{transition.Id}' requires provenance.", $"{transitionPath}/provenance"));
            if (transition.DisplayOrder < 1 || duplicateTransitionOrders.Contains(transition.DisplayOrder))
                diagnostics.Add(new DomainDiagnostic(TransitionOrderDuplicateCode,
                    $"Quest transition '{transition.Id}' requires a unique positive display order.",
                    $"{transitionPath}/displayOrder"));
            if (transition.EventBinding.SchemaVersion != QuestEventBinding.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(transition.EventBinding.Provenance) ||
                string.IsNullOrWhiteSpace(transition.EventBinding.EventName) ||
                !Enum.IsDefined(transition.EventBinding.Kind))
                diagnostics.Add(new DomainDiagnostic(TransitionBindingInvalidCode,
                    $"Quest transition '{transition.Id}' has an invalid event binding.",
                    $"{transitionPath}/eventBinding"));
            if (transition.Priority is < 1 or > 100 || transition.ForgeScriptApiVersion != 1 ||
                !Enum.IsDefined(transition.Idempotence) ||
                transition.Idempotence != QuestTransitionIdempotence.None &&
                string.IsNullOrWhiteSpace(transition.IdempotenceKey))
                diagnostics.Add(new DomainDiagnostic(TransitionPolicyInvalidCode,
                    $"Quest transition '{transition.Id}' has an invalid priority, idempotence or ForgeScript API version.",
                    transitionPath));
            for (var index = 0; index < transition.Conditions.Count; index++)
            {
                var condition = transition.Conditions[index];
                if (!Enum.IsDefined(condition.Kind) || string.IsNullOrWhiteSpace(condition.Key))
                    diagnostics.Add(new DomainDiagnostic(TransitionConditionInvalidCode,
                        $"Quest transition '{transition.Id}' has an invalid typed condition.",
                        $"{transitionPath}/conditions/{index}"));
            }
            for (var index = 0; index < transition.Actions.Count; index++)
            {
                var action = transition.Actions[index];
                if (!Enum.IsDefined(action.Kind) || string.IsNullOrWhiteSpace(action.Key))
                    diagnostics.Add(new DomainDiagnostic(TransitionActionInvalidCode,
                        $"Quest transition '{transition.Id}' has an invalid typed action.",
                        $"{transitionPath}/actions/{index}"));
            }
            if (!states.ContainsKey(transition.FromStateId) || !states.ContainsKey(transition.ToStateId))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingTransitionStateCode,
                    $"Quest transition '{transition.Id}' references a missing state.",
                    $"quests/{quest.Id}/transitions/{transition.Id}"));
            }

            if (states.TryGetValue(transition.FromStateId, out var fromState) && fromState.IsTerminal)
            {
                diagnostics.Add(new DomainDiagnostic(
                    TerminalStateTransitionCode,
                    $"Terminal quest state '{fromState.Id}' cannot have outgoing transitions.",
                    $"quests/{quest.Id}/transitions/{transition.Id}/fromStateId"));
            }
        }

        var reachable = new HashSet<QuestStateId>();
        var pending = new Queue<QuestStateId>();
        pending.Enqueue(quest.InitialStateId);
        while (pending.TryDequeue(out var stateId))
        {
            if (!reachable.Add(stateId)) continue;
            foreach (var target in quest.Transitions
                         .Where(transition => transition.FromStateId == stateId && states.ContainsKey(transition.ToStateId))
                         .Select(transition => transition.ToStateId))
            {
                pending.Enqueue(target);
            }
        }

        foreach (var state in quest.States.Where(state => !reachable.Contains(state.Id)))
        {
            diagnostics.Add(new DomainDiagnostic(
                UnreachableStateCode,
                $"Quest state '{state.Id}' cannot be reached from the initial state.",
                $"quests/{quest.Id}/states/{state.Id}"));
        }

        ValidateSimulationProfile(quest, diagnostics);
        if (!diagnostics.Any(item => item.Code == MissingTransitionStateCode))
            diagnostics.AddRange(QuestNarrativeValidator.Validate(quest));

        diagnostics.AddRange(QuestOperationsValidator.Validate(quest));

        return diagnostics;
    }

    private static void ValidateSimulationProfile(QuestDefinition quest, ICollection<DomainDiagnostic> diagnostics)
    {
        var profile = quest.Simulation;
        var path = $"quests/{quest.Id}/simulation";
        if (profile.SchemaVersion != QuestSimulationProfile.CurrentSchemaVersion || profile.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(profile.Provenance) || profile.MaximumSteps is < 1 or > 10_000)
            diagnostics.Add(new DomainDiagnostic(SimulationProfileInvalidCode,
                $"Quest '{quest.Id}' has an invalid simulation profile.", path));
        var duplicateIds = profile.Events.GroupBy(item => item.Id).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        foreach (var simulationEvent in profile.Events)
        {
            var eventPath = $"{path}/events/{simulationEvent.Id}";
            if (simulationEvent.SchemaVersion != QuestSimulationEvent.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(simulationEvent.Provenance) ||
                string.IsNullOrWhiteSpace(simulationEvent.EventName) ||
                string.IsNullOrWhiteSpace(simulationEvent.ActorId) ||
                simulationEvent.Variables.Keys.Any(string.IsNullOrWhiteSpace))
                diagnostics.Add(new DomainDiagnostic(SimulationEventInvalidCode,
                    $"Simulation event '{simulationEvent.Id}' is invalid.", eventPath));
            if (duplicateIds.Contains(simulationEvent.Id))
                diagnostics.Add(new DomainDiagnostic(SimulationEventIdentityDuplicateCode,
                    $"Simulation event UUID '{simulationEvent.Id}' is used more than once.", $"{eventPath}/id"));
        }
    }

    private static void ValidateLocalizedText(
        Dialogues.LocalizedText text,
        string path,
        ICollection<DomainDiagnostic> diagnostics)
    {
        if (text.SchemaVersion != Dialogues.LocalizedText.CurrentSchemaVersion || text.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(text.Provenance) || string.IsNullOrWhiteSpace(text.Key) ||
            string.IsNullOrWhiteSpace(text.Fallback))
            diagnostics.Add(new DomainDiagnostic(JournalLocalizationInvalidCode,
                $"Localized text '{text.Key}' has invalid identity, schema, provenance, key or fallback.", path));
    }
}
