using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Entities;

public sealed class SetEntityTagsHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "command.area_missing";
    public const string MissingEntityCode = "command.entity_missing";
    public const string InvalidTagCode = "command.entity_tag_invalid";

    private const string CommandName = "set_entity_tags";
    private readonly IClock _clock;

    public SetEntityTagsHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetEntityTagsCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Tags);

        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "The project revision has changed.");
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

        if (command.Tags.Any(string.IsNullOrWhiteSpace))
        {
            return Failure(state, command, InvalidTagCode, "Entity tags cannot be empty.");
        }

        var tags = command.Tags
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var path = $"areas/{area.Id}/entities/{command.EntityId}/tags";
        var patch = new ProjectPatch("replace", path, $"Set {tags.Length} tags on entity '{command.EntityId}'.");
        var inverse = new ProjectPatch(
            "replace",
            path,
            $"Restore {entity.Tags.Count} tags on entity '{command.EntityId}'.");

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
                Audit(command, false));
        }

        var updatedEntity = new EntityInstance(
            entity.Id,
            entity.BlueprintId,
            entity.Transform,
            tags,
            entity.Overrides,
            entity.BlueprintVersionRequirement);
        var updatedArea = area.WithEntities(
            area.Entities.Select(candidate => candidate.Id == entity.Id ? updatedEntity : candidate).ToArray());
        var updatedProject = ReplaceArea(state.Project, updatedArea);
        var newRevision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(updatedProject, newRevision),
            command.BaseRevision,
            newRevision,
            [],
            patch,
            inverse,
            Audit(command, true));
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
        SetEntityTagsCommand command,
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
            Audit(command, false));

    private AuditEntry Audit(SetEntityTagsCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
