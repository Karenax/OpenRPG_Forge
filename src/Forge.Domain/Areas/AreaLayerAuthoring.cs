using Forge.Domain.Entities;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public sealed record AreaLayerEffectiveState(
    Guid LayerId,
    string Name,
    Guid? ParentId,
    int Depth,
    bool Visible,
    bool Locked,
    string Owner,
    bool ExportEnabled,
    IReadOnlyList<EntityId> EntityIds);

public sealed record AreaLayerRuntimeHandle(
    AreaId AreaId,
    int SchemaVersion,
    IReadOnlyList<AreaLayerEffectiveState> Layers);

public sealed record AreaLayerRuntimeResolution(
    AreaLayerRuntimeHandle? Handle,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaLayerAuthoringValidator
{
    public const string IdentityCode = "area.layer.identity_invalid";
    public const string SchemaCode = "area.layer.schema_unsupported";
    public const string ProvenanceCode = "area.layer.provenance_missing";
    public const string ParentCode = "area.layer.parent_invalid";
    public const string CycleCode = "area.layer.hierarchy_cycle";
    public const string DuplicateNameCode = "area.layer.name_duplicate";
    public const string EntityReferenceCode = "area.layer.entity_missing";
    public const string EntityOwnershipCode = "area.layer.entity_duplicated";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var layers = area.Authoring.Layers;
        var layerIds = layers.Select(item => item.Id).ToHashSet();
        var entityIds = area.Entities.Select(item => item.Id).ToHashSet();
        var diagnostics = new List<DomainDiagnostic>();

        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            var path = $"areas/{area.Id}/authoring/layers/{index}";
            if (layer.Id == Guid.Empty || string.IsNullOrWhiteSpace(layer.Name) || string.IsNullOrWhiteSpace(layer.Owner))
                diagnostics.Add(new(IdentityCode, "Layer UUID, name and owner are required.", path));
            if (layer.SchemaVersion != AreaEditorLayer.CurrentSchemaVersion)
                diagnostics.Add(new(SchemaCode, $"Layer schema {layer.SchemaVersion} is unsupported.", path));
            if (string.IsNullOrWhiteSpace(layer.Provenance))
                diagnostics.Add(new(ProvenanceCode, "Layer provenance is required.", path));
            if (layer.ParentId is not null && (!layerIds.Contains(layer.ParentId.Value) || layer.ParentId == layer.Id))
                diagnostics.Add(new(ParentCode, "Layer parent must reference another layer in the same area.", path));
            foreach (var entityId in layer.EntityIds.Where(id => !entityIds.Contains(id)))
                diagnostics.Add(new(EntityReferenceCode, $"Layer references missing entity '{entityId}'.", path));
        }

        foreach (var group in layers.GroupBy(item => (item.ParentId, item.Name), new LayerNameComparer())
                     .Where(group => group.Count() > 1))
            diagnostics.Add(new(DuplicateNameCode, $"Sibling layer name '{group.Key.Name}' is duplicated.",
                $"areas/{area.Id}/authoring/layers"));

        foreach (var duplicate in layers.SelectMany(item => item.EntityIds.Select(id => (item.Id, EntityId: id)))
                     .GroupBy(item => item.EntityId).Where(group => group.Count() > 1))
            diagnostics.Add(new(EntityOwnershipCode, $"Entity '{duplicate.Key}' belongs to multiple layers.",
                $"areas/{area.Id}/authoring/layers"));

        var byId = layers.ToDictionary(item => item.Id);
        foreach (var layer in layers)
        {
            var visited = new HashSet<Guid>();
            var current = layer;
            while (current.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent))
            {
                if (!visited.Add(current.Id) || parent.Id == layer.Id)
                {
                    diagnostics.Add(new(CycleCode, $"Layer '{layer.Name}' participates in a hierarchy cycle.",
                        $"areas/{area.Id}/authoring/layers/{layer.Id}"));
                    break;
                }
                current = parent;
            }
        }

        return diagnostics.Distinct().ToArray();
    }

    private sealed class LayerNameComparer : IEqualityComparer<(Guid? ParentId, string Name)>
    {
        public bool Equals((Guid? ParentId, string Name) x, (Guid? ParentId, string Name) y) =>
            x.ParentId == y.ParentId && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Guid? ParentId, string Name) obj) =>
            HashCode.Combine(obj.ParentId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}

public static class AreaLayerHierarchy
{
    public static IReadOnlyList<AreaLayerEffectiveState> Resolve(AreaAuthoringProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var byId = profile.Layers.ToDictionary(item => item.Id);
        return profile.Layers.Select(layer => Resolve(layer, byId)).OrderBy(item => Path(item, byId),
            StringComparer.Ordinal).ToArray();
    }

    private static AreaLayerEffectiveState Resolve(AreaEditorLayer layer,
        IReadOnlyDictionary<Guid, AreaEditorLayer> byId)
    {
        var visible = layer.Visible;
        var locked = layer.Locked;
        var depth = 0;
        var visited = new HashSet<Guid> { layer.Id };
        var current = layer;
        while (current.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent) &&
               visited.Add(parent.Id))
        {
            depth++;
            visible &= parent.Visible;
            locked |= parent.Locked;
            current = parent;
        }
        return new(layer.Id, layer.Name, layer.ParentId, depth, visible, locked, layer.Owner,
            layer.ExportEnabled, layer.EntityIds);
    }

    private static string Path(AreaLayerEffectiveState layer, IReadOnlyDictionary<Guid, AreaEditorLayer> byId)
    {
        var parts = new List<string> { layer.Name };
        var parentId = layer.ParentId;
        var visited = new HashSet<Guid> { layer.LayerId };
        while (parentId is { } id && byId.TryGetValue(id, out var parent) && visited.Add(id))
        {
            parts.Add(parent.Name);
            parentId = parent.ParentId;
        }
        parts.Reverse();
        return string.Join('/', parts);
    }
}

public static class AreaLayerRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.area_layers.required_missing";

    public static AreaLayerRuntimeResolution Resolve(AreaDefinition area, bool required)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (area.Authoring.Layers.Count == 0)
            return required
                ? new(null, [new(RequiredMissingCode, "Required area layer metadata is missing.",
                    $"areas/{area.Id}/authoring/layers")])
                : new(null, []);

        var diagnostics = AreaLayerAuthoringValidator.Validate(area);
        return diagnostics.Count == 0
            ? new(new(area.Id, AreaEditorLayer.CurrentSchemaVersion,
                AreaLayerHierarchy.Resolve(area.Authoring)), [])
            : new(null, diagnostics);
    }
}
