namespace Forge.RpgKit.Domain.Merchants;

public sealed record PricingContext(string CurrencyId, double Reputation, double Scarcity, double Condition, double Negotiation, IReadOnlySet<string> Tags);
public sealed record PricingPolicy(decimal BuyMultiplier, decimal SellMultiplier, IReadOnlyDictionary<string, decimal> CurrencyRates, decimal ReputationFactor, decimal ScarcityFactor, decimal ConditionFactor, decimal NegotiationFactor, decimal MinimumPrice, decimal MaximumPrice);
public sealed record PricedItem(Guid ItemDefinitionId, decimal BaseValue, decimal BuyPrice, decimal SellPrice, string CurrencyId, IReadOnlyList<string> Breakdown);
public sealed record MerchantItemView(Guid DefinitionId, string Type, IReadOnlySet<string> Tags, int Quality, string Provenance, double Condition, string OwnerId);
public sealed record ItemQuery(IReadOnlySet<string> Types, IReadOnlySet<string> RequiredTags, IReadOnlySet<string> ExcludedTags, int MinimumQuality, int MaximumQuality, IReadOnlySet<string> Provenances, double MinimumCondition, string? OwnerId, Func<MerchantItemView, bool>? CustomRule);
public sealed record StoreUiConfiguration(bool Keyboard, bool Gamepad, bool Compare, bool Filters, bool Cart, bool Confirmation, bool ScreenReaderLabels, bool HighContrast, IReadOnlyList<string> Columns);
public sealed record StorePriceContext(string CurrencyId, double Reputation, IReadOnlyDictionary<Guid, PricedItem> Prices);
public sealed record StoreSessionOpened(Guid EventId, Guid StoreId, string CustomerId, string VendorId, StorePriceContext PriceContext, DateTimeOffset At);
public sealed record StoreSessionClosed(Guid EventId, Guid StoreId, string CustomerId, string VendorId, StorePriceContext PriceContext, IReadOnlyList<Guid> TransactionIds, decimal BoughtTotal, decimal SoldTotal, DateTimeOffset At);

public static class MerchantExperienceService
{
    public static PricedItem Price(Guid itemId, decimal baseValue, PricingPolicy policy, PricingContext context)
    {
        if (!policy.CurrencyRates.TryGetValue(context.CurrencyId, out var rate) || rate <= 0) throw new ArgumentException("Currency rate is unavailable.", nameof(context));
        var reputation = 1m - (policy.ReputationFactor * (decimal)Math.Clamp(context.Reputation, -1, 1));
        var scarcity = 1m + (policy.ScarcityFactor * (decimal)Math.Max(0, context.Scarcity));
        var condition = 1m - (policy.ConditionFactor * (decimal)(1 - Math.Clamp(context.Condition, 0, 1)));
        var negotiation = 1m - (policy.NegotiationFactor * (decimal)Math.Clamp(context.Negotiation, 0, 1));
        decimal Clamp(decimal value) => Math.Clamp(decimal.Round(value, 2, MidpointRounding.AwayFromZero), policy.MinimumPrice, policy.MaximumPrice);
        return new(itemId, baseValue, Clamp(baseValue * rate * policy.BuyMultiplier * reputation * scarcity * condition * negotiation), Clamp(baseValue * rate * policy.SellMultiplier * reputation * condition), context.CurrencyId, [$"rate:{rate}", $"reputation:{reputation}", $"scarcity:{scarcity}", $"condition:{condition}", $"negotiation:{negotiation}"]);
    }

    public static bool Matches(MerchantItemView item, ItemQuery query) =>
        (query.Types.Count == 0 || query.Types.Contains(item.Type)) && query.RequiredTags.All(item.Tags.Contains) &&
        !query.ExcludedTags.Any(item.Tags.Contains) && item.Quality >= query.MinimumQuality && item.Quality <= query.MaximumQuality &&
        (query.Provenances.Count == 0 || query.Provenances.Contains(item.Provenance)) && item.Condition >= query.MinimumCondition &&
        (query.OwnerId is null || query.OwnerId == item.OwnerId) && (query.CustomRule?.Invoke(item) ?? true);

    public static IReadOnlyList<MerchantDiagnostic> Validate(StoreDefinition store, PricingPolicy pricing, ItemQuery query, StoreUiConfiguration ui)
    {
        var root = $"stores/{store.Id}"; var diagnostics = new List<MerchantDiagnostic>();
        if (pricing.BuyMultiplier < 0 || pricing.SellMultiplier < 0 || pricing.MinimumPrice < 0 || pricing.MaximumPrice < pricing.MinimumPrice || pricing.CurrencyRates.Keys.Any(currency => !store.CurrencyIds.Contains(currency))) diagnostics.Add(new("RPG_STORE_PRICING_INVALID", $"{root}/pricing", "Pricing multipliers, bounds and currencies are invalid."));
        if (query.MinimumQuality < 0 || query.MaximumQuality < query.MinimumQuality || query.MinimumCondition is < 0 or > 1) diagnostics.Add(new("RPG_STORE_QUERY_INVALID", $"{root}/itemQuery", "Item query quality or condition range is invalid."));
        if ((!ui.Keyboard && !ui.Gamepad) || !ui.ScreenReaderLabels || ui.Columns.Count == 0 || !ui.Cart || !ui.Confirmation) diagnostics.Add(new("RPG_STORE_UI_INACCESSIBLE", $"{root}/ui", "Store UI needs navigation, labels, columns, cart and confirmation."));
        return diagnostics;
    }

    public static StoreSessionOpened Open(StoreDefinition store, string customerId, string vendorId, StorePriceContext priceContext, DateTimeOffset at) => new(Guid.NewGuid(), store.Id, customerId, vendorId, priceContext, at);
    public static StoreSessionClosed Close(StoreSessionOpened opened, IReadOnlyList<Guid> transactionIds, decimal boughtTotal, decimal soldTotal, DateTimeOffset at) => new(Guid.NewGuid(), opened.StoreId, opened.CustomerId, opened.VendorId, opened.PriceContext, transactionIds, boughtTotal, soldTotal, at);
}
