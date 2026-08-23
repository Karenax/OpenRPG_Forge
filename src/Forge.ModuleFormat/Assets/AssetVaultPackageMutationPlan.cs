using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public enum AssetVaultMutationOperation
{
    Install,
    Uninstall,
    Restore,
}

public sealed record AssetVaultPackageMutationPlan(
    Guid PlanId,
    string BaseRevision,
    AssetVaultMutationOperation Operation,
    string PackageId,
    bool DryRun,
    AssetVaultInstallationState ResultingState,
    IReadOnlyList<AssetVaultPlannedObject> Objects,
    IReadOnlyList<AssetCreditEntry> Credits,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public bool CanApply => Diagnostics.Count == 0;

    public long NewBytes => Objects.Where(item => !item.Reused).Sum(item => item.Bytes);

    public long ReusedBytes => Objects.Where(item => item.Reused).Sum(item => item.Bytes);
}
