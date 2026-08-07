using Forge.Domain.Blueprints;

namespace Forge.Domain.WorldFeatures;

public sealed record MerchantWorldProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BlueprintId,
    IReadOnlyList<MerchantWorldProfile.CurrencyDefinition> Currencies,
    IReadOnlyList<MerchantWorldProfile.TreasuryAccount> Treasury,
    MerchantWorldProfile.PurchasePolicy Purchase,
    MerchantWorldProfile.StolenGoodsPolicy StolenGoods,
    MerchantWorldProfile.RestockPolicy Restock,
    MerchantWorldProfile.NegotiationPolicy Negotiation,
    MerchantWorldProfile.MarketPolicy? Market,
    IReadOnlyList<MerchantWorldProfile.ServiceEntry> Services,
    MerchantWorldProfile.LedgerPolicy Ledger)
{
    public sealed record CurrencyDefinition(
        string Id,
        string DisplayNameKey,
        decimal ExchangeRate,
        bool Tradeable,
        bool ReputationBacked);

    public sealed record TreasuryAccount(
        string CurrencyId,
        decimal Balance,
        decimal CreditLimit,
        decimal Debt,
        decimal ReplenishAmount,
        decimal MaximumBalance);

    public sealed record PurchasePolicy(
        decimal MaximumPerItem,
        decimal MaximumPerTransaction,
        IReadOnlyDictionary<string, decimal> CategoryCaps,
        double MinimumRelationship,
        bool RequireTreasuryFunds);

    public sealed record StolenGoodsPolicy(
        bool Accepted,
        IReadOnlyList<string> Jurisdictions,
        bool RequireProof,
        bool FenceRoleRequired,
        double RiskPremium,
        string CrimeEventId);

    public sealed record RestockPolicy(
        double IntervalWorldHours,
        string EventId,
        Guid? SupplierRouteId,
        bool DemandAware,
        bool Persistent);

    public sealed record NegotiationPolicy(
        string SkillId,
        int Difficulty,
        double MaximumDiscount,
        double MaximumMarkup,
        double CooldownSeconds,
        bool ServerAuthoritative);

    public sealed record MarketPolicy(
        string RegionId,
        double SupplyWeight,
        double DemandWeight,
        IReadOnlyDictionary<string, double> EventModifiers,
        bool AdminOverrideAllowed);

    public sealed record ServiceEntry(
        Guid Id,
        string Kind,
        string LabelKey,
        string CurrencyId,
        decimal Price,
        string ActionId,
        string Requirement);

    public sealed record LedgerPolicy(
        bool Enabled,
        int RetentionDays,
        decimal AnomalyThreshold,
        bool RecordActor,
        bool RecordItem,
        bool RecordSourceAndSink);
}
