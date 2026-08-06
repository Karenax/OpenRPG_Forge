using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record InstallAssetBlueprintCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AssetCatalogItem Item,
    AssetPackageManifest Manifest,
    bool DryRun = false);
