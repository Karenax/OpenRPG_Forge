using Forge.Application.Commands;
using Forge.Domain.Build;

namespace Forge.Application.Build;

public sealed record SetWorkspaceBuildProfileCommand(
    CommandId CommandId,
    WorkspaceBuildProfileRevision BaseRevision,
    WorkspaceBuildProfile Profile,
    bool DryRun);
