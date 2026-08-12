using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableCompositionEventsHandle(Guid PlaceableId, Guid ProfileId);

public sealed record PlaceableCompositionEventsResolution(
    bool Available,
    bool OptionalFallback,
    PlaceableCompositionEventsHandle? Handle,
    string? DiagnosticCode);

public sealed record PlaceableCompositionEventsRuntime(
    PlaceableDamageEventsProfile DamageEvents,
    IReadOnlyList<AttachmentSocket> Sockets,
    IReadOnlyList<PlaceableAttachment> Attachments,
    PlaceableMultiVerbProfile MultiVerb,
    IReadOnlyList<ContextualVerb> Verbs);

public sealed class PlaceableCompositionEventsRuntimeAdapter
{
    public PlaceableCompositionEventsResolution Resolve(PlaceableWorldProfile placeable)
    {
        if (placeable.CompositionEvents is null)
        {
            return placeable.CompositionEventsRequiredAtRuntime
                ? new(false, false, null, "WORLD_PLACEABLE_COMPOSITION_EVENTS_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        return new(true, false, new(placeable.Id, placeable.CompositionEvents.Id), null);
    }

    public PlaceableCompositionEventsRuntime Build(
        PlaceableCompositionEventsHandle handle,
        PlaceableWorldProfile placeable)
    {
        var profile = Require(handle, placeable);
        return new(
            profile.DamageEvents,
            profile.Sockets.Select(socket => new AttachmentSocket(socket.SocketId, socket.Kind,
                Transform(socket.LocalTransform), socket.AllowedAttachmentTags.ToHashSet(StringComparer.Ordinal),
                socket.SnapEnabled, socket.MaximumAttachments)).ToArray(),
            profile.Attachments.Select(attachment => new PlaceableAttachment(attachment.Id,
                attachment.SocketId, attachment.ChildBlueprintId.Value,
                attachment.Tags.ToHashSet(StringComparer.Ordinal), Transform(attachment.Transform),
                attachment.PrefabId)).ToArray(),
            profile.MultiVerb,
            profile.MultiVerb.Verbs.Select(verb => new ContextualVerb(verb.VerbId, verb.LabelKey,
                verb.BasePriority, verb.RequiredContext.ToHashSet(StringComparer.Ordinal),
                verb.ForbiddenContext.ToHashSet(StringComparer.Ordinal), verb.EventBinding)).ToArray());
    }

    public PlaceableBoundEvent<EntityEventPayload> Damage(
        PlaceableCompositionEventsHandle handle,
        PlaceableWorldProfile placeable,
        string sourceId,
        double amount,
        string damageType,
        double previousHealth,
        double currentHealth,
        IReadOnlyDictionary<string, string>? context = null)
    {
        var runtime = Build(handle, placeable);
        var payload = PlaceableEventService.DamageEvent(placeable.Id, sourceId, amount, damageType,
            previousHealth, currentHealth, placeable.CompositionEvents!.Provenance, context);
        return new(runtime.DamageEvents.ForgeScriptApiVersion,
            currentHealth <= 0 ? runtime.DamageEvents.DestroyedBinding : runtime.DamageEvents.DamagedBinding,
            payload);
    }

    public AttachmentResult Attach(
        PlaceableCompositionEventsHandle handle,
        PlaceableWorldProfile placeable,
        Guid attachmentId,
        IReadOnlyList<PlaceableAttachment>? current = null)
    {
        var runtime = Build(handle, placeable);
        var attachment = runtime.Attachments.Single(item => item.Id == attachmentId);
        var socket = runtime.Sockets.Single(item => item.Id == attachment.SocketId);
        return PlaceableEventService.Attach(current ?? [], socket, attachment);
    }

    public IReadOnlyList<ContextualVerb> ResolveVerbs(
        PlaceableCompositionEventsHandle handle,
        PlaceableWorldProfile placeable,
        IReadOnlySet<string> context)
        => PlaceableEventService.ResolveVerbs(Build(handle, placeable).Verbs, context);

    private static PlaceableCompositionEventsProfile Require(
        PlaceableCompositionEventsHandle handle,
        PlaceableWorldProfile placeable)
    {
        if (handle.PlaceableId != placeable.Id || placeable.CompositionEvents is null ||
            handle.ProfileId != placeable.CompositionEvents.Id)
            throw new InvalidOperationException("WORLD_PLACEABLE_COMPOSITION_EVENTS_HANDLE_STALE");
        return placeable.CompositionEvents;
    }

    private static PlaceableTransform Transform(PlaceableWorldProfile.TransformPolicy transform)
        => new(
            new(transform.Position.X, transform.Position.Y, transform.Position.Z),
            new(transform.RotationDegrees.X, transform.RotationDegrees.Y, transform.RotationDegrees.Z),
            new(transform.Scale.X, transform.Scale.Y, transform.Scale.Z),
            new(transform.Pivot.X, transform.Pivot.Y, transform.Pivot.Z),
            transform.SnapIncrement, transform.ParentId, transform.AlignToSurface,
            transform.TransformAnimationId);
}
