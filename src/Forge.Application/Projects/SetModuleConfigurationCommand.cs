using Forge.Application.Commands;
using Forge.Domain.Modules;

namespace Forge.Application.Projects;

public sealed record SetModuleConfigurationCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ProjectMetadata Metadata,
    IReadOnlyList<ProjectVariableDefinition> Variables,
    bool DryRun);
