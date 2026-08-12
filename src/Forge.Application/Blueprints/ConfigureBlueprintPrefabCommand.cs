using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Application.Blueprints;

public sealed record ConfigureBlueprintPrefabCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    PrefabVariantDefinition? Variant,
    PrefabCompositionDefinition? Composition,
    BlueprintAssetBudgetProfile? AssetBudget,
    IReadOnlyList<string> AuthorCollections,
    bool DryRun);

public sealed record PlacePrefabCompositionCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    BlueprintId BlueprintId,
    AreaId AreaId,
    Vector3Value Origin,
    bool DryRun);
