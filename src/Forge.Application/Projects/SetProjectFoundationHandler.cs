using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class SetProjectFoundationHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    private const string CommandName = "set_project_foundation";
    private readonly IClock _clock;

    public SetProjectFoundationHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetProjectFoundationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode,
                $"Command revision {command.BaseRevision} does not match project revision {state.Revision}.");
        var metadata = state.Project.Module.Metadata;
        var updatedMetadata = new ProjectMetadata(
            metadata.Id,
            metadata.DefaultLocale,
            metadata.Localizations,
            metadata.Author,
            metadata.Version,
            metadata.License,
            metadata.ImagePath,
            metadata.Tags,
            metadata.Classification,
            command.Foundation.Provenance,
            metadata.SchemaVersion,
            command.Foundation);
        var module = state.Project.Module;
        var updatedModule = new ModuleDefinition(module.Id, module.Slug, module.DisplayName, module.Description,
            module.EntryAreaId, updatedMetadata, module.Variables, module.Distribution, module.Localization,
            module.Content, module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, updatedModule,
            state.Project.Blueprints, state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
        if (diagnostic is not null)
            return Failure(state, command, diagnostic.Code, $"{diagnostic.Message} Path: {diagnostic.Path}.");
        var previous = metadata.Foundation;
        var patch = new ProjectPatch("replace", "module/metadata/foundation",
            $"Set project foundation '{command.Foundation.Id:D}' ({command.Foundation.Template}).");
        var inverse = new ProjectPatch(previous is null ? "remove" : "replace", "module/metadata/foundation",
            previous is null ? "Remove the project foundation extension." :
                $"Restore project foundation '{previous.Id:D}' ({previous.Template}).");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse,
                new(command.CommandId, CommandName, _clock.UtcNow, false));
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            new(command.CommandId, CommandName, _clock.UtcNow, true));
    }

    private CommandResult Failure(ProjectState state, SetProjectFoundationCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            new(command.CommandId, CommandName, _clock.UtcNow, false));
}
