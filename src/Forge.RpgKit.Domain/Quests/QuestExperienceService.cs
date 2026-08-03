using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Quests;

public enum QuestResourceKind { Item, Placeable, Door, Zone, Asset }
public enum QuestVisibility { Hidden, Rumor, Discovered, Tracked, SecretCompleted }
public enum ObjectiveMarkerBindingKind { Zone, Entity, Query, Waypoint }
public enum QuestHistoryView { Player, Debug }
public sealed record QuestResource(string Id, QuestResourceKind Kind, string ReferenceId, string OwnerId, bool Required, IReadOnlySet<string> Conditions);
public sealed record QuestStepTemplate(string TemplateId, int Version, string Label, IReadOnlyList<QuestObjective> Objectives, IReadOnlyList<string> TransitionIds, IReadOnlyDictionary<string, string> Parameters, string Provenance);
public sealed record GeneratedQuestStep(Guid Id, string Label, IReadOnlyList<QuestObjective> Objectives, IReadOnlyList<string> TransitionIds, string TemplateId, int TemplateVersion, string Provenance, bool Editable);
public sealed record QuestVisibilityPolicy(QuestVisibility Initial, IReadOnlyDictionary<QuestVisibility, IReadOnlySet<string>> RevealConditions, bool AllowManualTracking);
public sealed record QuestHistoryEvent(Guid Id, Guid QuestId, string FromState, string ToState, string Cause, string ActorId, DateTimeOffset At, IReadOnlyDictionary<string, string> DebugContext, string PlayerSummary);
public sealed record ObjectiveMarker(Guid Id, Guid ObjectiveId, ObjectiveMarkerBindingKind BindingKind, string BindingValue, QuestVisibility Visibility, double PrecisionRadius, bool Tracked, string Label, Vector3Value? FallbackPosition);
public sealed record QuestExperienceDefinition(IReadOnlyList<QuestResource> Resources, IReadOnlyList<GeneratedQuestStep> Steps, QuestVisibilityPolicy Visibility, IReadOnlyList<ObjectiveMarker> Markers);

public static class QuestExperienceService
{
    public static GeneratedQuestStep Instantiate(QuestStepTemplate template, string questSlug)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"openrpg:quest-step:{questSlug}:{template.TemplateId}:{template.Version}"));
        return new(new Guid(hash[..16]), template.Label, template.Objectives.Select(objective => objective with { Id = StableObjectiveId(questSlug, template.TemplateId, objective.Label) }).ToArray(), template.TransitionIds.ToArray(), template.TemplateId, template.Version, template.Provenance, true);
    }

    public static QuestVisibility Reveal(QuestVisibilityPolicy policy, QuestVisibility current, IReadOnlySet<string> state)
    {
        var ordered = Enum.GetValues<QuestVisibility>(); var best = current;
        foreach (var candidate in ordered.Where(item => item > current)) if (policy.RevealConditions.TryGetValue(candidate, out var conditions) && conditions.All(state.Contains)) best = candidate; else break;
        return best;
    }

    public static IReadOnlyList<object> History(IReadOnlyList<QuestHistoryEvent> events, QuestHistoryView view) => events.OrderBy(item => item.At).ThenBy(item => item.Id).Select(item => view == QuestHistoryView.Player ? (object)new { item.At, item.PlayerSummary } : item).ToArray();

    public static ObjectiveMarker UpdateMarker(ObjectiveMarker marker, QuestVisibility questVisibility, bool objectiveActive)
    {
        var visible = marker.Visibility <= questVisibility && objectiveActive; return marker with { Tracked = visible && marker.Tracked };
    }

    public static IReadOnlyList<QuestDiagnostic> Validate(QuestDefinition quest, QuestExperienceDefinition experience, IReadOnlySet<string> availableReferences, IReadOnlySet<string> availableOwners, IReadOnlySet<string> transitionIds)
    {
        var root = $"quests/{quest.Id}/experience"; var result = new List<QuestDiagnostic>();
        if (experience.Resources.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != experience.Resources.Count || experience.Resources.Any(item => string.IsNullOrWhiteSpace(item.Id) || !availableReferences.Contains(item.ReferenceId) || !availableOwners.Contains(item.OwnerId))) result.Add(new("RPG_QUEST_RESOURCE_INVALID", $"{root}/resources", "Resource identities, references and ownership must resolve."));
        if (experience.Steps.Count == 0 || experience.Steps.Select(item => item.Id).Distinct().Count() != experience.Steps.Count || experience.Steps.Any(item => item.Id == Guid.Empty || item.TemplateVersion <= 0 || !item.Editable || item.TransitionIds.Any(id => !transitionIds.Contains(id)))) result.Add(new("RPG_QUEST_STEP_INVALID", $"{root}/steps", "Generated steps require provenance, editability and valid transitions."));
        if (experience.Visibility.RevealConditions.Any(item => item.Key <= experience.Visibility.Initial || item.Value.Count == 0)) result.Add(new("RPG_QUEST_VISIBILITY_INVALID", $"{root}/visibility", "Reveal states need ordered non-empty conditions."));
        var objectives = quest.Objectives.Select(item => item.Id).Concat(experience.Steps.SelectMany(item => item.Objectives).Select(item => item.Id)).ToHashSet();
        if (experience.Markers.Select(item => item.Id).Distinct().Count() != experience.Markers.Count || experience.Markers.Any(item => item.Id == Guid.Empty || !objectives.Contains(item.ObjectiveId) || string.IsNullOrWhiteSpace(item.BindingValue) || item.PrecisionRadius < 0 || !availableReferences.Contains(item.BindingValue))) result.Add(new("RPG_QUEST_MARKER_INVALID", $"{root}/markers", "Markers require valid objective, binding and precision."));
        return result;
    }

    private static Guid StableObjectiveId(string questSlug, string templateId, string label) { var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"openrpg:objective:{questSlug}:{templateId}:{label}")); return new(hash[..16]); }
}
