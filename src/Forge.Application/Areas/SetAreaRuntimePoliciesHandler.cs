using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed class SetAreaRuntimePoliciesHandler
{
    private readonly IClock _clock;

    public SetAreaRuntimePoliciesHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetAreaRuntimePoliciesCommand command)
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

        var updated = area.WithRuntimePolicies(command.Profile);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas.Select(item => item.Id == area.Id ? updated : item).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null)
        {
            return Fail(state, command, diagnostic.Code, diagnostic.Message);
        }

        var patch = new ProjectPatch("replace", $"areas/{area.Id}/runtimePolicies",
            $"Set {command.Profile.Heartbeats.Count} area heartbeat(s), rest policy and {command.Profile.Rest.Volumes.Count} volume(s).");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/runtimePolicies", "Restore previous area runtime policies.");
        var audit = new AuditEntry(command.CommandId, "set_area_runtime_policies", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, audit with { Applied = false });
        }

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
            inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetAreaRuntimePoliciesCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_area_runtime_policies", _clock.UtcNow, false));
}
