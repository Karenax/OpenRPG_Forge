using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetModuleConfigurationHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string DuplicateVariableIdCode = "project.variable.id_duplicate";
    public const string DuplicateVariableKeyCode = "project.variable.key_duplicate";
    public const string TitleRequiredCode = "module.title_required";

    private const string CommandName = "set_module_configuration";
    private readonly IClock _clock;

    public SetModuleConfigurationHandler(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public CommandResult Handle(ProjectState state, SetModuleConfigurationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        }

        var variableDiagnostic = ProjectVariableValidator.Validate(command.Variables).FirstOrDefault();
        if (variableDiagnostic is not null)
            return Failure(state, command, variableDiagnostic.Code,
                $"{variableDiagnostic.Message} Path: {variableDiagnostic.Path}.");

        var module = state.Project.Module;
        var projectTitle = command.ProjectTitle?.Trim() ?? state.Project.Title;
        var moduleDisplayName = command.ModuleDisplayName?.Trim() ?? module.DisplayName;
        var moduleDescription = command.ModuleDescription?.Trim() ?? module.Description;
        if (string.IsNullOrWhiteSpace(projectTitle) || string.IsNullOrWhiteSpace(moduleDisplayName))
            return Failure(state, command, TitleRequiredCode,
                "Project and module display names are required.");
        var updatedModule = new ModuleDefinition(
            module.Id,
            module.Slug,
            moduleDisplayName,
            moduleDescription,
            module.EntryAreaId,
            command.Metadata,
            command.Variables,
            module.Distribution,
            module.Localization,
            module.Content,
            module.ArtisticStyle,
            module.RuntimePolicies,
            module.Factions,
            module.WorldFeatures);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            projectTitle,
            updatedModule,
            state.Project.Blueprints,
            state.Project.Areas,
            state.Project.Dialogues,
            state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(updatedProject);
        if (diagnostics.Count > 0)
        {
            return Failure(state, command, diagnostics[0].Code, diagnostics[0].Message);
        }

        var patch = new ProjectPatch(
            "replace",
            "module/configuration",
            $"Update project title, {command.Metadata.Localizations.Count} localization(s) and " +
            $"{command.Variables.Count} typed project variable(s).");
        var inverse = new ProjectPatch(
            "replace",
            "module/configuration",
            $"Restore {module.Metadata.Localizations.Count} localization(s) and " +
            $"{module.Variables.Count} typed project variable(s).");
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
        SetModuleConfigurationCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetModuleConfigurationCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
