using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetCreatureRoleBehaviorHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string BlueprintMissingCode = "creature.role_behavior.blueprint_missing";
    public const string BlueprintKindCode = "creature.role_behavior.blueprint_kind_invalid";
    public const string ReadOnlyCode = "blueprint.palette.read_only";

    private readonly IClock _clock;

    public SetCreatureRoleBehaviorHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetCreatureRoleBehaviorCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode,
                "The project revision changed before role and behavior could be applied.");

        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, BlueprintMissingCode, "The target creature blueprint does not exist.");
        if (existing.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            return Failure(state, command, BlueprintKindCode,
                "Role and behavior can only target a creature or player blueprint.");
        if (existing.IsReadOnly())
            return Failure(state, command, ReadOnlyCode,
                "Installed blueprints are read-only; create a project variant before editing role and behavior.");

        CreatureAutoLevelPreview? preview = null;
        if (command.RoleBehavior is not null)
        {
            var diagnostics = CreatureRoleBehaviorValidator.Validate(command.RoleBehavior,
                existing.CreatureProgression, $"blueprints/{existing.Id}/creatureRoleBehavior");
            if (diagnostics.Count > 0)
                return Invalid(state, command, diagnostics);
            if (state.Project.Blueprints.Any(item => item.Id != existing.Id &&
                    item.CreatureRoleBehavior?.Id == command.RoleBehavior.Id))
                return Failure(state, command, "creature.role_behavior.uuid_duplicate",
                    "The role and behavior UUID is already owned by another blueprint.");
            preview = CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(command.RoleBehavior,
                existing.CreatureProgression);
            if (!preview.Succeeded) return Invalid(state, command, preview.Diagnostics);
        }

        var root = $"blueprints/{existing.Id}/creatureRoleBehavior";
        var autoLevelSummary = preview is { ManualLocked: false, Changes.Count: > 0 }
            ? $" Auto-level preview contains {preview.Changes.Count} change(s)."
            : string.Empty;
        var patch = new ProjectPatch(command.RoleBehavior is null ? "remove" :
            existing.CreatureRoleBehavior is null ? "add" : "replace", root,
            command.RoleBehavior is null
                ? $"Clear role and behavior for '{existing.Slug}'."
                : $"Set role and behavior for '{existing.Slug}'.{autoLevelSummary}");
        var inverse = new ProjectPatch(existing.CreatureRoleBehavior is null ? "remove" : "replace", root,
            existing.CreatureRoleBehavior is null
                ? $"Clear role and behavior for '{existing.Slug}'."
                : $"Restore role and behavior for '{existing.Slug}'.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, false));

        var progression = command.RoleBehavior?.AutoLevel.ApplyOnSave == true && preview is { ManualLocked: false }
            ? preview.Result
            : existing.CreatureProgression;
        var updated = new BlueprintDefinition(existing.Id, existing.Slug, existing.DisplayName, existing.Kind,
            existing.Combat, existing.PaletteOrigin, existing.IsFavorite, existing.Provenance,
            existing.CreaturePresentation, progression, existing.CreatureLoot, command.RoleBehavior);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            CreateAudit(command, true));
    }

    private CommandResult Invalid(
        ProjectState state,
        SetCreatureRoleBehaviorCommand command,
        IReadOnlyList<CreatureRoleBehaviorDiagnostic> diagnostics) =>
        new(false, state, command.BaseRevision, null,
            diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
            null, null, CreateAudit(command, false));

    private CommandResult Failure(
        ProjectState state,
        SetCreatureRoleBehaviorCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetCreatureRoleBehaviorCommand command, bool applied) =>
        new(command.CommandId, "set_creature_role_behavior", _clock.UtcNow, applied);
}
