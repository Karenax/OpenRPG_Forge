using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetCinematicProfilesHandler
{
    private readonly IClock _clock;

    public SetCinematicProfilesHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetCinematicProfilesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        }

        var area = state.Project.Areas.SingleOrDefault(item => item.Id == command.AreaId);
        if (area is null)
        {
            return Fail(state, command, "cinematic.area_missing", "Area does not exist.");
        }

        var previousModule = state.Project.Module.RuntimePolicies;
        var modulePolicies = new ModuleRuntimePolicyProfile(previousModule.Id, previousModule.SchemaVersion,
            previousModule.Heartbeats, previousModule.Rest, previousModule.SequenceAbort, "cinematic-command",
            command.ModuleProfile, previousModule.Gameplay, previousModule.Rules);
        var previousArea = area.RuntimePolicies;
        var areaPolicies = new AreaRuntimePolicyProfile(previousArea.Id, previousArea.SchemaVersion,
            previousArea.Heartbeats, previousArea.Rest, "cinematic-command", command.AreaProfile,
            previousArea.Rules, previousArea.Experience);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithRuntimePolicies(modulePolicies), state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? area.WithRuntimePolicies(areaPolicies) : item)
                .ToArray(), state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null)
        {
            return Fail(state, command, diagnostic.Code, diagnostic.Message);
        }

        var patch = new ProjectPatch("replace", "runtimePolicies/cinematics",
            $"Set startup flow, {command.ModuleProfile.Sequences.Count} sequence(s) and " +
            $"{command.AreaProfile.CameraAnchors.Count} camera anchor(s).");
        var inverse = new ProjectPatch("replace", "runtimePolicies/cinematics",
            "Restore previous module and active-area cinematic profiles.");
        var audit = new AuditEntry(command.CommandId, "set_cinematic_profiles", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false });
        }

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetCinematicProfilesCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_cinematic_profiles", _clock.UtcNow, false));
}
