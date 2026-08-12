using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public enum MerchantSupplyPolicy
{
    Infinite,
    Finite,
    Generated,
    Crafted,
    SharedServer,
    Personal,
}

public sealed record MerchantFoundationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    string DisplayNameKey,
    IReadOnlyList<string> Locales,
    IReadOnlyList<MerchantVendorBindingProfile> Vendors,
    MerchantStoreWizardProfile Wizard,
    IReadOnlyList<MerchantStockEntryProfile> Stock,
    MerchantPricingPolicyProfile Pricing,
    MerchantItemQueryProfile Restrictions)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record MerchantVendorBindingProfile(
    Guid Id,
    BlueprintId VendorBlueprintId,
    Guid DialogueId,
    string DialogueChoiceId,
    string InteractionId,
    string Channel);

public sealed record MerchantStoreWizardProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool CreateStoreDefinition,
    bool CreateVendorBinding,
    bool CreateDialogueChoice,
    bool CreateInteraction,
    bool CreateTests,
    string TestFixtureId);

public sealed record MerchantStockEntryProfile(
    Guid Id,
    BlueprintId ItemBlueprintId,
    string VariantId,
    MerchantSupplyPolicy Supply,
    int Quantity,
    string? SupplierId,
    double RestockWorldHours,
    int DisplayOrder,
    IReadOnlyList<string> HiddenConditions,
    decimal BaseValue,
    string CurrencyId);

public sealed record MerchantPriceCurvePoint(decimal BaseValue, decimal Multiplier);

public sealed record MerchantPricingPolicyProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    decimal BuyMultiplier,
    decimal SellMultiplier,
    IReadOnlyList<MerchantPriceCurvePoint> BuyCurve,
    IReadOnlyList<MerchantPriceCurvePoint> SellCurve,
    IReadOnlyDictionary<string, decimal> CurrencyRates,
    decimal ReputationFactor,
    decimal ScarcityFactor,
    decimal ConditionFactor,
    decimal NegotiationFactor,
    decimal MinimumPrice,
    decimal MaximumPrice);

public sealed record MerchantItemQueryProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<string> WhitelistTypes,
    IReadOnlyList<string> WhitelistTags,
    IReadOnlyList<string> BlacklistTags,
    int MinimumQuality,
    int MaximumQuality,
    IReadOnlyList<string> Provenances,
    double MinimumCondition,
    string? OwnerId,
    string? CustomRuleBinding);

public sealed record MerchantStoreExperienceProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    MerchantSessionLifecycleProfile Sessions,
    MerchantStoreUiProfile Ui)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record MerchantSessionLifecycleProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ForgeScriptApiVersion,
    string OpenEventBinding,
    string CloseEventBinding,
    bool IncludeCustomer,
    bool IncludeVendor,
    bool IncludePriceContext,
    bool IncludeTransactionSummary);

public sealed record MerchantStoreUiProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool KeyboardNavigation,
    bool GamepadNavigation,
    bool Compare,
    bool Filters,
    bool Cart,
    bool Confirmation,
    bool ScreenReaderLabels,
    bool HighContrast,
    IReadOnlyList<string> Columns);
