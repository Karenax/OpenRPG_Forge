using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Entities;

public sealed class ScaleEntitiesHandler
{
    public const string RevisionConflictCode = "command.revision_conflict";
    public const string MissingAreaCode = "command.area_missing";
    public const string EmptySelectionCode = "scale.selection.empty";
    public const string DuplicateSelectionCode = "scale.selection.duplicate";
    public const string MissingEntityCode = "scale.entity_missing";
    public const string InvalidScaleCode = "scale.value.invalid";
    public const string GeometryCode = "scale.geometry.invalid";
    public const float MinimumScale = 0.05f;
    public const float MaximumScale = 100f;

    private readonly IClock _clock;

    public ScaleEntitiesHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, ScaleEntitiesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.BaseRevision != state.Revision)
        {
            return Failure(state, command, RevisionConflictCode, "La révision du projet a changé.");
        }

        var area = state.Project.Areas.SingleOrDefault(candidate => candidate.Id == command.AreaId);
        if (area is null)
        {
            return Failure(state, command, MissingAreaCode, $"La zone '{command.AreaId}' n’existe pas.");
        }

        if (command.EntityIds.Count == 0)
        {
            return Failure(state, command, EmptySelectionCode, "Sélectionnez au moins une entité à mettre à l’échelle.");
        }

        if (command.EntityIds.Distinct().Count() != command.EntityIds.Count)
        {
            return Failure(state, command, DuplicateSelectionCode, "Une entité ne peut apparaître qu’une fois dans la sélection.");
        }

        var selected = area.Entities.Where(entity => command.EntityIds.Contains(entity.Id)).ToArray();
        if (selected.Length != command.EntityIds.Count)
        {
            return Failure(state, command, MissingEntityCode, "Une entité sélectionnée n’existe plus dans la zone active.");
        }

        if (!IsValid(command.Scale))
        {
            return Failure(state, command, InvalidScaleCode,
                $"Chaque composante doit être comprise entre {MinimumScale:0.##} et {MaximumScale:0.##}.");
        }

        var replacements = new Dictionary<EntityId, EntityInstance>();
        foreach (var entity in selected)
        {
            var scale = command.Multiply
                ? new Vector3Value(
                    entity.Transform.Scale.X * command.Scale.X,
                    entity.Transform.Scale.Y * command.Scale.Y,
                    entity.Transform.Scale.Z * command.Scale.Z)
                : command.Scale;
            if (!IsValid(scale))
            {
                return Failure(state, command, InvalidScaleCode,
                    $"L’échelle résultante de l’entité '{entity.Id}' dépasse les limites autorisées.");
            }

            replacements[entity.Id] = new EntityInstance(
                entity.Id,
                entity.BlueprintId,
                new TransformValue(entity.Transform.Position, entity.Transform.Rotation, scale),
                entity.Tags,
                entity.Overrides,
                entity.BlueprintVersionRequirement);
        }

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
        {
            return Failure(state, command, GeometryCode,
                $"{introduced.Code} · {introduced.Message} · {introduced.Path}");
        }

        var ids = string.Join(',', command.EntityIds.OrderBy(id => id.ToString()));
        var patch = new ProjectPatch("replace", $"areas/{area.Id}/entities/{ids}/scale",
            $"Scale {command.EntityIds.Count} entity or entities.");
        var inverse = new ProjectPatch("replace", $"areas/{area.Id}/entities/{ids}/scale",
            $"Restore scale for {command.EntityIds.Count} entity or entities.");
        if (command.DryRun)
        {
            return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        }

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

    private static bool IsValid(Vector3Value scale) =>
        float.IsFinite(scale.X) && float.IsFinite(scale.Y) && float.IsFinite(scale.Z) &&
        scale.X is >= MinimumScale and <= MaximumScale &&
        scale.Y is >= MinimumScale and <= MaximumScale &&
        scale.Z is >= MinimumScale and <= MaximumScale;

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

    private CommandResult Failure(ProjectState state, ScaleEntitiesCommand command, string code, string message) => new(
        false,
        state,
        command.BaseRevision,
        null,
        [new CommandDiagnostic(code, message)],
        null,
        null,
        Audit(command, false));

    private AuditEntry Audit(ScaleEntitiesCommand command, bool applied) =>
        new(command.CommandId, "scale_entities", _clock.UtcNow, applied);
}
