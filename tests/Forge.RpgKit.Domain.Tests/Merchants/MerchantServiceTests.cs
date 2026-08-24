using Forge.RpgKit.Domain.Merchants;
using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Merchants;

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
    public void Aggregate_buy_cannot_overspend_or_overdraw_finite_stock_and_rolls_back_exactly()
    {
        var itemId = Guid.NewGuid(); var store = MerchantService.Apply(Request(itemId), Context(itemId)); var original = State(store);
        var lines = new[] { new TradeLine(itemId, "standard", 8, 5, "shard"), new TradeLine(itemId, "standard", 8, 5, "shard") };
        var result = MerchantService.Execute(original, new(Guid.NewGuid(), TradeKind.Buy, "player", "store", lines, []));
        Assert.False(result.Committed); Assert.Same(original, result.State);
        Assert.Contains(result.Diagnostics, item => item.Code == "RPG_TRADE_FUNDS_INSUFFICIENT");
        Assert.Contains(result.Diagnostics, item => item.Code == "RPG_TRADE_STOCK_INSUFFICIENT");
        Assert.Contains(result.Diagnostics, item => item.Code == "RPG_TRADE_CAPACITY_EXCEEDED");
    }

    [Fact]
    public void Sell_and_barter_enforce_aggregate_ownership_kind_currency_and_canonical_price()
    {
        var itemId = Guid.NewGuid(); var store = MerchantService.Apply(Request(itemId), Context(itemId));
        var original = State(store) with { Buyer = State(store).Buyer with { Items = new Dictionary<Guid, int> { [itemId] = 2 } } };
        var overSell = MerchantService.Execute(original, new(Guid.NewGuid(), TradeKind.Sell, "player", "store", [],
            [new(itemId, "standard", 2, 5, "shard"), new(itemId, "standard", 1, 5, "shard")]));
        Assert.False(overSell.Committed); Assert.Same(original, overSell.State);
        Assert.Contains(overSell.Diagnostics, item => item.Code == "RPG_TRADE_OWNERSHIP_INVALID");

        var malformedBarter = MerchantService.Execute(original, new(Guid.NewGuid(), TradeKind.Barter, "player", "store",
            [new(itemId, "standard", 1, 4, "unknown")], []));
        Assert.False(malformedBarter.Committed); Assert.Same(original, malformedBarter.State);
        Assert.Contains(malformedBarter.Diagnostics, item => item.Code == "RPG_TRADE_KIND_INVALID");
        Assert.Contains(malformedBarter.Diagnostics, item => item.Code == "RPG_TRADE_CURRENCY_INVALID");
        Assert.Contains(malformedBarter.Diagnostics, item => item.Code == "RPG_TRADE_STOCK_MISMATCH");
    }

    [Fact]
    public void Validation_reports_vendor_currency_stock_localization_and_script_paths()
    {
        var itemId = Guid.NewGuid(); var request = Request(itemId) with { Vendors = [new("missing", "", "", "npc")], CurrencyIds = new HashSet<string> { "missing" }, Locales = [], Scripts = new Dictionary<string, string> { ["onOpen"] = "missing" }, Stock = [new(Guid.NewGuid(), "bad", SupplyPolicy.Finite, -1, "missing", TimeSpan.Zero, 0, new HashSet<string>(), -1)] };
        var preview = MerchantService.Preview(request, Context(itemId)); Assert.False(preview.CanApply);
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_VENDOR_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_STOCK_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_CURRENCY_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_LOCALIZATION_INVALID"); Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_STORE_SCRIPT_INVALID");
    }

    [Fact]
    public void Typed_world_handle_resolves_optional_and_required_runtime_then_executes_canonical_trade()
    {
        var itemId = Guid.NewGuid(); var service = new MerchantWorldTransactionService(); var profile = WorldProfile(itemId);
        var resolved = service.Resolve(profile);
        Assert.True(resolved.Available); Assert.NotNull(resolved.TransactionHandle);
        var buyer = new TradePartyState("player", new Dictionary<string, decimal> { ["shard"] = 20 }, new Dictionary<Guid, int>(), 10);
        var seller = new TradePartyState("store", new Dictionary<string, decimal> { ["shard"] = 100 }, new Dictionary<Guid, int>(), 100);
        var result = service.Execute(resolved.TransactionHandle!, profile, buyer, seller,
            new(Guid.NewGuid(), TradeKind.Buy, "player", "store", [new(itemId, "standard", 2, 5, "shard")], []));
        Assert.True(result.Committed); Assert.Equal(2, result.State.Buyer.Items[itemId]);

        var optional = service.Resolve(profile with { Transactions = null, TransactionsRequiredAtRuntime = false });
        Assert.False(optional.Available); Assert.Empty(optional.DiagnosticCode);
        var required = service.Resolve(profile with { Transactions = null, TransactionsRequiredAtRuntime = true });
        Assert.False(required.Available); Assert.Equal("WORLD_MERCHANT_TRANSACTIONS_REQUIRED_MISSING", required.DiagnosticCode);
        var stale = service.Execute(resolved.TransactionHandle! with { PolicyId = Guid.NewGuid() }, profile, buyer, seller,
            new(Guid.NewGuid(), TradeKind.Buy, "player", "store", [new(itemId, "standard", 1, 5, "shard")], []));
        Assert.False(stale.Committed); Assert.Contains(stale.Diagnostics, item => item.Code == "RPG_TRADE_HANDLE_STALE");
    }

    private static StoreWizardRequest Request(Guid itemId) => new("demo-market", "Marché de démonstration", ["fr", "en"], [new("vendor:lyra", "interaction:trade", "choice:trade", "npc")], [new(itemId, "standard", SupplyPolicy.Finite, 14, "supplier:glassworks", TimeSpan.FromHours(12), 1, new HashSet<string> { "quest:closed" }, 5)], new HashSet<string> { "shard" }, new Dictionary<string, decimal> { ["shard"] = 100 }, new Dictionary<string, string> { ["onOpen"] = "script:market-open" }, "author:test");
    private static MerchantValidationContext Context(Guid itemId) => new(new HashSet<Guid> { itemId }, new HashSet<string> { "vendor:lyra" }, new HashSet<string> { "shard" }, new HashSet<string> { "script:market-open" }, new HashSet<string> { "supplier:glassworks" }, 100);
    private static TradeState State(StoreDefinition store) => new(store, new("player", new Dictionary<string, decimal> { ["shard"] = 20 }, new Dictionary<Guid, int>(), 10), new("store", new Dictionary<string, decimal> { ["shard"] = 100 }, new Dictionary<Guid, int>(), 100));
    private static MerchantWorldProfile WorldProfile(Guid itemId) => new(Guid.NewGuid(), 1, "test:r045",
        BlueprintId.From(Guid.NewGuid()), [new("shard", "currency.shard", 1, true, false)],
        [new("shard", 100, 0, 0, 10, 200)], new(50, 100, new Dictionary<string, decimal>(), -1, true),
        new(false, [], false, false, 0, "crime"), new(24, "restock", null, true, true),
        new("bargain", 1, .1, .1, 0, true), null, [], new(true, 30, 100, true, true, true),
        new(Guid.NewGuid(), 1, "test:r045", "interaction:trade", "choice:trade",
            [new(Guid.NewGuid(), BlueprintId.From(itemId), "standard", 14, 5, "shard", [])], ["fr-FR"], [],
            new Dictionary<string, string> { ["onOpen"] = "scripts/merchant_open" }, 10, 100, true), true);
}
