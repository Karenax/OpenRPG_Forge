using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;

namespace Forge.Application.Blueprints;

public sealed record ConfigureBlueprintPaletteCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    BlueprintPaletteOrigin Origin,
    bool IsFavorite,
    BlueprintProvenance Provenance,
    bool DryRun);
