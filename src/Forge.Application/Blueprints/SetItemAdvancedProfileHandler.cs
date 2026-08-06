using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Blueprints;

public sealed class SetItemAdvancedProfileHandler
{
    private readonly IClock _clock;

    public SetItemAdvancedProfileHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetItemAdvancedProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict", "The project revision changed.");

        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, "item.blueprint_missing", "The target item blueprint does not exist.");
        if (existing.Kind != BlueprintKind.Item)
            return Failure(state, command, "item.blueprint_kind_invalid", "Advanced item data requires an item blueprint.");
        if (existing.IsReadOnly())
            return Failure(state, command, "blueprint.palette.read_only",
                "Installed blueprints are read-only; create a project variant before editing item data.");

        var updated = existing.WithItemAdvanced(command.Profile);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == existing.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
            return new(false, state, state.Revision, null,
                diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                null, null, Audit(command, false));

        var path = $"blueprints/{existing.Id}/itemAdvanced";
        var patch = new ProjectPatch(command.Profile is null ? "remove" : existing.ItemAdvanced is null ? "add" : "replace",
            path, command.Profile is null ? $"Clear advanced item data for '{existing.Slug}'." :
            $"Set advanced item data for '{existing.Slug}'.");
        var inverse = new ProjectPatch(existing.ItemAdvanced is null ? "remove" : "replace", path,
            $"Restore advanced item data for '{existing.Slug}'.");
        if (command.DryRun)
            return new(true, state, state.Revision, null, [], patch, inverse, Audit(command, false));

        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), state.Revision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetItemAdvancedProfileCommand command, string code,
        string message) => new(false, state, state.Revision, null, [new CommandDiagnostic(code, message)], null, null,
        Audit(command, false));

    private AuditEntry Audit(SetItemAdvancedProfileCommand command, bool applied) =>
        new(command.CommandId, "set_item_advanced_profile", _clock.UtcNow, applied);
}
