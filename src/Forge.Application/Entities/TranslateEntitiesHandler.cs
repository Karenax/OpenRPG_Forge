using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Entities;

public sealed class TranslateEntitiesHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "command.area_missing";
    public const string EmptySelectionCode = "translation.selection.empty";
    public const string DuplicateSelectionCode = "translation.selection.duplicate";
    public const string MissingEntityCode = "translation.entity_missing";
    public const string InvalidDeltaCode = "translation.delta.invalid";
    public const string GeometryCode = "translation.geometry.invalid";
    public const float MaximumComponent = 100_000f;

    private readonly IClock _clock;

    public TranslateEntitiesHandler(IClock clock) =>
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, TranslateEntitiesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
            return Failure(state, command, RevisionConflictCode, "La révision du projet a changé.");

        var area = state.Project.Areas.SingleOrDefault(candidate => candidate.Id == command.AreaId);
        if (area is null)
            return Failure(state, command, MissingAreaCode, $"La zone '{command.AreaId}' n’existe pas.");
        if (command.EntityIds.Count == 0)
            return Failure(state, command, EmptySelectionCode, "Sélectionnez au moins une entité à déplacer.");
        if (command.EntityIds.Distinct().Count() != command.EntityIds.Count)
            return Failure(state, command, DuplicateSelectionCode,
                "Une entité ne peut apparaître qu’une fois dans la sélection.");

        var selected = area.Entities.Where(entity => command.EntityIds.Contains(entity.Id)).ToArray();
        if (selected.Length != command.EntityIds.Count)
            return Failure(state, command, MissingEntityCode,
                "Une entité sélectionnée n’existe plus dans la zone active.");
        if (!IsValid(command.Delta))
            return Failure(state, command, InvalidDeltaCode,
                $"Chaque composante du déplacement doit être finie et comprise entre {-MaximumComponent:0} et {MaximumComponent:0}.");

        var replacements = selected.ToDictionary(
            entity => entity.Id,
            entity => new EntityInstance(
                entity.Id,
                entity.BlueprintId,
                new TransformValue(
                    new Vector3Value(
                        entity.Transform.Position.X + command.Delta.X,
                        entity.Transform.Position.Y + command.Delta.Y,
                        entity.Transform.Position.Z + command.Delta.Z),
                    entity.Transform.Rotation,
                    entity.Transform.Scale),
                entity.Tags,
                entity.Overrides,
                entity.BlueprintVersionRequirement));
        if (replacements.Values.Any(entity => !IsFinite(entity.Transform.Position)))
            return Failure(state, command, InvalidDeltaCode, "Le déplacement produit une position non finie.");

        var updatedArea = area.WithEntities(area.Entities
            .Select(entity => replacements.GetValueOrDefault(entity.Id, entity))
            .ToArray());
        var updatedProject = ReplaceArea(state.Project, updatedArea);
        var previousDiagnostics = AreaGeometryValidator.Validate(state.Project, area)
            .Select(DiagnosticIdentity)
            .ToHashSet(StringComparer.Ordinal);
        var introduced = AreaGeometryValidator.Validate(updatedProject, updatedArea)
            .FirstOrDefault(diagnostic => !previousDiagnostics.Contains(DiagnosticIdentity(diagnostic)));
        if (introduced is not null)
            return Failure(state, command, GeometryCode,
                $"{introduced.Code} · {introduced.Message} · {introduced.Path}");

        var ids = string.Join(',', command.EntityIds.OrderBy(id => id.ToString()));
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/entities/{ids}/position",
            $"Translate {command.EntityIds.Count} entity or entities by " +
            $"({command.Delta.X:0.###}, {command.Delta.Y:0.###}, {command.Delta.Z:0.###}).");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/entities/{ids}/position",
            $"Restore position for {command.EntityIds.Count} entity or entities.");
        if (command.DryRun)
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse,
                Audit(command, false));

        var revision = state.Revision.Next();
        return new CommandResult(
            true,
            new ProjectState(updatedProject, revision),
            command.BaseRevision,
            revision,
            [],
            patch,
            inverse,
            Audit(command, true));
    }

    private static bool IsValid(Vector3Value value) =>
        IsFinite(value) &&
        MathF.Abs(value.X) <= MaximumComponent &&
        MathF.Abs(value.Y) <= MaximumComponent &&
        MathF.Abs(value.Z) <= MaximumComponent;

    private static bool IsFinite(Vector3Value value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static string DiagnosticIdentity(DomainDiagnostic diagnostic) =>
        $"{diagnostic.Code}|{diagnostic.Path}|{diagnostic.Message}";

    private static ProjectDefinition ReplaceArea(ProjectDefinition project, AreaDefinition updatedArea) => new(
        project.Id,
        project.Title,
        project.Module,
        project.Blueprints,
        project.Areas.Select(area => area.Id == updatedArea.Id ? updatedArea : area).ToArray(),
        project.Dialogues,
        project.Quests);

    private CommandResult Failure(
        ProjectState state,
        TranslateEntitiesCommand command,
        string code,
        string message) => new(
            false,
            state,
            command.BaseRevision,
            null,
            [new CommandDiagnostic(code, message)],
            null,
            null,
            Audit(command, false));

    private AuditEntry Audit(TranslateEntitiesCommand command, bool applied) =>
        new(command.CommandId, "translate_entities", _clock.UtcNow, applied);
}
