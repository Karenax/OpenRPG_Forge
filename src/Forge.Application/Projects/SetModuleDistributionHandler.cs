using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetModuleDistributionHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";

    private const string CommandName = "set_module_distribution";
    private readonly IClock _clock;

    public SetModuleDistributionHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(ProjectState state, SetModuleDistributionCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(
                state,
                command,
                RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var module = state.Project.Module;
        var updatedModule = new ModuleDefinition(
            module.Id,
            module.Slug,
            module.DisplayName,
            module.Description,
            module.EntryAreaId,
            module.Metadata,
            module.Variables,
            command.Distribution,
            module.Localization,
            module.Content,
            module.ArtisticStyle,
            module.RuntimePolicies);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            updatedModule,
            state.Project.Blueprints,
            state.Project.Areas,
            state.Project.Dialogues,
            state.Project.Quests);
        var projectDiagnostics = ProjectValidator.Validate(updatedProject);
        if (projectDiagnostics.Count > 0)
        {
            return Failure(state, command, projectDiagnostics[0].Code, projectDiagnostics[0].Message);
        }

        var patch = new ProjectPatch(
            "replace",
            "module/distribution",
            $"Update {command.Distribution.TargetProfiles.Count} target profile(s) and " +
            $"{command.Distribution.Dependencies.Count} dependency approval(s) for " +
            $"{command.Distribution.Release.Channel} on {command.Distribution.Release.Platforms.Count} platform(s).");
        var inverse = new ProjectPatch(
            "replace",
            "module/distribution",
            $"Restore {module.Distribution.TargetProfiles.Count} target profile(s) and " +
            $"{module.Distribution.Dependencies.Count} dependency approval(s).");
        if (command.DryRun)
        {
            return new CommandResult(
                true,
                state,
                command.BaseRevision,
                null,
                [],
                patch,
                inverse,
                CreateAudit(command, false));
        }

        var revision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(updatedProject, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetModuleDistributionCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetModuleDistributionCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
