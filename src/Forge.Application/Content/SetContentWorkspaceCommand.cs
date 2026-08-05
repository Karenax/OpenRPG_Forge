using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Content;

namespace Forge.Application.Content;

public sealed record SetContentWorkspaceCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ContentWorkspaceConfiguration Configuration,
    bool DryRun);
