using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Entities;

public sealed class SetEntityOverridesHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "command.area_missing";
    public const string MissingEntityCode = "command.entity_missing";

    private const string CommandName = "set_entity_overrides";
    private readonly IClock _clock;

    public SetEntityOverridesHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetEntityOverridesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Overrides);

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

        var entity = area.Entities.SingleOrDefault(candidate => candidate.Id == command.EntityId);
        if (entity is null)
        {
            return Failure(
                state,
                command,
                MissingEntityCode,
                $"Entity '{command.EntityId}' does not exist in area '{command.AreaId}'.");
        }

        var path = $"areas/{area.Id}/entities/{command.EntityId}/overrides";
        var patch = new ProjectPatch(
            "replace",
            path,
            $"Set {command.Overrides.Properties.Count} local properties on entity '{command.EntityId}'.");
        var inverse = new ProjectPatch(
            "replace",
            path,
            $"Restore {entity.Overrides.Properties.Count} local properties on entity '{command.EntityId}'.");

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

        var updatedEntity = new EntityInstance(
            entity.Id,
            entity.BlueprintId,
            entity.Transform,
            entity.Tags,
            command.Overrides);
        var updatedArea = area.WithEntities(
            area.Entities.Select(candidate => candidate.Id == entity.Id ? updatedEntity : candidate).ToArray());
        var updatedProject = ReplaceArea(state.Project, updatedArea);
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

    private static ProjectDefinition ReplaceArea(ProjectDefinition project, AreaDefinition updatedArea) =>
        new(
            project.Id,
            project.Title,
            project.Module,
            project.Blueprints,
            project.Areas.Select(area => area.Id == updatedArea.Id ? updatedArea : area).ToArray(),
            project.Dialogues,
            project.Quests);

    private CommandResult Failure(
        ProjectState state,
        SetEntityOverridesCommand command,
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

    private AuditEntry CreateAudit(SetEntityOverridesCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
