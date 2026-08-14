using Forge.Application.Commands;

namespace Forge.Application.Projects;

public enum AuthoringElementKind
{
    Area,
    Blueprint,
    Dialogue,
    Quest,
}

public sealed record DeleteAuthoringElementCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AuthoringElementKind Kind,
    Guid ElementId,
    bool DryRun);
