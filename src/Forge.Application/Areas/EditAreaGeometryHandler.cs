using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class EditAreaGeometryHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "area.edit.missing";
    public const string DuplicateEntityCode = "area.edit.entity_duplicate";
    public const string MissingBlueprintCode = "area.edit.blueprint_missing";
    private readonly IClock _clock;
    public EditAreaGeometryHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, EditAreaGeometryCommand command)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision) return Fail(state, command, RevisionConflictCode, "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null) return Fail(state, command, MissingAreaCode, "Area does not exist.");
        if (command.Entities.Select(item => item.Id).Distinct().Count() != command.Entities.Count)
            return Fail(state, command, DuplicateEntityCode, "Edited entities must have unique IDs.");
        var blueprintIds = state.Project.Blueprints.Select(item => item.Id).ToHashSet();
        if (command.Entities.Any(item => !blueprintIds.Contains(item.BlueprintId)))
            return Fail(state, command, MissingBlueprintCode, "Edited entities must reference existing blueprints.");
        var world = command.Dimensions == area.Dimensions
            ? area.World
            : Domain.Areas.AreaWorldProfile.ResizeForArea(area.World, command.Dimensions);
        var updatedArea = new Domain.Areas.AreaDefinition(area.Id, area.Slug, area.DisplayName, command.Entities,
            area.Kind, command.Dimensions, area.Portals, area.SchemaVersion, area.Navigation, area.Environment,
            area.Authoring, world);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas.Select(item => item.Id == area.Id ? updatedArea : item).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var invalid = ProjectValidator.Validate(project).FirstOrDefault();
        if (invalid is not null) return Fail(state, command, invalid.Code, invalid.Message);
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/geometry", command.Reason.Trim());
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/geometry", "Restore previous area geometry.");
        var audit = new AuditEntry(command.CommandId, "edit_area_geometry", _clock.UtcNow, !command.DryRun);
        if (command.DryRun) return new(true, state, command.BaseRevision, null, [], patch, inverse, audit with { Applied = false });
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse, audit);
    }

    private CommandResult Fail(ProjectState state, EditAreaGeometryCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "edit_area_geometry", _clock.UtcNow, false));
}
