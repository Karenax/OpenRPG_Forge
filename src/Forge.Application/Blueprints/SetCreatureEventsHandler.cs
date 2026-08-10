using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Blueprints;

public sealed class SetCreatureEventsHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string BlueprintMissingCode = "creature.events.blueprint_missing";
    public const string BlueprintKindCode = "creature.events.blueprint_kind_invalid";
    public const string ReadOnlyCode = "blueprint.palette.read_only";

    private readonly IClock _clock;

    public SetCreatureEventsHandler(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public CommandResult Handle(ProjectState state, SetCreatureEventsCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode,
                "The project revision changed before creature events could be applied.");

        var existing = state.Project.Blueprints.SingleOrDefault(item => item.Id == command.BlueprintId);
        if (existing is null)
            return Failure(state, command, BlueprintMissingCode, "The target creature blueprint does not exist.");
        if (existing.Kind is not (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            return Failure(state, command, BlueprintKindCode,
                "Creature events can only target a creature or player blueprint.");
        if (existing.IsReadOnly())
            return Failure(state, command, ReadOnlyCode,
                "Installed blueprints are read-only; create a project variant before editing events.");

        if (command.Events is not null)
        {
            var diagnostics = CreatureEventValidator.Validate(command.Events,
                $"blueprints/{existing.Id}/creatureEvents");
            if (diagnostics.Count > 0) return Invalid(state, command, diagnostics);
            if (state.Project.Blueprints.Any(item => item.Id != existing.Id &&
                    item.CreatureEvents?.Id == command.Events.Id))
                return Failure(state, command, "creature.events.uuid_duplicate",
                    "The creature-event UUID is already owned by another blueprint.");
        }

        var root = $"blueprints/{existing.Id}/creatureEvents";
        var bindingCount = command.Events is null ? 0 : CreatureEventValidator.EnumerateBindings(command.Events).Count;
        var patch = new ProjectPatch(command.Events is null ? "remove" :
            existing.CreatureEvents is null ? "add" : "replace", root,
            command.Events is null
                ? $"Clear creature events for '{existing.Slug}'."
                : $"Set {bindingCount} creature-event binding(s) for '{existing.Slug}'.");
        var inverse = new ProjectPatch(existing.CreatureEvents is null ? "remove" : "replace", root,
            existing.CreatureEvents is null
                ? $"Clear creature events for '{existing.Slug}'."
                : $"Restore creature events for '{existing.Slug}'.");
        if (command.DryRun)
            return new(true, state, command.BaseRevision, null, [], patch, inverse, CreateAudit(command, false));

        var updated = new BlueprintDefinition(existing.Id, existing.Slug, existing.DisplayName, existing.Kind,
            existing.Combat, existing.PaletteOrigin, existing.IsFavorite, existing.Provenance,
            existing.CreaturePresentation, existing.CreatureProgression, existing.CreatureLoot,
            existing.CreatureRoleBehavior, command.Events, existing.ItemAdvanced, existing.Authoring,
            existing.CreatureFoundation, existing.ItemFoundation);
        var project = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints.Select(item => item.Id == updated.Id ? updated : item).ToArray(),
            state.Project.Areas, state.Project.Dialogues, state.Project.Quests);
        var revision = state.Revision.Next();
        return new(true, new ProjectState(project, revision), command.BaseRevision, revision, [], patch, inverse,
            CreateAudit(command, true));
    }

    private CommandResult Invalid(
        ProjectState state,
        SetCreatureEventsCommand command,
        IReadOnlyList<CreatureEventDiagnostic> diagnostics) =>
        new(false, state, command.BaseRevision, null,
            diagnostics.Select(item => new CommandDiagnostic(item.Code, $"{item.Path}: {item.Message}")).ToArray(),
            null, null, CreateAudit(command, false));

    private CommandResult Failure(
        ProjectState state,
        SetCreatureEventsCommand command,
        string code,
        string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null,
            CreateAudit(command, false));

    private AuditEntry CreateAudit(SetCreatureEventsCommand command, bool applied) =>
        new(command.CommandId, "set_creature_events", _clock.UtcNow, applied);
}
