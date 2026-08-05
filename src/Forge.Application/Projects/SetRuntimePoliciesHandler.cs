using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetRuntimePoliciesHandler
{
    private readonly IClock _clock;

    public SetRuntimePoliciesHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetRuntimePoliciesCommand command)
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
            return Fail(state, command, "runtime.policy.area_missing", "Area does not exist.");
        }

        var module = state.Project.Module.WithRuntimePolicies(command.ModuleProfile);
        var updatedArea = area.WithRuntimePolicies(command.AreaProfile);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, module, state.Project.Blueprints,
            state.Project.Areas.Select(item => item.Id == area.Id ? updatedArea : item).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null)
        {
            return Fail(state, command, diagnostic.Code, diagnostic.Message);
        }

        var patch = new ProjectPatch("replace", "runtimePolicies",
            $"Set {command.ModuleProfile.Heartbeats.Count} global and {command.AreaProfile.Heartbeats.Count} area heartbeat(s), rest and sequence abort policies.");
        var inverse = new ProjectPatch("replace", "runtimePolicies",
            "Restore previous module and active-area runtime policies.");
        var audit = new AuditEntry(command.CommandId, "set_runtime_policies", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false });
        }

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetRuntimePoliciesCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_runtime_policies", _clock.UtcNow, false));
}
