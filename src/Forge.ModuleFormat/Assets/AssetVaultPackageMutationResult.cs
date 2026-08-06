using Forge.Domain.Assets;
using Forge.Domain.Validation;

namespace Forge.ModuleFormat.Assets;

public sealed record AssetVaultPackageMutationResult(
    bool Succeeded,
    bool Applied,
    Guid PlanId,
    AssetVaultMutationOperation Operation,
    string PackageId,
    AssetVaultInstallationState State,
    int WrittenObjectCount,
    int ReusedObjectCount,
    int DeletedObjectCount,
    long NewBytes,
    long ReusedBytes,
    string? ReceiptRelativePath,
    string? BackupRelativePath,
    IReadOnlyList<AssetCreditEntry> Credits,
    IReadOnlyList<DomainDiagnostic> Diagnostics);
