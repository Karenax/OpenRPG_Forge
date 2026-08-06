using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetCreatureProgressionHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string BlueprintMissingCode = "creature.progression.blueprint_missing";
    public const string BlueprintKindCode = "creature.progression.blueprint_kind_invalid";
    public const string ReadOnlyCode = "blueprint.palette.read_only";

    private readonly IClock _clock;

    public SetCreatureProgressionHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetCreatureProgressionCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode,
                "The project revision changed before progression could be applied.");
        }

        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, BlueprintMissingCode, "The target creature blueprint does not exist.");
        if (existing.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            return Failure(state, command, BlueprintKindCode, "Progression can only target a creature or player blueprint.");
        if (existing.IsReadOnly())
            return Failure(state, command, ReadOnlyCode,
                "Installed blueprints are read-only; create a project variant before editing progression.");

        if (command.Progression is not null)
        {
            var availableAbilities = (existing.Combat?.Abilities ?? [])
                .Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
            var diagnostics = CreatureProgressionValidator.Validate(command.Progression,
                $"blueprints/{existing.Id}/creatureProgression", availableAbilities);
            if (diagnostics.Count > 0)
            {
                return new CommandResult(false, state, command.BaseRevision, null,
                    diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                    null, null, CreateAudit(command, false));
            }

            if (state.Project.Blueprints.Any(item => item.Id != existing.Id &&
                    item.CreatureProgression?.Id == command.Progression.Id))
            {
                return Failure(state, command, "creature.progression.uuid_duplicate",
                    "The progression UUID is already owned by another blueprint.");
            }

            if (existing.CreatureRoleBehavior is not null)
            {
                var behaviorDiagnostics = CreatureRoleBehaviorValidator.Validate(existing.CreatureRoleBehavior,
                    command.Progression, $"blueprints/{existing.Id}/creatureRoleBehavior");
                if (behaviorDiagnostics.Count > 0)
                {
                    return new CommandResult(false, state, command.BaseRevision, null,
                        behaviorDiagnostics.Select(item => new CommandDiagnostic(item.Code,
                            $"{item.Path}: {item.Message}")).ToArray(),
                        null, null, CreateAudit(command, false));
                }
            }
        }
        else if (existing.CreatureRoleBehavior is { AutoLevel.ManualLock: false } roleBehavior)
        {
            var behaviorDiagnostics = CreatureRoleBehaviorValidator.Validate(roleBehavior, null,
                $"blueprints/{existing.Id}/creatureRoleBehavior");
            return new CommandResult(false, state, command.BaseRevision, null,
                behaviorDiagnostics.Select(item => new CommandDiagnostic(item.Code,
                    $"{item.Path}: {item.Message}")).ToArray(),
                null, null, CreateAudit(command, false));
        }

        var root = $"blueprints/{existing.Id}/creatureProgression";
        var patch = new ProjectPatch(command.Progression is null ? "remove" : existing.CreatureProgression is null ? "add" : "replace",
            root, command.Progression is null ? $"Clear progression for '{existing.Slug}'." : $"Set progression for '{existing.Slug}'.");
        var inverse = new ProjectPatch(existing.CreatureProgression is null ? "remove" : "replace", root,
            existing.CreatureProgression is null ? $"Clear progression for '{existing.Slug}'." : $"Restore progression for '{existing.Slug}'.");
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, false));

        var updated = new BlueprintDefinition(existing.Id, existing.Slug, existing.DisplayName, existing.Kind,
            existing.Combat, existing.PaletteOrigin, existing.IsFavorite, existing.Provenance,
            existing.CreaturePresentation, command.Progression, existing.CreatureLoot, existing.CreatureRoleBehavior,
            existing.CreatureEvents);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [],
            patch, inverse, CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetCreatureProgressionCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetCreatureProgressionCommand command, bool applied) =>
        new(command.CommandId, "set_creature_progression", _clock.UtcNow, applied);
}
