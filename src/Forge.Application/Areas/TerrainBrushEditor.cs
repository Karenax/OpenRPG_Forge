using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public static class TerrainBrushEditor
{
    public static TerrainBrushEditResult Apply(
        TerrainDefinition source,
        TerrainBrushStroke stroke,
        Guid strokeId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(stroke);
        if (!source.Enabled || source.Provider == TerrainProviderKind.Flat)
        {
            throw new ArgumentException("Terrain brush operations require an enabled non-flat terrain.", nameof(source));
        }

        if (!Enum.IsDefined(stroke.Operation) || !float.IsFinite(stroke.WorldX) || !float.IsFinite(stroke.WorldZ) ||
            !float.IsFinite(stroke.Radius) || stroke.Radius <= 0 || stroke.Radius > 4096 ||
            !float.IsFinite(stroke.Strength) || stroke.Strength is <= 0 or > 1 ||
            !float.IsFinite(stroke.TargetHeight) || strokeId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(stroke), "Terrain brush values are invalid.");
        }

        return stroke.Operation switch
        {
            TerrainBrushOperation.OpenHole or TerrainBrushOperation.CloseHole => EditHoles(source, stroke, strokeId),
            TerrainBrushOperation.PaintMaterial => EditMaterials(source, stroke),
            _ => EditHeights(source, stroke),
        };
    }

    private static TerrainBrushEditResult EditHeights(TerrainDefinition source, TerrainBrushStroke stroke)
    {
        if (source.HeightScale <= 0)
        {
            throw new ArgumentException("Terrain height scale must be positive before sculpting.", nameof(source));
        }

        var original = source.Heights.ToArray();
        var edited = original.ToArray();
        var changed = 0;
        for (var z = 0; z < source.SamplesZ; z++)
        {
            for (var x = 0; x < source.SamplesX; x++)
            {
                var falloff = Falloff(source, stroke, x, z, cellCenter: false);
                if (falloff <= 0) continue;
                var index = (z * source.SamplesX) + x;
                var value = stroke.Operation switch
                {
                    TerrainBrushOperation.Raise => original[index] + (stroke.Strength * falloff),
                    TerrainBrushOperation.Lower => original[index] - (stroke.Strength * falloff),
                    TerrainBrushOperation.Flatten => Lerp(original[index],
                        Math.Clamp(stroke.TargetHeight / source.HeightScale, -1, 1), stroke.Strength * falloff),
                    TerrainBrushOperation.Smooth => Lerp(original[index], NeighbourAverage(source, original, x, z),
                        stroke.Strength * falloff),
                    _ => original[index],
                };
                edited[index] = Math.Clamp(value, -1, 1);
                if (Math.Abs(edited[index] - original[index]) > 0.000001f) changed++;
            }
        }

        return new TerrainBrushEditResult(Copy(source, edited, source.Holes, "terrain-brush:sculpt"), changed, 0, 0);
    }

    private static TerrainBrushEditResult EditHoles(TerrainDefinition source, TerrainBrushStroke stroke, Guid strokeId)
    {
        var before = ExpandHoleCells(source);
        var after = before.ToHashSet();
        for (var z = 0; z < source.SamplesZ - 1; z++)
        {
            for (var x = 0; x < source.SamplesX - 1; x++)
            {
                if (Falloff(source, stroke, x, z, cellCenter: true) <= 0) continue;
                if (stroke.Operation == TerrainBrushOperation.OpenHole) after.Add((x, z));
                else after.Remove((x, z));
            }
        }

        var changed = before.Except(after).Count() + after.Except(before).Count();
        var holes = after.OrderBy(cell => cell.Z).ThenBy(cell => cell.X)
            .Select(cell => new TerrainHoleDefinition(
                StableGuid(strokeId, cell.X, cell.Z),
                $"Trou {cell.X}:{cell.Z}",
                cell.X,
                cell.Z,
                1,
                1,
                "terrain-brush:hole"))
            .ToArray();
        return new TerrainBrushEditResult(Copy(source, source.Heights, holes, "terrain-brush:hole"), 0, changed, 0);
    }

    private static TerrainBrushEditResult EditMaterials(TerrainDefinition source, TerrainBrushStroke stroke)
    {
        if (stroke.MaterialLayerId != Guid.Empty &&
            source.MaterialLayers.All(layer => layer.Id != stroke.MaterialLayerId))
        {
            throw new ArgumentException("Terrain paint material does not belong to this terrain.", nameof(stroke));
        }

        var paint = source.MaterialPaint.ToDictionary(cell => (cell.X, cell.Z));
        var changed = 0;
        for (var z = 0; z < source.SamplesZ - 1; z++)
        {
            for (var x = 0; x < source.SamplesX - 1; x++)
            {
                if (Falloff(source, stroke, x, z, cellCenter: true) <= 0) continue;
                paint.TryGetValue((x, z), out var existing);
                var previous = existing?.MaterialLayerId ?? Guid.Empty;
                if (previous == stroke.MaterialLayerId) continue;
                changed++;
                if (stroke.MaterialLayerId == Guid.Empty) paint.Remove((x, z));
                else paint[(x, z)] = new TerrainMaterialPaintCell(x, z, stroke.MaterialLayerId);
            }
        }

        var edited = paint.Values.OrderBy(cell => cell.Z).ThenBy(cell => cell.X).ToArray();
        return new TerrainBrushEditResult(
            Copy(source, source.Heights, source.Holes, "terrain-brush:material", edited), 0, 0, changed);
    }

    private static HashSet<(int X, int Z)> ExpandHoleCells(TerrainDefinition source)
    {
        var cells = new HashSet<(int X, int Z)>();
        foreach (var hole in source.Holes)
        {
            for (var z = hole.StartZ; z < hole.StartZ + hole.Depth; z++)
            {
                for (var x = hole.StartX; x < hole.StartX + hole.Width; x++) cells.Add((x, z));
            }
        }

        return cells;
    }

    private static float Falloff(TerrainDefinition source, TerrainBrushStroke stroke, int x, int z, bool cellCenter)
    {
        var offset = cellCenter ? .5f : 0;
        var divisorX = source.SamplesX - 1f;
        var divisorZ = source.SamplesZ - 1f;
        var worldX = (-source.Width / 2f) + (((x + offset) / divisorX) * source.Width);
        var worldZ = (-source.Depth / 2f) + (((z + offset) / divisorZ) * source.Depth);
        var distance = MathF.Sqrt(MathF.Pow(worldX - stroke.WorldX, 2) + MathF.Pow(worldZ - stroke.WorldZ, 2));
        return Math.Clamp(1 - (distance / stroke.Radius), 0, 1);
    }

    private static float NeighbourAverage(TerrainDefinition source, IReadOnlyList<float> heights, int x, int z)
    {
        var total = 0f;
        var count = 0;
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var sampleX = x + dx;
                var sampleZ = z + dz;
                if (sampleX < 0 || sampleX >= source.SamplesX || sampleZ < 0 || sampleZ >= source.SamplesZ) continue;
                total += heights[(sampleZ * source.SamplesX) + sampleX];
                count++;
            }
        }

        return total / count;
    }

    private static TerrainDefinition Copy(TerrainDefinition source, IReadOnlyList<float> heights,
        IReadOnlyList<TerrainHoleDefinition> holes, string provenance,
        IReadOnlyList<TerrainMaterialPaintCell>? materialPaint = null) =>
        new(source.Id, source.SchemaVersion, source.Provider, source.Width, source.Depth, source.SamplesX,
            source.SamplesZ, source.ChunkSize, source.Seed, source.HeightScale, heights, source.MaterialLayers, holes,
            source.HeightmapResource, source.VoxelResource, source.MeshResource, provenance,
            materialPaint ?? source.MaterialPaint);

    private static Guid StableGuid(Guid strokeId, int x, int z)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:terrain-brush:{strokeId:D}:{x}:{z}"));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
        return new Guid(guidBytes);
    }

    private static float Lerp(float start, float end, float amount) => start + ((end - start) * amount);
}
