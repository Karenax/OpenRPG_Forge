using Forge.RpgKit.Domain.Merchants;

namespace Forge.RpgKit.Domain.Tests.Merchants;

public sealed class MerchantExperienceServiceTests
{
    [Fact]
    public void Pricing_combines_currency_reputation_scarcity_condition_and_negotiation_with_breakdown()
    {
        var policy = Pricing(); var context = new PricingContext("shard", 0.5, 0.25, 0.8, 0.5, new HashSet<string>());
        var price = MerchantExperienceService.Price(Guid.NewGuid(), 100, policy, context);
        Assert.InRange(price.BuyPrice, 1, 500); Assert.InRange(price.SellPrice, 1, price.BuyPrice);
        Assert.Contains(price.Breakdown, item => item.StartsWith("reputation:", StringComparison.Ordinal));
        Assert.Contains(price.Breakdown, item => item.StartsWith("negotiation:", StringComparison.Ordinal));
    }

    [Fact]
    public void Item_query_filters_type_tags_quality_provenance_condition_owner_and_custom_rule()
    {
        var item = new MerchantItemView(Guid.NewGuid(), "relic", new HashSet<string> { "glass", "rare" }, 4, "author:forge", 0.9, "player");
        var query = new ItemQuery(new HashSet<string> { "relic" }, new HashSet<string> { "glass" }, new HashSet<string> { "stolen" }, 3, 5, new HashSet<string> { "author:forge" }, 0.75, "player", candidate => candidate.Tags.Contains("rare"));
        Assert.True(MerchantExperienceService.Matches(item, query)); Assert.False(MerchantExperienceService.Matches(item with { Tags = new HashSet<string> { "glass", "stolen", "rare" } }, query));
    }

    [Fact]
    public void Session_events_preserve_customer_vendor_prices_and_transaction_summary()
    {
        var store = Store(); var price = MerchantExperienceService.Price(Guid.NewGuid(), 10, Pricing(), new("shard", 0, 0, 1, 0, new HashSet<string>()));
        var context = new StorePriceContext("shard", 0, new Dictionary<Guid, PricedItem> { [price.ItemDefinitionId] = price }); var at = DateTimeOffset.UnixEpoch;
        var opened = MerchantExperienceService.Open(store, "player", "vendor", context, at); var transaction = Guid.NewGuid();
        var closed = MerchantExperienceService.Close(opened, [transaction], 12, 3, at.AddMinutes(2));
        Assert.Equal("player", closed.CustomerId); Assert.Equal("vendor", closed.VendorId); Assert.Same(context, closed.PriceContext); Assert.Contains(transaction, closed.TransactionIds); Assert.Equal(12, closed.BoughtTotal);
    }

    [Fact]
    public void Ui_contract_requires_navigation_accessibility_cart_confirmation_and_valid_rules()
    {
        var query = new ItemQuery(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), 0, 5, new HashSet<string>(), 0, null, null);
        var valid = new StoreUiConfiguration(true, true, true, true, true, true, true, true, ["name", "compare", "price"]);
        Assert.Empty(MerchantExperienceService.Validate(Store(), Pricing(), query, valid));
        var invalid = valid with { Keyboard = false, Gamepad = false, ScreenReaderLabels = false, Cart = false };
        Assert.Contains(MerchantExperienceService.Validate(Store(), Pricing(), query, invalid), item => item.Code == "RPG_STORE_UI_INACCESSIBLE");
    }

    [Fact]
    public void Supply_policy_exposes_infinite_finite_generated_crafted_shared_and_personal_modes() => Assert.Equal(6, Enum.GetValues<SupplyPolicy>().Length);

    private static PricingPolicy Pricing() => new(1.2m, 0.6m, new Dictionary<string, decimal> { ["shard"] = 1 }, 0.1m, 0.2m, 0.2m, 0.1m, 1, 500);
    private static StoreDefinition Store() => new(Guid.NewGuid(), 1, "author:test", "demo-store", "Boutique de démonstration", ["fr"], [new("vendor", "interaction", "dialogue", "npc")], [], new HashSet<string> { "shard" }, new Dictionary<string, decimal> { ["shard"] = 100 }, new Dictionary<string, string>());
}
