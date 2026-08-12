using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Domain.Merchants;

namespace Forge.RpgKit.Domain.Tests.Merchants;

public sealed class MerchantFoundationRuntimeAdapterTests
{
    [Fact]
    public void Independent_store_wizard_maps_multiple_vendors_and_complete_stock_contract()
    {
        var merchant = Merchant();
        var adapter = new MerchantFoundationRuntimeAdapter();
        var handle = adapter.Resolve(merchant).FoundationHandle!;
        var store = adapter.BuildStore(handle, merchant);
        var stock = Assert.Single(store.Stock);

        Assert.Equal(2, store.Vendors.Count);
        Assert.Equal("vigil-market", store.Slug);
        Assert.Equal(SupplyPolicy.Finite, stock.Supply);
        Assert.Equal("supplier:glassworks", stock.SupplierId);
        Assert.Equal(TimeSpan.FromHours(12), stock.RestockInterval);
        Assert.Contains("quest:market-closed", stock.HiddenConditions);
        Assert.Equal("merchant-vigil-nominal", merchant.Foundation!.Wizard.TestFixtureId);
    }

    [Fact]
    public void Wizard_preview_is_deterministic_and_ready_to_apply()
    {
        var merchant = Merchant();
        var adapter = new MerchantFoundationRuntimeAdapter();
        var store = adapter.BuildStore(adapter.Resolve(merchant).FoundationHandle!, merchant);
        var context = new MerchantValidationContext(
            store.Stock.Select(item => item.ItemDefinitionId).ToHashSet(),
            store.Vendors.Select(item => item.VendorId).ToHashSet(StringComparer.Ordinal),
            store.CurrencyIds,
            new HashSet<string>(StringComparer.Ordinal) { "scripts/merchant_rule" },
            new HashSet<string>(StringComparer.Ordinal) { "supplier:glassworks" },
            100);

        var preview = adapter.PreviewWizard(adapter.Resolve(merchant).FoundationHandle!, merchant, context);

        Assert.True(preview.CanApply);
        Assert.Contains("vendors:2", preview.SemanticDiff);
        Assert.Contains("stock:1", preview.SemanticDiff);
    }

    [Fact]
    public void Pricing_curves_and_item_query_reuse_rpg_kit_algorithms()
    {
        var merchant = Merchant();
        var adapter = new MerchantFoundationRuntimeAdapter();
        var handle = adapter.Resolve(merchant).FoundationHandle!;
        var itemId = merchant.Foundation!.Stock[0].ItemBlueprintId.Value;
        var price = adapter.Price(handle, merchant, itemId, 50,
            new("shard", .5, .25, .9, .5, new HashSet<string>()));
        var accepted = new MerchantItemView(itemId, "relic", new HashSet<string> { "rare", "glass" }, 4,
            "author:forge", .9, "player");

        Assert.InRange(price.BuyPrice, 1, 500);
        Assert.True(adapter.Matches(handle, merchant, accepted,
            (binding, candidate) => binding == "scripts/merchant_rule" && candidate.Tags.Contains("rare")));
        Assert.False(adapter.Matches(handle, merchant,
            accepted with { Tags = new HashSet<string> { "rare", "stolen" } }, (_, _) => true));
    }

    [Fact]
    public void Optional_missing_foundation_is_ignored_required_missing_is_diagnostic_and_stale_handle_is_refused()
    {
        var adapter = new MerchantFoundationRuntimeAdapter();
        var merchant = Merchant();
        var handle = adapter.Resolve(merchant).FoundationHandle!;

        Assert.True(adapter.Resolve(merchant with { Foundation = null, FoundationRequiredAtRuntime = false })
            .OptionalFallback);
        Assert.Equal("WORLD_MERCHANT_FOUNDATION_REQUIRED_MISSING",
            adapter.Resolve(merchant with { Foundation = null, FoundationRequiredAtRuntime = true }).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.BuildStore(handle with { FoundationId = Guid.NewGuid() },
            merchant));
    }

    private static MerchantWorldProfile Merchant()
    {
        var merchantId = Guid.Parse("c2910000-0000-4000-8000-000000000001");
        var vendor = BlueprintId.From(Guid.Parse("c2910000-0000-4000-8000-000000000002"));
        var secondVendor = BlueprintId.From(Guid.Parse("c2910000-0000-4000-8000-000000000003"));
        var item = BlueprintId.From(Guid.Parse("c2910000-0000-4000-8000-000000000004"));
        var dialogue = Guid.Parse("c2910000-0000-4000-8000-000000000005");
        var foundation = new MerchantFoundationProfile(
            Guid.Parse("c2910000-0000-4000-8000-000000000006"), 1, "test:r091", "vigil-market",
            "merchant.vigil.name", ["fr-FR", "en-US"],
            [new(Guid.Parse("c2910000-0000-4000-8000-000000000007"), vendor, dialogue, "choice:trade",
                    "interaction:trade", "npc"),
                new(Guid.Parse("c2910000-0000-4000-8000-000000000008"), secondVendor, dialogue,
                    "choice:trade", "interaction:trade", "kiosk")],
            new(Guid.Parse("c2910000-0000-4000-8000-000000000009"), 1, "test:r091", true, true, true,
                true, true, "merchant-vigil-nominal"),
            [new(Guid.Parse("c2910000-0000-4000-8000-000000000010"), item, "standard",
                MerchantSupplyPolicy.Finite, 14, "supplier:glassworks", 12, 1, ["quest:market-closed"], 50,
                "shard")],
            new(Guid.Parse("c2910000-0000-4000-8000-000000000011"), 1, "test:r091", 1.2m, .6m,
                [new(0, 1), new(100, 1.1m)], [new(0, 1), new(100, .9m)],
                new Dictionary<string, decimal> { ["shard"] = 1 }, .1m, .2m, .2m, .1m, 1, 500),
            new(Guid.Parse("c2910000-0000-4000-8000-000000000012"), 1, "test:r091", ["relic"],
                ["glass"], ["stolen"], 3, 5, ["author:forge"], .75, "player", "scripts/merchant_rule"));
        return new(merchantId, 1, "test:r091", vendor,
            [new("shard", "currency.shard", 1, true, false)], [new("shard", 100, 0, 0, 10, 200)],
            new(50, 100, new Dictionary<string, decimal>(), -1, true),
            new(false, [], false, false, 0, "crime"), new(24, "restock", null, true, true),
            new("bargain", 1, .1, .1, 0, true), null, [], new(true, 30, 100, true, true, true),
            null, false, foundation, true);
    }
}
