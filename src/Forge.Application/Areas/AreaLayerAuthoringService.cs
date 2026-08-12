using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;

namespace Forge.Application.Areas;

public sealed record AreaLayerScriptSnapshot(
    AreaId AreaId,
    int SchemaVersion,
    IReadOnlyList<AreaLayerEffectiveState> Layers,
    bool RuntimeMetadataRequired);

public sealed record AreaLayerSelection(
    Guid LayerId,
    IReadOnlyList<Guid> LayerIds,
    IReadOnlyList<EntityId> EntityIds,
    bool IncludesDescendants,
    bool HiddenEntitiesExcluded);

public sealed record AreaLayerPartialExportPlan(
    AreaId AreaId,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<AreaEditorLayer> Layers,
    IReadOnlyList<EntityInstance> Entities,
    IReadOnlyList<BlueprintId> BlueprintDependencies,
    IReadOnlyList<string> Diagnostics);

public sealed record AreaLayerMutationResult(
    bool Succeeded,
    AreaAuthoringProfile Profile,
    IReadOnlyList<string> Diagnostics);

public static class AreaLayerAuthoringService
{
    public const string MissingLayerCode = "area.layer.target_missing";
    public const string LockedLayerCode = "area.layer.locked";
    public const string InvalidParentCode = "area.layer.parent_invalid";
    public const string MissingEntityCode = "area.layer.entity_missing";

    public static AreaLayerScriptSnapshot Read(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        return new(area.Id, AreaEditorLayer.CurrentSchemaVersion, AreaLayerHierarchy.Resolve(area.Authoring),
            area.Authoring.RequireLayerRuntimeMetadata);
    }

    public static AreaLayerSelection SelectGroup(AreaDefinition area, Guid layerId,
        bool includeDescendants = true, bool includeHidden = false)
    {
        ArgumentNullException.ThrowIfNull(area);
        var selectedIds = Expand(area.Authoring.Layers, layerId, includeDescendants);
        var effective = AreaLayerHierarchy.Resolve(area.Authoring).ToDictionary(item => item.LayerId);
        var entities = area.Authoring.Layers.Where(item => selectedIds.Contains(item.Id))
            .Where(item => includeHidden || effective[item.Id].Visible)
            .SelectMany(item => item.EntityIds).Distinct().OrderBy(item => item.Value).ToArray();
        return new(layerId, selectedIds.Order().ToArray(), entities, includeDescendants, !includeHidden);
    }

    public static AreaLayerPartialExportPlan PreviewPartialExport(AreaDefinition area,
        IReadOnlyCollection<Guid> layerIds, bool includeDescendants = true)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(layerIds);
        var selectedIds = layerIds.SelectMany(id => Expand(area.Authoring.Layers, id, includeDescendants))
            .ToHashSet();
        var layers = area.Authoring.Layers.Where(item => selectedIds.Contains(item.Id) && item.ExportEnabled)
            .OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var entityIds = layers.SelectMany(item => item.EntityIds).ToHashSet();
        var entities = area.Entities.Where(item => entityIds.Contains(item.Id)).OrderBy(item => item.Id.Value).ToArray();
        var blueprints = entities.Select(item => item.BlueprintId).Distinct().OrderBy(item => item.Value).ToArray();
        var diagnostics = layerIds.Where(id => area.Authoring.Layers.All(item => item.Id != id))
            .Select(id => $"{MissingLayerCode}:{id}").ToArray();
        return new(area.Id, AreaEditorLayer.CurrentSchemaVersion, "area-layer-partial-export:r056",
            layers, entities, blueprints, diagnostics);
    }

    public static AreaLayerMutationResult Reparent(AreaAuthoringProfile profile, Guid layerId, Guid? parentId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var source = profile.Layers.SingleOrDefault(item => item.Id == layerId);
        if (source is null) return new(false, profile, [$"{MissingLayerCode}:{layerId}"]);
        var effective = AreaLayerHierarchy.Resolve(profile).ToDictionary(item => item.LayerId);
        if (effective[layerId].Locked) return new(false, profile, [$"{LockedLayerCode}:{layerId}"]);
        if (parentId is not null && (!effective.TryGetValue(parentId.Value, out var parent) || parent.Locked ||
                                     Expand(profile.Layers, layerId, true).Contains(parentId.Value)))
            return new(false, profile, [$"{InvalidParentCode}:{parentId}"]);

        var replacement = new AreaEditorLayer(source.Id, source.Name, parentId, source.Visible, source.Locked,
            source.Owner, source.EntityIds, source.SchemaVersion, "author:layer-reparent", source.ExportEnabled);
        return new(true, Replace(profile, profile.Layers.Select(item => item.Id == layerId ? replacement : item)), []);
    }

    public static AreaLayerMutationResult AssignEntity(AreaDefinition area, Guid layerId, EntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(entityId);
        if (area.Entities.All(item => item.Id != entityId))
            return new(false, area.Authoring, [$"{MissingEntityCode}:{entityId}"]);
        var target = area.Authoring.Layers.SingleOrDefault(item => item.Id == layerId);
        if (target is null) return new(false, area.Authoring, [$"{MissingLayerCode}:{layerId}"]);
        var effective = AreaLayerHierarchy.Resolve(area.Authoring).ToDictionary(item => item.LayerId);
        if (effective[layerId].Locked) return new(false, area.Authoring, [$"{LockedLayerCode}:{layerId}"]);

        var layers = area.Authoring.Layers.Select(item =>
        {
            var ids = item.EntityIds.Where(id => id != entityId).ToList();
            if (item.Id == layerId) ids.Add(entityId);
            return new AreaEditorLayer(item.Id, item.Name, item.ParentId, item.Visible, item.Locked, item.Owner,
                ids, item.SchemaVersion, "author:layer-assignment", item.ExportEnabled);
        });
        return new(true, Replace(area.Authoring, layers), []);
    }

    private static AreaAuthoringProfile Replace(AreaAuthoringProfile profile,
        IEnumerable<AreaEditorLayer> layers) => new(profile.SchemaVersion, profile.UserEvents, profile.Map,
        layers.ToArray(), profile.Experience, profile.RequireLayerRuntimeMetadata);

    private static IReadOnlySet<Guid> Expand(IReadOnlyList<AreaEditorLayer> layers, Guid root,
        bool includeDescendants)
    {
        var result = new HashSet<Guid>();
        if (layers.All(item => item.Id != root)) return result;
        result.Add(root);
        if (!includeDescendants) return result;
        var queue = new Queue<Guid>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var current))
        {
            foreach (var child in layers.Where(item => item.ParentId == current && result.Add(item.Id)))
                queue.Enqueue(child.Id);
        }
        return result;
    }
}
