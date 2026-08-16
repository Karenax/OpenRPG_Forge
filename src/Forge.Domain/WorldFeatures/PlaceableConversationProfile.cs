using Forge.Domain.Blueprints;
using Forge.Domain.Validation;

namespace Forge.Domain.WorldFeatures;

public enum PlaceableConversationTrigger
{
    Interaction,
    State,
    User,
    Item,
    Event,
}

public sealed record PlaceableConversationBinding(
    Guid Id,
    Guid DialogueId,
    PlaceableConversationTrigger Trigger,
    string Selector,
    BlueprintId? ItemBlueprintId,
    string EntryPointKey,
    int Priority,
    bool OncePerUser);

public sealed record PlaceableConversationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<PlaceableConversationBinding> Bindings)
{
    public const int CurrentSchemaVersion = 1;
}

public static class PlaceableConversationProfileValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(
        PlaceableConversationProfile profile,
        IReadOnlyDictionary<Guid, IReadOnlySet<string>> dialogueEntryPoints,
        IReadOnlySet<BlueprintId> itemBlueprintIds,
        string root)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(dialogueEntryPoints);
        ArgumentNullException.ThrowIfNull(itemBlueprintIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty ||
            profile.SchemaVersion != PlaceableConversationProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
        {
            diagnostics.Add(new(
                "world.placeable.conversation_identity_invalid",
                "Placeable conversation requires a stable identity, supported schema and provenance.",
                root));
        }

        if (profile.Bindings.Count == 0)
        {
            diagnostics.Add(new(
                "world.placeable.conversation_bindings_empty",
                "Placeable conversation requires at least one dialogue binding.",
                $"{root}/bindings"));
            return diagnostics;
        }

        foreach (var duplicate in profile.Bindings
                     .GroupBy(binding => binding.Id)
                     .Where(group => group.Key == Guid.Empty || group.Count() > 1))
        {
            diagnostics.Add(new(
                "world.placeable.conversation_binding_uuid_invalid",
                "Dialogue binding UUIDs must be non-empty and unique.",
                $"{root}/bindings/{duplicate.Key}"));
        }

        foreach (var binding in profile.Bindings)
        {
            var path = $"{root}/bindings/{binding.Id}";
            if (binding.DialogueId == Guid.Empty || !dialogueEntryPoints.TryGetValue(binding.DialogueId, out var entryPoints))
            {
                diagnostics.Add(new(
                    "world.placeable.conversation_dialogue_missing",
                    "Placeable conversation binding references an unknown dialogue.",
                    $"{path}/dialogueId"));
            }
            else if (string.IsNullOrWhiteSpace(binding.EntryPointKey) ||
                     !entryPoints.Contains(binding.EntryPointKey))
            {
                diagnostics.Add(new(
                    "world.placeable.conversation_entry_point_missing",
                    "Placeable conversation binding references an unknown dialogue entry point.",
                    $"{path}/entryPointKey"));
            }

            if (binding.Priority is < -1_000 or > 1_000)
            {
                diagnostics.Add(new(
                    "world.placeable.conversation_priority_invalid",
                    "Dialogue binding priority must remain between -1000 and 1000.",
                    $"{path}/priority"));
            }

            if (binding.Trigger == PlaceableConversationTrigger.Item)
            {
                if (binding.ItemBlueprintId is null || !itemBlueprintIds.Contains(binding.ItemBlueprintId))
                {
                    diagnostics.Add(new(
                        "world.placeable.conversation_item_missing",
                        "Item-triggered dialogue binding requires an existing item blueprint.",
                        $"{path}/itemBlueprintId"));
                }
                if (!string.IsNullOrWhiteSpace(binding.Selector))
                {
                    diagnostics.Add(new(
                        "world.placeable.conversation_selector_invalid",
                        "Item-triggered dialogue binding must use the selected item rather than a text selector.",
                        $"{path}/selector"));
                }
            }
            else
            {
                if (binding.ItemBlueprintId is not null || !ValidSelector(binding.Selector))
                {
                    diagnostics.Add(new(
                        "world.placeable.conversation_selector_invalid",
                        "Dialogue binding requires a short human-readable selector matching its trigger.",
                        $"{path}/selector"));
                }
            }
        }

        return diagnostics;
    }

    private static bool ValidSelector(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 96 &&
        value.All(character => char.IsLetterOrDigit(character) ||
                               character is '-' or '_' or ':' or '.' or '/' or ' ');
}
