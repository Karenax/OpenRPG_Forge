using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public enum QuestObjectiveKind
{
    Counter,
    Boolean,
    Collection,
    Destination,
    Escort,
    Survival,
    Choice,
    Custom,
}

public enum QuestParticipantBindingKind
{
    Blueprint,
    Instance,
    Query,
}

public enum QuestEditorialStatus
{
    Draft,
    Review,
    Approved,
    Deprecated,
}

public sealed record QuestAuthoringObjective(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    int DisplayOrder,
    QuestObjectiveKind Kind,
    LocalizedText Label,
    double Target,
    IReadOnlyList<string> TargetIds,
    string? DestinationId,
    double DurationSeconds,
    IReadOnlyList<string> ChoiceIds,
    string? CustomRuleId,
    bool Optional);

public sealed record QuestAuthoringParticipant(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    int DisplayOrder,
    string Role,
    QuestParticipantBindingKind BindingKind,
    string BindingValue,
    IReadOnlyList<string> PresenceConditions,
    bool Required);

public sealed record QuestAuthoringRewardBundle(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    double Progression,
    IReadOnlyDictionary<string, int> Items,
    IReadOnlyDictionary<string, decimal> Currencies,
    IReadOnlyDictionary<string, double> Reputation,
    IReadOnlyList<string> Unlocks,
    IReadOnlyList<string> ChoiceIds);

public sealed record QuestAuthorReview(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string AuthorId,
    QuestEditorialStatus Status,
    string Message,
    DateTimeOffset At);

public sealed record QuestAuthorNotes(
    string Design,
    string Canon,
    string OwnerId,
    QuestEditorialStatus Status,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Todos,
    IReadOnlyList<string> Sources,
    IReadOnlyList<QuestAuthorReview> ReviewHistory);

public sealed record QuestPlotWizardPlan(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<string> ZoneIds,
    IReadOnlyList<Guid> ParticipantIds,
    IReadOnlyList<string> PropIds,
    IReadOnlyList<DialogueId> DialogueIds,
    IReadOnlyList<Guid> ObjectiveIds,
    IReadOnlyList<QuestTransitionId> TransitionIds,
    IReadOnlyList<string> GeneratedTestScenarios,
    bool GeneratesOpaqueCode);

public sealed record QuestAuthoringProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<QuestAuthoringObjective> Objectives,
    QuestAuthoringRewardBundle Rewards,
    QuestAuthorNotes Notes,
    IReadOnlyList<QuestAuthoringParticipant> Participants,
    QuestPlotWizardPlan Wizard);

public sealed record QuestAuthoringRewardState(
    double Progression,
    IReadOnlyDictionary<string, int> Items,
    IReadOnlyDictionary<string, decimal> Currencies,
    IReadOnlyDictionary<string, double> Reputation,
    IReadOnlySet<string> Unlocks,
    IReadOnlySet<string> Choices);

public sealed record QuestAuthoringRewardResult(
    bool Committed,
    QuestAuthoringRewardState State,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    string SemanticDiff);

public static class QuestAuthoringRuntimeService
{
    public static QuestAuthoringRewardResult GrantRewards(
        QuestDefinition quest,
        QuestAuthoringRewardState original,
        string actorId)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(original);
        var profile = quest.Authoring;
        if (profile is null || !profile.Enabled)
            return new(false, original,
                [new DomainDiagnostic("quest.authoring.profile_missing", "Quest authoring profile is absent.",
                    $"quests/{quest.Id}/authoring")], "no-change");
        var validation = QuestAuthoringProfileValidator.Validate(quest);
        if (string.IsNullOrWhiteSpace(actorId) || validation.Count > 0)
            return new(false, original, validation.Count > 0 ? validation :
                [new DomainDiagnostic("quest.authoring.reward_actor_invalid", "Reward actor is required.",
                    $"quests/{quest.Id}/authoring/rewards/actorId")], "no-change");

        var rewards = profile.Rewards;
        var items = original.Items.ToDictionary(StringComparer.Ordinal);
        foreach (var item in rewards.Items) items[item.Key] = items.GetValueOrDefault(item.Key) + item.Value;
        var currencies = original.Currencies.ToDictionary(StringComparer.Ordinal);
        foreach (var currency in rewards.Currencies)
            currencies[currency.Key] = currencies.GetValueOrDefault(currency.Key) + currency.Value;
        var reputation = original.Reputation.ToDictionary(StringComparer.Ordinal);
        foreach (var faction in rewards.Reputation)
            reputation[faction.Key] = reputation.GetValueOrDefault(faction.Key) + faction.Value;
        return new(true, original with
        {
            Progression = original.Progression + rewards.Progression,
            Items = items,
            Currencies = currencies,
            Reputation = reputation,
            Unlocks = original.Unlocks.Concat(rewards.Unlocks).ToHashSet(StringComparer.Ordinal),
            Choices = original.Choices.Concat(rewards.ChoiceIds).ToHashSet(StringComparer.Ordinal),
        }, [], $"grant:/quests/{quest.Id}/authoring/rewards|actor:{actorId}|progression:{rewards.Progression}|" +
               $"items:{rewards.Items.Count}|currencies:{rewards.Currencies.Count}|" +
               $"reputation:{rewards.Reputation.Count}|unlocks:{rewards.Unlocks.Count}|choices:{rewards.ChoiceIds.Count}");
    }
}

public static class QuestAuthoringProfileValidator
{
    public const string RequiredMissingCode = "quest.authoring.required_missing";
    public const string IdentityInvalidCode = "quest.authoring.identity_invalid";
    public const string ObjectiveInvalidCode = "quest.authoring.objective_invalid";
    public const string RewardInvalidCode = "quest.authoring.reward_invalid";
    public const string NotesInvalidCode = "quest.authoring.notes_invalid";
    public const string ParticipantInvalidCode = "quest.authoring.participant_invalid";
    public const string WizardInvalidCode = "quest.authoring.wizard_invalid";
    public const string ReferenceMissingCode = "quest.authoring.reference_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var path = $"quests/{quest.Id}/authoring";
        var profile = quest.Authoring;
        if (profile is null || !profile.Enabled)
            return profile?.RequiredAtRuntime == true
                ? [new DomainDiagnostic(RequiredMissingCode, "Required quest authoring profile is disabled.", path)]
                : [];
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Quest authoring identity, schema or provenance is invalid.", path));

        var duplicateObjectiveIds = profile.Objectives.GroupBy(item => item.Id).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        var duplicateObjectiveOrders = profile.Objectives.GroupBy(item => item.DisplayOrder)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        for (var index = 0; index < profile.Objectives.Count; index++)
        {
            var objective = profile.Objectives[index];
            var invalidKindContract = objective.Kind switch
            {
                QuestObjectiveKind.Counter or QuestObjectiveKind.Boolean => objective.Target <= 0,
                QuestObjectiveKind.Collection => objective.Target <= 0 || objective.TargetIds.Count == 0,
                QuestObjectiveKind.Destination => string.IsNullOrWhiteSpace(objective.DestinationId),
                QuestObjectiveKind.Escort => objective.TargetIds.Count == 0 ||
                                             string.IsNullOrWhiteSpace(objective.DestinationId),
                QuestObjectiveKind.Survival => objective.DurationSeconds <= 0,
                QuestObjectiveKind.Choice => objective.ChoiceIds.Count < 2,
                QuestObjectiveKind.Custom => string.IsNullOrWhiteSpace(objective.CustomRuleId),
                _ => true,
            };
            if (objective.Id == Guid.Empty || objective.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(objective.Provenance) || objective.DisplayOrder < 1 ||
                duplicateObjectiveIds.Contains(objective.Id) || duplicateObjectiveOrders.Contains(objective.DisplayOrder) ||
                objective.Label.Id == Guid.Empty || string.IsNullOrWhiteSpace(objective.Label.Key) ||
                string.IsNullOrWhiteSpace(objective.Label.Fallback) || objective.TargetIds.Any(string.IsNullOrWhiteSpace) ||
                objective.ChoiceIds.Any(string.IsNullOrWhiteSpace) || invalidKindContract)
                diagnostics.Add(new(ObjectiveInvalidCode, "Quest objective identity, order or typed contract is invalid.",
                    $"{path}/objectives/{index}"));
        }
        if (profile.Objectives.Count == 0)
            diagnostics.Add(new(ObjectiveInvalidCode, "At least one explicit quest objective is required.",
                $"{path}/objectives"));

        var rewards = profile.Rewards;
        if (rewards.Id == Guid.Empty || rewards.SchemaVersion != 1 || string.IsNullOrWhiteSpace(rewards.Provenance) ||
            !double.IsFinite(rewards.Progression) || rewards.Progression < 0 ||
            rewards.Items.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Value <= 0) ||
            rewards.Currencies.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Value < 0) ||
            rewards.Reputation.Any(item => string.IsNullOrWhiteSpace(item.Key) || !double.IsFinite(item.Value)) ||
            rewards.Unlocks.Any(string.IsNullOrWhiteSpace) || rewards.ChoiceIds.Any(string.IsNullOrWhiteSpace))
            diagnostics.Add(new(RewardInvalidCode, "Quest reward bundle is invalid.", $"{path}/rewards"));

        var notes = profile.Notes;
        if (string.IsNullOrWhiteSpace(notes.Design) || string.IsNullOrWhiteSpace(notes.Canon) ||
            string.IsNullOrWhiteSpace(notes.OwnerId) || notes.Sources.Count == 0 ||
            notes.Sources.Any(string.IsNullOrWhiteSpace) || notes.ReviewHistory.Count == 0 ||
            notes.ReviewHistory.Select(item => item.Id).Distinct().Count() != notes.ReviewHistory.Count ||
            notes.ReviewHistory.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || string.IsNullOrWhiteSpace(item.AuthorId) ||
                string.IsNullOrWhiteSpace(item.Message)))
            diagnostics.Add(new(NotesInvalidCode, "Quest author notes or review history are invalid.", $"{path}/notes"));

        var duplicateParticipantIds = profile.Participants.GroupBy(item => item.Id).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        var duplicateRoles = profile.Participants.GroupBy(item => item.Role, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        if (profile.Participants.Count == 0 || profile.Participants.Any(item => item.Id == Guid.Empty ||
                item.SchemaVersion != 1 || string.IsNullOrWhiteSpace(item.Provenance) || item.DisplayOrder < 1 ||
                string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.BindingValue) ||
                duplicateParticipantIds.Contains(item.Id) || duplicateRoles.Contains(item.Role) ||
                item.PresenceConditions.Any(string.IsNullOrWhiteSpace)))
            diagnostics.Add(new(ParticipantInvalidCode, "Quest participant roles or bindings are invalid.",
                $"{path}/participants"));

        var wizard = profile.Wizard;
        var objectiveIds = profile.Objectives.Select(item => item.Id).ToHashSet();
        var participantIds = profile.Participants.Select(item => item.Id).ToHashSet();
        var transitionIds = quest.Transitions.Select(item => item.Id).ToHashSet();
        if (wizard.Id == Guid.Empty || wizard.SchemaVersion != 1 || string.IsNullOrWhiteSpace(wizard.Provenance) ||
            wizard.ZoneIds.Count == 0 || wizard.ParticipantIds.Count == 0 || wizard.DialogueIds.Count == 0 ||
            wizard.ObjectiveIds.Count == 0 || wizard.TransitionIds.Count == 0 || wizard.GeneratesOpaqueCode ||
            wizard.ParticipantIds.Any(id => !participantIds.Contains(id)) ||
            wizard.ObjectiveIds.Any(id => !objectiveIds.Contains(id)) ||
            wizard.TransitionIds.Any(id => !transitionIds.Contains(id)) ||
            wizard.GeneratedTestScenarios.Count < 5 || wizard.GeneratedTestScenarios.Any(string.IsNullOrWhiteSpace))
            diagnostics.Add(new(WizardInvalidCode, "Plot Wizard plan is incomplete, unresolved or opaque.",
                $"{path}/wizard"));
        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateReferences(ProjectDefinition project, QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(quest);
        var profile = quest.Authoring;
        if (profile is null || !profile.Enabled) return [];
        var path = $"quests/{quest.Id}/authoring";
        var diagnostics = new List<DomainDiagnostic>();
        var blueprints = project.Blueprints.ToDictionary(item => item.Id.ToString(), StringComparer.OrdinalIgnoreCase);
        var blueprintSlugs = project.Blueprints.ToDictionary(item => item.Slug, StringComparer.OrdinalIgnoreCase);
        var entities = project.Areas.SelectMany(item => item.Entities).Select(item => item.Id.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool BlueprintExists(string value, Blueprints.BlueprintKind? kind = null) =>
            blueprints.TryGetValue(value, out var byId) && (kind is null || byId.Kind == kind) ||
            blueprintSlugs.TryGetValue(value, out var bySlug) && (kind is null || bySlug.Kind == kind);
        for (var index = 0; index < profile.Participants.Count; index++)
        {
            var participant = profile.Participants[index];
            var resolved = participant.BindingKind switch
            {
                QuestParticipantBindingKind.Blueprint => BlueprintExists(participant.BindingValue,
                    Blueprints.BlueprintKind.Creature),
                QuestParticipantBindingKind.Instance => entities.Contains(participant.BindingValue),
                QuestParticipantBindingKind.Query => participant.BindingValue.StartsWith("query:",
                    StringComparison.Ordinal) && participant.BindingValue.Length > "query:".Length,
                _ => false,
            };
            if (!resolved)
                diagnostics.Add(new(ReferenceMissingCode, "Quest participant binding does not resolve.",
                    $"{path}/participants/{index}/bindingValue"));
        }
        foreach (var item in profile.Rewards.Items.Keys.Where(value =>
                     !BlueprintExists(value, Blueprints.BlueprintKind.Item)))
            diagnostics.Add(new(ReferenceMissingCode, "Quest reward item does not resolve.",
                $"{path}/rewards/items/{item}"));
        var factions = project.Module.Factions.Factions.SelectMany(item => new[] { item.Id.ToString("D"), item.Slug })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var faction in profile.Rewards.Reputation.Keys.Where(value => !factions.Contains(value)))
            diagnostics.Add(new(ReferenceMissingCode, "Quest reward faction does not resolve.",
                $"{path}/rewards/reputation/{faction}"));
        var zones = project.Areas.SelectMany(item => new[] { item.Id.ToString(), item.Slug })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in profile.Wizard.ZoneIds.Where(value => !zones.Contains(value)))
            diagnostics.Add(new(ReferenceMissingCode, "Plot Wizard zone does not resolve.",
                $"{path}/wizard/zoneIds/{zone}"));
        var dialogues = project.Dialogues.Select(item => item.Id).ToHashSet();
        foreach (var dialogue in profile.Wizard.DialogueIds.Where(id => !dialogues.Contains(id)))
            diagnostics.Add(new(ReferenceMissingCode, "Plot Wizard dialogue does not resolve.",
                $"{path}/wizard/dialogueIds/{dialogue}"));
        foreach (var prop in profile.Wizard.PropIds.Where(value =>
                     !BlueprintExists(value) && !entities.Contains(value)))
            diagnostics.Add(new(ReferenceMissingCode, "Plot Wizard prop does not resolve.",
                $"{path}/wizard/propIds/{prop}"));
        return diagnostics;
    }
}
