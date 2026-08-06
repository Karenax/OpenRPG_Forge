using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetCreatureLootHandler
{
    private readonly IClock _clock;

    public SetCreatureLootHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetCreatureLootCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, "command.revision_conflict", "The project revision changed before loot could be applied.");
        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, "creature.loot.blueprint_missing", "The target creature blueprint does not exist.");
        if (existing.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            return Failure(state, command, "creature.loot.blueprint_kind_invalid", "Loot can only target a creature or player blueprint.");
        if (existing.IsReadOnly())
            return Failure(state, command, "blueprint.palette.read_only",
                "Installed blueprints are read-only; create a project variant before editing loot.");

        if (command.Loot is not null)
        {
            var itemSlugs = (existing.Combat?.Loot ?? []).Select(item => item.ItemSlug)
                .ToHashSet(StringComparer.Ordinal);
            var skillIds = (existing.CreatureProgression?.Skills ?? []).Select(item => item.SkillId)
                .ToHashSet(StringComparer.Ordinal);
            var diagnostics = CreatureLootValidator.Validate(command.Loot,
                $"blueprints/{existing.Id}/creatureLoot", itemSlugs, skillIds);
            if (diagnostics.Count > 0)
            {
                return new CommandResult(false, state, command.BaseRevision, null,
                    diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                    null, null, CreateAudit(command, false));
            }
            if (state.Project.Blueprints.Any(item => item.Id != existing.Id && item.CreatureLoot?.Id == command.Loot.Id))
                return Failure(state, command, "creature.loot.uuid_duplicate",
                    "The loot profile UUID is already owned by another blueprint.");
        }

        var root = $"blueprints/{existing.Id}/creatureLoot";
        var patch = new ProjectPatch(command.Loot is null ? "remove" : existing.CreatureLoot is null ? "add" : "replace",
            root, command.Loot is null ? $"Clear loot for '{existing.Slug}'." : $"Set loot for '{existing.Slug}'.");
        var inverse = new ProjectPatch(existing.CreatureLoot is null ? "remove" : "replace", root,
            existing.CreatureLoot is null ? $"Clear loot for '{existing.Slug}'." : $"Restore loot for '{existing.Slug}'.");
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, false));

        var updated = new BlueprintDefinition(existing.Id, existing.Slug, existing.DisplayName, existing.Kind,
            existing.Combat, existing.PaletteOrigin, existing.IsFavorite, existing.Provenance,
            existing.CreaturePresentation, existing.CreatureProgression, command.Loot, existing.CreatureRoleBehavior,
            existing.CreatureEvents, existing.ItemAdvanced);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [],
            patch, inverse, CreateAudit(command, true));
    }

    private CommandResult Failure(ProjectState state, SetCreatureLootCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetCreatureLootCommand command, bool applied) =>
        new(command.CommandId, "set_creature_loot", _clock.UtcNow, applied);
}
