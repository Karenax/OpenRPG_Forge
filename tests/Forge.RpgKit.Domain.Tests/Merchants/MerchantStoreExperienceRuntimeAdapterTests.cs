using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Domain.Merchants;

namespace Forge.RpgKit.Domain.Tests.Merchants;

public sealed class MerchantStoreExperienceRuntimeAdapterTests
{
    [Fact]
    public void Infinite_stock_satisfies_large_purchase_without_decrementing_canonical_quantity()
    {
        var merchant = Merchant();
        var foundation = new MerchantFoundationRuntimeAdapter();
        var store = foundation.BuildStore(foundation.Resolve(merchant).FoundationHandle!, merchant);
        var item = Assert.Single(store.Stock);
        var state = new TradeState(store,
            new("customer", new Dictionary<string, decimal> { ["shard"] = 10_000 },
                new Dictionary<Guid, int>(), 100),
            new("store", new Dictionary<string, decimal> { ["shard"] = 1_000 },
                new Dictionary<Guid, int>(), 1_000));
        var result = MerchantService.Execute(state, new(Guid.NewGuid(), TradeKind.Buy, "customer", "store",
            [new(item.ItemDefinitionId, item.VariantId, 50, item.UnitPrice, "shard")], []));

        Assert.True(result.Committed);
        Assert.Equal(5, result.State.Store.Stock.Single().Quantity);
        Assert.Equal(50, result.State.Buyer.Items[item.ItemDefinitionId]);
    }

    [Fact]
    public void Open_and_close_events_preserve_customer_vendor_price_context_and_transaction_summary()
    {
        var merchant = Merchant();
        var adapter = new MerchantStoreExperienceRuntimeAdapter();
        var handle = adapter.Resolve(merchant).ExperienceHandle!;
        var store = new MerchantFoundationRuntimeAdapter().BuildStore(
            new MerchantFoundationRuntimeAdapter().Resolve(merchant).FoundationHandle!, merchant);
        var price = new PricedItem(store.Stock[0].ItemDefinitionId, 5, 6, 3, "shard", ["rate:1"]);
        var priceContext = new StorePriceContext("shard", .5,
            new Dictionary<Guid, PricedItem> { [price.ItemDefinitionId] = price });
        var openedAt = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
        var opened = adapter.Open(handle, merchant, "player:hero", store.Vendors[0].VendorId, priceContext,
            openedAt);
        var transactionId = Guid.Parse("c2920000-0000-4000-8000-000000000020");
        var closed = adapter.Close(handle, merchant, opened, [transactionId], 12, 3, openedAt.AddMinutes(2));

        Assert.Equal("player:hero", opened.CustomerId);
        Assert.Equal(store.Vendors[0].VendorId, opened.VendorId);
        Assert.Same(priceContext, opened.PriceContext);
        Assert.Equal([transactionId], closed.TransactionIds);
        Assert.Equal(12, closed.BoughtTotal);
        Assert.Equal(3, closed.SoldTotal);
    }

    [Fact]
    public void Store_ui_maps_keyboard_gamepad_compare_filters_cart_confirmation_and_accessibility()
    {
        var merchant = Merchant();
        var adapter = new MerchantStoreExperienceRuntimeAdapter();
        var ui = adapter.Ui(adapter.Resolve(merchant).ExperienceHandle!, merchant);
        var store = new MerchantFoundationRuntimeAdapter().BuildStore(
            new MerchantFoundationRuntimeAdapter().Resolve(merchant).FoundationHandle!, merchant);
        var diagnostics = MerchantExperienceService.Validate(store,
            new(1.2m, .6m, new Dictionary<string, decimal> { ["shard"] = 1 }, .1m, .2m, .2m, .1m, 1,
                500),
            new(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), 0, 5,
                new HashSet<string>(), 0, null, null), ui);

        Assert.True(ui.Keyboard && ui.Gamepad && ui.Compare && ui.Filters && ui.Cart && ui.Confirmation);
        Assert.True(ui.ScreenReaderLabels && ui.HighContrast);
        Assert.Equal(["name", "compare", "price", "quantity"], ui.Columns);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Optional_missing_experience_is_ignored_required_missing_is_diagnostic_and_stale_handle_is_refused()
    {
        var merchant = Merchant();
        var adapter = new MerchantStoreExperienceRuntimeAdapter();
        var handle = adapter.Resolve(merchant).ExperienceHandle!;

        Assert.True(adapter.Resolve(merchant with { Experience = null, ExperienceRequiredAtRuntime = false })
            .OptionalFallback);
        Assert.Equal("WORLD_MERCHANT_EXPERIENCE_REQUIRED_MISSING",
            adapter.Resolve(merchant with { Experience = null, ExperienceRequiredAtRuntime = true })
                .DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Ui(handle with { ExperienceId = Guid.NewGuid() },
            merchant));
    }

    private static MerchantWorldProfile Merchant()
    {
        var merchantId = Guid.Parse("c2920000-0000-4000-8000-000000000001");
        var vendor = BlueprintId.From(Guid.Parse("c2920000-0000-4000-8000-000000000002"));
        var item = BlueprintId.From(Guid.Parse("c2920000-0000-4000-8000-000000000003"));
        var foundation = new MerchantFoundationProfile(
            Guid.Parse("c2920000-0000-4000-8000-000000000004"), 1, "test:r092", "vigil-market",
            "merchant.vigil.name", ["fr-FR", "en-US"],
            [new(Guid.Parse("c2920000-0000-4000-8000-000000000005"), vendor, Guid.NewGuid(), "choice:trade",
                "interaction:trade", "npc")],
            new(Guid.Parse("c2920000-0000-4000-8000-000000000006"), 1, "test:r092", true, true, true,
                true, true, "merchant-vigil-nominal"),
            [new(Guid.Parse("c2920000-0000-4000-8000-000000000007"), item, "standard",
                MerchantSupplyPolicy.Infinite, 5, null, 0, 1, [], 5, "shard")],
            new(Guid.Parse("c2920000-0000-4000-8000-000000000008"), 1, "test:r092", 1.2m, .6m,
                [new(0, 1), new(100, 1.1m)], [new(0, 1), new(100, .9m)],
                new Dictionary<string, decimal> { ["shard"] = 1 }, .1m, .2m, .2m, .1m, 1, 500),
            new(Guid.Parse("c2920000-0000-4000-8000-000000000009"), 1, "test:r092", [], [], [], 0, 5,
                [], 0, null, null));
        var experience = new MerchantStoreExperienceProfile(
            Guid.Parse("c2920000-0000-4000-8000-000000000010"), 1, "test:r092",
            new(Guid.Parse("c2920000-0000-4000-8000-000000000011"), 1, "test:r092", "1.0.0",
                "StoreSessionOpened", "StoreSessionClosed", true, true, true, true),
            new(Guid.Parse("c2920000-0000-4000-8000-000000000012"), 1, "test:r092", true, true, true,
                true, true, true, true, true, ["name", "compare", "price", "quantity"]));
        return new(merchantId, 1, "test:r092", vendor,
            [new("shard", "currency.shard", 1, true, false)], [new("shard", 100, 0, 0, 10, 200)],
            new(50, 100, new Dictionary<string, decimal>(), -1, true),
            new(false, [], false, false, 0, "crime"), new(24, "restock", null, true, true),
            new("bargain", 1, .1, .1, 0, true), null, [], new(true, 30, 100, true, true, true),
            null, false, foundation, true, experience, true);
    }
}
