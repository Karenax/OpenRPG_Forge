using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Content;

public sealed class SetArtisticStyleProfileHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string ProfileIdentityCode = "art.style.id_mismatch";

    private readonly IClock _clock;

    public SetArtisticStyleProfileHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetArtisticStyleProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "The artistic style command revision is stale.");
        }

        var module = state.Project.Module;
        if (command.Profile.Id.Value != module.Id.Value)
        {
            return Failure(state, command, ProfileIdentityCode, "The artistic style profile ID must match the module ID.");
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
            module.Content,
            command.Profile);
        var updatedProject = new ProjectDefinition(
            state.Project.Id,
            state.Project.Title,
            updatedModule,
            state.Project.Blueprints,
            state.Project.Areas,
            state.Project.Dialogues,
            state.Project.Quests);
        var structural = ProjectValidator.Validate(updatedProject).FirstOrDefault();
        if (structural is not null)
        {
            return Failure(state, command, structural.Code, structural.Message);
        }

        var warnings = ArtisticCompatibilityValidator.Analyze(updatedProject);
        var patch = new ProjectPatch(
            "replace",
            "module/artisticStyle",
            $"Configure {command.Profile.Assets.Count} artistic asset profile(s); {warnings.Count} compatibility warning(s).");
        var inverse = new ProjectPatch(
            "replace",
            "module/artisticStyle",
            $"Restore {module.ArtisticStyle.Assets.Count} artistic asset profile(s).");
        if (command.DryRun)
        {
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        }

        var revision = state.Revision.Next();
        return new(
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
        SetArtisticStyleProfileCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            Audit(command, false));

    private AuditEntry Audit(SetArtisticStyleProfileCommand command, bool applied) =>
        new(command.CommandId, "set_artistic_style_profile", _clock.UtcNow, applied);
}
