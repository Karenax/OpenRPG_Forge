using Forge.Application.Commands;
using Forge.Application.Projects;

namespace Forge.Application.Creator;

public sealed record SafeRenameCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    CreatorContentKind TargetKind,
    string TargetId,
    string DisplayName,
    string? Slug,
    bool DryRun);
