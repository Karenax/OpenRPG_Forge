using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetRuntimeExtensionsHandler
{
    private readonly IClock _clock;

    public SetRuntimeExtensionsHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetRuntimeExtensionsCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Fail(state, command, "command.revision_conflict", "Project revision changed.");
        var current = state.Project.Module.RuntimePolicies;
        var policies = new ModuleRuntimePolicyProfile(current.Id, current.SchemaVersion, current.Heartbeats,
            current.Rest, current.SequenceAbort, "runtime-extension-command", current.Cinematics, current.Gameplay,
            current.Rules, command.Profile, current.GameplayEvents, current.World);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title,
            state.Project.Module.WithRuntimePolicies(policies), state.Project.Blueprints, state.Project.Areas,
            state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null) return Fail(state, command, diagnostic.Code, diagnostic.Message);

        var patch = new ProjectPatch("replace", "module/runtimePolicies/extensions",
            $"Configure {command.Profile.RuleSchemas.Count} rule schema(s), {command.Profile.UiDocuments.Count} " +
            $"scriptable UI document(s), {command.Profile.JsonDocuments.Count} runtime JSON document(s), and " +
            $"entity snapshot policy '{command.Profile.SnapshotPolicy.Id:D}' plus " +
            $"{command.Profile.OperationalReadiness.TaskManifests.Count} bounded task manifest(s); " +
            $"post-launch: achievements={command.Profile.PostLaunch.Gameplay.Achievements.Count}, " +
            $"director-groups={command.Profile.PostLaunch.LiveDirector.SavedGroups.Count}, " +
            $"shards={command.Profile.PostLaunch.Network.Shards.Count}.");
        var inverse = new ProjectPatch("replace", "module/runtimePolicies/extensions",
            "Restore the previous runtime extension profile.");
        var audit = new AuditEntry(command.CommandId, "set_runtime_extensions", _clock.UtcNow, !command.DryRun);
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                audit with { Applied = false });
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [],
            patch, inverse, audit);
    }

    private CommandResult Fail(ProjectState state, SetRuntimeExtensionsCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new AuditEntry(command.CommandId, "set_runtime_extensions", _clock.UtcNow, false));
}
