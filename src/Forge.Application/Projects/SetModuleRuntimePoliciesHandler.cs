using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetModuleRuntimePoliciesHandler
{
    private readonly IClock _clock;

    public SetModuleRuntimePoliciesHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetModuleRuntimePoliciesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        }

        var module = state.Project.Module.WithRuntimePolicies(command.Profile);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, module, state.Project.Blueprints,
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null)
        {
            return Fail(state, command, diagnostic.Code, diagnostic.Message);
        }

        var patch = new ProjectPatch("replace", "module/runtimePolicies",
            $"Set {command.Profile.Heartbeats.Count} global heartbeat(s), rest lifecycle and sequence abort policy.");
        var inverse = new ProjectPatch("replace", "module/runtimePolicies", "Restore previous module runtime policies.");
        var audit = new AuditEntry(command.CommandId, "set_module_runtime_policies", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, audit with { Applied = false });
        }

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetModuleRuntimePoliciesCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_module_runtime_policies", _clock.UtcNow, false));
}
