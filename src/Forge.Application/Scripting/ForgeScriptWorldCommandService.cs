using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Scripting;

namespace Forge.Application.Scripting;

public enum ScriptMovementTaskStatus { Running, Completed, Cancelled }

public sealed record ScriptMovementTask(
    ulong Handle,
    EntityId EntityId,
    string Kind,
    string Mode,
    Vector3Value Destination,
    ScriptMovementTaskStatus Status);

public sealed record ForgeScriptWorldState(
    IReadOnlyList<AreaDefinition> Areas,
    IReadOnlyDictionary<ulong, AreaId> AreaHandles,
    IReadOnlyDictionary<ulong, EntityId> EntityHandles,
    IReadOnlyDictionary<ulong, ScriptMovementTask> MovementTasks,
    IReadOnlyDictionary<EntityId, Vector3Value> Velocities,
    long Revision);

public sealed record ForgeScriptWorldIntentCommand(
    Guid TransactionId,
    long ExpectedRevision,
    string Provenance,
    IReadOnlyList<ForgeScriptEvent> Events,
    bool RequiredAtRuntime);

public sealed record ForgeScriptWorldDiagnostic(
    string Code,
    string Path,
    Guid? SubjectId,
    string Severity,
    string Message);

public sealed record ForgeScriptWorldMutationResult(
    bool Succeeded,
    ForgeScriptWorldState State,
    long Revision,
    string SemanticDiff,
    IReadOnlyList<ForgeScriptWorldDiagnostic> Diagnostics);

/// <summary>
/// Translates the native host's opaque, structured intents into canonical area/entity mutations.
/// The service never exposes engine objects and preview never mutates the active state.
/// </summary>
public sealed class ForgeScriptWorldCommandService
{
    private readonly IReadOnlyDictionary<string, BlueprintId> _blueprints;
    private readonly Stack<ForgeScriptWorldState> _undo = [];
    private readonly Stack<ForgeScriptWorldState> _redo = [];

    public ForgeScriptWorldCommandService(IReadOnlyList<AreaDefinition> areas,
        IReadOnlyDictionary<string, BlueprintId> blueprints)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentNullException.ThrowIfNull(blueprints);
        if (areas.Count == 0) throw new ArgumentException("At least one canonical area is required.", nameof(areas));
        if (areas.Select(item => item.Id).Distinct().Count() != areas.Count)
            throw new ArgumentException("Canonical area IDs must be unique.", nameof(areas));
        _blueprints = new Dictionary<string, BlueprintId>(blueprints, StringComparer.Ordinal);
        var ordered = areas.OrderBy(item => item.Id.ToString(), StringComparer.Ordinal).ToArray();
        State = new(ordered, new Dictionary<ulong, AreaId>(),
            new Dictionary<ulong, EntityId>(), new Dictionary<ulong, ScriptMovementTask>(),
            new Dictionary<EntityId, Vector3Value>(), 0);
    }

    public ForgeScriptWorldState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ForgeScriptWorldMutationResult Preview(ForgeScriptWorldIntentCommand command) => Evaluate(command, State);

    public ForgeScriptWorldMutationResult Apply(ForgeScriptWorldIntentCommand command)
    {
        var result = Evaluate(command, State);
        if (!result.Succeeded) return result;
        _undo.Push(State);
        _redo.Clear();
        State = result.State;
        return result;
    }

    public ForgeScriptWorldMutationResult Undo()
    {
        if (_undo.Count == 0) return Rejected("script.world.undo_empty", "script/world/history", "Nothing to undo.");
        _redo.Push(State);
        State = _undo.Pop();
        return new(true, State, State.Revision, "undo:script-world-transaction", []);
    }

    public ForgeScriptWorldMutationResult Redo()
    {
        if (_redo.Count == 0) return Rejected("script.world.redo_empty", "script/world/history", "Nothing to redo.");
        _undo.Push(State);
        State = _redo.Pop();
        return new(true, State, State.Revision, "redo:script-world-transaction", []);
    }

    private ForgeScriptWorldMutationResult Evaluate(ForgeScriptWorldIntentCommand command,
        ForgeScriptWorldState source)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TransactionId == Guid.Empty || string.IsNullOrWhiteSpace(command.Provenance))
            return Rejected("script.world.command_identity_invalid", "script/world/command", "Transaction UUID and provenance are required.");
        if (command.ExpectedRevision != source.Revision)
            return Rejected("script.world.revision_conflict", "script/world/revision", $"Expected revision {command.ExpectedRevision}, active revision is {source.Revision}.");

        var areas = source.Areas.ToList();
        var areaHandles = source.AreaHandles.ToDictionary();
        var entityHandles = source.EntityHandles.ToDictionary();
        var tasks = source.MovementTasks.ToDictionary();
        var velocities = source.Velocities.ToDictionary();
        var diagnostics = new List<ForgeScriptWorldDiagnostic>();
        var mutations = 0;

        for (var index = 0; index < command.Events.Count; index++)
        {
            var @event = command.Events[index];
            var path = $"script/world/events/{index}";
            try
            {
                switch (@event.Kind)
                {
                    case "world.area":
                        areaHandles[ParseHandle(@event.Value)] = areas
                            .OrderBy(item => item.Id.ToString(), StringComparer.Ordinal).First().Id;
                        mutations++;
                        break;
                    case "world.position":
                        break;
                    case "world.query":
                        ApplyQuery(@event.Value, areas, areaHandles, entityHandles);
                        mutations++;
                        break;
                    case "world.spawn":
                        ApplySpawn(command.TransactionId, @event.Value, areas, areaHandles, entityHandles);
                        mutations++;
                        break;
                    case "world.destroy":
                        ApplyDestroy(@event.Value, areas, entityHandles);
                        mutations++;
                        break;
                    case "movement.started":
                        ApplyMovementStarted(@event.Value, entityHandles, tasks);
                        mutations++;
                        break;
                    case "movement.teleported":
                        ApplyTeleport(@event.Value, areas, areaHandles, entityHandles);
                        mutations++;
                        break;
                    case "movement.velocity":
                        ApplyVelocity(@event.Value, entityHandles, velocities);
                        mutations++;
                        break;
                    case "movement.completed":
                        ApplyMovementCompleted(@event.Value, tasks);
                        mutations++;
                        break;
                    default:
                        if (command.RequiredAtRuntime && (@event.Kind.StartsWith("world.", StringComparison.Ordinal) ||
                            @event.Kind.StartsWith("movement.", StringComparison.Ordinal)))
                            throw new InvalidOperationException($"Unsupported required intent '{@event.Kind}'.");
                        break;
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException)
            {
                diagnostics.Add(new("script.world.intent_invalid", path, command.TransactionId, "error",
                    exception.Message));
            }
        }

        if (diagnostics.Count > 0)
            return new(false, source, source.Revision, "no-change", diagnostics);
        var next = new ForgeScriptWorldState(areas, areaHandles, entityHandles, tasks, velocities,
            checked(source.Revision + 1));
        return new(true, next, next.Revision,
            $"script-world:{command.TransactionId:D}|events:{command.Events.Count}|mutations:{mutations}", []);
    }

    private static void ApplyQuery(string value, IReadOnlyList<AreaDefinition> areas,
        IReadOnlyDictionary<ulong, AreaId> areaHandles, IDictionary<ulong, EntityId> entityHandles)
    {
        var parts = Split(value, 3);
        var area = ResolveArea(ParseHandle(parts[0]), areas, areaHandles);
        var entity = area.Entities.Where(item => item.Tags.Contains(parts[1], StringComparer.Ordinal))
            .OrderBy(item => item.Id.ToString(), StringComparer.Ordinal).FirstOrDefault()
            ?? throw new InvalidOperationException($"No entity matches tag '{parts[1]}' in area '{area.Id}'.");
        entityHandles[ParseHandle(parts[2])] = entity.Id;
    }

    private void ApplySpawn(Guid transactionId, string value, IList<AreaDefinition> areas,
        IReadOnlyDictionary<ulong, AreaId> areaHandles, IDictionary<ulong, EntityId> entityHandles)
    {
        var parts = Split(value, 4);
        var area = ResolveArea(ParseHandle(parts[0]), areas, areaHandles);
        if (!_blueprints.TryGetValue(parts[1], out var blueprint))
            throw new InvalidOperationException($"Blueprint '{parts[1]}' is not available in the canonical catalog.");
        var handle = ParseHandle(parts[3]);
        var id = EntityId.From(DeterministicGuid(transactionId, handle));
        if (areas.SelectMany(item => item.Entities).Any(item => item.Id == id))
            throw new InvalidOperationException($"Entity UUID '{id}' already exists.");
        var entity = new EntityInstance(id, blueprint,
            new TransformValue(ParseVector(parts[2]), QuaternionValue.Identity, Vector3Value.One),
            ["script-spawned"]);
        ReplaceArea(areas, area.WithEntities([.. area.Entities, entity]));
        entityHandles[handle] = id;
    }

    private static void ApplyDestroy(string value, IList<AreaDefinition> areas,
        IReadOnlyDictionary<ulong, EntityId> entityHandles)
    {
        var entityId = ResolveEntity(ParseHandle(value), entityHandles);
        var area = areas.SingleOrDefault(item => item.Entities.Any(entity => entity.Id == entityId))
            ?? throw new InvalidOperationException($"Entity '{entityId}' is not present in the active world.");
        ReplaceArea(areas, area.WithEntities(area.Entities.Where(item => item.Id != entityId).ToArray()));
    }

    private static void ApplyMovementStarted(string value, IReadOnlyDictionary<ulong, EntityId> entityHandles,
        IDictionary<ulong, ScriptMovementTask> tasks)
    {
        var first = value.IndexOf(':');
        if (first <= 0) throw new FormatException("Movement intent has no task handle.");
        var taskHandle = ParseHandle(value[..first]);
        var payload = value[(first + 1)..];
        var parts = payload.Split(':');
        var kind = parts.ElementAtOrDefault(0) ?? throw new FormatException("Movement kind is missing.");
        var entity = ResolveEntity(ParseHandle(parts.ElementAtOrDefault(1) ?? string.Empty), entityHandles);
        var mode = kind == "move" ? parts.ElementAtOrDefault(2) ?? string.Empty : kind;
        var destinationText = kind == "move" ? parts.ElementAtOrDefault(3) : kind == "face" ? parts.ElementAtOrDefault(2) : null;
        var destination = destinationText is null ? Vector3Value.Zero : ParseVector(destinationText);
        if (kind is not ("move" or "face" or "follow") || mode is not ("walk" or "run" or "fly" or "swim" or "face" or "follow"))
            throw new InvalidOperationException($"Movement kind or mode '{kind}/{mode}' is not allowed.");
        if (tasks.ContainsKey(taskHandle)) throw new InvalidOperationException($"Movement task '{taskHandle}' already exists.");
        tasks.Add(taskHandle, new(taskHandle, entity, kind, mode, destination, ScriptMovementTaskStatus.Running));
    }

    private static void ApplyTeleport(string value, IList<AreaDefinition> areas,
        IReadOnlyDictionary<ulong, AreaId> areaHandles, IReadOnlyDictionary<ulong, EntityId> entityHandles)
    {
        var parts = Split(value, 3);
        var entityId = ResolveEntity(ParseHandle(parts[0]), entityHandles);
        var target = ResolveArea(ParseHandle(parts[1]), areas, areaHandles);
        var source = areas.SingleOrDefault(item => item.Entities.Any(entity => entity.Id == entityId))
            ?? throw new InvalidOperationException($"Entity '{entityId}' is not present in the active world.");
        var entity = source.Entities.Single(item => item.Id == entityId);
        var moved = new EntityInstance(entity.Id, entity.BlueprintId,
            new TransformValue(ParseVector(parts[2]), entity.Transform.Rotation, entity.Transform.Scale),
            entity.Tags, entity.Overrides, entity.BlueprintVersionRequirement);
        ReplaceArea(areas, source.WithEntities(source.Entities.Where(item => item.Id != entityId).ToArray()));
        target = areas.Single(item => item.Id == target.Id);
        ReplaceArea(areas, target.WithEntities([.. target.Entities, moved]));
    }

    private static void ApplyVelocity(string value, IReadOnlyDictionary<ulong, EntityId> entityHandles,
        IDictionary<EntityId, Vector3Value> velocities)
    {
        var parts = Split(value, 2);
        velocities[ResolveEntity(ParseHandle(parts[0]), entityHandles)] = ParseVector(parts[1]);
    }

    private static void ApplyMovementCompleted(string value, IDictionary<ulong, ScriptMovementTask> tasks)
    {
        var parts = Split(value, 2);
        var handle = ParseHandle(parts[0]);
        if (!tasks.TryGetValue(handle, out var task))
            throw new InvalidOperationException($"Movement task '{handle}' is unknown.");
        tasks[handle] = task with { Status = parts[1] == "cancelled" ? ScriptMovementTaskStatus.Cancelled : ScriptMovementTaskStatus.Completed };
    }

    private ForgeScriptWorldMutationResult Rejected(string code, string path, string message) =>
        new(false, State, State.Revision, "no-change", [new(code, path, null, "error", message)]);

    private static AreaDefinition ResolveArea(ulong handle, IEnumerable<AreaDefinition> areas,
        IReadOnlyDictionary<ulong, AreaId> areaHandles) =>
        areaHandles.TryGetValue(handle, out var id)
            ? areas.SingleOrDefault(item => item.Id == id)
              ?? throw new InvalidOperationException($"Area handle '{handle}' targets a missing area.")
            : throw new InvalidOperationException($"Area handle '{handle}' is unknown.");

    private static EntityId ResolveEntity(ulong handle, IReadOnlyDictionary<ulong, EntityId> entityHandles) =>
        entityHandles.TryGetValue(handle, out var id)
            ? id
            : throw new InvalidOperationException($"Entity handle '{handle}' is unknown.");

    private static ulong ParseHandle(string value) =>
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var handle) && handle > 0
            ? handle
            : throw new FormatException($"Opaque handle '{value}' is invalid.");

    private static Vector3Value ParseVector(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 3 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            throw new FormatException($"Vector '{value}' is invalid.");
        return new(x, y, z);
    }

    private static string[] Split(string value, int count)
    {
        var parts = value.Split(':', count);
        return parts.Length == count && parts.All(part => part.Length > 0)
            ? parts
            : throw new FormatException($"Intent payload '{value}' is invalid.");
    }

    private static void ReplaceArea(IList<AreaDefinition> areas, AreaDefinition replacement)
    {
        var index = areas.ToList().FindIndex(item => item.Id == replacement.Id);
        if (index < 0) throw new InvalidOperationException($"Area '{replacement.Id}' is missing.");
        areas[index] = replacement;
    }

    private static Guid DeterministicGuid(Guid transactionId, ulong handle)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{transactionId:D}:{handle}"));
        return new Guid(bytes[..16]);
    }
}
