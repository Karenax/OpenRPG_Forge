using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record PatchAreaBiomesCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    IReadOnlyList<BiomeScatterPatch> Patches,
    bool DryRun);
