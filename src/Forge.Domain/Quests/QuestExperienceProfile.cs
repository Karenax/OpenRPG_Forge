using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public enum QuestExperienceResourceKind { Item, Placeable, Door, Zone, Asset }
public enum QuestExperienceVisibility { Hidden, Rumor, Discovered, Tracked, SecretCompleted }
public enum QuestExperienceMarkerBindingKind { Zone, Entity, Query, Waypoint }
public enum QuestExperienceHistoryView { Player, Debug }

public sealed record QuestExperienceResource(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder,
    QuestExperienceResourceKind Kind, string ReferenceId, string OwnerId, bool Required,
    IReadOnlyList<string> Conditions);

public sealed record QuestExperienceStepTemplate(
    Guid Id, int SchemaVersion, string Provenance, string TemplateId, int TemplateVersion,
    LocalizedText Label, IReadOnlyList<Guid> ObjectiveIds, IReadOnlyList<QuestTransitionId> TransitionIds,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record QuestExperienceStep(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder, LocalizedText Label,
    IReadOnlyList<Guid> ObjectiveIds, IReadOnlyList<QuestTransitionId> TransitionIds,
    string TemplateId, int TemplateVersion, bool Editable);

public sealed record QuestExperienceVisibilityPolicy(
    Guid Id, int SchemaVersion, string Provenance, QuestExperienceVisibility Initial,
    IReadOnlyDictionary<QuestExperienceVisibility, IReadOnlyList<string>> RevealConditions,
    bool AllowManualTracking);

public sealed record QuestExperienceHistoryEvent(
    Guid Id, int SchemaVersion, string Provenance, QuestId QuestId, string FromState, string ToState,
    string Cause, string ActorId, DateTimeOffset At, IReadOnlyDictionary<string, string> DebugContext,
    LocalizedText PlayerSummary);

public sealed record QuestExperienceMarker(
    Guid Id, int SchemaVersion, string Provenance, int DisplayOrder, Guid ObjectiveId,
    QuestExperienceMarkerBindingKind BindingKind, string BindingValue,
    QuestExperienceVisibility Visibility, double PrecisionRadius, bool Tracked,
    LocalizedText Label, Vector3Value? FallbackPosition);

public sealed record QuestExperienceProfile(
    Guid Id, int SchemaVersion, string Provenance, bool Enabled, bool RequiredAtRuntime,
    IReadOnlyList<QuestExperienceResource> Resources,
    IReadOnlyList<QuestExperienceStep> Steps,
    QuestExperienceVisibilityPolicy Visibility,
    IReadOnlyList<QuestExperienceHistoryEvent> History,
    IReadOnlyList<QuestExperienceMarker> Markers);

public sealed record QuestExperienceHistoryItem(
    DateTimeOffset At, string Summary, string? FromState, string? ToState,
    string? Cause, string? ActorId, IReadOnlyDictionary<string, string>? DebugContext);

public static class QuestExperienceRuntimeService
{
    public static QuestExperienceStep Instantiate(
        QuestExperienceStepTemplate template,
        QuestId questId,
        int displayOrder)
    {
        ArgumentNullException.ThrowIfNull(template);
        var id = StableId($"openrpg:quest-experience-step:{questId}:{template.TemplateId}:{template.TemplateVersion}");
        return new(id, 1, $"{template.Provenance}:generated", displayOrder, template.Label,
            template.ObjectiveIds.ToArray(), template.TransitionIds.ToArray(), template.TemplateId,
            template.TemplateVersion, true);
    }

    public static QuestExperienceVisibility Reveal(
        QuestExperienceVisibilityPolicy policy,
        QuestExperienceVisibility current,
        IReadOnlySet<string> state)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(state);
        var best = current;
        foreach (var candidate in Enum.GetValues<QuestExperienceVisibility>().Where(item => item > current))
        {
            if (!policy.RevealConditions.TryGetValue(candidate, out var conditions) ||
                conditions.Count == 0 || !conditions.All(state.Contains)) break;
            best = candidate;
        }
        return best;
    }

    public static IReadOnlyList<QuestExperienceHistoryItem> ReadHistory(
        QuestExperienceProfile profile,
        QuestExperienceHistoryView view)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.History.OrderBy(item => item.At).ThenBy(item => item.Id).Select(item =>
            view == QuestExperienceHistoryView.Player
                ? new QuestExperienceHistoryItem(item.At, item.PlayerSummary.Fallback, null, null, null, null, null)
                : new QuestExperienceHistoryItem(item.At, item.PlayerSummary.Fallback, item.FromState, item.ToState,
                    item.Cause, item.ActorId, item.DebugContext)).ToArray();
    }

    public static QuestExperienceMarker ResolveMarker(
        QuestExperienceMarker marker,
        QuestExperienceVisibility questVisibility,
        bool objectiveActive) => marker with
        {
            Tracked = marker.Tracked && objectiveActive && marker.Visibility <= questVisibility,
        };

    private static Guid StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash[..16]);
    }
}

public static class QuestExperienceProfileValidator
{
    public const string RequiredMissingCode = "quest.experience.required_missing";
    public const string IdentityInvalidCode = "quest.experience.identity_invalid";
    public const string ResourceInvalidCode = "quest.experience.resource_invalid";
    public const string StepInvalidCode = "quest.experience.step_invalid";
    public const string VisibilityInvalidCode = "quest.experience.visibility_invalid";
    public const string HistoryInvalidCode = "quest.experience.history_invalid";
    public const string MarkerInvalidCode = "quest.experience.marker_invalid";
    public const string ReferenceMissingCode = "quest.experience.reference_missing";

    public static IReadOnlyList<DomainDiagnostic> Validate(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var profile = quest.Experience;
        var path = $"quests/{quest.Id}/experience";
        if (profile is null) return [];
        if (!profile.Enabled)
            return profile.RequiredAtRuntime
                ? [new(RequiredMissingCode, "Required quest experience profile is disabled.", path)]
                : [];

        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Quest experience identity, schema or provenance is invalid.", path));

        var objectiveIds = quest.Authoring?.Objectives.Select(item => item.Id).ToHashSet() ?? [];
        var transitionIds = quest.Transitions.Select(item => item.Id).ToHashSet();
        if (profile.Resources.Count == 0 || HasDuplicateIdentityOrOrder(profile.Resources.Select(item => (item.Id, item.DisplayOrder))) ||
            profile.Resources.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || item.DisplayOrder < 1 ||
                string.IsNullOrWhiteSpace(item.ReferenceId) || string.IsNullOrWhiteSpace(item.OwnerId) ||
                item.Conditions.Any(string.IsNullOrWhiteSpace)))
            diagnostics.Add(new(ResourceInvalidCode, "Quest resources require unique identity, order, ownership and typed references.", $"{path}/resources"));

        if (profile.Steps.Count == 0 || HasDuplicateIdentityOrOrder(profile.Steps.Select(item => (item.Id, item.DisplayOrder))) ||
            profile.Steps.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || item.DisplayOrder < 1 || item.Label.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.Label.Key) || string.IsNullOrWhiteSpace(item.Label.Fallback) ||
                string.IsNullOrWhiteSpace(item.TemplateId) || item.TemplateVersion < 1 || !item.Editable ||
                item.ObjectiveIds.Count == 0 || item.ObjectiveIds.Any(id => !objectiveIds.Contains(id)) ||
                item.TransitionIds.Count == 0 || item.TransitionIds.Any(id => !transitionIds.Contains(id))))
            diagnostics.Add(new(StepInvalidCode, "Generated quest steps require editable objectives, transitions and provenance.", $"{path}/steps"));

        var visibility = profile.Visibility;
        if (visibility.Id == Guid.Empty || visibility.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(visibility.Provenance) || visibility.Initial != QuestExperienceVisibility.Hidden ||
            visibility.RevealConditions.Count == 0 || visibility.RevealConditions.Any(item =>
                item.Key <= visibility.Initial || item.Value.Count == 0 || item.Value.Any(string.IsNullOrWhiteSpace)))
            diagnostics.Add(new(VisibilityInvalidCode, "Visibility requires ordered non-empty reveal conditions from Hidden.", $"{path}/visibility"));

        if (profile.History.Count == 0 || profile.History.Select(item => item.Id).Distinct().Count() != profile.History.Count ||
            profile.History.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || item.QuestId != quest.Id ||
                string.IsNullOrWhiteSpace(item.FromState) || string.IsNullOrWhiteSpace(item.ToState) ||
                string.IsNullOrWhiteSpace(item.Cause) || string.IsNullOrWhiteSpace(item.ActorId) ||
                item.PlayerSummary.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.PlayerSummary.Key) ||
                string.IsNullOrWhiteSpace(item.PlayerSummary.Fallback)))
            diagnostics.Add(new(HistoryInvalidCode, "Quest history requires unique timestamped transitions, causes, actors and localized summaries.", $"{path}/history"));

        if (profile.Markers.Count == 0 || HasDuplicateIdentityOrOrder(profile.Markers.Select(item => (item.Id, item.DisplayOrder))) ||
            profile.Markers.Any(item => item.Id == Guid.Empty || item.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(item.Provenance) || item.DisplayOrder < 1 ||
                !objectiveIds.Contains(item.ObjectiveId) || string.IsNullOrWhiteSpace(item.BindingValue) ||
                !double.IsFinite(item.PrecisionRadius) || item.PrecisionRadius < 0 ||
                item.Label.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Label.Key) ||
                string.IsNullOrWhiteSpace(item.Label.Fallback)))
            diagnostics.Add(new(MarkerInvalidCode, "Objective markers require unique identity, objective, binding, visibility and precision.", $"{path}/markers"));
        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateReferences(ProjectDefinition project, QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(quest);
        var profile = quest.Experience;
        if (profile is null || !profile.Enabled) return [];
        var path = $"quests/{quest.Id}/experience";
        var diagnostics = new List<DomainDiagnostic>();
        var areas = project.Areas.SelectMany(item => new[] { item.Id.ToString(), item.Slug })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entities = project.Areas.SelectMany(item => item.Entities).Select(item => item.Id.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blueprints = project.Blueprints.ToArray();
        bool BlueprintExists(string value, BlueprintKind kind) => blueprints.Any(item => item.Kind == kind &&
            (string.Equals(item.Id.ToString(), value, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(item.Slug, value, StringComparison.OrdinalIgnoreCase)));
        bool ResourceExists(QuestExperienceResource resource) => resource.Kind switch
        {
            QuestExperienceResourceKind.Item => BlueprintExists(resource.ReferenceId, BlueprintKind.Item),
            QuestExperienceResourceKind.Placeable => BlueprintExists(resource.ReferenceId, BlueprintKind.Placeable),
            QuestExperienceResourceKind.Door => BlueprintExists(resource.ReferenceId, BlueprintKind.Door),
            QuestExperienceResourceKind.Zone => areas.Contains(resource.ReferenceId),
            QuestExperienceResourceKind.Asset => resource.ReferenceId.StartsWith("asset:", StringComparison.Ordinal),
            _ => false,
        };
        foreach (var resource in profile.Resources.Where(item => item.Required && !ResourceExists(item)))
            diagnostics.Add(new(ReferenceMissingCode, $"Required quest resource '{resource.ReferenceId}' does not resolve as {resource.Kind}.",
                $"{path}/resources/{resource.Id}/referenceId"));
        foreach (var marker in profile.Markers)
        {
            var resolved = marker.BindingKind switch
            {
                QuestExperienceMarkerBindingKind.Zone => areas.Contains(marker.BindingValue),
                QuestExperienceMarkerBindingKind.Entity => entities.Contains(marker.BindingValue),
                QuestExperienceMarkerBindingKind.Query => marker.BindingValue.StartsWith("query:", StringComparison.Ordinal),
                QuestExperienceMarkerBindingKind.Waypoint => marker.BindingValue.StartsWith("waypoint:", StringComparison.Ordinal),
                _ => false,
            };
            if (!resolved) diagnostics.Add(new(ReferenceMissingCode,
                $"Quest marker binding '{marker.BindingValue}' does not resolve as {marker.BindingKind}.",
                $"{path}/markers/{marker.Id}/bindingValue"));
        }
        return diagnostics;
    }

    private static bool HasDuplicateIdentityOrOrder(IEnumerable<(Guid Id, int Order)> values)
    {
        var materialized = values.ToArray();
        return materialized.Select(item => item.Id).Distinct().Count() != materialized.Length ||
               materialized.Select(item => item.Order).Distinct().Count() != materialized.Length;
    }
}
