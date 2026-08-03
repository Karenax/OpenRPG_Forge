namespace Forge.RpgKit.Domain.Placeables;

public sealed record LockStateEvent(Guid EventId, int SchemaVersion, string Provenance, string Name, Guid PlaceableId, LockState PreviousState, LockState RequestedState, string Method, string? Credential, string ActorId, bool Vetoed, string? VetoReason);
public sealed record ContainerContentDelta(Guid ItemInstanceId, int QuantityDelta, string Operation, string? SourceContainerId, string? TargetContainerId);
public sealed record ContainerContentsChanged(Guid EventId, int SchemaVersion, string Provenance, Guid PlaceableId, long PreviousRevision, long CurrentRevision, IReadOnlyList<ContainerContentDelta> Delta, string Cause, string ActorId);
public sealed record EntityEventPayload(Guid EventId, int SchemaVersion, string Provenance, Guid EntityId, string EntityKind, string Name, string SourceId, double Amount, string DamageType, double PreviousHealth, double CurrentHealth, IReadOnlyDictionary<string, string> Context);
public sealed record AttachmentSocket(string Id, string Kind, PlaceableTransform LocalTransform, IReadOnlySet<string> AllowedAttachmentTags, bool SnapEnabled, int MaximumAttachments);
public sealed record PlaceableAttachment(Guid Id, string SocketId, Guid ChildDefinitionId, IReadOnlySet<string> Tags, PlaceableTransform Transform, string? PrefabId);
public sealed record AttachmentResult(IReadOnlyList<PlaceableAttachment> Attachments, PlaceableAttachment Attached, string Patch, string InversePatch);
public sealed record ContextualVerb(string Id, string Label, int BasePriority, IReadOnlySet<string> RequiredContext, IReadOnlySet<string> ForbiddenContext, string EventName);

public static class PlaceableEventService
{
    public static (LockStateEvent Changing, LockStateEvent? Changed, LockComponent Lock) ChangeLockState(LockComponent component, Guid placeableId, LockState requested, string method, string? credential, string actorId, string provenance, Func<LockStateEvent, string?>? veto = null)
    {
        var changing = new LockStateEvent(Guid.NewGuid(), 1, provenance, "LockStateChanging", placeableId, component.State, requested, method, credential, actorId, false, null);
        var reason = veto?.Invoke(changing);
        if (!string.IsNullOrWhiteSpace(reason)) return (changing with { Vetoed = true, VetoReason = reason }, null, component);
        var updated = component with { State = requested }; var changed = changing with { EventId = Guid.NewGuid(), Name = "LockStateChanged" };
        return (changing, changed, updated);
    }

    public static ContainerContentsChanged ContentsChanged(Guid placeableId, long previousRevision, IReadOnlyList<ContainerContentDelta> delta, string cause, string actorId, string provenance)
    {
        if (previousRevision < 0 || delta.Count == 0 || delta.Any(item => item.ItemInstanceId == Guid.Empty || item.QuantityDelta == 0) || string.IsNullOrWhiteSpace(cause)) throw new InvalidOperationException("RPG_PLACEABLE_CONTENT_DELTA_INVALID");
        return new(Guid.NewGuid(), 1, provenance, placeableId, previousRevision, previousRevision + 1, delta, cause, actorId);
    }

    public static EntityEventPayload DamageEvent(Guid entityId, string sourceId, double amount, string damageType, double previousHealth, double currentHealth, string provenance, IReadOnlyDictionary<string, string>? context = null)
    {
        if (entityId == Guid.Empty || amount < 0 || currentHealth < 0 || currentHealth > previousHealth) throw new InvalidOperationException("RPG_ENTITY_DAMAGE_EVENT_INVALID");
        return new(Guid.NewGuid(), 1, provenance, entityId, "placeable", currentHealth <= 0 ? "EntityDestroyed" : "DamageApplied", sourceId, amount, damageType, previousHealth, currentHealth, context ?? new Dictionary<string, string>());
    }

    public static AttachmentResult Attach(IReadOnlyList<PlaceableAttachment> current, AttachmentSocket socket, PlaceableAttachment attachment)
    {
        if (attachment.SocketId != socket.Id || !socket.AllowedAttachmentTags.Overlaps(attachment.Tags)) throw new InvalidOperationException("RPG_PLACEABLE_ATTACHMENT_INCOMPATIBLE");
        if (current.Count(item => item.SocketId == socket.Id) >= socket.MaximumAttachments) throw new InvalidOperationException("RPG_PLACEABLE_SOCKET_FULL");
        var attached = socket.SnapEnabled ? attachment with { Transform = socket.LocalTransform } : attachment;
        return new(current.Append(attached).OrderBy(item => item.SocketId, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray(), attached, $"attach:/sockets/{socket.Id}/{attached.Id}", $"detach:/sockets/{socket.Id}/{attached.Id}");
    }

    public static IReadOnlyList<ContextualVerb> ResolveVerbs(IEnumerable<ContextualVerb> verbs, IReadOnlySet<string> context)
        => verbs.Where(verb => verb.RequiredContext.All(context.Contains) && !verb.ForbiddenContext.Any(context.Contains)).OrderByDescending(verb => verb.BasePriority).ThenBy(verb => StandardOrder(verb.Id)).ThenBy(verb => verb.Id, StringComparer.Ordinal).ToArray();

    private static int StandardOrder(string verb) => verb.ToLowerInvariant() switch { "inspect" => 0, "use" => 1, "open" => 2, "talk" => 3, "take" => 4, "push" => 5, "climb" => 6, _ => 100 };
}
