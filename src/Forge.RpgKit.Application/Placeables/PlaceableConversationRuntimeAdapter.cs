using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Application.Placeables;

public sealed record PlaceableConversationHandle(Guid PlaceableId, Guid ConversationId);

public sealed record PlaceableConversationResolution(
    bool Available,
    bool OptionalFallback,
    PlaceableConversationHandle? Handle,
    string? DiagnosticCode);

public sealed record PlaceableConversationRuntime(
    IReadOnlyList<PlaceableConversationBinding> Bindings);

public sealed class PlaceableConversationRuntimeAdapter
{
    public PlaceableConversationResolution Resolve(
        PlaceableWorldProfile placeable,
        IReadOnlyList<DialogueDefinition> dialogues,
        IReadOnlyList<BlueprintDefinition> blueprints)
    {
        ArgumentNullException.ThrowIfNull(placeable);
        ArgumentNullException.ThrowIfNull(dialogues);
        ArgumentNullException.ThrowIfNull(blueprints);
        if (placeable.Conversation is null)
        {
            return placeable.ConversationRequiredAtRuntime
                ? new(false, false, null, "WORLD_PLACEABLE_CONVERSATION_REQUIRED_MISSING")
                : new(false, true, null, null);
        }

        var diagnostic = Validate(placeable, dialogues, blueprints).FirstOrDefault();
        return diagnostic is null
            ? new(true, false, new(placeable.Id, placeable.Conversation.Id), null)
            : new(false, false, null, diagnostic);
    }

    public PlaceableConversationRuntime Build(
        PlaceableConversationHandle handle,
        PlaceableWorldProfile placeable,
        IReadOnlyList<DialogueDefinition> dialogues,
        IReadOnlyList<BlueprintDefinition> blueprints)
    {
        var conversation = Require(handle, placeable);
        var diagnostic = Validate(placeable, dialogues, blueprints).FirstOrDefault();
        if (diagnostic is not null)
        {
            throw new InvalidOperationException(diagnostic);
        }
        return new(conversation.Bindings
            .OrderByDescending(binding => binding.Priority)
            .ThenBy(binding => binding.Id)
            .ToArray());
    }

    public PlaceableConversationDecision Evaluate(
        PlaceableConversationHandle handle,
        PlaceableWorldProfile placeable,
        PlaceableConversationContext context)
    {
        var conversation = Require(handle, placeable);
        return PlaceableConversationService.Resolve(conversation, context);
    }

    private static PlaceableConversationProfile Require(
        PlaceableConversationHandle handle,
        PlaceableWorldProfile placeable)
    {
        if (handle.PlaceableId != placeable.Id || placeable.Conversation is null ||
            handle.ConversationId != placeable.Conversation.Id)
        {
            throw new InvalidOperationException("WORLD_PLACEABLE_CONVERSATION_HANDLE_STALE");
        }
        return placeable.Conversation;
    }

    private static IReadOnlyList<string> Validate(
        PlaceableWorldProfile placeable,
        IReadOnlyList<DialogueDefinition> dialogues,
        IReadOnlyList<BlueprintDefinition> blueprints)
    {
        var entryPoints = dialogues.ToDictionary(
            dialogue => dialogue.Id.Value,
            dialogue => (IReadOnlySet<string>)dialogue.EntryPoints
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal));
        var items = blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.Item)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        return PlaceableConversationProfileValidator.Validate(
                placeable.Conversation!,
                entryPoints,
                items,
                $"module/worldFeatures/placeables/{placeable.Id}/conversation")
            .Select(diagnostic => diagnostic.Code.ToUpperInvariant().Replace('.', '_'))
            .ToArray();
    }
}
