namespace Forge.RpgKit.Domain.Merchants;

public static class MerchantService
{
    public static StoreWizardPreview Preview(StoreWizardRequest request, MerchantValidationContext context)
    {
        var store = new StoreDefinition(StableId(request.Slug), 1, request.Provenance, request.Slug.Trim(), request.DisplayName.Trim(), request.Locales, request.Vendors, request.Stock.OrderBy(item => item.DisplayOrder).ToArray(), request.CurrencyIds, request.Treasury, request.Scripts);
        var diagnostics = Validate(store, context);
        return new(store, diagnostics, $"add:/stores/{store.Id}|vendors:{store.Vendors.Count}|stock:{store.Stock.Count}|dialogue:{string.Join(',', store.Vendors.Select(item => item.DialogueChoiceId))}", diagnostics.Count == 0);
    }

    public static StoreDefinition Apply(StoreWizardRequest request, MerchantValidationContext context)
    {
        var preview = Preview(request, context);
        if (!preview.CanApply) throw new InvalidOperationException(string.Join("; ", preview.Diagnostics.Select(item => item.Code)));
        return preview.Store;
    }

    public static IReadOnlyList<MerchantDiagnostic> Validate(StoreDefinition store, MerchantValidationContext context)
    {
        var root = $"stores/{store.Id}"; var diagnostics = new List<MerchantDiagnostic>();
        if (store.Id == Guid.Empty || store.SchemaVersion != 1 || string.IsNullOrWhiteSpace(store.Provenance) || string.IsNullOrWhiteSpace(store.Slug) || string.IsNullOrWhiteSpace(store.DisplayName)) diagnostics.Add(new("RPG_STORE_IDENTITY_INVALID", root, "Store identity is incomplete."));
        if (store.Vendors.Count == 0 || store.Vendors.Any(item => string.IsNullOrWhiteSpace(item.InteractionId) || string.IsNullOrWhiteSpace(item.DialogueChoiceId) || !context.VendorIds.Contains(item.VendorId))) diagnostics.Add(new("RPG_STORE_VENDOR_INVALID", $"{root}/vendors", "Vendor bindings, interaction and dialogue are required."));
        if (store.Stock.Count > context.MaximumStockEntries || store.Stock.Any(item => !context.ItemDefinitionIds.Contains(item.ItemDefinitionId) || item.UnitPrice < 0 || (item.Supply == SupplyPolicy.Finite && item.Quantity < 0) || (item.SupplierId is { Length: > 0 } supplier && !context.SupplierIds.Contains(supplier)))) diagnostics.Add(new("RPG_STORE_STOCK_INVALID", $"{root}/stock", "Stock references, prices, quantities or suppliers are invalid."));
        if (store.CurrencyIds.Count == 0 || store.CurrencyIds.Any(currency => !context.CurrencyIds.Contains(currency)) || store.Treasury.Any(item => !store.CurrencyIds.Contains(item.Key) || item.Value < 0)) diagnostics.Add(new("RPG_STORE_CURRENCY_INVALID", $"{root}/currencies", "Currencies and treasury must resolve and remain non-negative."));
        if (store.Locales.Count == 0 || store.Locales.Any(string.IsNullOrWhiteSpace)) diagnostics.Add(new("RPG_STORE_LOCALIZATION_INVALID", $"{root}/locales", "At least one valid locale is required."));
        if (store.Scripts.Values.Any(script => !context.ScriptIds.Contains(script))) diagnostics.Add(new("RPG_STORE_SCRIPT_INVALID", $"{root}/scripts", "Store scripts must resolve."));
        return diagnostics;
    }

    public static TradeResult Execute(TradeState original, TradeRequest request)
    {
        var diagnostics = ValidateTrade(original, request);
        if (diagnostics.Count != 0) return new(false, original, diagnostics, "no-change");
        var buyerFunds = original.Buyer.Funds.ToDictionary(); var sellerFunds = original.Seller.Funds.ToDictionary();
        var buyerItems = original.Buyer.Items.ToDictionary(); var sellerItems = original.Seller.Items.ToDictionary(); var stock = original.Store.Stock.ToArray();
        Transfer(request.OfferedBySeller, original.Seller.PartyId, buyerFunds, sellerFunds, buyerItems, sellerItems, stock, -1);
        Transfer(request.OfferedByBuyer, original.Buyer.PartyId, sellerFunds, buyerFunds, sellerItems, buyerItems, stock, 1);
        var updated = original with { Store = original.Store with { Stock = stock }, Buyer = original.Buyer with { Funds = buyerFunds, Items = buyerItems }, Seller = original.Seller with { Funds = sellerFunds, Items = sellerItems } };
        return new(true, updated, [], $"transaction:{request.TransactionId}|kind:{request.Kind}|seller-lines:{request.OfferedBySeller.Count}|buyer-lines:{request.OfferedByBuyer.Count}");
    }

    private static List<MerchantDiagnostic> ValidateTrade(TradeState state, TradeRequest request)
    {
        var result = new List<MerchantDiagnostic>(); var root = $"transactions/{request.TransactionId}";
        if (request.TransactionId == Guid.Empty || request.BuyerId != state.Buyer.PartyId || request.SellerId != state.Seller.PartyId || (request.OfferedBySeller.Count == 0 && request.OfferedByBuyer.Count == 0)) result.Add(new("RPG_TRADE_IDENTITY_INVALID", root, "Transaction parties and lines are required."));
        ValidateSide(request.OfferedBySeller, state.Seller, state.Buyer, state.Store, $"{root}/seller", result);
        ValidateSide(request.OfferedByBuyer, state.Buyer, state.Seller, null, $"{root}/buyer", result);
        return result;
    }

    private static void ValidateSide(IReadOnlyList<TradeLine> lines, TradePartyState source, TradePartyState destination, StoreDefinition? store, string path, List<MerchantDiagnostic> result)
    {
        foreach (var line in lines)
        {
            if (line.Quantity <= 0 || line.UnitPrice < 0) result.Add(new("RPG_TRADE_LINE_INVALID", path, "Trade quantities and prices are invalid."));
            var total = line.UnitPrice * line.Quantity;
            if (destination.Funds.GetValueOrDefault(line.CurrencyId) < total) result.Add(new("RPG_TRADE_FUNDS_INSUFFICIENT", path, "Destination cannot fund this transfer."));
            if (destination.Items.Values.Sum() + line.Quantity > destination.Capacity) result.Add(new("RPG_TRADE_CAPACITY_EXCEEDED", path, "Destination inventory has insufficient capacity."));
            if (store is null && source.Items.GetValueOrDefault(line.ItemDefinitionId) < line.Quantity) result.Add(new("RPG_TRADE_OWNERSHIP_INVALID", path, "Source does not own the offered quantity."));
            if (store is not null)
            {
                var stock = store.Stock.FirstOrDefault(item => item.ItemDefinitionId == line.ItemDefinitionId && item.VariantId == line.VariantId);
                if (stock is null || (stock.Supply == SupplyPolicy.Finite && stock.Quantity < line.Quantity)) result.Add(new("RPG_TRADE_STOCK_INSUFFICIENT", path, "Store stock cannot satisfy this transfer."));
            }
        }
    }

    private static void Transfer(IReadOnlyList<TradeLine> lines, string sourceId, Dictionary<string, decimal> payerFunds, Dictionary<string, decimal> receiverFunds, Dictionary<Guid, int> receiverItems, Dictionary<Guid, int> sourceItems, StockEntry[] stock, int stockDirection)
    {
        foreach (var line in lines)
        {
            var total = line.UnitPrice * line.Quantity; payerFunds[line.CurrencyId] -= total; receiverFunds[line.CurrencyId] = receiverFunds.GetValueOrDefault(line.CurrencyId) + total;
            receiverItems[line.ItemDefinitionId] = receiverItems.GetValueOrDefault(line.ItemDefinitionId) + line.Quantity;
            if (stockDirection > 0) sourceItems[line.ItemDefinitionId] -= line.Quantity;
            var index = Array.FindIndex(stock, item => item.ItemDefinitionId == line.ItemDefinitionId && item.VariantId == line.VariantId);
            if (index >= 0 && stock[index].Supply == SupplyPolicy.Finite) stock[index] = stock[index] with { Quantity = stock[index].Quantity + (stockDirection * line.Quantity) };
        }
    }

    private static Guid StableId(string slug) { var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"openrpg:store:{slug.Trim().ToLowerInvariant()}")); return new(bytes[..16]); }
}
