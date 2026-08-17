using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.RpgKit.Domain.Doors;

namespace Forge.Application.Navigation;

public sealed class SetNavigationTopologyHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string EmptyMutationCode = "navigation.topology.empty";
    public const string DuplicateAreaUpdateCode = "navigation.topology.area_duplicate";
    public const string DuplicatePortalMutationCode = "navigation.topology.portal_duplicate";
    public const string MissingAreaCode = "navigation.topology.area_missing";
    public const string PortalMutationInvalidCode = "navigation.topology.portal_mutation_invalid";
    public const string MissingPortalCode = "navigation.topology.portal_missing";
    public const string CatalogIdentityCode = "navigation.topology.catalog_id_mismatch";

    private const string CommandName = "set_navigation_topology";
    private readonly IClock _clock;

    public SetNavigationTopologyHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetNavigationTopologyCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.NavigationUpdates);
        ArgumentNullException.ThrowIfNull(command.PortalMutations);
        if (command.BaseRevision != state.Revision)
        {
            return Fail(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        if (command.NavigationUpdates.Count == 0 && command.PortalMutations.Count == 0 &&
            command.WorldFeatures is null)
        {
            return Fail(state, command, EmptyMutationCode, "The navigation topology mutation is empty.");
        }

        if (command.NavigationUpdates.GroupBy(item => item.AreaId).Any(group => group.Count() > 1))
        {
            return Fail(state, command, DuplicateAreaUpdateCode,
                "An area navigation profile can only be replaced once per topology command.");
        }

        if (command.PortalMutations.GroupBy(item => (item.SourceAreaId, item.PortalId))
            .Any(group => group.Count() > 1))
        {
            return Fail(state, command, DuplicatePortalMutationCode,
                "A portal can only be mutated once per topology command.");
        }

        var updates = command.NavigationUpdates.ToDictionary(item => item.AreaId);
        var portalMutations = command.PortalMutations.GroupBy(item => item.SourceAreaId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var knownAreaIds = state.Project.Areas.Select(item => item.Id).ToHashSet();
        var missingArea = updates.Keys.Concat(portalMutations.Keys).FirstOrDefault(id => !knownAreaIds.Contains(id));
        if (missingArea is not null)
        {
            return Fail(state, command, MissingAreaCode, $"Area '{missingArea}' does not exist.");
        }

        var areas = new List<AreaDefinition>(state.Project.Areas.Count);
        foreach (var source in state.Project.Areas)
        {
            var area = updates.TryGetValue(source.Id, out var update)
                ? source.WithNavigation(update.Navigation)
                : source;
            if (portalMutations.TryGetValue(source.Id, out var operations))
            {
                var result = ApplyPortalMutations(area, operations);
                if (!result.Succeeded)
                {
                    return Fail(state, command, result.Code!, result.Message!);
                }
                area = result.Area!;
            }
            areas.Add(area);
        }

        var replacementWorldFeatures = command.WorldFeatures;
        if (replacementWorldFeatures is { } worldFeatures &&
            worldFeatures.Id != state.Project.Module.Id.Value)
        {
            return Fail(state, command, CatalogIdentityCode,
                "The world-feature catalog ID must match the module ID.");
        }

        var module = replacementWorldFeatures is null
            ? state.Project.Module
            : state.Project.Module.WithWorldFeatures(replacementWorldFeatures);
        var candidate = new ProjectDefinition(state.Project.Id, state.Project.Title, module,
            state.Project.Blueprints, areas, state.Project.Dialogues, state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(candidate).ToList();
        if (replacementWorldFeatures is not null)
        {
            diagnostics.AddRange(candidate.Module.WorldFeatures.Doors
                .Where(item => item.Validation is not null || item.ValidationRequiredAtRuntime)
                .SelectMany(item => DoorWorldValidationService.Validate(candidate, item.Id).Diagnostics)
                .Select(item => new DomainDiagnostic(item.Code, item.Message, item.Path)));
        }

        var existingTopologyDiagnostics = NavigationTopologyValidator.Validate(state.Project)
            .Select(Key)
            .ToHashSet();
        var changedFeaturePaths = ChangedWorldFeaturePaths(state.Project, candidate);
        diagnostics.AddRange(NavigationTopologyValidator.Validate(candidate)
            .Where(item => !existingTopologyDiagnostics.Contains(Key(item)) ||
                           changedFeaturePaths.Any(prefix => item.Path.StartsWith(prefix, StringComparison.Ordinal))));
        var distinct = diagnostics.DistinctBy(Key)
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
        if (distinct.Length > 0)
        {
            return new CommandResult(false, state, command.BaseRevision, null,
                distinct.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                null, null, Audit(command, false));
        }

        var summary = $"navigation={command.NavigationUpdates.Count};" +
                      $"portal_upserts={command.PortalMutations.Count(item => item.Kind == NavigationPortalMutationKind.Upsert)};" +
                      $"portal_removals={command.PortalMutations.Count(item => item.Kind == NavigationPortalMutationKind.Remove)};" +
                      $"world_features={(command.WorldFeatures is null ? "preserved" : "replaced")}";
        var patch = new ProjectPatch("replace", "navigation/topology", summary);
        var inverse = new ProjectPatch("replace", "navigation/topology", "Restore previous navigation topology.");
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                Audit(command, false));
        }

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(candidate, revision), command.BaseRevision, revision, [],
            patch, inverse, Audit(command, true));
    }

    private static (bool Succeeded, AreaDefinition? Area, string? Code, string? Message) ApplyPortalMutations(
        AreaDefinition area, IReadOnlyList<NavigationPortalMutation> mutations)
    {
        var portals = area.Portals.ToList();
        foreach (var mutation in mutations)
        {
            var index = portals.FindIndex(item => item.Id == mutation.PortalId);
            if (mutation.Kind == NavigationPortalMutationKind.Upsert)
            {
                if (mutation.Portal is null || mutation.Portal.Id != mutation.PortalId)
                {
                    return (false, null, PortalMutationInvalidCode,
                        $"Portal upsert '{mutation.PortalId}' requires a matching portal payload.");
                }
                if (index < 0)
                {
                    portals.Add(mutation.Portal);
                }
                else
                {
                    portals[index] = mutation.Portal;
                }
            }
            else if (mutation.Kind == NavigationPortalMutationKind.Remove)
            {
                if (mutation.Portal is not null)
                {
                    return (false, null, PortalMutationInvalidCode,
                        $"Portal removal '{mutation.PortalId}' cannot carry an upsert payload.");
                }
                if (index < 0)
                {
                    return (false, null, MissingPortalCode,
                        $"Portal '{mutation.PortalId}' does not exist in area '{area.Id}'.");
                }
                portals.RemoveAt(index);
            }
            else
            {
                return (false, null, PortalMutationInvalidCode,
                    $"Portal mutation kind '{mutation.Kind}' is not supported.");
            }
        }

        return (true, area.WithPortals(portals), null, null);
    }

    private static (string Code, string Path, string Message) Key(DomainDiagnostic diagnostic) =>
        (diagnostic.Code, diagnostic.Path, diagnostic.Message);

    private static IReadOnlyList<string> ChangedWorldFeaturePaths(ProjectDefinition before,
        ProjectDefinition after)
    {
        var paths = new List<string>();
        var beforeDoors = before.Module.WorldFeatures.Doors.ToDictionary(item => item.Id);
        foreach (var door in after.Module.WorldFeatures.Doors)
        {
            if (!beforeDoors.TryGetValue(door.Id, out var previous) || !Equals(previous, door))
            {
                paths.Add($"module/worldFeatures/doors/{door.Id}/");
            }
        }
        var beforeTriggers = before.Module.WorldFeatures.Triggers.ToDictionary(item => item.Id);
        foreach (var trigger in after.Module.WorldFeatures.Triggers)
        {
            if (!beforeTriggers.TryGetValue(trigger.Id, out var previous) || !Equals(previous, trigger))
            {
                paths.Add($"module/worldFeatures/triggers/{trigger.Id}/");
            }
        }
        return paths;
    }

    private CommandResult Fail(ProjectState state, SetNavigationTopologyCommand command, string code,
        string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)],
        null, null, Audit(command, false));

    private AuditEntry Audit(SetNavigationTopologyCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
