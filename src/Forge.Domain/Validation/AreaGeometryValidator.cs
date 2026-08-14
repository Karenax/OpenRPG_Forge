using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Domain.Validation;

public static class AreaGeometryValidator
{
    public const string OutOfBoundsCode = "area.geometry.out_of_bounds";
    public const string CollisionOverlapCode = "area.geometry.collision_overlap";
    public const string SocketMismatchCode = "area.geometry.socket_mismatch";
    public const string InvalidScaleCode = "area.geometry.scale_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(ProjectDefinition project, AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);

        var diagnostics = new List<DomainDiagnostic>();
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        for (var index = 0; index < area.Entities.Count; index++)
        {
            var entity = area.Entities[index];
            if (!float.IsFinite(entity.Transform.Scale.X) || !float.IsFinite(entity.Transform.Scale.Y) ||
                !float.IsFinite(entity.Transform.Scale.Z) || entity.Transform.Scale.X <= 0 ||
                entity.Transform.Scale.Y <= 0 || entity.Transform.Scale.Z <= 0)
            {
                diagnostics.Add(new DomainDiagnostic(
                    InvalidScaleCode,
                    $"Entity '{entity.Id}' has a non-finite or non-positive scale.",
                    $"areas/{area.Id}/entities/{index}/transform/scale"));
                continue;
            }
            var halfWidth = area.Dimensions.Width / 2f;
            var halfDepth = area.Dimensions.Depth / 2f;
            if (entity.Transform.Position.X - (entity.Transform.Scale.X / 2f) < -halfWidth ||
                entity.Transform.Position.X + (entity.Transform.Scale.X / 2f) > halfWidth ||
                entity.Transform.Position.Z - (entity.Transform.Scale.Z / 2f) < -halfDepth ||
                entity.Transform.Position.Z + (entity.Transform.Scale.Z / 2f) > halfDepth)
            {
                diagnostics.Add(new DomainDiagnostic(
                    OutOfBoundsCode,
                    $"Entity '{entity.Id}' extends outside area '{area.DisplayName}'.",
                    $"areas/{area.Id}/entities/{index}/transform"));
            }
        }

        var colliders = area.Entities
            .Select((entity, index) => (Entity: entity, Index: index))
            .Where(item => blueprints.TryGetValue(item.Entity.BlueprintId, out var blueprint) &&
                IsCollisionGeometry(item.Entity, blueprint))
            .ToArray();
        for (var left = 0; left < colliders.Length; left++)
        {
            for (var right = left + 1; right < colliders.Length; right++)
            {
                var first = colliders[left];
                var second = colliders[right];
                if (IsIntentionalWallJunction(first.Entity, second.Entity) || !Overlaps(first.Entity, second.Entity))
                {
                    continue;
                }

                diagnostics.Add(new DomainDiagnostic(
                    CollisionOverlapCode,
                    $"Collision geometry '{first.Entity.Id}' overlaps '{second.Entity.Id}'.",
                    $"areas/{area.Id}/entities/{first.Index}/transform"));
            }
        }

        foreach (var portal in area.Portals)
        {
            var source = area.Entities.FirstOrDefault(entity => entity.Id == portal.SourceEntityId);
            var destinationArea = project.Areas.FirstOrDefault(candidate => candidate.Id == portal.DestinationAreaId);
            var destination = destinationArea?.Entities.FirstOrDefault(entity => entity.Id == portal.DestinationEntityId);
            var sourceSocket = SocketName(source);
            var destinationSocket = SocketName(destination);
            if (sourceSocket is not null && destinationSocket is not null &&
                !string.Equals(sourceSocket, destinationSocket, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DomainDiagnostic(
                    SocketMismatchCode,
                    $"Portal '{portal.DisplayName}' connects socket '{sourceSocket}' to incompatible socket '{destinationSocket}'.",
                    $"areas/{area.Id}/portals/{portal.Id}"));
            }
        }

        return diagnostics;
    }

    private static bool IsCollisionGeometry(EntityInstance entity, BlueprintDefinition blueprint) =>
        !entity.Tags.Contains("nonblocking", StringComparer.OrdinalIgnoreCase) &&
        (blueprint.Kind == BlueprintKind.Door ||
        (blueprint.Kind == BlueprintKind.Placeable &&
         !entity.Tags.Contains("floor", StringComparer.OrdinalIgnoreCase) &&
         entity.Tags.Any(tag => tag is "wall" or "pillar" or "architecture" or "solid" or "collision")));

    private static bool IsIntentionalWallJunction(EntityInstance first, EntityInstance second) =>
        first.Tags.Contains("wall", StringComparer.OrdinalIgnoreCase) &&
        second.Tags.Contains("wall", StringComparer.OrdinalIgnoreCase) &&
        ((first.Transform.Scale.X > first.Transform.Scale.Z && second.Transform.Scale.Z > second.Transform.Scale.X) ||
         (first.Transform.Scale.Z > first.Transform.Scale.X && second.Transform.Scale.X > second.Transform.Scale.Z));

    private static bool Overlaps(EntityInstance first, EntityInstance second) =>
        MathF.Abs(first.Transform.Position.X - second.Transform.Position.X) <
            (first.Transform.Scale.X + second.Transform.Scale.X) / 2f &&
        MathF.Abs(first.Transform.Position.Y - second.Transform.Position.Y) <
            (first.Transform.Scale.Y + second.Transform.Scale.Y) / 2f &&
        MathF.Abs(first.Transform.Position.Z - second.Transform.Position.Z) <
            (first.Transform.Scale.Z + second.Transform.Scale.Z) / 2f;

    private static string? SocketName(EntityInstance? entity) => entity?.Tags
        .FirstOrDefault(tag => tag.StartsWith("socket:", StringComparison.OrdinalIgnoreCase))?
        .Split(':', 2)[1]
        .Trim();
}
