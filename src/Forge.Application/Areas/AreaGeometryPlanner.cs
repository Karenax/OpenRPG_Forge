using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public enum AreaResizeAnchor { Center, NorthWest, NorthEast, SouthWest, SouthEast }
public sealed record AreaResizePreview(AreaDimensions Dimensions, IReadOnlyList<EntityInstance> Kept,
    IReadOnlyList<EntityId> Removed, Vector3Value Translation, IReadOnlyList<EntityId>? Repositioned = null);
public sealed record AreaGroupPart(BlueprintId BlueprintId, Vector3Value Offset, IReadOnlyList<string>? Tags = null);
public sealed record AreaTileFillCell(Guid PlacementId, EntityId EntityId, Guid DefinitionId, int GridX, int GridZ);
public sealed record AreaTileEditRequest(
    AreaTileEditOperationKind Operation,
    IReadOnlyList<Guid> PlacementIds,
    Guid? ReplacementDefinitionId = null,
    string? ReplacementKitKey = null,
    bool PreserveAttachedObjects = true,
    string SelectionType = "",
    IReadOnlyList<AreaTileFillCell>? FillCells = null);
public sealed record AreaTileEditPreview(
    IReadOnlyList<EntityInstance> Entities,
    AreaFoundationProfile Foundation,
    IReadOnlyList<Guid> SelectedPlacementIds,
    IReadOnlyList<EntityId> PreservedAttachedEntityIds,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaGeometryPlanner
{
    public static IReadOnlyList<EntityInstance> ComposeGroup(Vector3Value origin, IReadOnlyList<AreaGroupPart> parts,
        IReadOnlyList<EntityId> ids)
    {
        if (parts.Count == 0 || parts.Count != ids.Count || ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("Group parts require the same number of unique entity IDs.");
        return parts.Select((part, index) => new EntityInstance(ids[index], part.BlueprintId,
            new TransformValue(new Vector3Value(origin.X + part.Offset.X, origin.Y + part.Offset.Y, origin.Z + part.Offset.Z),
                QuaternionValue.Identity, Vector3Value.One), part.Tags)).ToArray();
    }

    public static IReadOnlyList<EntityInstance> Replace(IReadOnlyList<EntityInstance> entities,
        IReadOnlySet<EntityId> selected, BlueprintId replacement) => entities.Select(entity => selected.Contains(entity.Id)
        ? new EntityInstance(entity.Id, replacement, entity.Transform, entity.Tags, entity.Overrides) : entity).ToArray();

    public static BlueprintId ChooseCompatible(ProjectDefinition project, BlueprintId current, int seed, bool locked)
    {
        if (locked) return current;
        var source = project.Blueprints.Single(item => item.Id == current);
        var candidates = project.Blueprints.Where(item => item.Kind == source.Kind)
            .OrderBy(item => item.Id.ToString(), StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return current;
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{seed}:{current.Value:D}"));
        var index = (int)(BitConverter.ToUInt32(bytes, 0) % (uint)candidates.Length);
        return candidates[index].Id;
    }

    public static AreaTileAutoFitPreview ChooseCompatible(ModularTileKitProfile kit, Guid placementId) =>
        AreaTileAutoFitService.Preview(kit, placementId);

    public static AreaTileEditPreview Edit(AreaDefinition area, AreaTileEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(request);
        var foundation = area.Foundation;
        var kit = foundation.TileKit;
        var definitions = kit.Definitions.ToDictionary(item => item.Id);
        var placements = kit.Placements.ToList();
        var entities = area.Entities.ToList();
        var selected = request.Operation == AreaTileEditOperationKind.SelectByType &&
                       !string.IsNullOrWhiteSpace(request.SelectionType)
            ? placements.Where(item => definitions.TryGetValue(item.DefinitionId, out var definition) &&
                    string.Equals(definition.Key, request.SelectionType.Trim(), StringComparison.Ordinal))
                .Select(item => item.Id).ToArray()
            : request.PlacementIds.Distinct().OrderBy(item => item).ToArray();
        var selectedPlacements = placements.Where(item => selected.Contains(item.Id)).ToArray();
        var selectedEntities = selectedPlacements.Select(item => item.EntityId).ToHashSet();
        var attached = entities.Where(item => item.Tags.Any(tag => selectedEntities.Any(parent =>
                string.Equals(tag, $"attached-to:{parent}", StringComparison.OrdinalIgnoreCase))))
            .Select(item => item.Id).ToArray();
        var diagnostics = new List<DomainDiagnostic>();

        switch (request.Operation)
        {
            case AreaTileEditOperationKind.Erase:
                placements.RemoveAll(item => selected.Contains(item.Id));
                entities.RemoveAll(item => selectedEntities.Contains(item.Id) ||
                    !request.PreserveAttachedObjects && attached.Contains(item.Id));
                break;
            case AreaTileEditOperationKind.Replace:
            case AreaTileEditOperationKind.RemapKit:
                if (request.ReplacementDefinitionId is not { } replacementId ||
                    !definitions.TryGetValue(replacementId, out var replacement))
                {
                    diagnostics.Add(new(AreaTileEditingValidator.ReferenceCode,
                        "Replacement requires an existing tile definition.",
                        $"areas/{area.Id}/foundation/tileKit/editing/tools/replacementDefinitionId"));
                    break;
                }

                placements = placements.Select(item => selected.Contains(item.Id)
                    ? item with { DefinitionId = replacementId, VariantKey = string.Empty }
                    : item).ToList();
                entities = entities.Select(item => selectedEntities.Contains(item.Id)
                    ? new EntityInstance(item.Id, replacement.BlueprintId, item.Transform, item.Tags, item.Overrides,
                        item.BlueprintVersionRequirement)
                    : item).ToList();
                break;
            case AreaTileEditOperationKind.Fill:
                foreach (var cell in request.FillCells ?? [])
                {
                    if (!definitions.TryGetValue(cell.DefinitionId, out var definition) ||
                        placements.Any(item => item.Id == cell.PlacementId ||
                            item.GridX == cell.GridX && item.GridZ == cell.GridZ) ||
                        entities.Any(item => item.Id == cell.EntityId))
                    {
                        diagnostics.Add(new(AreaTileEditingValidator.ToolCode,
                            "Fill cells require unique placement/entity IDs, empty cells and an existing definition.",
                            $"areas/{area.Id}/foundation/tileKit/editing/tools/fill"));
                        continue;
                    }

                    var position = new Vector3Value(cell.GridX * kit.GridSizeMeters, 0,
                        cell.GridZ * kit.GridSizeMeters);
                    entities.Add(new(cell.EntityId, definition.BlueprintId,
                        new TransformValue(position, QuaternionValue.Identity, Vector3Value.One),
                        ["tile", $"tile-type:{definition.Key}"]));
                    placements.Add(new(cell.PlacementId, cell.DefinitionId, cell.EntityId, cell.GridX, cell.GridZ,
                        0, ModularTileMirrorMode.None, string.Empty, null));
                }
                break;
        }

        var remainingPlacementIds = placements.Select(item => item.Id).ToHashSet();
        var groups = kit.Groups.Select(group => group with
        { PlacementIds = group.PlacementIds.Where(remainingPlacementIds.Contains).ToArray() })
            .Where(group => group.PlacementIds.Count > 0).ToArray();
        var editing = kit.Editing ?? AreaTileEditingProfile.CreateDefault(area.Id);
        var updatedTools = editing.Tools with
        {
            Operation = request.Operation,
            ReplacementDefinitionId = request.ReplacementDefinitionId,
            ReplacementKitKey = request.ReplacementKitKey,
            SelectionType = request.SelectionType,
            PreserveAttachedObjects = request.PreserveAttachedObjects,
        };
        var updatedEditing = new AreaTileEditingProfile(editing.Id, editing.SchemaVersion,
            "author:area-tile-edit", editing.Groups, updatedTools, editing.AutoFit,
            editing.Properties.Where(item => remainingPlacementIds.Contains(item.PlacementId)).ToArray(),
            editing.Resize);
        var updatedKit = new ModularTileKitProfile(kit.Id, kit.SchemaVersion, "author:area-tile-edit",
            kit.GridSizeMeters, kit.Definitions, kit.ConnectionRules, placements, groups, kit.Brushes,
            updatedEditing);
        var creation = request.Operation == AreaTileEditOperationKind.RemapKit &&
                       !string.IsNullOrWhiteSpace(request.ReplacementKitKey)
            ? foundation.Creation with { KitKey = request.ReplacementKitKey.Trim() }
            : foundation.Creation;
        var updatedFoundation = new AreaFoundationProfile(foundation.Id, foundation.SchemaVersion,
            "author:area-tile-edit", creation, updatedKit, foundation.Lifecycle, foundation.Volumes,
            foundation.Dependencies, foundation.RequireTileEditingRuntimeMetadata);
        var candidate = new AreaDefinition(area.Id, area.Slug, area.DisplayName, entities, area.Kind,
            area.Dimensions, area.Portals, area.SchemaVersion, area.Navigation, area.Environment, area.Authoring,
            area.World, area.RuntimePolicies, updatedFoundation, area.Playtest);
        diagnostics.AddRange(AreaFoundationValidator.Validate(candidate, updatedFoundation));
        return new(entities, updatedFoundation, selected,
            request.PreserveAttachedObjects ? attached : [], diagnostics);
    }

    public static (IReadOnlyList<EntityInstance> Entities, AreaFoundationProfile Foundation)
        ConvertGroupToEditableInstances(AreaDefinition area, Guid groupId)
    {
        ArgumentNullException.ThrowIfNull(area);
        var kit = area.Foundation.TileKit;
        var group = kit.Groups.Single(item => item.Id == groupId);
        var placementIds = group.PlacementIds.ToHashSet();
        var entityIds = kit.Placements.Where(item => placementIds.Contains(item.Id)).Select(item => item.EntityId)
            .ToHashSet();
        var entities = area.Entities.Select(entity => !entityIds.Contains(entity.Id) ? entity : new EntityInstance(
            entity.Id, entity.BlueprintId, entity.Transform,
            entity.Tags.Concat(["tile-editable", $"tile-group-source:{groupId:D}"])
                .Distinct(StringComparer.Ordinal).ToArray(), entity.Overrides, entity.BlueprintVersionRequirement))
            .ToArray();
        var placements = kit.Placements.Select(item => placementIds.Contains(item.Id)
            ? item with { GroupId = null }
            : item).ToArray();
        var editing = kit.Editing ?? AreaTileEditingProfile.CreateDefault(area.Id);
        var editableGroups = editing.Groups.Select(item => item.GroupId == groupId
            ? item with { ConvertToEditableInstances = true }
            : item).ToArray();
        var updatedEditing = new AreaTileEditingProfile(editing.Id, editing.SchemaVersion,
            "author:area-group-convert", editableGroups, editing.Tools, editing.AutoFit, editing.Properties,
            editing.Resize);
        var updatedKit = new ModularTileKitProfile(kit.Id, kit.SchemaVersion, "author:area-group-convert",
            kit.GridSizeMeters, kit.Definitions, kit.ConnectionRules, placements,
            kit.Groups, kit.Brushes, updatedEditing);
        var foundation = area.Foundation;
        return (entities, new(foundation.Id, foundation.SchemaVersion, "author:area-group-convert",
            foundation.Creation, updatedKit, foundation.Lifecycle, foundation.Volumes, foundation.Dependencies,
            foundation.RequireTileEditingRuntimeMetadata));
    }

    public static AreaResizePreview Resize(AreaDefinition area, AreaDimensions dimensions, AreaResizeAnchor anchor,
        IReadOnlyDictionary<EntityId, Vector3Value>? lossRepositions = null)
    {
        var dx = anchor switch { AreaResizeAnchor.NorthEast or AreaResizeAnchor.SouthEast => (dimensions.Width - area.Dimensions.Width) / 2f, AreaResizeAnchor.NorthWest or AreaResizeAnchor.SouthWest => (area.Dimensions.Width - dimensions.Width) / 2f, _ => 0f };
        var dz = anchor switch { AreaResizeAnchor.SouthWest or AreaResizeAnchor.SouthEast => (dimensions.Depth - area.Dimensions.Depth) / 2f, AreaResizeAnchor.NorthWest or AreaResizeAnchor.NorthEast => (area.Dimensions.Depth - dimensions.Depth) / 2f, _ => 0f };
        var translation = new Vector3Value(dx, 0, dz);
        var shifted = area.Entities.Select(entity => new EntityInstance(entity.Id, entity.BlueprintId,
            new TransformValue(lossRepositions is not null && lossRepositions.TryGetValue(entity.Id, out var position)
                    ? position
                    : new Vector3Value(entity.Transform.Position.X + dx, entity.Transform.Position.Y,
                        entity.Transform.Position.Z + dz), entity.Transform.Rotation, entity.Transform.Scale), entity.Tags, entity.Overrides,
            entity.BlueprintVersionRequirement)).ToArray();
        var halfW = dimensions.Width / 2f; var halfD = dimensions.Depth / 2f;
        var kept = shifted.Where(entity => MathF.Abs(entity.Transform.Position.X) + entity.Transform.Scale.X / 2f <= halfW && MathF.Abs(entity.Transform.Position.Z) + entity.Transform.Scale.Z / 2f <= halfD).ToArray();
        return new(dimensions, kept, shifted.Where(entity => !kept.Contains(entity)).Select(entity => entity.Id).ToArray(),
            translation, shifted.Where(entity => lossRepositions?.ContainsKey(entity.Id) == true)
                .Select(entity => entity.Id).ToArray());
    }
}
