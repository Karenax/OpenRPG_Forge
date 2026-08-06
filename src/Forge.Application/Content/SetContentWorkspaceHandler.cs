using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Content;

public sealed class SetContentWorkspaceHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string ConfigurationIdentityCode = "content.workspace.id_mismatch";

    private readonly IClock _clock;

    public SetContentWorkspaceHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetContentWorkspaceCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "The content workspace command revision is stale.");
        }

        var module = state.Project.Module;
        if (command.Configuration.Id.Value != module.Id.Value)
        {
            return Failure(
                state,
                command,
                ConfigurationIdentityCode,
                "The content workspace configuration ID must match the module ID.");
        }

        var updatedModule = new ModuleDefinition(
            module.Id,
            module.Slug,
            module.DisplayName,
            module.Description,
            module.EntryAreaId,
            module.Metadata,
            module.Variables,
            module.Distribution,
            module.Localization,
            command.Configuration,
            module.ArtisticStyle,
            module.RuntimePolicies,
            module.Factions);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            updatedModule,
            state.Project.Blueprints,
            state.Project.Areas,
            state.Project.Dialogues,
            state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(updatedProject).FirstOrDefault();
        if (diagnostic is not null)
        {
            return Failure(state, command, diagnostic.Code, diagnostic.Message);
        }

        var patch = new ProjectPatch(
            "replace",
            "module/content",
            $"Configure {command.Configuration.DataTables.Count} data table(s), " +
            $"batch collision {command.Configuration.Batch.CollisionPolicy} and merge {command.Configuration.MergePolicy}.");
        var inverse = new ProjectPatch(
            "replace",
            "module/content",
            $"Restore {module.Content.DataTables.Count} data table(s).");
        if (command.DryRun)
        {
            return new CommandResult(
                true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
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
            Audit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetContentWorkspaceCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            Audit(command, false));

    private AuditEntry Audit(SetContentWorkspaceCommand command, bool applied) =>
        new(command.CommandId, "set_content_workspace", _clock.UtcNow, applied);
}
