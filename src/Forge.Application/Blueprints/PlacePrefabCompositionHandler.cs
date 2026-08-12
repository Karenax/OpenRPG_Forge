using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class PlacePrefabCompositionHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string InvalidCompositionCode = "blueprint.composition.placement_invalid";
    private const string CommandName = "place_prefab_composition";
    private readonly IClock _clock;

    public PlacePrefabCompositionHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, PlacePrefabCompositionCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (state.Revision != command.BaseRevision)
            return Failure(state, command, RevisionConflictCode, "Project revision changed before composition placement.");
        PrefabCompositionPlacementPlan plan;
        try
        {
            plan = PrefabCompositionPlacementPlanner.Build(state.Project, command.BlueprintId, command.AreaId,
                command.Origin, command.CommandId.Value);
        }
        catch (ArgumentException exception)
        {
            return Failure(state, command, InvalidCompositionCode, exception.Message);
        }
        var patch = new ProjectPatch("add", $"areas/{command.AreaId}/entities",
            $"Place {plan.Entities.Count} composition nodes atomically from blueprint {command.BlueprintId}.");
        var inverse = new ProjectPatch("remove", $"areas/{command.AreaId}/entities",
            $"Remove {plan.Entities.Count} composition nodes.");
        if (command.DryRun)
            return new(true, state, state.Revision, null, [], patch, inverse, Audit(command, false));
        var ids = plan.Entities.Select(item => item.Id).ToHashSet();
        var areas = state.Project.Areas.Select(area => area.Id == command.AreaId
            ? area.WithEntities([.. area.Entities.Where(item => !ids.Contains(item.Id)), .. plan.Entities])
            : area).ToArray();
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), state.Revision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, PlacePrefabCompositionCommand command, string code,
        string message) => new(false, state, state.Revision, null, [new CommandDiagnostic(code, message)], null, null,
        Audit(command, false));

    private AuditEntry Audit(PlacePrefabCompositionCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
