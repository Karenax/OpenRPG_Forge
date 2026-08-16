using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;

namespace Forge.RpgKit.Domain.Placeables;

public sealed record PlaceableConversationContext(
    PlaceableConversationTrigger Trigger,
    string Selector,
    BlueprintId? ItemBlueprintId,
    IReadOnlySet<Guid> CompletedOnceBindingIds);

public sealed record PlaceableConversationDecision(
    bool Matched,
    Guid? BindingId,
    Guid? DialogueId,
    string? EntryPointKey,
    bool OncePerUser,
    string Code);

public static class PlaceableConversationService
{
    public static PlaceableConversationDecision Resolve(
        PlaceableConversationProfile profile,
        PlaceableConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.CompletedOnceBindingIds);

        var matchingTrigger = profile.Bindings
            .Where(binding => Matches(binding, context))
            .OrderByDescending(binding => binding.Priority)
            .ThenBy(binding => binding.Id)
            .ToArray();
        var selected = matchingTrigger.FirstOrDefault(binding =>
            !binding.OncePerUser || !context.CompletedOnceBindingIds.Contains(binding.Id));
        if (selected is not null)
        {
            return new(
                true,
                selected.Id,
                selected.DialogueId,
                selected.EntryPointKey,
                selected.OncePerUser,
                "RPG_PLACEABLE_CONVERSATION_MATCHED");
        }

        return matchingTrigger.Length > 0
            ? new(false, null, null, null, false, "RPG_PLACEABLE_CONVERSATION_ALREADY_USED")
            : new(false, null, null, null, false, "RPG_PLACEABLE_CONVERSATION_NO_MATCH");
    }

    private static bool Matches(
        PlaceableConversationBinding binding,
        PlaceableConversationContext context)
    {
        if (binding.Trigger != context.Trigger)
        {
            return false;
        }
        return binding.Trigger == PlaceableConversationTrigger.Item
            ? binding.ItemBlueprintId is not null && binding.ItemBlueprintId == context.ItemBlueprintId
            : string.Equals(binding.Selector.Trim(), context.Selector.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
