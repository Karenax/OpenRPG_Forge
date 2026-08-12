using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Merchants;

namespace Forge.RpgKit.Application.Merchants;

public sealed class MerchantFoundationRuntimeAdapter
{
    public sealed record Handle(Guid MerchantId, Guid FoundationId, int SchemaVersion, string Provenance);

    public sealed record Resolution(
        bool Available,
        Handle? FoundationHandle,
        bool OptionalFallback,
        string DiagnosticCode,
        string DiagnosticPath);

    public Resolution Resolve(MerchantWorldProfile merchant)
    {
        ArgumentNullException.ThrowIfNull(merchant);
        if (merchant.Foundation is null)
            return merchant.FoundationRequiredAtRuntime
                ? new(false, null, false, "WORLD_MERCHANT_FOUNDATION_REQUIRED_MISSING",
                    $"module/worldFeatures/merchants/{merchant.Id}/foundation")
                : new(false, null, true, string.Empty, string.Empty);
        return new(true, new(merchant.Id, merchant.Foundation.Id, merchant.Foundation.SchemaVersion,
            merchant.Foundation.Provenance), false, string.Empty, string.Empty);
    }

    public StoreDefinition BuildStore(Handle handle, MerchantWorldProfile merchant)
    {
        var foundation = Require(handle, merchant);
        return new StoreDefinition(
            foundation.Id,
            foundation.SchemaVersion,
            foundation.Provenance,
            foundation.Slug,
            foundation.DisplayNameKey,
            foundation.Locales,
            foundation.Vendors.Select(item => new VendorBinding(
                item.VendorBlueprintId.ToString(), item.InteractionId, item.DialogueChoiceId, item.Channel)).ToArray(),
            foundation.Stock.Select(item => new StockEntry(
                item.ItemBlueprintId.Value,
                item.VariantId,
                Enum.Parse<SupplyPolicy>(item.Supply.ToString()),
                item.Quantity,
                item.SupplierId,
                TimeSpan.FromHours(item.RestockWorldHours),
                item.DisplayOrder,
                item.HiddenConditions.ToHashSet(StringComparer.Ordinal),
                item.BaseValue)).ToArray(),
            merchant.Currencies.Select(item => item.Id).ToHashSet(StringComparer.Ordinal),
            merchant.Treasury.ToDictionary(item => item.CurrencyId, item => item.Balance, StringComparer.Ordinal),
            foundation.Restrictions.CustomRuleBinding is { Length: > 0 } binding
                ? new Dictionary<string, string>(StringComparer.Ordinal) { ["customRule"] = binding }
                : new Dictionary<string, string>(StringComparer.Ordinal));
    }

    public StoreWizardPreview PreviewWizard(
        Handle handle,
        MerchantWorldProfile merchant,
        MerchantValidationContext context)
    {
        var store = BuildStore(handle, merchant);
        var request = new StoreWizardRequest(store.Slug, store.DisplayName, store.Locales, store.Vendors,
            store.Stock, store.CurrencyIds, store.Treasury, store.Scripts, store.Provenance);
        return MerchantService.Preview(request, context);
    }

    public PricedItem Price(
        Handle handle,
        MerchantWorldProfile merchant,
        Guid itemId,
        decimal baseValue,
        PricingContext context)
    {
        var foundation = Require(handle, merchant);
        var pricing = foundation.Pricing;
        var policy = new PricingPolicy(
            pricing.BuyMultiplier * Evaluate(pricing.BuyCurve, baseValue),
            pricing.SellMultiplier * Evaluate(pricing.SellCurve, baseValue),
            pricing.CurrencyRates,
            pricing.ReputationFactor,
            pricing.ScarcityFactor,
            pricing.ConditionFactor,
            pricing.NegotiationFactor,
            pricing.MinimumPrice,
            pricing.MaximumPrice);
        return MerchantExperienceService.Price(itemId, baseValue, policy, context);
    }

    public bool Matches(
        Handle handle,
        MerchantWorldProfile merchant,
        MerchantItemView item,
        Func<string, MerchantItemView, bool>? customRule = null)
    {
        var query = Require(handle, merchant).Restrictions;
        Func<MerchantItemView, bool>? evaluator = query.CustomRuleBinding is { Length: > 0 } binding
            ? candidate => customRule?.Invoke(binding, candidate) ?? false
            : null;
        return MerchantExperienceService.Matches(item, new ItemQuery(
            query.WhitelistTypes.ToHashSet(StringComparer.Ordinal),
            query.WhitelistTags.ToHashSet(StringComparer.Ordinal),
            query.BlacklistTags.ToHashSet(StringComparer.Ordinal),
            query.MinimumQuality,
            query.MaximumQuality,
            query.Provenances.ToHashSet(StringComparer.Ordinal),
            query.MinimumCondition,
            query.OwnerId,
            evaluator));
    }

    private static MerchantFoundationProfile Require(Handle handle, MerchantWorldProfile merchant)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(merchant);
        var foundation = merchant.Foundation ?? throw new InvalidOperationException(
            "Merchant foundation profile is unavailable.");
        if (handle.MerchantId != merchant.Id || handle.FoundationId != foundation.Id ||
            handle.SchemaVersion != foundation.SchemaVersion || handle.Provenance != foundation.Provenance)
            throw new InvalidOperationException("Merchant foundation handle is stale or mismatched.");
        return foundation;
    }

    private static decimal Evaluate(IReadOnlyList<MerchantPriceCurvePoint> curve, decimal baseValue)
    {
        if (curve.Count == 0) return 1;
        if (baseValue <= curve[0].BaseValue) return curve[0].Multiplier;
        for (var index = 1; index < curve.Count; index++)
        {
            var right = curve[index];
            if (baseValue > right.BaseValue) continue;
            var left = curve[index - 1];
            var width = right.BaseValue - left.BaseValue;
            if (width == 0) return right.Multiplier;
            var ratio = (baseValue - left.BaseValue) / width;
            return left.Multiplier + ((right.Multiplier - left.Multiplier) * ratio);
        }
        return curve[^1].Multiplier;
    }
}
