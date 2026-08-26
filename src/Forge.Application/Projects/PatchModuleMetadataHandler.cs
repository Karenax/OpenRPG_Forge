using Forge.Application.Commands;
using Forge.Application.Time;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Projects;

public sealed class PatchModuleMetadataHandler
{
    private const string CommandName = "patch_module_metadata";
    private readonly IClock _clock;

    public PatchModuleMetadataHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, PatchModuleMetadataCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict", "Project revision changed.");
        if (command.MetadataPatch is null && command.Foundation is null)
            return Failure(state, command, "module.metadata.patch_empty",
                "A metadata patch or project foundation is required.");

        try
        {
            var module = state.Project.Module;
            var metadata = command.MetadataPatch is null
                ? module.Metadata
                : ModuleMetadataPatch.Apply(module.Metadata, command.MetadataPatch);
            if (command.Foundation is not null)
                metadata = new ProjectMetadata(metadata.Id, metadata.DefaultLocale, metadata.Localizations,
                    metadata.Author, metadata.Version, metadata.License, metadata.ImagePath, metadata.Tags,
                    metadata.Classification, command.Foundation.Provenance, metadata.SchemaVersion,
                    command.Foundation);

            var defaultLocalization = metadata.GetDefaultLocalization();
            var defaultLocaleChanged = command.MetadataPatch?.Fields.Contains(
                ModuleMetadataPatchField.DefaultLocale) == true;
            var localizedPatch = command.MetadataPatch?.Localizations?.SingleOrDefault(item =>
                !item.Remove && string.Equals(item.Locale, metadata.DefaultLocale,
                    StringComparison.OrdinalIgnoreCase));
            var nameChanged = defaultLocaleChanged || localizedPatch?.Fields.Contains(
                ProjectLocalizedMetadataPatchField.Name) == true;
            var descriptionChanged = defaultLocaleChanged || localizedPatch?.Fields.Contains(
                ProjectLocalizedMetadataPatchField.Description) == true;
            var projectTitle = nameChanged ? defaultLocalization.Name : state.Project.Title;
            var displayName = nameChanged ? defaultLocalization.Name : module.DisplayName;
            var description = descriptionChanged ? defaultLocalization.Description : module.Description;
            var updatedModule = new ModuleDefinition(module.Id, module.Slug, displayName, description,
                module.EntryAreaId, metadata, module.Variables, module.Distribution, module.Localization,
                module.Content, module.ArtisticStyle, module.RuntimePolicies, module.Factions,
                module.WorldFeatures);
            var project = new ProjectDefinition(state.Project.Id, projectTitle, updatedModule,
                state.Project.Blueprints, state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
            var diagnostic = ProjectValidator.Validate(project).FirstOrDefault();
            if (diagnostic is not null)
                return Failure(state, command, diagnostic.Code, $"{diagnostic.Message} Path: {diagnostic.Path}.");

            var paths = (command.MetadataPatch?.RelativePaths ?? []).ToList();
            if (command.Foundation is not null) paths.Add("module/metadata/foundation");
            var path = paths.Count == 1 ? paths[0] : "module/metadata";
            var patch = new ProjectPatch("patch", path,
                $"Patch only {paths.Count} selected module metadata path(s).");
            var inverse = new ProjectPatch("patch", path,
                "Restore only the selected module metadata paths.");
            if (command.DryRun)
                return new(true, state, command.BaseRevision, null, [], patch, inverse,
                    Audit(command, false));
            var revision = state.Revision.Next();
            return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch,
                inverse, Audit(command, true));
        }
        catch (ArgumentException exception)
        {
            return Failure(state, command, "module.metadata.patch_invalid", exception.Message);
        }
    }

    private CommandResult Failure(ProjectState state, PatchModuleMetadataCommand command, string code,
        string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)],
        null, null, Audit(command, false));

    private AuditEntry Audit(PatchModuleMetadataCommand command, bool applied) =>
        new(command.CommandId, CommandName, _clock.UtcNow, applied);
}
