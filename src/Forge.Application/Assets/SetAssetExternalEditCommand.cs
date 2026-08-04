using Forge.Application.Commands;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record SetAssetExternalEditCommand(
    CommandId CommandId,
    AssetExternalEditRevision BaseRevision,
    AssetExternalEditConfiguration Configuration,
    bool DryRun);
