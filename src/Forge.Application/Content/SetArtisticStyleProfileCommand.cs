using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Assets;

namespace Forge.Application.Content;

public sealed record SetArtisticStyleProfileCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ArtisticStyleProfile Profile,
    bool DryRun);
