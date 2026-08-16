using Forge.Application.Commands;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Projects;

public sealed record NewModuleCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ProjectId ProjectId,
    ModuleId ModuleId,
    AreaId AreaId,
    BlueprintId FloorBlueprintId,
    EntityId FloorEntityId,
    BlueprintId PlayerSpawnBlueprintId,
    EntityId PlayerSpawnEntityId,
    string Title,
    string ModuleDescription,
    string AreaDisplayName,
    bool DryRun,
    AreaKind AreaKind = AreaKind.Interior,
    AreaDimensions? Dimensions = null,
    ProjectTemplateKind Template = ProjectTemplateKind.Rpg,
    IReadOnlyList<string>? Kits = null,
    IReadOnlyList<ModuleDistributionPlatform>? TargetPlatforms = null,
    string RulesProvider = "forge.generic",
    ProjectRenderPreset RenderPreset = ProjectRenderPreset.Balanced,
    ProjectNetworkMode NetworkMode = ProjectNetworkMode.Offline,
    string PackageNamespace = "",
    string GameMode = "campaign",
    string EntryClass = "adventurer",
    string PlayerSelectionRule = "default",
    bool? CreateInitialArea = null,
    NewModuleMetadataInput? Metadata = null,
    float PlayerOrientationDegrees = 0);
