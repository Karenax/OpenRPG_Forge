using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetCreaturePresentationHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string BlueprintMissingCode = "creature.presentation.blueprint_missing";
    public const string BlueprintKindCode = "creature.presentation.blueprint_kind_invalid";
    public const string ReadOnlyCode = "blueprint.palette.read_only";

    private readonly IClock _clock;

    public SetCreaturePresentationHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetCreaturePresentationCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "The project revision changed before presentation could be applied.");

        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, BlueprintMissingCode, "The target creature blueprint does not exist.");
        if (existing.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            return Failure(state, command, BlueprintKindCode, "Presentation can only target a creature or player blueprint.");
        if (existing.IsReadOnly())
            return Failure(state, command, ReadOnlyCode, "Installed blueprints are read-only; create a project variant before editing presentation.");

        if (command.Presentation is not null)
        {
            var diagnostics = CreaturePresentationValidator.Validate(
                command.Presentation,
                $"blueprints/{existing.Id}/creaturePresentation");
            if (diagnostics.Count > 0)
            {
                return new CommandResult(false, state, command.BaseRevision, null,
                    diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
                    null, null, CreateAudit(command, false));
            }

            if (state.Project.Blueprints.Any(item => item.Id != existing.Id &&
                    item.CreaturePresentation?.Id == command.Presentation.Id))
            {
                return Failure(state, command, "creature.presentation.uuid_duplicate",
                    "The presentation UUID is already owned by another blueprint.");
            }
        }

        var root = $"blueprints/{existing.Id}/creaturePresentation";
        var patch = new ProjectPatch(command.Presentation is null ? "remove" : existing.CreaturePresentation is null ? "add" : "replace",
            root, command.Presentation is null ? $"Clear presentation for '{existing.Slug}'." : $"Set presentation for '{existing.Slug}'.");
        var inverse = new ProjectPatch(existing.CreaturePresentation is null ? "remove" : "replace", root,
            existing.CreaturePresentation is null ? $"Clear presentation for '{existing.Slug}'." : $"Restore presentation for '{existing.Slug}'.");
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, false));

        var updated = new BlueprintDefinition(existing.Id, existing.Slug, existing.DisplayName, existing.Kind,
            existing.Combat, existing.PaletteOrigin, existing.IsFavorite, existing.Provenance, command.Presentation,
            existing.CreatureProgression);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(project, revision), command.BaseRevision, revision, [],
            patch, inverse, CreateAudit(command, true));
    }

    private CommandResult Failure(
        ProjectState state,
        SetCreaturePresentationCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetCreaturePresentationCommand command, bool applied) =>
        new(command.CommandId, "set_creature_presentation", _clock.UtcNow, applied);
}
