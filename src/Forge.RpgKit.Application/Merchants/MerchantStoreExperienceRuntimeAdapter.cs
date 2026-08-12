using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Domain.Merchants;

namespace Forge.RpgKit.Application.Merchants;

public sealed class MerchantStoreExperienceRuntimeAdapter
{
    public sealed record Handle(Guid MerchantId, Guid ExperienceId, int SchemaVersion, string Provenance);

    public sealed record Resolution(
        bool Available,
        Handle? ExperienceHandle,
        bool OptionalFallback,
        string DiagnosticCode,
        string DiagnosticPath);

    public Resolution Resolve(MerchantWorldProfile merchant)
    {
        ArgumentNullException.ThrowIfNull(merchant);
        if (merchant.Experience is null)
            return merchant.ExperienceRequiredAtRuntime
                ? new(false, null, false, "WORLD_MERCHANT_EXPERIENCE_REQUIRED_MISSING",
                    $"module/worldFeatures/merchants/{merchant.Id}/experience")
                : new(false, null, true, string.Empty, string.Empty);
        return new(true, new(merchant.Id, merchant.Experience.Id, merchant.Experience.SchemaVersion,
            merchant.Experience.Provenance), false, string.Empty, string.Empty);
    }

    public StoreUiConfiguration Ui(Handle handle, MerchantWorldProfile merchant)
    {
        var ui = Require(handle, merchant).Ui;
        return new(ui.KeyboardNavigation, ui.GamepadNavigation, ui.Compare, ui.Filters, ui.Cart,
            ui.Confirmation, ui.ScreenReaderLabels, ui.HighContrast, ui.Columns);
    }

    public StoreSessionOpened Open(
        Handle handle,
        MerchantWorldProfile merchant,
        string customerId,
        string vendorId,
        StorePriceContext priceContext,
        DateTimeOffset at)
    {
        var experience = Require(handle, merchant);
        var foundation = new MerchantFoundationRuntimeAdapter();
        var foundationHandle = foundation.Resolve(merchant).FoundationHandle ??
                               throw new InvalidOperationException("Merchant foundation is unavailable.");
        var store = foundation.BuildStore(foundationHandle, merchant);
        if (string.IsNullOrWhiteSpace(customerId) ||
            store.Vendors.All(item => !string.Equals(item.VendorId, vendorId, StringComparison.Ordinal)) ||
            !store.CurrencyIds.Contains(priceContext.CurrencyId) ||
            priceContext.Prices.Values.Any(item => item.CurrencyId != priceContext.CurrencyId))
            throw new ArgumentException("Store session customer, vendor or price context is invalid.");
        if (!experience.Sessions.IncludeCustomer || !experience.Sessions.IncludeVendor ||
            !experience.Sessions.IncludePriceContext)
            throw new InvalidOperationException("Store open event payload contract is incomplete.");
        return MerchantExperienceService.Open(store, customerId, vendorId, priceContext, at);
    }

    public StoreSessionClosed Close(
        Handle handle,
        MerchantWorldProfile merchant,
        StoreSessionOpened opened,
        IReadOnlyList<Guid> transactionIds,
        decimal boughtTotal,
        decimal soldTotal,
        DateTimeOffset at)
    {
        var experience = Require(handle, merchant);
        var foundation = new MerchantFoundationRuntimeAdapter();
        var foundationHandle = foundation.Resolve(merchant).FoundationHandle ??
                               throw new InvalidOperationException("Merchant foundation is unavailable.");
        var store = foundation.BuildStore(foundationHandle, merchant);
        if (!experience.Sessions.IncludeTransactionSummary || opened.StoreId != store.Id ||
            transactionIds.Any(id => id == Guid.Empty) || transactionIds.Distinct().Count() != transactionIds.Count ||
            boughtTotal < 0 || soldTotal < 0 || at < opened.At)
            throw new ArgumentException("Store close event transaction summary is invalid.");
        return MerchantExperienceService.Close(opened, transactionIds, boughtTotal, soldTotal, at);
    }

    private static MerchantStoreExperienceProfile Require(Handle handle, MerchantWorldProfile merchant)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(merchant);
        var experience = merchant.Experience ?? throw new InvalidOperationException(
            "Merchant store experience profile is unavailable.");
        if (handle.MerchantId != merchant.Id || handle.ExperienceId != experience.Id ||
            handle.SchemaVersion != experience.SchemaVersion || handle.Provenance != experience.Provenance)
            throw new InvalidOperationException("Merchant store experience handle is stale or mismatched.");
        return experience;
    }
}
