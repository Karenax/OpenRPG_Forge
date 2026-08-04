using Forge.Application.Commands;
using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record SetAssetProcessingProfileCommand(
    CommandId CommandId,
    AssetProcessingProfileRevision BaseRevision,
    AssetProcessingProfile Profile,
    bool DryRun);
