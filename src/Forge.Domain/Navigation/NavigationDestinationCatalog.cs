using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Navigation;

public enum NavigationDestinationKind
{
    AreaEntity,
    Door,
    Trigger,
    Waypoint,
    SpawnAnchor,
}

public sealed record NavigationDestinationReference(
    NavigationDestinationKind Kind,
    Guid Id,
    AreaId? AreaId,
    string HumanLabel,
    string Slug,
    string Provenance,
    string? ArrivalAnchorKey = null);

public sealed class NavigationDestinationCatalog
{
    private readonly IReadOnlyDictionary<(NavigationDestinationKind Kind, Guid Id), NavigationDestinationReference>
        _byIdentity;
    private readonly IReadOnlyDictionary<string, NavigationDestinationReference> _arrivalAnchors;

    public IReadOnlyList<NavigationDestinationReference> Entries { get; }

    private NavigationDestinationCatalog(IReadOnlyList<NavigationDestinationReference> entries)
    {
        Entries = entries;
        _byIdentity = entries.GroupBy(item => (item.Kind, item.Id))
            .ToDictionary(group => group.Key, group => group.First());
        _arrivalAnchors = entries
            .Where(item => item.Kind == NavigationDestinationKind.SpawnAnchor &&
                           !string.IsNullOrWhiteSpace(item.ArrivalAnchorKey))
            .GroupBy(item => item.ArrivalAnchorKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    public static NavigationDestinationCatalog Build(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var candidates = new List<Candidate>();
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);

        foreach (var area in project.Areas)
        {
            foreach (var entity in area.Entities)
            {
                blueprints.TryGetValue(entity.BlueprintId, out var blueprint);
                candidates.Add(new(
                    NavigationDestinationKind.AreaEntity,
                    entity.Id.Value,
                    area.Id,
                    $"Objet · {Human(blueprint?.DisplayName, "Objet protégé")}",
                    HumanSlug(blueprint?.Slug, area.Slug),
                    blueprint?.Provenance.Source ?? "project-area",
                    null,
                    area.Slug,
                    0));
            }

            var definitions = area.Navigation.Anchors.Definitions.GroupBy(item => item.Id)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var instance in area.Navigation.Anchors.Instances)
            {
                if (!definitions.TryGetValue(instance.DefinitionId, out var definition))
                {
                    continue;
                }

                if (definition.Kind == AnchorKind.Spawn)
                {
                    candidates.Add(new(
                        NavigationDestinationKind.SpawnAnchor,
                        instance.Id,
                        area.Id,
                        $"Apparition · {Human(definition.Label.Fallback, "Ancre protégée")}",
                        HumanSlug(definition.Slug, area.Slug),
                        definition.Provenance,
                        definition.Slug,
                        area.Slug,
                        instance.DisplayOrder));
                }
                else if (definition.Kind is AnchorKind.Waypoint or AnchorKind.TransitionDestination)
                {
                    candidates.Add(new(
                        NavigationDestinationKind.Waypoint,
                        instance.Id,
                        area.Id,
                        $"Ancre · {Human(definition.Label.Fallback, "Point protégé")}",
                        HumanSlug(definition.Slug, area.Slug),
                        definition.Provenance,
                        null,
                        area.Slug,
                        instance.DisplayOrder));
                }
            }

            foreach (var waypoint in area.Navigation.Waypoints.Waypoints)
            {
                candidates.Add(new(
                    NavigationDestinationKind.Waypoint,
                    waypoint.Id,
                    area.Id,
                    $"Waypoint · {Human(waypoint.Name, "Point protégé")}",
                    area.Slug,
                    waypoint.Provenance,
                    null,
                    area.Slug,
                    0));
            }
        }

        var world = project.Module.WorldFeatures;
        foreach (var door in world.Doors)
        {
            blueprints.TryGetValue(door.BlueprintId, out var blueprint);
            candidates.Add(new(
                NavigationDestinationKind.Door,
                door.Id,
                null,
                $"Porte · {Human(blueprint?.DisplayName, "Porte protégée")}",
                HumanSlug(blueprint?.Slug, "porte-protegee"),
                door.Provenance,
                null,
                string.Empty,
                0));
        }

        foreach (var trigger in world.Triggers)
        {
            blueprints.TryGetValue(trigger.BlueprintId, out var blueprint);
            candidates.Add(new(
                NavigationDestinationKind.Trigger,
                trigger.Id,
                trigger.AreaId,
                $"Déclencheur · {Human(blueprint?.DisplayName, "Déclencheur protégé")}",
                HumanSlug(blueprint?.Slug, "declencheur-protege"),
                trigger.Provenance,
                null,
                project.Areas.FirstOrDefault(item => item.Id == trigger.AreaId)?.Slug ?? string.Empty,
                0));
        }

        var entries = candidates
            .GroupBy(item => item.Kind)
            .SelectMany(Disambiguate)
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.HumanLabel, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Id)
            .ToArray();
        return new NavigationDestinationCatalog(entries);
    }

    public NavigationDestinationReference? Find(NavigationDestinationKind kind, Guid id) =>
        _byIdentity.GetValueOrDefault((kind, id));

    public NavigationDestinationReference? FindArrivalAnchor(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : _arrivalAnchors.GetValueOrDefault(key.Trim());

    private static IEnumerable<NavigationDestinationReference> Disambiguate(
        IGrouping<NavigationDestinationKind, Candidate> kindGroup)
    {
        foreach (var labelGroup in kindGroup.GroupBy(item => item.BaseLabel,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            var ordered = labelGroup.OrderBy(item => item.AreaSlug, StringComparer.Ordinal)
                .ThenBy(item => item.Slug, StringComparer.Ordinal)
                .ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id)
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var item = ordered[index];
                var label = item.BaseLabel;
                if (!string.IsNullOrWhiteSpace(item.AreaSlug))
                {
                    label = $"{label} · {item.AreaSlug}";
                }
                if (ordered.Length > 1 &&
                    !string.Equals(item.Slug, item.AreaSlug, StringComparison.Ordinal))
                {
                    label += $" · {item.Slug}";
                }
                if (ordered.Count(candidate =>
                        string.Equals(candidate.AreaSlug, item.AreaSlug, StringComparison.Ordinal) &&
                        string.Equals(candidate.Slug, item.Slug, StringComparison.Ordinal)) > 1)
                {
                    label += $" · {index + 1}";
                }

                yield return new NavigationDestinationReference(item.Kind, item.Id, item.AreaId, label,
                    item.Slug, item.Provenance, item.ArrivalAnchorKey);
            }
        }
    }

    private static string Human(string? value, string fallback)
    {
        var candidate = value?.Trim();
        return string.IsNullOrWhiteSpace(candidate) || Guid.TryParse(candidate, out _) ? fallback : candidate;
    }

    private static string HumanSlug(string? value, string fallback)
    {
        var candidate = value?.Trim();
        return string.IsNullOrWhiteSpace(candidate) || Guid.TryParse(candidate, out _) ? fallback : candidate;
    }

    private sealed record Candidate(
        NavigationDestinationKind Kind,
        Guid Id,
        AreaId? AreaId,
        string BaseLabel,
        string Slug,
        string Provenance,
        string? ArrivalAnchorKey,
        string AreaSlug,
        int DisplayOrder);
}
