using Forge.Application.Projects;

namespace Forge.Application.Commands;

public sealed record CommandHistoryResult(
    bool Succeeded,
    ProjectState State,
    ProjectPatch? Patch);
