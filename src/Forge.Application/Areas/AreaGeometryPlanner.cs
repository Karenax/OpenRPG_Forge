using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Areas;

public enum AreaResizeAnchor { Center, NorthWest, NorthEast, SouthWest, SouthEast }
public sealed record AreaResizePreview(AreaDimensions Dimensions, IReadOnlyList<EntityInstance> Kept,
    IReadOnlyList<EntityId> Removed, Vector3Value Translation);
public sealed record AreaGroupPart(BlueprintId BlueprintId, Vector3Value Offset, IReadOnlyList<string>? Tags = null);

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
        var index = (int)((uint)HashCode.Combine(seed, current.Value) % (uint)candidates.Length);
        return candidates[index].Id;
    }

    public static AreaResizePreview Resize(AreaDefinition area, AreaDimensions dimensions, AreaResizeAnchor anchor)
    {
        var dx = anchor switch { AreaResizeAnchor.NorthEast or AreaResizeAnchor.SouthEast => (dimensions.Width - area.Dimensions.Width) / 2f, AreaResizeAnchor.NorthWest or AreaResizeAnchor.SouthWest => (area.Dimensions.Width - dimensions.Width) / 2f, _ => 0f };
        var dz = anchor switch { AreaResizeAnchor.SouthWest or AreaResizeAnchor.SouthEast => (dimensions.Depth - area.Dimensions.Depth) / 2f, AreaResizeAnchor.NorthWest or AreaResizeAnchor.NorthEast => (area.Dimensions.Depth - dimensions.Depth) / 2f, _ => 0f };
        var translation = new Vector3Value(dx, 0, dz);
        var shifted = area.Entities.Select(entity => new EntityInstance(entity.Id, entity.BlueprintId,
            new TransformValue(new Vector3Value(entity.Transform.Position.X + dx, entity.Transform.Position.Y, entity.Transform.Position.Z + dz), entity.Transform.Rotation, entity.Transform.Scale), entity.Tags, entity.Overrides,
            entity.BlueprintVersionRequirement)).ToArray();
        var halfW = dimensions.Width / 2f; var halfD = dimensions.Depth / 2f;
        var kept = shifted.Where(entity => MathF.Abs(entity.Transform.Position.X) + entity.Transform.Scale.X / 2f <= halfW && MathF.Abs(entity.Transform.Position.Z) + entity.Transform.Scale.Z / 2f <= halfD).ToArray();
        return new(dimensions, kept, shifted.Where(entity => !kept.Contains(entity)).Select(entity => entity.Id).ToArray(), translation);
    }
}
