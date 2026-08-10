using Forge.Application.Commands;
using Forge.Domain.Projects;

namespace Forge.Application.Projects;

public sealed record SetProjectFoundationCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ProjectFoundationProfile Foundation,
    bool DryRun);
