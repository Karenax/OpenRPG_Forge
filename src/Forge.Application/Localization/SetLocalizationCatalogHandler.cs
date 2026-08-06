using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Localization;

public sealed class SetLocalizationCatalogHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string CatalogIdentityCode = "localization.catalog.id_mismatch";

    private readonly IClock _clock;

    public SetLocalizationCatalogHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetLocalizationCatalogCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "The localization command revision is stale.");
        }

        var module = state.Project.Module;
        if (command.Catalog.Id.Value != module.Id.Value)
        {
            return Failure(state, command, CatalogIdentityCode, "The localization catalog ID must match the module ID.");
        }

        var updatedModule = new ModuleDefinition(
            module.Id, module.Slug, module.DisplayName, module.Description, module.EntryAreaId,
            module.Metadata, module.Variables, module.Distribution, command.Catalog, module.Content, module.ArtisticStyle,
            module.RuntimePolicies, module.Factions);
        var updatedProject = new ProjectDefinition(
            state.Project.Id, state.Project.Title, updatedModule, state.Project.Blueprints,
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(updatedProject);
        if (diagnostics.Count > 0)
        {
            return Failure(state, command, diagnostics[0].Code, diagnostics[0].Message);
        }

        var patch = new ProjectPatch("replace", "module/localization", $"Update {command.Catalog.Messages.Count} localization message(s).");
        var inverse = new ProjectPatch("replace", "module/localization", $"Restore {module.Localization.Messages.Count} localization message(s).");
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        }

        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(updatedProject, revision), command.BaseRevision, revision, [], patch, inverse, Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetLocalizationCatalogCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null, Audit(command, false));

    private AuditEntry Audit(SetLocalizationCatalogCommand command, bool applied) =>
        new(command.CommandId, "set_localization_catalog", _clock.UtcNow, applied);
}
