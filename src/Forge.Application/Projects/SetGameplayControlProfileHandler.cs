using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetGameplayControlProfileHandler
{
    private readonly IClock _clock;

    public SetGameplayControlProfileHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetGameplayControlProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");

        var previous = state.Project.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(previous.Id, previous.SchemaVersion, previous.Heartbeats,
            previous.Rest, previous.SequenceAbort, "gameplay-command", previous.Cinematics, command.Profile,
            previous.Rules);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithRuntimePolicies(policies), state.Project.Blueprints, state.Project.Areas,
            state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null) return Fail(state, command, diagnostic.Code, diagnostic.Message);

        var provider = command.Profile.CombatModes.DefaultProvider;
        var patch = new ProjectPatch("replace", "module/runtimePolicies/gameplay",
            $"Configure swimming, climbing, {command.Profile.RadialMenu.Actions.Count} radial actions, " +
            $"time control and combat provider '{provider.ProviderId}'.");
        var inverse = new ProjectPatch("replace", "module/runtimePolicies/gameplay",
            "Restore previous gameplay control profile.");
        var audit = new AuditEntry(command.CommandId, "set_gameplay_control_profile", _clock.UtcNow,
            !command.DryRun);
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false });

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [],
            patch, inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetGameplayControlProfileCommand command, string code,
        string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)],
        null, null, new AuditEntry(command.CommandId, "set_gameplay_control_profile", _clock.UtcNow, false));
}
