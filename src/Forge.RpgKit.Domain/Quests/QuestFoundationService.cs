namespace Forge.RpgKit.Domain.Quests;

public enum QuestObjectiveKind { Counter, Boolean, Collection, Destination, Escort, Survival, Choice, Custom }
public enum QuestParticipantBindingKind { Blueprint, Instance, Query }
public enum QuestEditorialStatus { Draft, Review, Approved, Deprecated }
public sealed record QuestObjective(Guid Id, QuestObjectiveKind Kind, string Label, double Target, IReadOnlySet<string> TargetIds, string? DestinationId, TimeSpan Duration, IReadOnlySet<string> ChoiceIds, string? CustomRuleId, bool Optional);
public sealed record QuestParticipant(string Role, QuestParticipantBindingKind BindingKind, string BindingValue, IReadOnlySet<string> PresenceConditions, bool Required);
public sealed record QuestRewardBundle(double Progression, IReadOnlyDictionary<Guid, int> Items, IReadOnlyDictionary<string, decimal> Currencies, IReadOnlyDictionary<string, double> Reputation, IReadOnlySet<string> Unlocks, IReadOnlyList<string> ChoiceIds);
public sealed record QuestAuthorNotes(string Design, string Canon, string OwnerId, QuestEditorialStatus Status, IReadOnlyList<string> Risks, IReadOnlyList<string> Todos, IReadOnlyList<string> Sources, IReadOnlyList<string> ReviewHistory);
public sealed record QuestDefinition(Guid Id, int SchemaVersion, string Provenance, string Slug, string DisplayName, IReadOnlyList<string> ZoneIds, IReadOnlyList<QuestParticipant> Participants, IReadOnlyList<string> PropIds, IReadOnlyList<Guid> DialogueIds, IReadOnlyList<QuestObjective> Objectives, IReadOnlyList<string> TransitionIds, QuestRewardBundle Rewards, QuestAuthorNotes Notes);
public sealed record QuestWizardRequest(string Slug, string DisplayName, IReadOnlyList<string> ZoneIds, IReadOnlyList<QuestParticipant> Participants, IReadOnlyList<string> PropIds, IReadOnlyList<Guid> DialogueIds, IReadOnlyList<QuestObjective> Objectives, IReadOnlyList<string> TransitionIds, QuestRewardBundle Rewards, QuestAuthorNotes Notes, string Provenance);
public sealed record QuestValidationContext(IReadOnlySet<string> ZoneIds, IReadOnlySet<string> ParticipantBindings, IReadOnlySet<string> PropIds, IReadOnlySet<Guid> DialogueIds, IReadOnlySet<string> TransitionIds, IReadOnlySet<Guid> ItemIds, IReadOnlySet<string> CurrencyIds, IReadOnlySet<string> FactionIds, IReadOnlySet<string> UnlockIds, IReadOnlySet<string> ObjectiveTargetIds, int MaximumObjectives);
public sealed record QuestDiagnostic(string Code, string Path, string Message);
public sealed record QuestWizardPreview(QuestDefinition Quest, IReadOnlyList<QuestDiagnostic> Diagnostics, IReadOnlyList<string> GeneratedTestScenarios, string SemanticDiff, bool CanApply);
public sealed record QuestRewardState(double Progression, IReadOnlyDictionary<Guid, int> Items, IReadOnlyDictionary<string, decimal> Currencies, IReadOnlyDictionary<string, double> Reputation, IReadOnlySet<string> Unlocks);
public sealed record QuestRewardResult(bool Committed, QuestRewardState State, IReadOnlyList<QuestDiagnostic> Diagnostics, string SemanticDiff);

public static class QuestFoundationService
{
    public static QuestWizardPreview Preview(QuestWizardRequest request, QuestValidationContext context)
    {
        var quest = new QuestDefinition(StableId(request.Slug), 1, request.Provenance, request.Slug.Trim(), request.DisplayName.Trim(), request.ZoneIds, request.Participants, request.PropIds, request.DialogueIds, request.Objectives, request.TransitionIds, request.Rewards, request.Notes);
        var diagnostics = Validate(quest, context); var tests = new[] { "nominal-path", "objective-failure", "repeated-transition", "multiplayer-authority", "save-resume" };
        return new(quest, diagnostics, tests, $"add:/quests/{quest.Id}|zones:{quest.ZoneIds.Count}|participants:{quest.Participants.Count}|objectives:{quest.Objectives.Count}|transitions:{quest.TransitionIds.Count}|generated-tests:{tests.Length}", diagnostics.Count == 0);
    }

    public static QuestDefinition Apply(QuestWizardRequest request, QuestValidationContext context)
    {
        var preview = Preview(request, context); if (!preview.CanApply) throw new InvalidOperationException(string.Join("; ", preview.Diagnostics.Select(item => item.Code))); return preview.Quest;
    }

    public static QuestRewardResult GrantRewards(QuestRewardState original, QuestRewardBundle rewards, string actorId)
    {
        var diagnostics = new List<QuestDiagnostic>(); if (string.IsNullOrWhiteSpace(actorId) || rewards.Progression < 0 || rewards.Items.Any(item => item.Value <= 0) || rewards.Currencies.Any(item => item.Value < 0) || rewards.Reputation.Any(item => !double.IsFinite(item.Value))) diagnostics.Add(new("RPG_QUEST_REWARD_INVALID", "quest/rewards", "Reward bundle or actor is invalid."));
        if (diagnostics.Count != 0) return new(false, original, diagnostics, "no-change");
        var items = original.Items.ToDictionary(); foreach (var item in rewards.Items) items[item.Key] = items.GetValueOrDefault(item.Key) + item.Value;
        var currencies = original.Currencies.ToDictionary(); foreach (var item in rewards.Currencies) currencies[item.Key] = currencies.GetValueOrDefault(item.Key) + item.Value;
        var reputation = original.Reputation.ToDictionary(); foreach (var item in rewards.Reputation) reputation[item.Key] = reputation.GetValueOrDefault(item.Key) + item.Value;
        return new(true, original with { Progression = original.Progression + rewards.Progression, Items = items, Currencies = currencies, Reputation = reputation, Unlocks = original.Unlocks.Concat(rewards.Unlocks).ToHashSet(StringComparer.Ordinal) }, [], $"grant-quest-rewards|actor:{actorId}|progression:{rewards.Progression}|items:{rewards.Items.Count}|currencies:{rewards.Currencies.Count}|reputation:{rewards.Reputation.Count}|unlocks:{rewards.Unlocks.Count}|choices:{rewards.ChoiceIds.Count}");
    }

    public static IReadOnlyList<QuestDiagnostic> Validate(QuestDefinition quest, QuestValidationContext context)
    {
        var root = $"quests/{quest.Id}"; var result = new List<QuestDiagnostic>();
        if (quest.Id == Guid.Empty || quest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(quest.Provenance) || string.IsNullOrWhiteSpace(quest.Slug) || string.IsNullOrWhiteSpace(quest.DisplayName)) result.Add(new("RPG_QUEST_IDENTITY_INVALID", root, "Quest identity is incomplete."));
        if (quest.ZoneIds.Count == 0 || quest.ZoneIds.Any(id => !context.ZoneIds.Contains(id))) result.Add(new("RPG_QUEST_ZONE_INVALID", $"{root}/zones", "Quest zones must resolve."));
        if (quest.Participants.Count == 0 || quest.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != quest.Participants.Count || quest.Participants.Any(item => string.IsNullOrWhiteSpace(item.Role) || !context.ParticipantBindings.Contains(item.BindingValue))) result.Add(new("RPG_QUEST_PARTICIPANT_INVALID", $"{root}/participants", "Participant roles and bindings must resolve."));
        if (quest.PropIds.Any(id => !context.PropIds.Contains(id)) || quest.DialogueIds.Any(id => !context.DialogueIds.Contains(id)) || quest.TransitionIds.Any(id => !context.TransitionIds.Contains(id))) result.Add(new("RPG_QUEST_REFERENCE_INVALID", root, "Props, dialogues or transitions do not resolve."));
        if (quest.Objectives.Count == 0 || quest.Objectives.Count > context.MaximumObjectives || quest.Objectives.Select(item => item.Id).Distinct().Count() != quest.Objectives.Count || quest.Objectives.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Label) || item.Target < 0 || item.TargetIds.Any(id => !context.ObjectiveTargetIds.Contains(id)))) result.Add(new("RPG_QUEST_OBJECTIVE_INVALID", $"{root}/objectives", "Objectives are missing, duplicated or reference invalid targets."));
        if (quest.Rewards.Items.Keys.Any(id => !context.ItemIds.Contains(id)) || quest.Rewards.Currencies.Keys.Any(id => !context.CurrencyIds.Contains(id)) || quest.Rewards.Reputation.Keys.Any(id => !context.FactionIds.Contains(id)) || quest.Rewards.Unlocks.Any(id => !context.UnlockIds.Contains(id))) result.Add(new("RPG_QUEST_REWARD_REFERENCE_INVALID", $"{root}/rewards", "Reward references must resolve."));
        if (string.IsNullOrWhiteSpace(quest.Notes.Design) || string.IsNullOrWhiteSpace(quest.Notes.Canon) || string.IsNullOrWhiteSpace(quest.Notes.OwnerId) || quest.Notes.Sources.Count == 0 || quest.Notes.ReviewHistory.Count == 0) result.Add(new("RPG_QUEST_NOTES_INVALID", $"{root}/notes", "Design, canon, owner, sources and review history are required."));
        return result;
    }

    private static Guid StableId(string slug) { var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"openrpg:quest:{slug.Trim().ToLowerInvariant()}")); return new(hash[..16]); }
}
