using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class SetAreaWorldProfileHandler
{
    private readonly IClock _clock;

    public SetAreaWorldProfileHandler(IClock clock) => _clock = clock;

    public CommandResult Handle(ProjectState state, SetAreaWorldProfileCommand command)
    {
        if (command.BaseRevision != state.Revision)
        {
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        }

        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null)
        {
            return Fail(state, command, "area.world.area_missing", "Area does not exist.");
        }

        var diagnostics = AreaWorldValidator.Validate(area, command.World);
        if (diagnostics.Count > 0)
        {
            return new CommandResult(false, state, command.BaseRevision, null,
                diagnostics.Select(item => new CommandDiagnostic(item.Code, item.Message)).ToArray(), null, null,
                new AuditEntry(command.CommandId, "set_area_world_profile", _clock.UtcNow, false));
        }

        var patch = new ProjectPatch("replace", $"areas/{area.Id}/world",
            "Set terrain, biomes, parametric features, volumetric navigation, flight routes and streaming.");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/world", "Restore previous area world profile.");
        var audit = new AuditEntry(command.CommandId, "set_area_world_profile", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, audit);
        }

        var updated = area.WithWorld(command.World);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetAreaWorldProfileCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_area_world_profile", _clock.UtcNow, false));
}
