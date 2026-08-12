using Forge.Domain.Items;

namespace Forge.RpgKit.Domain.Items;

public sealed record ItemGameplayPolicyResolution(
    CriticalItemPolicy? Critical,
    TransferPolicy? Transfer,
    IReadOnlyList<ItemModifierDefinition> Modifiers,
    ItemAbilityBinding? Ability,
    IReadOnlyDictionary<string, EquipmentSlotDefinition> Slots,
    IReadOnlySet<string> RequiredSlots,
    bool OptionalFallback,
    IReadOnlyList<ItemFoundationDiagnostic> Diagnostics);

public static class ItemGameplayRuntimeAdapter
{
    public static ItemGameplayPolicyResolution Resolve(ItemFoundationProfile? foundation, bool required = false)
    {
        var resolved = ItemFoundationValidator.ResolveGameplay(foundation, required);
        if (!resolved.Succeeded || resolved.Profile is null)
            return new(null, null, [], null, new Dictionary<string, EquipmentSlotDefinition>(),
                new HashSet<string>(), resolved.UsedFallback, resolved.Diagnostics);
        var profile = resolved.Profile;
        var destinations = profile.Transfer.AllowedDestinations.ToDictionary(
            item => Enum.Parse<TransferDestination>(item.Key.ToString()), item => item.Value);
        var slots = profile.Equipment.SlotDefinitions.ToDictionary(item => item.Id,
            item => new EquipmentSlotDefinition(item.Id, item.Ruleset, item.Rig, item.Layer,
                item.Conflicts.ToHashSet(StringComparer.Ordinal)), StringComparer.Ordinal);
        return new(
            new(profile.Critical.ProtectDestruction, profile.Critical.ProtectSale,
                profile.Critical.ProtectDrop, profile.Critical.ProtectTheft,
                profile.Critical.ProtectConsumption, profile.Critical.ProtectCleanup),
            new(destinations, profile.Transfer.RequiredPermissions.ToHashSet(StringComparer.Ordinal)),
            profile.Modifiers.Select(item => new ItemModifierDefinition(item.Id, item.SchemaVersion,
                item.Provenance, item.Kind, item.Parameters, item.StackingRule, item.Condition ?? string.Empty,
                item.UiSummary, item.BeforeHook, item.AfterHook)).ToArray(),
            profile.Ability is null ? null : new(profile.Ability.AbilityDefinitionId, profile.Ability.Targeting,
                profile.Ability.Costs, profile.Ability.ChargeCost,
                TimeSpan.FromMilliseconds(profile.Ability.CooldownMilliseconds),
                profile.Ability.RequestedEvent, profile.Ability.ResolvedEvent),
            slots, profile.Equipment.RequiredSlots.ToHashSet(StringComparer.Ordinal), false, []);
    }

    public static ItemActionDecision AuthorizeCriticalAction(
        ItemGameplayProfile profile,
        string action,
        bool gameMasterOverride,
        string actorId,
        string reason)
    {
        if (gameMasterOverride && !profile.Critical.AllowAuditedGameMasterOverride)
            return new(false, "RPG_ITEM_GM_OVERRIDE_FORBIDDEN", $"blocked-gm-override:{action}:{actorId}");
        var critical = new CriticalItemPolicy(profile.Critical.ProtectDestruction, profile.Critical.ProtectSale,
            profile.Critical.ProtectDrop, profile.Critical.ProtectTheft, profile.Critical.ProtectConsumption,
            profile.Critical.ProtectCleanup);
        return ItemInventoryService.AuthorizeCriticalAction(critical, action, gameMasterOverride, actorId, reason);
    }
}
