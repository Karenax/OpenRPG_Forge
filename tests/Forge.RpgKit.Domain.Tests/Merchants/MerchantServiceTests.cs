using Forge.RpgKit.Domain.Merchants;

namespace Forge.RpgKit.Domain.Tests.Merchants;

public sealed class MerchantServiceTests
{
    [Fact]
    public void Wizard_previews_independent_store_vendor_dialogue_interaction_stock_and_applies_valid_definition()
    {
        var itemId = Guid.NewGuid(); var request = Request(itemId); var context = Context(itemId);
        var preview = MerchantService.Preview(request, context);
        Assert.True(preview.CanApply); Assert.Contains("dialogue:choice:trade", preview.SemanticDiff);
        var store = MerchantService.Apply(request, context);
        Assert.Equal("vendor:lyra", store.Vendors.Single().VendorId);
        Assert.Equal(SupplyPolicy.Finite, store.Stock.Single().Supply);
        Assert.Equal(store.Id, MerchantService.Apply(request, context).Id);
    }

    [Fact]
    public void Atomic_buy_commits_funds_inventory_and_finite_stock_together()
    {
        var itemId = Guid.NewGuid(); var store = MerchantService.Apply(Request(itemId), Context(itemId)); var original = State(store);
        var request = new TradeRequest(Guid.NewGuid(), TradeKind.Buy, "player", "store", [new(itemId, "standard", 2, 5, "shard")], []);
        var result = MerchantService.Execute(original, request);
        Assert.True(result.Committed); Assert.Equal(10, result.State.Buyer.Funds["shard"]); Assert.Equal(2, result.State.Buyer.Items[itemId]);
        Assert.Equal(12, result.State.Store.Stock.Single().Quantity); Assert.Equal(110, result.State.Seller.Funds["shard"]);
    }

    [Fact]
    public void Failed_trade_rolls_back_exact_original_state_for_funds_stock_ownership_or_capacity()
    {
        var itemId = Guid.NewGuid(); var store = MerchantService.Apply(Request(itemId), Context(itemId)); var original = State(store) with { Buyer = State(store).Buyer with { Funds = new Dictionary<string, decimal> { ["shard"] = 1 } } };
        var request = new TradeRequest(Guid.NewGuid(), TradeKind.Buy, "player", "store", [new(itemId, "standard", 2, 5, "shard")], []);
        var result = MerchantService.Execute(original, request);
        Assert.False(result.Committed); Assert.Same(original, result.State); Assert.Equal("no-change", result.SemanticDiff);
        Assert.Contains(result.Diagnostics, item => item.Code == "RPG_TRADE_FUNDS_INSUFFICIENT");
    }

    [Fact]
    public void Validation_reports_vendor_currency_stock_localization_and_script_paths()
    {
        var itemId = Guid.NewGuid(); var request = Request(itemId) with { Vendors = [new("missing", "", "", "npc")], CurrencyIds = new HashSet<string> { "missing" }, Locales = [], Scripts = new Dictionary<string, string> { ["onOpen"] = "missing" }, Stock = [new(Guid.NewGuid(), "bad", SupplyPolicy.Finite, -1, "missing", TimeSpan.Zero, 0, new HashSet<string>(), -1)] };
        var preview = MerchantService.Preview(request, Context(itemId)); Assert.False(preview.CanApply);
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_VENDOR_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_STOCK_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_CURRENCY_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_LOCALIZATION_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_SCRIPT_INVALID");
    }

    private static StoreWizardRequest Request(Guid itemId) => new("vigil-market", "Marché de la Veillée", ["fr", "en"], [new("vendor:lyra", "interaction:trade", "choice:trade", "npc")], [new(itemId, "standard", SupplyPolicy.Finite, 14, "supplier:glassworks", TimeSpan.FromHours(12), 1, new HashSet<string> { "quest:closed" }, 5)], new HashSet<string> { "shard" }, new Dictionary<string, decimal> { ["shard"] = 100 }, new Dictionary<string, string> { ["onOpen"] = "script:market-open" }, "author:test");
    private static MerchantValidationContext Context(Guid itemId) => new(new HashSet<Guid> { itemId }, new HashSet<string> { "vendor:lyra" }, new HashSet<string> { "shard" }, new HashSet<string> { "script:market-open" }, new HashSet<string> { "supplier:glassworks" }, 100);
    private static TradeState State(StoreDefinition store) => new(store, new("player", new Dictionary<string, decimal> { ["shard"] = 20 }, new Dictionary<Guid, int>(), 10), new("store", new Dictionary<string, decimal> { ["shard"] = 100 }, new Dictionary<Guid, int>(), 100));
}
