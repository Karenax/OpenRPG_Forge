using Forge.Domain.Blueprints;
using Forge.Domain.Items;

namespace Forge.Application.Blueprints;

public readonly record struct ItemAdvancedHandle(Guid Value);

public sealed record ItemAdvancedSnapshot(
    ItemAdvancedHandle Handle,
    BlueprintId BlueprintId,
    decimal BaseCost,
    bool Identifiable,
    int ProcCount,
    bool IsWeapon,
    bool IsArmor,
    bool UsesAmmo,
    bool UsesDurability,
    bool IsContainer,
    int RecipeCount,
    bool IsProcedural);

public sealed class ItemAdvancedScriptService
{
    private readonly BlueprintDefinition _blueprint;
    private ItemAdvancedProfile Profile => _blueprint.ItemAdvanced!;

    public ItemAdvancedScriptService(BlueprintDefinition blueprint)
    {
        _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
        if (blueprint.Kind != BlueprintKind.Item || blueprint.ItemAdvanced is null)
            throw new ArgumentException("Advanced item data is not configured.", nameof(blueprint));
    }

    public ItemAdvancedHandle Handle => new(Profile.Id);

    public ItemAdvancedSnapshot Read(ItemAdvancedHandle handle)
    {
        Ensure(handle);
        return new(handle, _blueprint.Id, Profile.Economy.BaseCost, Profile.Identification.Levels.Count > 1,
            Profile.OnHitProcs.Count, Profile.Weapon is not null, Profile.Armor is not null, Profile.Ammo is not null,
            Profile.Durability?.Enabled == true, Profile.Container is not null, Profile.Recipes.Count,
            Profile.Generator?.Enabled == true);
    }

    public ItemRequirementDecision CanUse(ItemAdvancedHandle handle, ItemRuntimeContext context)
    {
        Ensure(handle);
        return ItemAdvancedRuntimeResolver.ResolveRequirements(Profile, context);
    }

    public decimal EconomicValue(ItemAdvancedHandle handle, ItemRuntimeContext context)
    {
        Ensure(handle);
        return ItemAdvancedRuntimeResolver.ResolveEconomicValue(Profile, context);
    }

    public ItemGenerationResult Generate(ItemAdvancedHandle handle, ItemRuntimeContext context)
    {
        Ensure(handle);
        return ItemAdvancedRuntimeResolver.Generate(Profile, context);
    }

    private void Ensure(ItemAdvancedHandle handle)
    {
        if (handle != Handle) throw new InvalidOperationException("item.advanced.handle_invalid");
    }
}
