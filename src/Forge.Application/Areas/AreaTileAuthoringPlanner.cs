using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed record AreaTilePlacementDraft(
    Guid PlacementId,
    EntityId EntityId,
    Guid DefinitionId,
    int GridX,
    int GridZ,
    float RotationDegrees,
    ModularTileMirrorMode Mirror,
    string VariantKey,
    Guid? GroupId);

public sealed record AreaTileAuthoringPreview(
    IReadOnlyList<EntityInstance> Entities,
    AreaFoundationProfile Foundation,
    ModularTilePlacement? Placement,
    AreaTileAutoFitPreview? AutoFit,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    IReadOnlyList<string> Notices)
{
    public bool CanApply => Diagnostics.Count == 0;
}

/// <summary>
/// Canonical, side-effect-free planner for the Studio tile tools. Every method returns the complete
/// geometry/foundation candidate consumed atomically by <see cref="EditAreaGeometryHandler"/>.
/// </summary>
public static class AreaTileAuthoringPlanner
{
    public const string PlacementMissingCode = "area.tile_authoring.placement_missing";
    public const string DefinitionMissingCode = "area.tile_authoring.definition_missing";
    public const string GroupMissingCode = "area.tile_authoring.group_missing";

    public static AreaTileAuthoringPreview UpsertPlacement(AreaDefinition area, AreaTilePlacementDraft draft)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(draft);

        var kit = area.Foundation.TileKit;
        var definition = kit.Definitions.SingleOrDefault(item => item.Id == draft.DefinitionId);
        if (definition is null)
        {
            return Failure(area, DefinitionMissingCode,
                "Tile placement requires an existing definition.",
                $"areas/{area.Id}/foundation/tileKit/placements/{draft.PlacementId:D}");
        }

        var normalizedVariant = draft.VariantKey?.Trim() ?? string.Empty;
        var placements = kit.Placements.Where(item => item.Id != draft.PlacementId).ToList();
        var previous = kit.Placements.SingleOrDefault(item => item.Id == draft.PlacementId);
        var placement = new ModularTilePlacement(
            draft.PlacementId,
            draft.DefinitionId,
            draft.EntityId,
            draft.GridX,
            draft.GridZ,
            draft.RotationDegrees,
            draft.Mirror,
            normalizedVariant,
            draft.GroupId);
        placements.Add(placement);

        var sourceEntity = area.Entities.SingleOrDefault(item => item.Id == draft.EntityId);
        if (sourceEntity is null && previous is not null)
        {
            sourceEntity = area.Entities.SingleOrDefault(item => item.Id == previous.EntityId);
        }

        var position = new Vector3Value(
            draft.GridX * kit.GridSizeMeters,
            sourceEntity?.Transform.Position.Y ?? 0,
            draft.GridZ * kit.GridSizeMeters);
        var entity = sourceEntity is null
            ? new EntityInstance(draft.EntityId, definition.BlueprintId,
                new TransformValue(position, QuaternionValue.Identity, Vector3Value.One),
                ["tile", $"tile-type:{definition.Key}"])
            : new EntityInstance(
                draft.EntityId,
                definition.BlueprintId,
                new TransformValue(position, sourceEntity.Transform.Rotation, sourceEntity.Transform.Scale),
                sourceEntity.Tags,
                sourceEntity.Overrides,
                sourceEntity.BlueprintVersionRequirement);
        var replacedEntityIds = previous is null || previous.EntityId == draft.EntityId
            ? new HashSet<EntityId> { draft.EntityId }
            : new HashSet<EntityId> { previous.EntityId, draft.EntityId };
        var entities = area.Entities.Where(item => !replacedEntityIds.Contains(item.Id)).Append(entity).ToArray();

        var foundation = RebuildFoundation(area.Foundation, placements: placements);
        return Build(area, entities, foundation, placement, null, []);
    }

    public static AreaTileAuthoringPreview TransformPlacement(
        AreaDefinition area,
        Guid placementId,
        float rotationDegrees,
        ModularTileMirrorMode mirror,
        string variantKey)
    {
        ArgumentNullException.ThrowIfNull(area);
        var placement = area.Foundation.TileKit.Placements.SingleOrDefault(item => item.Id == placementId);
        if (placement is null)
        {
            return Failure(area, PlacementMissingCode, "Tile placement does not exist.",
                $"areas/{area.Id}/foundation/tileKit/placements/{placementId:D}");
        }

        return UpsertPlacement(area, new(
            placement.Id,
            placement.EntityId,
            placement.DefinitionId,
            placement.GridX,
            placement.GridZ,
            rotationDegrees,
            mirror,
            variantKey,
            placement.GroupId));
    }

    public static AreaTileAuthoringPreview ApplyAutoFit(AreaDefinition area, Guid placementId)
    {
        ArgumentNullException.ThrowIfNull(area);
        var autoFit = AreaTileAutoFitService.Preview(area.Foundation.TileKit, placementId);
        if (autoFit.SelectedDefinitionId == Guid.Empty)
        {
            return Failure(area, AreaTileAutoFitService.NoCandidateCode,
                "No compatible tile definition is available.",
                $"areas/{area.Id}/foundation/tileKit/placements/{placementId:D}", autoFit);
        }

        var placement = area.Foundation.TileKit.Placements.Single(item => item.Id == placementId);
        var updated = UpsertPlacement(area, new(
            placement.Id,
            placement.EntityId,
            autoFit.SelectedDefinitionId,
            placement.GridX,
            placement.GridZ,
            placement.RotationDegrees,
            placement.Mirror,
            string.Empty,
            placement.GroupId));
        return updated with { AutoFit = autoFit, Notices = autoFit.Diagnostics };
    }

    public static AreaTileAuthoringPreview UpsertGroup(
        AreaDefinition area,
        ModularTileGroup group,
        EditableTileGroupProfile? editableProfile)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(group);

        var kit = area.Foundation.TileKit;
        var groups = kit.Groups.Where(item => item.Id != group.Id).Append(group).ToArray();
        var memberIds = group.PlacementIds.ToHashSet();
        var placements = kit.Placements.Select(item => item.GroupId == group.Id || memberIds.Contains(item.Id)
            ? item with { GroupId = memberIds.Contains(item.Id) ? group.Id : null }
            : item).ToArray();
        var editing = kit.Editing ?? AreaTileEditingProfile.CreateDefault(area.Id);
        var editableGroups = editableProfile is null
            ? editing.Groups.Where(item => item.GroupId != group.Id).ToArray()
            : editing.Groups.Where(item => item.GroupId != group.Id).Append(editableProfile).ToArray();
        var updatedEditing = RebuildEditing(editing, groups: editableGroups);
        var foundation = RebuildFoundation(area.Foundation, placements: placements, groups: groups,
            editing: updatedEditing);
        return Build(area, area.Entities, foundation, null, null, []);
    }

    public static AreaTileAuthoringPreview RemoveGroup(AreaDefinition area, Guid groupId)
    {
        ArgumentNullException.ThrowIfNull(area);
        var kit = area.Foundation.TileKit;
        if (kit.Groups.All(item => item.Id != groupId))
        {
            return Failure(area, GroupMissingCode, "Tile group does not exist.",
                $"areas/{area.Id}/foundation/tileKit/groups/{groupId:D}");
        }

        var placements = kit.Placements.Select(item => item.GroupId == groupId
            ? item with { GroupId = null }
            : item).ToArray();
        var editing = kit.Editing ?? AreaTileEditingProfile.CreateDefault(area.Id);
        var updatedEditing = RebuildEditing(editing,
            groups: editing.Groups.Where(item => item.GroupId != groupId).ToArray());
        var foundation = RebuildFoundation(area.Foundation,
            placements: placements,
            groups: kit.Groups.Where(item => item.Id != groupId).ToArray(),
            editing: updatedEditing);
        return Build(area, area.Entities, foundation, null, null, []);
    }

    public static AreaTileAuthoringPreview UpsertProperty(AreaDefinition area, TileInstancePropertyProfile property)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(property);
        var kit = area.Foundation.TileKit;
        var editing = kit.Editing ?? AreaTileEditingProfile.CreateDefault(area.Id);
        var properties = editing.Properties.Where(item => item.PlacementId != property.PlacementId)
            .Append(property).ToArray();
        var updatedEditing = RebuildEditing(editing, properties: properties);
        var foundation = RebuildFoundation(area.Foundation, editing: updatedEditing);
        return Build(area, area.Entities, foundation,
            kit.Placements.SingleOrDefault(item => item.Id == property.PlacementId), null, []);
    }

    public static AreaTileAuthoringPreview RemoveProperty(AreaDefinition area, Guid placementId)
    {
        ArgumentNullException.ThrowIfNull(area);
        var kit = area.Foundation.TileKit;
        var editing = kit.Editing ?? AreaTileEditingProfile.CreateDefault(area.Id);
        var updatedEditing = RebuildEditing(editing,
            properties: editing.Properties.Where(item => item.PlacementId != placementId).ToArray());
        var foundation = RebuildFoundation(area.Foundation, editing: updatedEditing);
        return Build(area, area.Entities, foundation,
            kit.Placements.SingleOrDefault(item => item.Id == placementId), null, []);
    }

    private static AreaTileAuthoringPreview Build(
        AreaDefinition source,
        IReadOnlyList<EntityInstance> entities,
        AreaFoundationProfile foundation,
        ModularTilePlacement? placement,
        AreaTileAutoFitPreview? autoFit,
        IReadOnlyList<string> notices)
    {
        var candidate = new AreaDefinition(source.Id, source.Slug, source.DisplayName, entities, source.Kind,
            source.Dimensions, source.Portals, source.SchemaVersion, source.Navigation, source.Environment,
            source.Authoring, source.World, source.RuntimePolicies, foundation, source.Playtest);
        return new(entities, foundation, placement, autoFit,
            AreaFoundationValidator.Validate(candidate, foundation), notices);
    }

    private static AreaTileAuthoringPreview Failure(
        AreaDefinition area,
        string code,
        string message,
        string path,
        AreaTileAutoFitPreview? autoFit = null) => new(
            area.Entities,
            area.Foundation,
            null,
            autoFit,
            [new(code, message, path)],
            autoFit?.Diagnostics ?? []);

    private static AreaFoundationProfile RebuildFoundation(
        AreaFoundationProfile foundation,
        IReadOnlyList<ModularTilePlacement>? placements = null,
        IReadOnlyList<ModularTileGroup>? groups = null,
        AreaTileEditingProfile? editing = null)
    {
        var kit = foundation.TileKit;
        var updatedKit = new ModularTileKitProfile(
            kit.Id,
            kit.SchemaVersion,
            "author:area-tile-studio",
            kit.GridSizeMeters,
            kit.Definitions,
            kit.ConnectionRules,
            placements ?? kit.Placements,
            groups ?? kit.Groups,
            kit.Brushes,
            editing ?? kit.Editing);
        return new(
            foundation.Id,
            foundation.SchemaVersion,
            "author:area-tile-studio",
            foundation.Creation,
            updatedKit,
            foundation.Lifecycle,
            foundation.Volumes,
            foundation.Dependencies,
            foundation.RequireTileEditingRuntimeMetadata);
    }

    private static AreaTileEditingProfile RebuildEditing(
        AreaTileEditingProfile source,
        IReadOnlyList<EditableTileGroupProfile>? groups = null,
        IReadOnlyList<TileInstancePropertyProfile>? properties = null) => new(
            source.Id,
            source.SchemaVersion,
            "author:area-tile-studio",
            groups ?? source.Groups,
            source.Tools,
            source.AutoFit,
            properties ?? source.Properties,
            source.Resize);
}
