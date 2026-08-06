using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetBlueprintCombatHandler(IClock clock)
{
    public CommandResult Handle(ProjectState state, SetBlueprintCombatCommand command)
    {
        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId); if (command.BaseRevision != state.Revision || existing is null || existing.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn)) return Failure(state, command, existing is null ? "blueprint.missing" : "blueprint.combat_invalid"); var patch = new ProjectPatch("replace", $"blueprints/{existing.Id}/combat", $"Set combat profile for '{existing.Slug}'."); var inverse = new ProjectPatch(existing.Combat is null ? "remove" : "replace", $"blueprints/{existing.Id}/combat", $"Restore combat profile for '{existing.Slug}'."); if (command.DryRun) return new(true, state, state.Revision, null, [], patch, inverse, Audit(command, false)); var updated = new BlueprintDefinition(existing.Id, existing.Slug, existing.DisplayName, existing.Kind, command.Combat, existing.PaletteOrigin, existing.IsFavorite, existing.Provenance, existing.CreaturePresentation, existing.CreatureProgression, existing.CreatureLoot, existing.CreatureRoleBehavior); var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module, state.Project.Blueprints.Select(item => item.Id == existing.Id ? updated : item).ToArray(), state.Project.Areas, state.Project.Dialogues, state.Project.Quests); var revision = state.Revision.Next(); return new(true, new(project, revision), state.Revision, revision, [], patch, inverse, Audit(command, true));
    }
    private CommandResult Failure(ProjectState state, SetBlueprintCombatCommand command, string code) => new(false, state, state.Revision, null, [new(code, "Combat profile command rejected.")], null, null, Audit(command, false));
    private AuditEntry Audit(SetBlueprintCombatCommand command, bool applied) => new(command.CommandId, "set_blueprint_combat", clock.UtcNow, applied);
}
