using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public sealed record ResolvedModularTileEntity(
    EntityInstance Entity,
    ModularTileMirrorMode Mirror,
    string SemanticState,
    Guid PlacementId);

public static class ModularTilePlacementResolver
{
    public static ResolvedModularTileEntity Resolve(AreaDefinition area, EntityInstance entity)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(entity);

        var placement = area.Foundation.TileKit.Placements.SingleOrDefault(item => item.EntityId == entity.Id);
        if (placement is null)
        {
            return new(entity, ModularTileMirrorMode.None, string.Empty, Guid.Empty);
        }

        var definition = area.Foundation.TileKit.Definitions.SingleOrDefault(item => item.Id == placement.DefinitionId)
            ?? throw new InvalidOperationException(
                $"Tile placement '{placement.Id:D}' references missing definition '{placement.DefinitionId:D}'.");
        var variant = string.IsNullOrWhiteSpace(placement.VariantKey)
            ? null
            : definition.Variants.SingleOrDefault(item =>
                string.Equals(item.Key, placement.VariantKey, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(placement.VariantKey) && variant is null)
        {
            throw new InvalidOperationException(
                $"Tile placement '{placement.Id:D}' references missing variant '{placement.VariantKey}'.");
        }

        var radians = placement.RotationDegrees * MathF.PI / 180f;
        var half = radians / 2f;
        var transform = new TransformValue(
            new Vector3Value(
                placement.GridX * area.Foundation.TileKit.GridSizeMeters,
                entity.Transform.Position.Y,
                placement.GridZ * area.Foundation.TileKit.GridSizeMeters),
            new QuaternionValue(0, MathF.Sin(half), 0, MathF.Cos(half)),
            entity.Transform.Scale);
        var resolved = new EntityInstance(
            entity.Id,
            variant?.BlueprintId ?? definition.BlueprintId,
            transform,
            entity.Tags,
            entity.Overrides);
        return new(resolved, placement.Mirror, variant?.SemanticState ?? string.Empty, placement.Id);
    }
}
