using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Application.Areas;

public sealed class SetAreaPlaytestHandler(IClock clock)
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string AreaMissingCode = "area.playtest.area_missing";

    public CommandResult Handle(ProjectState state, SetAreaPlaytestCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (state.Revision != command.BaseRevision)
            return Failure(state, command, RevisionConflictCode, "Project revision changed.");
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null)
            return Failure(state, command, AreaMissingCode, $"Area '{command.AreaId}' does not exist.");
        var diagnostics = AreaPlaytestProfileValidator.Validate(state.Project, area, command.Profile)
            .Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray();
        if (diagnostics.Length > 0)
            return new(false, state, command.BaseRevision, null, diagnostics, null, null, Audit(command, false));

        var patch = new ProjectPatch("replace", $"areas/{area.Id}/playtest",
            $"Configure Play From Here '{command.Profile.StartMode}' with profile '{command.Profile.TestProfile}'.");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/playtest",
            $"Restore Play From Here '{area.Playtest.StartMode}'.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        var updatedArea = area.WithPlaytest(command.Profile);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? updatedArea : item).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetAreaPlaytestCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            Audit(command, false));

    private AuditEntry Audit(SetAreaPlaytestCommand command, bool applied) =>
        new(command.CommandId, "set_area_playtest", clock.UtcNow, applied);
}
