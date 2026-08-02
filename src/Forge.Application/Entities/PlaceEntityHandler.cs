using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Entities;

public sealed class PlaceEntityHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "command.area_missing";
    public const string MissingBlueprintCode = "command.blueprint_missing";
    public const string DuplicateEntityCode = "command.entity_duplicate";

    private const string CommandName = "place_entity";
    private readonly IClock _clock;

    public PlaceEntityHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, PlaceEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (command.BaseRevision != state.Revision)
        {
            return Failure(
                state,
                command,
                RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var area = state.Project.Areas.SingleOrDefault(candidate => candidate.Id == command.AreaId);
        if (area is null)
        {
            return Failure(state, command, MissingAreaCode, $"Area '{command.AreaId}' does not exist.");
        }

        if (!state.Project.Blueprints.Any(blueprint => blueprint.Id == command.BlueprintId))
        {
            return Failure(
                state,
                command,
                MissingBlueprintCode,
                $"Blueprint '{command.BlueprintId}' does not exist.");
        }

        if (state.Project.Areas.SelectMany(candidate => candidate.Entities).Any(entity => entity.Id == command.EntityId))
        {
            return Failure(
                state,
                command,
                DuplicateEntityCode,
                $"Entity '{command.EntityId}' already exists.");
        }

        var path = $"areas/{area.Id}/entities/{command.EntityId}";
        var patch = new ProjectPatch("add", path, $"Place entity '{command.EntityId}'.");
        var inverse = new ProjectPatch("remove", path, $"Remove entity '{command.EntityId}'.");

        if (command.DryRun)
        {
            return new CommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                [],
                patch,
                inverse,
                CreateAudit(command, false));
        }

        var entity = new EntityInstance(command.EntityId, command.BlueprintId, command.Transform);
        var updatedArea = new AreaDefinition(
            area.Id,
            area.Slug,
            area.DisplayName,
            [.. area.Entities, entity]);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas.Select(candidate => candidate.Id == area.Id ? updatedArea : candidate).ToArray(),
            state.Project.Dialogues,
            state.Project.Quests);
        var newRevision = state.Revision.Next();
        var updatedState = new ProjectState(updatedProject, newRevision);

        return new CommandResult(
            true,
            updatedState,
            command.BaseRevision,
            newRevision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        PlaceEntityCommand command,
        string code,
        string message) =>
        new(
            false,
            state,
            command.BaseRevision,
            null,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(PlaceEntityCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
