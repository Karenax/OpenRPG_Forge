using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Domain.Navigation;

public static class NavigationSurfaceBuilder
{
    public const int MaximumCellCount = 1_000_000;

    public static NavigationSurface Build(
        ProjectDefinition project,
        AreaDefinition area,
        NavigationAgentProfile agent,
        NavigationDoorState doorState = NavigationDoorState.Current)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(agent);
        var cellSize = area.Navigation.CellSize;
        var widthCells = (int)Math.Ceiling(area.Dimensions.Width / cellSize);
        var depthCells = (int)Math.Ceiling(area.Dimensions.Depth / cellSize);
        if ((long)widthCells * depthCells > MaximumCellCount)
        {
            throw new InvalidOperationException(
                $"Navigation surface would contain {(long)widthCells * depthCells:N0} cells; " +
                $"maximum is {MaximumCellCount:N0}. Increase cell size.");
        }

        var minimumX = -area.Dimensions.Width / 2f;
        var minimumZ = -area.Dimensions.Depth / 2f;
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var obstacles = area.Entities.Where(entity =>
            blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
            IsObstacle(entity, blueprint, agent, doorState)).ToArray();
        var walkable = new List<NavigationCell>(widthCells * depthCells);
        for (var z = 0; z < depthCells; z++)
        {
            for (var x = 0; x < widthCells; x++)
            {
                var centerX = minimumX + ((x + 0.5f) * cellSize);
                var centerZ = minimumZ + ((z + 0.5f) * cellSize);
                if (MathF.Abs(centerX) + agent.Radius > area.Dimensions.Width / 2f ||
                    MathF.Abs(centerZ) + agent.Radius > area.Dimensions.Depth / 2f)
                {
                    continue;
                }

                if (!agent.CanFly && obstacles.Any(entity => Intersects(entity, centerX, centerZ, agent.Radius)))
                {
                    continue;
                }

                walkable.Add(new NavigationCell(x, z));
            }
        }

        var walkableSet = walkable.ToHashSet();
        var edges = new List<NavigationEdge>();
        if (agent.CanUseOffMeshLinks)
        {
            foreach (var link in area.Navigation.OffMeshLinks.Where(link =>
                         agent.Radius <= link.MaximumAgentRadius &&
                         (link.Kind != NavigationLinkKind.Flight || agent.CanFly)))
            {
                var start = CellAt(link.Start, minimumX, minimumZ, cellSize);
                var end = CellAt(link.End, minimumX, minimumZ, cellSize);
                if (!walkableSet.Contains(start) || !walkableSet.Contains(end))
                {
                    continue;
                }

                edges.Add(new NavigationEdge(start, end, link.Id));
                if (link.Bidirectional)
                {
                    edges.Add(new NavigationEdge(end, start, link.Id));
                }
            }
        }

        return new NavigationSurface(
            NavigationSurfaceId.From(StableGuid(area.Id, agent.Id, doorState)),
            area.Id,
            agent.Id,
            doorState,
            widthCells,
            depthCells,
            cellSize,
            minimumX,
            minimumZ,
            walkable,
            edges,
            Fingerprint(area, agent, doorState),
            "derived-navigation-bake");
    }

    private static bool IsObstacle(
        EntityInstance entity,
        BlueprintDefinition blueprint,
        NavigationAgentProfile agent,
        NavigationDoorState doorState)
    {
        if (blueprint.Kind == BlueprintKind.Door)
        {
            return doorState switch
            {
                NavigationDoorState.AllOpen => false,
                NavigationDoorState.AllClosed => true,
                _ => !agent.CanOpenDoors && IsDoorClosed(entity),
            };
        }

        if (blueprint.Kind != BlueprintKind.Placeable)
        {
            return false;
        }

        return !entity.Tags.Contains("floor", StringComparer.OrdinalIgnoreCase) &&
            entity.Tags.Any(tag =>
                string.Equals(tag, "wall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "pillar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "architecture", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "solid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "collision", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDoorClosed(EntityInstance entity)
    {
        var locked = entity.Overrides.Properties.FirstOrDefault(item =>
            string.Equals(item.Key, "locked", StringComparison.OrdinalIgnoreCase));
        if (locked is not null && bool.TryParse(locked.Value, out var value))
        {
            return value;
        }

        return entity.Tags.Contains("locked", StringComparer.OrdinalIgnoreCase) ||
            !entity.Tags.Contains("open", StringComparer.OrdinalIgnoreCase);
    }

    private static bool Intersects(EntityInstance entity, float x, float z, float radius) =>
        MathF.Abs(x - entity.Transform.Position.X) < (entity.Transform.Scale.X / 2f) + radius &&
        MathF.Abs(z - entity.Transform.Position.Z) < (entity.Transform.Scale.Z / 2f) + radius;

    private static NavigationCell CellAt(
        Forge.Domain.Spatial.Vector3Value position,
        float minimumX,
        float minimumZ,
        float cellSize) => new(
            (int)MathF.Floor((position.X - minimumX) / cellSize),
            (int)MathF.Floor((position.Z - minimumZ) / cellSize));

    private static string Fingerprint(
        AreaDefinition area,
        NavigationAgentProfile agent,
        NavigationDoorState doorState)
    {
        var source = new StringBuilder()
            .Append(area.Id).Append('|')
            .Append(area.Dimensions.Width).Append('x').Append(area.Dimensions.Depth).Append('|')
            .Append(area.Navigation.SchemaVersion).Append('|').Append(Number(area.Navigation.CellSize)).Append('|')
            .Append(agent.Id).Append('|').Append(Number(agent.Radius)).Append('|').Append(Number(agent.Height)).Append('|')
            .Append(agent.CanOpenDoors).Append('|').Append(agent.CanUseOffMeshLinks).Append('|')
            .Append(agent.CanFly).Append('|').Append(doorState);
        foreach (var entity in area.Entities.OrderBy(entity => entity.Id.ToString(), StringComparer.Ordinal))
        {
            source.Append('|').Append(entity.Id).Append(':').Append(entity.BlueprintId).Append(':')
                .Append(Number(entity.Transform.Position.X)).Append(',').Append(Number(entity.Transform.Position.Y)).Append(',')
                .Append(Number(entity.Transform.Position.Z)).Append(':').Append(Number(entity.Transform.Scale.X)).Append(',')
                .Append(Number(entity.Transform.Scale.Y)).Append(',').Append(Number(entity.Transform.Scale.Z)).Append(':')
                .AppendJoin(',', entity.Tags);
            foreach (var property in entity.Overrides.Properties)
            {
                source.Append(':').Append(property.Key).Append('=').Append(property.Value);
            }
        }

        foreach (var link in area.Navigation.OffMeshLinks)
        {
            source.Append('|').Append(link.Id).Append(':').Append(link.Kind).Append(':')
                .Append(Number(link.Start.X)).Append(',').Append(Number(link.Start.Y)).Append(',').Append(Number(link.Start.Z)).Append('>')
                .Append(Number(link.End.X)).Append(',').Append(Number(link.End.Y)).Append(',').Append(Number(link.End.Z)).Append(':')
                .Append(link.Bidirectional).Append(':').Append(Number(link.MaximumAgentRadius));
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString()))).ToLowerInvariant();
    }

    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static Guid StableGuid(
        AreaId areaId,
        NavigationAgentProfileId agentId,
        NavigationDoorState doorState)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"openrpg-forge:navigation:{areaId}:{agentId}:{doorState}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }
}
