using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
namespace Forge.Application.Areas;

public sealed class SetAreaAuthoringHandler
{
    private readonly IClock _clock; public SetAreaAuthoringHandler(IClock clock) => _clock = clock;
    public CommandResult Handle(ProjectState state, SetAreaAuthoringCommand command)
    {
        if (command.BaseRevision != state.Revision) return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId); if (area is null) return Fail(state, command, "area.authoring.area_missing", "Area does not exist.");
        var entityIds = area.Entities.Select(item => item.Id).ToHashSet(); if (command.Authoring.Layers.SelectMany(item => item.EntityIds).Any(id => !entityIds.Contains(id))) return Fail(state, command, "area.layer.entity_missing", "Layer references an entity outside the area.");
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/authoring", "Set events, map and editor layers."); var inverse = new ProjectPatch("replace", $"areas/{area.Id}/authoring", "Restore previous authoring profile.");
        var audit = new AuditEntry(command.CommandId, "set_area_authoring", _clock.UtcNow, !command.DryRun); if (command.DryRun) return new(true, state, command.BaseRevision, null, [], patch, inverse, audit);
        var updated = area.WithAuthoring(command.Authoring); var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module, state.Project.Blueprints, state.Project.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(), state.Project.Dialogues, state.Project.Quests); var revision = state.Revision.Next(); return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse, audit);
    }
    private CommandResult Fail(ProjectState state, SetAreaAuthoringCommand command, string code, string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null, new AuditEntry(command.CommandId, "set_area_authoring", _clock.UtcNow, false));
}
