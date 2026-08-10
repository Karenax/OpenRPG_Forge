using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetItemFoundationHandler
{
    private readonly IClock _clock;

    public SetItemFoundationHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetItemFoundationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict",
                "The project revision changed before item foundation could be applied.");
        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, "item.foundation.blueprint_missing", "The target item blueprint does not exist.");
        if (existing.Kind != BlueprintKind.Item)
            return Failure(state, command, "item.foundation.blueprint_kind_invalid",
                "Item foundation can only target item blueprints.");
        if (existing.IsReadOnly())
            return Failure(state, command, "blueprint.palette.read_only",
                "Installed blueprints are read-only; create a project variant first.");

        var foundation = command.Foundation;
        if (foundation is not null)
        {
            if (!string.Equals(foundation.Archetype, existing.Slug, StringComparison.Ordinal))
                return Failure(state, command, "item.foundation.slug_mismatch",
                    "Item foundation archetype must match the owning blueprint slug.");
            if (existing.ItemFoundation is { } current && current.TypeId != foundation.TypeId)
            {
                var preview = ItemFoundationValidator.PreviewTypeMigration(current, foundation.Type);
                if (preview.RequiresAcceptance && !command.AcceptTypeMigration)
                    return Failure(state, command, "item.foundation.type_migration_preview_required",
                        $"Type migration would remove {preview.IncompatibleSlots.Count} slot(s), " +
                        $"{preview.IncompatiblePropertyIds.Count} property/properties and rig={preview.RequiresRigChange}.");
            }

            var diagnostics = ItemFoundationValidator.Validate(foundation,
                $"blueprints/{existing.Id}/itemFoundation");
            if (diagnostics.Count > 0)
                return new(false, state, command.BaseRevision, null,
                    diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                    null, null, Audit(command, false));
            if (state.Project.Blueprints.Any(item => item.Id != existing.Id && item.ItemFoundation?.Id == foundation.Id))
                return Failure(state, command, "item.foundation.uuid_duplicate",
                    "The item-foundation UUID is already owned by another blueprint.");
        }

        var root = $"blueprints/{existing.Id}/itemFoundation";
        var patch = new ProjectPatch(foundation is null ? "remove" : existing.ItemFoundation is null ? "add" : "replace",
            root, foundation is null ? $"Clear item foundation for '{existing.Slug}'." :
                $"Set item foundation for '{existing.Slug}' (type {foundation.Type.Slug}, {foundation.Texts.Count} locale(s)).");
        var inverse = new ProjectPatch(existing.ItemFoundation is null ? "remove" : "replace", root,
            existing.ItemFoundation is null ? $"Clear item foundation for '{existing.Slug}'." :
                $"Restore item foundation for '{existing.Slug}'.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));

        var updated = existing.WithItemFoundation(foundation);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetItemFoundationCommand command,
        string code,
        string message) => new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)],
            null, null, Audit(command, false));

    private AuditEntry Audit(SetItemFoundationCommand command, bool applied) =>
        new(command.CommandId, "set_item_foundation", _clock.UtcNow, applied);
}
