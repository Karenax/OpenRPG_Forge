using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetCreatureFoundationHandler
{
    private readonly IClock _clock;

    public SetCreatureFoundationHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, SetCreatureFoundationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict", "The project revision changed before creature foundation could be applied.");
        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, "creature.foundation.blueprint_missing", "The target creature blueprint does not exist.");
        if (existing.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            return Failure(state, command, "creature.foundation.blueprint_kind_invalid", "Creature foundation can only target creature or player blueprints.");
        if (existing.IsReadOnly())
            return Failure(state, command, "blueprint.palette.read_only", "Installed blueprints are read-only; create a project variant first.");

        if (command.Foundation is not null)
        {
            var diagnostics = CreatureFoundationValidator.Validate(command.Foundation,
                $"blueprints/{existing.Id}/creatureFoundation");
            if (diagnostics.Count > 0)
                return new(false, state, command.BaseRevision, null,
                    diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                    null, null, Audit(command, false));
            if (!string.Equals(command.Foundation.Identity.Slug, existing.Slug, StringComparison.Ordinal))
                return Failure(state, command, "creature.foundation.slug_mismatch",
                    "Creature identity slug must match the owning blueprint slug.");
            if (state.Project.Blueprints.Any(item => item.Id != existing.Id &&
                    item.CreatureFoundation?.Id == command.Foundation.Id))
                return Failure(state, command, "creature.foundation.uuid_duplicate",
                    "The creature-foundation UUID is already owned by another blueprint.");
        }

        var root = $"blueprints/{existing.Id}/creatureFoundation";
        var patch = new ProjectPatch(command.Foundation is null ? "remove" : existing.CreatureFoundation is null ? "add" : "replace",
            root, command.Foundation is null ? $"Clear creature foundation for '{existing.Slug}'." :
                $"Set creature foundation for '{existing.Slug}' ({command.Foundation.Resources.Count} resource pools, {command.Foundation.Movement.Mode}).");
        var inverse = new ProjectPatch(existing.CreatureFoundation is null ? "remove" : "replace", root,
            existing.CreatureFoundation is null ? $"Clear creature foundation for '{existing.Slug}'." :
                $"Restore creature foundation for '{existing.Slug}'.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));

        var updated = existing.WithCreatureFoundation(command.Foundation);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            Audit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetCreatureFoundationCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            Audit(command, false));

    private AuditEntry Audit(SetCreatureFoundationCommand command, bool applied) =>
        new(command.CommandId, "set_creature_foundation", _clock.UtcNow, applied);
}
