using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;
using Forge.Domain.Validation;

namespace Forge.Application.Blueprints;

/// <summary>
/// Applies the complete item identity, foundation/creation/gameplay/operations and advanced profile
/// as one revision. The narrower handlers remain the canonical authorities for partial workflows;
/// this bundle is the atomic authority used by the Studio item assistant.
/// </summary>
public sealed class SetItemAuthoringBundleHandler
{
    private readonly IClock _clock;

    public SetItemAuthoringBundleHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetItemAuthoringBundleCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Foundation);
        ArgumentNullException.ThrowIfNull(command.Advanced);

        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict",
                "The project revision changed before the item bundle could be applied.");

        var slug = command.Slug.Trim();
        if (string.IsNullOrWhiteSpace(slug) ||
            !string.Equals(slug, SlugGenerator.FromDisplayName(slug, "item"), StringComparison.Ordinal))
            return Failure(state, command, "blueprint.slug_invalid", "The item slug is not canonical.");
        if (string.IsNullOrWhiteSpace(command.DisplayName))
            return Failure(state, command, "blueprint.display_name_required", "The item display name is required.");
        if (state.Project.Blueprints.Any(item => item.Id != command.BlueprintId &&
                string.Equals(item.Slug, slug, StringComparison.Ordinal)))
            return Failure(state, command, "blueprint.slug_duplicate", "The item slug is already used.");

        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is not null && existing.Kind != BlueprintKind.Item)
            return Failure(state, command, "item.blueprint_kind_invalid", "The target blueprint is not an item.");
        if (existing?.IsReadOnly() == true)
            return Failure(state, command, "blueprint.palette.read_only",
                "Installed item blueprints are read-only; create a project variant first.");
        if (!string.Equals(command.Foundation.Archetype, slug, StringComparison.Ordinal))
            return Failure(state, command, "item.foundation.slug_mismatch",
                "The item foundation archetype must match its blueprint slug.");

        if (existing?.ItemFoundation is { } current && current.TypeId != command.Foundation.TypeId)
        {
            var migration = ItemFoundationValidator.PreviewTypeMigration(current, command.Foundation.Type);
            if (migration.RequiresAcceptance && !command.AcceptTypeMigration)
                return Failure(state, command, "item.foundation.type_migration_preview_required",
                    "The immutable type migration removes incompatible slots, properties or rig data and requires explicit acceptance.");
        }

        var foundationDiagnostics = ItemFoundationValidator.Validate(command.Foundation,
            $"blueprints/{command.BlueprintId}/itemFoundation");
        if (foundationDiagnostics.Count > 0)
            return new(false, state, state.Revision, null,
                foundationDiagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}"))
                    .ToArray(), null, null, Audit(command, false));
        if (state.Project.Blueprints.Any(item => item.Id != command.BlueprintId &&
                item.ItemFoundation?.Id == command.Foundation.Id))
            return Failure(state, command, "item.foundation.uuid_duplicate",
                "The item foundation identity is already owned by another blueprint.");

        var updated = new BlueprintDefinition(
            command.BlueprintId,
            slug,
            command.DisplayName.Trim(),
            BlueprintKind.Item,
            existing?.Combat,
            existing?.PaletteOrigin ?? BlueprintPaletteOrigin.ProjectContent,
            existing?.IsFavorite ?? false,
            existing?.Provenance,
            existing?.CreaturePresentation,
            existing?.CreatureProgression,
            existing?.CreatureLoot,
            existing?.CreatureRoleBehavior,
            existing?.CreatureEvents,
            command.Advanced,
            existing?.Authoring,
            existing?.CreatureFoundation,
            command.Foundation);
        var blueprints = existing is null
            ? state.Project.Blueprints.Append(updated).ToArray()
            : state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray();
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            blueprints, state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var diagnostics = ProjectValidator.Validate(project);
        if (diagnostics.Count > 0)
            return new(false, state, state.Revision, null,
                diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}"))
                    .ToArray(), null, null, Audit(command, false));

        var operation = existing is null ? "add" : "replace";
        var path = $"blueprints/{command.BlueprintId}/itemBundle";
        var patch = new ProjectPatch(operation, path,
            $"{(existing is null ? "Create" : "Update")} complete item '{slug}' with foundation, gameplay, operations and advanced rules.");
        var inverse = new ProjectPatch(existing is null ? "remove" : "replace", path,
            existing is null ? $"Remove complete item '{slug}'." : $"Restore complete item '{existing.Slug}'.");
        if (command.DryRun)
            return new(true, state, state.Revision, null, [], patch, inverse, Audit(command, false));

        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), state.Revision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetItemAuthoringBundleCommand command, string code,
        string message) => new(false, state, state.Revision, null, [new CommandDiagnostic(code, message)], null, null,
        Audit(command, false));

    private AuditEntry Audit(SetItemAuthoringBundleCommand command, bool applied) =>
        new(command.CommandId, "set_item_authoring_bundle", _clock.UtcNow, applied);
}
