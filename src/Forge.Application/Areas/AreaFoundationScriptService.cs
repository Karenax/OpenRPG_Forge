using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record AreaFoundationScriptSnapshot(
    AreaId AreaId,
    Guid FoundationId,
    int SchemaVersion,
    string KitKey,
    string TerrainProfile,
    string LightingProfile,
    string NavigationProfile,
    string StreamingProfile,
    string Biome,
    int Seed,
    int RuntimeBudgetMegabytes,
    int TileDefinitionCount,
    int TilePlacementCount,
    string? EnteredScript,
    string? ExitedScript,
    int EditableGroupCount,
    int TilePropertyCount,
    int ManualLockCount,
    bool ResizeMigrationEnabled,
    bool TileEditingRequired);

public static class AreaFoundationScriptService
{
    public static AreaFoundationScriptSnapshot Read(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var profile = area.Foundation;
        return new(area.Id, profile.Id, profile.SchemaVersion, profile.Creation.KitKey,
            profile.Creation.TerrainProfile, profile.Creation.LightingProfile,
            profile.Creation.NavigationProfile, profile.Creation.StreamingProfile, profile.Creation.Biome,
            profile.Creation.Seed, profile.Creation.RuntimeBudgetMegabytes, profile.TileKit.Definitions.Count,
            profile.TileKit.Placements.Count, profile.Lifecycle.Entered?.ScriptPath,
            profile.Lifecycle.Exited?.ScriptPath, profile.TileKit.Editing?.Groups.Count ?? 0,
            profile.TileKit.Editing?.Properties.Count ?? 0,
            profile.TileKit.Editing?.AutoFit.ManualLockedPlacementIds.Count ?? 0,
            profile.TileKit.Editing?.Resize.MigrateInstances ?? false,
            profile.RequireTileEditingRuntimeMetadata);
    }

    public static ModularTileConnectionPreview PreviewTile(AreaDefinition area, Guid placementId)
    {
        ArgumentNullException.ThrowIfNull(area);
        return ModularTileConnectionPreviewService.Preview(area.Foundation.TileKit, placementId);
    }

    public static AreaTileAutoFitPreview PreviewAutoFit(AreaDefinition area, Guid placementId)
    {
        ArgumentNullException.ThrowIfNull(area);
        return AreaTileAutoFitService.Preview(area.Foundation.TileKit, placementId);
    }
}
