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
            if (command.Foundation.AuthoringComposition is { } authoring &&
                state.Project.Blueprints.Any(item => item.Id != existing.Id &&
                    item.CreatureFoundation?.AuthoringComposition is { } other &&
                    (other.Id == authoring.Id || other.AnimationGraph.Id == authoring.AnimationGraph.Id ||
                     other.AnimationGraph.States.Select(state => state.Id)
                         .Concat(other.AnimationGraph.Transitions.Select(edge => edge.Id))
                         .Intersect(authoring.AnimationGraph.States.Select(state => state.Id)
                             .Concat(authoring.AnimationGraph.Transitions.Select(edge => edge.Id))).Any())))
                return Failure(state, command, "creature.authoring.uuid_duplicate",
                    "Creature-authoring profile, graph, node and edge UUIDs must be unique across blueprints.");
            if (command.Foundation.RulesComposition is { } rules)
            {
                var candidateIds = CreatureRulesCompositionValidator.EnumerateComponentIds(rules).ToHashSet();
                if (state.Project.Blueprints.Where(item => item.Id != existing.Id)
                    .Select(item => item.CreatureFoundation?.RulesComposition)
                    .Where(item => item is not null)
                    .SelectMany(item => CreatureRulesCompositionValidator.EnumerateComponentIds(item!))
                    .Any(candidateIds.Contains))
                    return Failure(state, command, "creature.rules.uuid_duplicate",
                        "Creature-rules profile and component UUIDs must be unique across blueprints.");
                var invalidItem = rules.Inventory.Containers.SelectMany(item => item.Items).FirstOrDefault(item =>
                    state.Project.Blueprints.All(blueprint => blueprint.Id != item.ItemBlueprintId ||
                        blueprint.Kind != BlueprintKind.Item));
                if (invalidItem is not null)
                    return Failure(state, command, "creature.rules.inventory_item_missing",
                        $"Creature inventory item blueprint '{invalidItem.ItemBlueprintId}' does not resolve to an item.");
            }
            if (command.Foundation.InteractionComposition is { } interaction)
            {
                var candidateIds = CreatureInteractionCompositionValidator.EnumerateComponentIds(interaction).ToHashSet();
                if (state.Project.Blueprints.Where(item => item.Id != existing.Id)
                    .Select(item => item.CreatureFoundation?.InteractionComposition)
                    .Where(item => item is not null)
                    .SelectMany(item => CreatureInteractionCompositionValidator.EnumerateComponentIds(item!))
                    .Any(candidateIds.Contains))
                    return Failure(state, command, "creature.interaction.uuid_duplicate",
                        "Creature-interaction profile and component UUIDs must be unique across blueprints.");
                var availableFactions = state.Project.Module.Factions.Factions.Select(item => item.Id).ToHashSet();
                var missingFaction = interaction.Factions.Memberships.FirstOrDefault(item =>
                    !availableFactions.Contains(item.FactionId));
                if (missingFaction is not null)
                    return Failure(state, command, "creature.interaction.faction_missing",
                        $"Creature faction '{missingFaction.FactionId}' does not resolve.");
                var availableDialogues = state.Project.Dialogues.Select(item => item.Id).ToHashSet();
                var missingDialogue = interaction.DialogueBindings.FirstOrDefault(item =>
                    !availableDialogues.Contains(item.DialogueId));
                if (missingDialogue is not null)
                    return Failure(state, command, "creature.interaction.dialogue_missing",
                        $"Creature dialogue '{missingDialogue.DialogueId}' does not resolve.");
            }
            if (command.Foundation.EventAiComposition is { } eventAi)
            {
                var candidateIds = CreatureEventAiCompositionValidator.EnumerateComponentIds(eventAi).ToHashSet();
                if (state.Project.Blueprints.Where(item => item.Id != existing.Id)
                    .Select(item => item.CreatureFoundation?.EventAiComposition)
                    .Where(item => item is not null)
                    .SelectMany(item => CreatureEventAiCompositionValidator.EnumerateComponentIds(item!))
                    .Any(candidateIds.Contains))
                    return Failure(state, command, "creature.event_ai.uuid_duplicate",
                        "Creature event/AI profile and component UUIDs must be unique across blueprints.");
            }
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
