using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Entities;

public sealed class ManipulateEntitiesHandler
{
    public const int CurrentSchemaVersion = 1;
    public const string RevisionConflictCode = "transform.revision_conflict";
    public const string SchemaCode = "transform.schema_unsupported";
    public const string ProvenanceCode = "transform.provenance_missing";
    public const string EmptySelectionCode = "transform.selection_empty";
    public const string DuplicateSelectionCode = "transform.selection_duplicate";
    public const string MissingAreaCode = "transform.area_missing";
    public const string MissingEntityCode = "transform.entity_missing";
    public const string ValueCode = "transform.value_invalid";
    public const string GeometryCode = "transform.geometry_invalid";

    private readonly IClock _clock;

    public ManipulateEntitiesHandler(IClock clock) => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public CommandResult Handle(ProjectState state, ManipulateEntitiesCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        if (command.SchemaVersion != CurrentSchemaVersion) return Failure(state, command, SchemaCode, "Schéma de manipulation non pris en charge.");
        if (string.IsNullOrWhiteSpace(command.Provenance)) return Failure(state, command, ProvenanceCode, "La provenance de modification est requise.");
        if (command.BaseRevision != state.Revision) return Failure(state, command, RevisionConflictCode, "La révision du projet a changé.");
        if (command.EntityIds.Count == 0) return Failure(state, command, EmptySelectionCode, "Sélectionnez au moins une entité.");
        if (command.EntityIds.Distinct().Count() != command.EntityIds.Count) return Failure(state, command, DuplicateSelectionCode, "La sélection contient un UUID dupliqué.");
        var area = state.Project.Areas.SingleOrDefault(candidate => candidate.Id == command.AreaId);
        if (area is null) return Failure(state, command, MissingAreaCode, "La zone active n’existe plus.");
        var entities = command.EntityIds.Select(id => area.Entities.SingleOrDefault(entity => entity.Id == id)).ToArray();
        if (entities.Any(entity => entity is null)) return Failure(state, command, MissingEntityCode, "Une entité sélectionnée n’existe plus.");
        if (!new[] { command.TranslationSnap, command.RotationSnapDegrees, command.ScaleSnap }.All(value => float.IsFinite(value) && value > 0))
            return Failure(state, command, ValueCode, "Les valeurs de snap doivent être finies et positives.");

        var selected = entities.Select(entity => entity!).ToArray();
        Dictionary<EntityId, EntityInstance> replacements;
        try
        {
            replacements = Transform(selected, command);
        }
        catch (ArgumentException exception)
        {
            return Failure(state, command, ValueCode, exception.Message);
        }

        var updatedArea = area.WithEntities(area.Entities.Select(entity =>
            replacements.GetValueOrDefault(entity.Id, entity)).ToArray());
        var updatedProject = new ProjectDefinition(state.Project.Id, state.Project.Title, state.Project.Module,
            state.Project.Blueprints, state.Project.Areas.Select(candidate => candidate.Id == area.Id ? updatedArea : candidate).ToArray(),
            state.Project.Dialogues, state.Project.Quests);
        var previous = AreaGeometryValidator.Validate(state.Project, area).Select(Identity).ToHashSet(StringComparer.Ordinal);
        var introduced = AreaGeometryValidator.Validate(updatedProject, updatedArea).FirstOrDefault(item => !previous.Contains(Identity(item)));
        if (introduced is not null) return Failure(state, command, GeometryCode, $"{introduced.Code} · {introduced.Message} · {introduced.Path}");

        var path = $"areas/{area.Id}/entities/{string.Join(',', command.EntityIds.OrderBy(id => id.Value))}/transform";
        var patch = new ProjectPatch("replace", path, $"{command.Tool} {command.EntityIds.Count} entity or entities.");
        var inverse = new ProjectPatch("replace", path, $"Restore {command.EntityIds.Count} entity transforms.");
        if (command.DryRun) return new CommandResult(true, state, command.BaseRevision, null, [], patch, inverse, Audit(command, false));
        var revision = state.Revision.Next();
        return new CommandResult(true, new ProjectState(updatedProject, revision), command.BaseRevision, revision,
            [], patch, inverse, Audit(command, true));
    }

    private static Dictionary<EntityId, EntityInstance> Transform(
        IReadOnlyList<EntityInstance> entities,
        ManipulateEntitiesCommand command)
    {
        var pivot = command.PivotMode switch
        {
            EditorPivotMode.ActiveObject => entities[0].Transform.Position,
            EditorPivotMode.SelectionCenter => new Vector3Value(
                entities.Average(entity => entity.Transform.Position.X),
                entities.Average(entity => entity.Transform.Position.Y),
                entities.Average(entity => entity.Transform.Position.Z)),
            _ => Vector3Value.Zero,
        };
        var rotationDelta = FromEulerDegrees(Snap(command.Value, command.RotationSnapDegrees));
        var scaleFactor = new Vector3Value(
            Snap(command.Value.X, command.ScaleSnap), Snap(command.Value.Y, command.ScaleSnap), Snap(command.Value.Z, command.ScaleSnap));
        if (command.Tool == EditorTransformTool.Scale &&
            (scaleFactor.X <= 0 || scaleFactor.Y <= 0 || scaleFactor.Z <= 0))
            throw new ArgumentException("Chaque facteur d’échelle doit être strictement positif.");

        var aligned = Align(entities, command.Alignment);
        var result = new Dictionary<EntityId, EntityInstance>();
        foreach (var entity in entities)
        {
            var source = entity.Transform;
            var position = aligned.GetValueOrDefault(entity.Id, source.Position);
            var rotation = source.Rotation;
            var scale = source.Scale;
            if (command.Alignment == EditorTransformAlignment.None)
            {
                switch (command.Tool)
                {
                    case EditorTransformTool.Translate:
                        var delta = Snap(command.Value, command.TranslationSnap);
                        if (command.CoordinateSpace == EditorCoordinateSpace.Local) delta = Rotate(source.Rotation, delta);
                        position = Add(source.Position, delta);
                        break;
                    case EditorTransformTool.Rotate:
                        rotation = Normalize(Multiply(rotationDelta, source.Rotation));
                        if (command.PivotMode != EditorPivotMode.Individual)
                            position = Add(pivot, Rotate(rotationDelta, Subtract(source.Position, pivot)));
                        break;
                    case EditorTransformTool.Scale:
                        scale = Multiply(source.Scale, scaleFactor);
                        if (scale.X is < .05f or > 100 || scale.Y is < .05f or > 100 || scale.Z is < .05f or > 100)
                            throw new ArgumentException("L’échelle résultante doit rester entre 0,05 et 100.");
                        if (command.PivotMode != EditorPivotMode.Individual)
                            position = Add(pivot, Multiply(Subtract(source.Position, pivot), scaleFactor));
                        break;
                }
            }
            var transformed = new TransformValue(position, rotation, scale);
            result[entity.Id] = new EntityInstance(entity.Id, entity.BlueprintId, transformed, entity.Tags,
                entity.Overrides, entity.BlueprintVersionRequirement);
        }
        return result;
    }

    private static Dictionary<EntityId, Vector3Value> Align(
        IReadOnlyList<EntityInstance> entities,
        EditorTransformAlignment alignment)
    {
        var result = new Dictionary<EntityId, Vector3Value>();
        if (alignment == EditorTransformAlignment.None) return result;
        var xs = entities.Select(entity => entity.Transform.Position.X).ToArray();
        var ys = entities.Select(entity => entity.Transform.Position.Y).ToArray();
        var zs = entities.Select(entity => entity.Transform.Position.Z).ToArray();
        var target = alignment switch
        {
            EditorTransformAlignment.MinimumX => xs.Min(),
            EditorTransformAlignment.MaximumX => xs.Max(),
            EditorTransformAlignment.CenterX => xs.Average(),
            EditorTransformAlignment.MinimumY => ys.Min(),
            EditorTransformAlignment.MaximumY => ys.Max(),
            EditorTransformAlignment.CenterY => ys.Average(),
            EditorTransformAlignment.MinimumZ => zs.Min(),
            EditorTransformAlignment.MaximumZ => zs.Max(),
            EditorTransformAlignment.CenterZ => zs.Average(),
            EditorTransformAlignment.Ground => 0,
            _ => 0,
        };
        foreach (var entity in entities)
        {
            var p = entity.Transform.Position;
            result[entity.Id] = alignment switch
            {
                EditorTransformAlignment.MinimumX or EditorTransformAlignment.MaximumX or EditorTransformAlignment.CenterX => new Vector3Value(target, p.Y, p.Z),
                EditorTransformAlignment.MinimumY or EditorTransformAlignment.MaximumY or EditorTransformAlignment.CenterY or EditorTransformAlignment.Ground => new Vector3Value(p.X, target, p.Z),
                _ => new Vector3Value(p.X, p.Y, target),
            };
        }
        return result;
    }

    private static Vector3Value Snap(Vector3Value value, float step) => new(Snap(value.X, step), Snap(value.Y, step), Snap(value.Z, step));
    private static float Snap(float value, float step) => MathF.Round(value / step) * step;
    private static Vector3Value Add(Vector3Value a, Vector3Value b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Vector3Value Subtract(Vector3Value a, Vector3Value b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Vector3Value Multiply(Vector3Value a, Vector3Value b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
    private static QuaternionValue FromEulerDegrees(Vector3Value value)
    {
        var x = value.X * MathF.PI / 360f; var y = value.Y * MathF.PI / 360f; var z = value.Z * MathF.PI / 360f;
        var cx = MathF.Cos(x); var sx = MathF.Sin(x); var cy = MathF.Cos(y); var sy = MathF.Sin(y); var cz = MathF.Cos(z); var sz = MathF.Sin(z);
        return Normalize(new QuaternionValue(sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz));
    }
    private static QuaternionValue Multiply(QuaternionValue a, QuaternionValue b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
    private static QuaternionValue Normalize(QuaternionValue q)
    {
        var length = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
        return new QuaternionValue(q.X / length, q.Y / length, q.Z / length, q.W / length);
    }
    private static Vector3Value Rotate(QuaternionValue q, Vector3Value v)
    {
        if (v == Vector3Value.Zero) return v;
        var unit = Normalize(q);
        var vector = new QuaternionValue(v.X, v.Y, v.Z, 0);
        var inverse = new QuaternionValue(-unit.X, -unit.Y, -unit.Z, unit.W);
        var rotated = Multiply(Multiply(unit, vector), inverse);
        return new Vector3Value(rotated.X, rotated.Y, rotated.Z);
    }
    private static string Identity(DomainDiagnostic item) => $"{item.Code}|{item.Path}|{item.Message}";
    private AuditEntry Audit(ManipulateEntitiesCommand command, bool applied) =>
        new(command.CommandId, "manipulate_entities", _clock.UtcNow, applied);
    private CommandResult Failure(ProjectState state, ManipulateEntitiesCommand command, string code, string message) =>
        new(false, state, command.BaseRevision, null, [new CommandDiagnostic(code, message)], null, null, Audit(command, false));
}
