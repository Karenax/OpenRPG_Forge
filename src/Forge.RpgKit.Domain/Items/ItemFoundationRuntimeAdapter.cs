using Forge.Domain.Items;

namespace Forge.RpgKit.Domain.Items;

public sealed record ItemCreationPolicyResolution(
    ItemPolicyBundle? Policies,
    bool OptionalFallback,
    IReadOnlyList<ItemFoundationDiagnostic> Diagnostics);

public static class ItemFoundationRuntimeAdapter
{
    public static ItemCreationPolicyResolution Resolve(ItemFoundationProfile? foundation, bool required = false)
    {
        var resolved = ItemFoundationValidator.ResolveCreation(foundation, required);
        if (!resolved.Succeeded || resolved.Profile is null)
            return new(null, resolved.UsedFallback, resolved.Diagnostics);
        var creation = resolved.Profile;
        return new(new(
            foundation!.Id,
            new(foundation.Id, creation.Appearance.MeshAssetIds, creation.Appearance.MaterialAssetIds,
                creation.Appearance.Sockets.ToHashSet(StringComparer.Ordinal), creation.Appearance.IconAssetId,
                creation.Appearance.VisualEffectAssetIds, creation.Appearance.EquippedPreviewAssetId),
            new(creation.Inventory.Mass, creation.Inventory.Volume, creation.Inventory.GridWidth,
                creation.Inventory.GridHeight, creation.Inventory.StackMassFactor,
                creation.Inventory.StackVolumeFactor),
            new(creation.Stack.Maximum, creation.Stack.CompatibilityKey, creation.Stack.Replicated),
            new(creation.Usage.TotalCharges, creation.Usage.CurrentCharges, creation.Usage.UsesPerRest,
                creation.Usage.UsesRemaining, TimeSpan.FromMilliseconds(creation.Usage.CooldownMilliseconds), null,
                creation.Usage.Durability, creation.Usage.Consumable, creation.Usage.RechargeRule),
            new(false, false, false, false, false, false)), false, []);
    }
}
