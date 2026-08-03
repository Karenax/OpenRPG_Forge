namespace Forge.RpgKit.Domain.Items;

public sealed record ItemVisualDefinition(Guid ItemDefinitionId, IReadOnlyList<string> Meshes, IReadOnlyList<string> Materials, IReadOnlySet<string> Sockets, string IconAssetId, IReadOnlyList<string> VisualEffects, string EquippedPreviewAssetId);
public sealed record InventoryPolicy(double Mass, double Volume, int GridWidth, int GridHeight, double StackMassFactor, double StackVolumeFactor);
public sealed record StackPolicy(int Maximum, string CompatibilityKey, bool Replicated);
public sealed record ItemStack(Guid InstanceId, Guid DefinitionId, string CompatibilityKey, int Quantity, long Revision);
public sealed record UsageResource(int TotalCharges, int CurrentCharges, int UsesPerRest, int UsesRemaining, TimeSpan Cooldown, DateTimeOffset? CooldownUntil, int Durability, bool Consumable, string RechargeRule);
public sealed record CriticalItemPolicy(bool ProtectDestruction, bool ProtectSale, bool ProtectDrop, bool ProtectTheft, bool ProtectConsumption, bool ProtectCleanup);
public sealed record ItemPolicyBundle(Guid ItemDefinitionId, ItemVisualDefinition Visual, InventoryPolicy Inventory, StackPolicy Stack, UsageResource Usage, CriticalItemPolicy Critical);
public sealed record StackMutation(ItemStack Primary, ItemStack? Secondary, string EventName, long Revision);
public sealed record ItemActionDecision(bool Allowed, string Code, string AuditEntry);

public static class ItemInventoryService
{
    public static IReadOnlyList<ItemDiagnostic> Validate(ItemPolicyBundle bundle)
    {
        var diagnostics = new List<ItemDiagnostic>(); var root = $"items/{bundle.ItemDefinitionId}/policies";
        if (bundle.ItemDefinitionId == Guid.Empty || bundle.Visual.ItemDefinitionId != bundle.ItemDefinitionId || bundle.Visual.Meshes.Count == 0 || string.IsNullOrWhiteSpace(bundle.Visual.IconAssetId)) diagnostics.Add(new("RPG_ITEM_VISUAL_INVALID", $"{root}/visual", "Visual meshes and icon are required."));
        if (bundle.Inventory.Mass < 0 || bundle.Inventory.Volume < 0 || bundle.Inventory.GridWidth < 1 || bundle.Inventory.GridHeight < 1 || bundle.Inventory.StackMassFactor is < 0 or > 1 || bundle.Inventory.StackVolumeFactor is < 0 or > 1) diagnostics.Add(new("RPG_ITEM_INVENTORY_POLICY_INVALID", $"{root}/inventory", "Mass, volume, footprint and stack contribution are invalid."));
        if (bundle.Stack.Maximum < 1 || string.IsNullOrWhiteSpace(bundle.Stack.CompatibilityKey)) diagnostics.Add(new("RPG_ITEM_STACK_POLICY_INVALID", $"{root}/stack", "Stack maximum and compatibility key are required."));
        if (bundle.Usage.TotalCharges < 0 || bundle.Usage.CurrentCharges is < 0 || bundle.Usage.CurrentCharges > bundle.Usage.TotalCharges || bundle.Usage.UsesPerRest < 0 || bundle.Usage.UsesRemaining < 0 || bundle.Usage.UsesRemaining > bundle.Usage.UsesPerRest || bundle.Usage.Durability is < 0 or > 100 || string.IsNullOrWhiteSpace(bundle.Usage.RechargeRule)) diagnostics.Add(new("RPG_ITEM_USAGE_INVALID", $"{root}/usage", "Usage resource is outside its valid range."));
        return diagnostics;
    }

    public static StackMutation Merge(ItemStack primary, ItemStack secondary, StackPolicy policy, long expectedRevision)
    {
        if (primary.Revision != expectedRevision || secondary.Revision != expectedRevision) throw new InvalidOperationException("RPG_ITEM_STACK_REVISION_CONFLICT");
        if (primary.DefinitionId != secondary.DefinitionId || primary.CompatibilityKey != policy.CompatibilityKey || secondary.CompatibilityKey != policy.CompatibilityKey) throw new InvalidOperationException("RPG_ITEM_STACK_INCOMPATIBLE");
        var moved = Math.Min(secondary.Quantity, policy.Maximum - primary.Quantity); if (moved <= 0) throw new InvalidOperationException("RPG_ITEM_STACK_FULL");
        var revision = expectedRevision + 1; var remaining = secondary.Quantity - moved;
        return new(primary with { Quantity = primary.Quantity + moved, Revision = revision }, remaining == 0 ? null : secondary with { Quantity = remaining, Revision = revision }, policy.Replicated ? "ItemStackMergedReplicated" : "ItemStackMerged", revision);
    }

    public static StackMutation Split(ItemStack stack, int quantity, long expectedRevision)
    {
        if (stack.Revision != expectedRevision) throw new InvalidOperationException("RPG_ITEM_STACK_REVISION_CONFLICT");
        if (quantity < 1 || quantity >= stack.Quantity) throw new InvalidOperationException("RPG_ITEM_STACK_SPLIT_INVALID");
        var revision = expectedRevision + 1;
        return new(stack with { Quantity = stack.Quantity - quantity, Revision = revision }, new(Guid.NewGuid(), stack.DefinitionId, stack.CompatibilityKey, quantity, revision), "ItemStackSplit", revision);
    }

    public static UsageResource Consume(UsageResource usage, DateTimeOffset now)
    {
        if (usage.CooldownUntil > now) throw new InvalidOperationException("RPG_ITEM_USAGE_COOLDOWN");
        if (usage.CurrentCharges <= 0 || usage.UsesRemaining <= 0 || usage.Durability <= 0) throw new InvalidOperationException("RPG_ITEM_USAGE_EXHAUSTED");
        return usage with { CurrentCharges = usage.CurrentCharges - 1, UsesRemaining = usage.UsesRemaining - 1, Durability = Math.Max(0, usage.Durability - (usage.Consumable ? 100 : 1)), CooldownUntil = now + usage.Cooldown };
    }

    public static ItemActionDecision AuthorizeCriticalAction(CriticalItemPolicy policy, string action, bool gameMasterOverride, string actorId, string reason)
    {
        var protectedAction = action.ToLowerInvariant() switch { "destroy" => policy.ProtectDestruction, "sell" => policy.ProtectSale, "drop" => policy.ProtectDrop, "steal" => policy.ProtectTheft, "consume" => policy.ProtectConsumption, "cleanup" => policy.ProtectCleanup, _ => false };
        if (!protectedAction) return new(true, "RPG_ITEM_ACTION_ALLOWED", $"allowed:{action}:{actorId}");
        if (gameMasterOverride) return new(true, "RPG_ITEM_GM_OVERRIDE", $"gm-override:{action}:{actorId}:{reason}");
        return new(false, "RPG_ITEM_CRITICAL_PROTECTED", $"blocked:{action}:{actorId}");
    }

    public static (double Mass, double Volume) Contribution(InventoryPolicy policy, int quantity)
    {
        if (quantity < 1) return (0, 0);
        return (policy.Mass * (1 + ((quantity - 1) * policy.StackMassFactor)), policy.Volume * (1 + ((quantity - 1) * policy.StackVolumeFactor)));
    }
}
