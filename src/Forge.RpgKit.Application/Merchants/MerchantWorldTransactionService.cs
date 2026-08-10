using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Merchants;

namespace Forge.RpgKit.Application.Merchants;

public sealed class MerchantWorldTransactionService
{
    public sealed record Handle(Guid MerchantId, Guid PolicyId, int SchemaVersion, string Provenance);
    public sealed record Resolution(bool Available, Handle? TransactionHandle, string DiagnosticCode, string Audit);

    public Resolution Resolve(MerchantWorldProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var resolution = WorldFeatureRuntimeResolver.ResolveMerchantTransactions(profile);
        return resolution.Policy is null
            ? new(false, null, resolution.DiagnosticCode, resolution.Audit)
            : new(true, new(profile.Id, resolution.Policy.Id, resolution.Policy.SchemaVersion,
                resolution.Policy.Provenance), string.Empty, resolution.Audit);
    }

    public TradeResult Execute(
        Handle handle,
        MerchantWorldProfile profile,
        TradePartyState buyer,
        TradePartyState seller,
        TradeRequest request)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(profile);
        var policy = profile.Transactions;
        if (policy is null || handle.MerchantId != profile.Id || handle.PolicyId != policy.Id ||
            handle.SchemaVersion != policy.SchemaVersion || !string.Equals(handle.Provenance, policy.Provenance, StringComparison.Ordinal))
            return new(false, new(BuildStore(profile, policy), buyer, seller),
                [new("RPG_TRADE_HANDLE_STALE", $"merchants/{profile.Id}/transactions", "Typed merchant transaction handle is stale or unresolved.")],
                "no-change");

        return MerchantService.Execute(new(BuildStore(profile, policy), buyer, seller), request);
    }

    private static StoreDefinition BuildStore(MerchantWorldProfile profile, MerchantWorldProfile.TransactionPolicy? policy)
    {
        var active = policy ?? new(Guid.Empty, 1, "optional-fallback", string.Empty, string.Empty, [], [], [],
            new Dictionary<string, string>(), 1, 1, true);
        return new(profile.Id, profile.SchemaVersion, profile.Provenance, $"merchant-{profile.Id:D}",
            $"merchant-{profile.Id:D}", active.Locales,
            [new(profile.BlueprintId.ToString(), active.VendorInteractionId, active.DialogueChoiceId, "npc")],
            active.Stock.Select((item, index) => new StockEntry(item.ItemBlueprintId.Value, item.VariantId,
                SupplyPolicy.Finite, item.Quantity, null, TimeSpan.Zero, index, item.RestrictionIds.ToHashSet(StringComparer.Ordinal),
                item.UnitPrice)).ToArray(),
            profile.Currencies.Select(item => item.Id).ToHashSet(StringComparer.Ordinal),
            profile.Treasury.ToDictionary(item => item.CurrencyId, item => item.Balance, StringComparer.Ordinal),
            active.Scripts);
    }
}
