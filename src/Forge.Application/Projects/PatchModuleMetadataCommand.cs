using Forge.Application.Commands;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Projects;

public sealed record PatchModuleMetadataCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ModuleMetadataPatch? MetadataPatch,
    ProjectFoundationProfile? Foundation,
    bool DryRun);
