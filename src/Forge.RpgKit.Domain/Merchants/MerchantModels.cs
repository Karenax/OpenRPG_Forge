namespace Forge.RpgKit.Domain.Merchants;

public enum SupplyPolicy { Infinite, Finite, Generated, Crafted, SharedServer, Personal }
public enum TradeKind { Buy, Sell, Barter }
public sealed record CurrencyAmount(string CurrencyId, decimal Amount);
public sealed record StockEntry(Guid ItemDefinitionId, string VariantId, SupplyPolicy Supply, int Quantity, string? SupplierId, TimeSpan RestockInterval, int DisplayOrder, IReadOnlySet<string> HiddenConditions, decimal UnitPrice);
public sealed record VendorBinding(string VendorId, string InteractionId, string DialogueChoiceId, string Channel);
public sealed record StoreDefinition(Guid Id, int SchemaVersion, string Provenance, string Slug, string DisplayName, IReadOnlyList<string> Locales, IReadOnlyList<VendorBinding> Vendors, IReadOnlyList<StockEntry> Stock, IReadOnlySet<string> CurrencyIds, IReadOnlyDictionary<string, decimal> Treasury, IReadOnlyDictionary<string, string> Scripts);
public sealed record StoreWizardRequest(string Slug, string DisplayName, IReadOnlyList<string> Locales, IReadOnlyList<VendorBinding> Vendors, IReadOnlyList<StockEntry> Stock, IReadOnlySet<string> CurrencyIds, IReadOnlyDictionary<string, decimal> Treasury, IReadOnlyDictionary<string, string> Scripts, string Provenance);
public sealed record MerchantDiagnostic(string Code, string Path, string Message);
public sealed record MerchantValidationContext(IReadOnlySet<Guid> ItemDefinitionIds, IReadOnlySet<string> VendorIds, IReadOnlySet<string> CurrencyIds, IReadOnlySet<string> ScriptIds, IReadOnlySet<string> SupplierIds, int MaximumStockEntries);
public sealed record StoreWizardPreview(StoreDefinition Store, IReadOnlyList<MerchantDiagnostic> Diagnostics, string SemanticDiff, bool CanApply);
public sealed record TradeLine(Guid ItemDefinitionId, string VariantId, int Quantity, decimal UnitPrice, string CurrencyId);
public sealed record TradePartyState(string PartyId, IReadOnlyDictionary<string, decimal> Funds, IReadOnlyDictionary<Guid, int> Items, int Capacity);
public sealed record TradeRequest(Guid TransactionId, TradeKind Kind, string BuyerId, string SellerId, IReadOnlyList<TradeLine> OfferedBySeller, IReadOnlyList<TradeLine> OfferedByBuyer);
public sealed record TradeState(StoreDefinition Store, TradePartyState Buyer, TradePartyState Seller);
public sealed record TradeResult(bool Committed, TradeState State, IReadOnlyList<MerchantDiagnostic> Diagnostics, string SemanticDiff);
