using Forge.Application.Commands;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record SetAssetPackageManifestCommand(
    CommandId CommandId,
    AssetPackageManifestRevision BaseRevision,
    AssetPackageManifest Manifest,
    bool DryRun = false);
