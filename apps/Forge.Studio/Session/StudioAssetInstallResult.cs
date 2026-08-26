using Forge.Domain.Blueprints;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.Studio.Session;

internal sealed record StudioAssetInstallResult(
    bool Succeeded,
    string Message,
    BlueprintId? BlueprintId = null,
    bool Reused = false);

internal sealed record StudioPreparedAssetInstall(
    string WorkspaceRoot,
    AssetCatalogItem CatalogItem,
    AssetPackageManifest Manifest,
    bool Reused,
    string VaultRoot,
    LocalAssetPackageInstallPlan LocalPackage);

internal sealed record StudioAssetPreparationResult(
    bool Succeeded,
    string Message,
    StudioPreparedAssetInstall? Prepared = null);
