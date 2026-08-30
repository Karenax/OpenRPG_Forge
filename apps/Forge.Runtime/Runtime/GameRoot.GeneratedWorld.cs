using Forge.Domain.Areas;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private void LoadPromotedGeneratedWorld(AreaLoadingProfile loading)
    {
        if (_activeArea is null || string.IsNullOrWhiteSpace(_moduleContentRoot)) return;

        var resolution = WorldGenerationRuntimeSceneResolver.Resolve(
            _moduleContentRoot,
            _activeArea.Id.Value,
            loading.RequiredResources);
        if (!resolution.HasScene)
        {
            if (resolution.Diagnostics.Count == 0)
            {
                GD.Print($"OPENRPG_FORGE_RUNTIME_WORLD_GENERATION_SKIPPED area={_activeArea.Id} " +
                         $"required={string.Join(',', loading.RequiredResources)}");
                return;
            }
            throw new InvalidOperationException(
                "The promoted generated world could not be verified: " +
                string.Join("; ", resolution.Diagnostics.Select(item =>
                    $"{item.Code} ({item.Path}): {item.Message}")));
        }

        var moduleKey = string.IsNullOrWhiteSpace(_moduleId)
            ? _activeArea.Id.Value.ToString("N")
            : string.Concat(_moduleId.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        if (moduleKey.Length == 0) moduleKey = _activeArea.Id.Value.ToString("N");
        var cacheDirectory = $"user://openrpg-forge/generated-world/{moduleKey}";
        var cacheAbsolute = ProjectSettings.GlobalizePath(cacheDirectory);
        Directory.CreateDirectory(cacheAbsolute);
        var cacheFileName = $"{_activeArea.Id.Value:N}-{resolution.Sha256}.tscn";
        var cacheResourcePath = $"{cacheDirectory}/{cacheFileName}";
        var cachePath = ProjectSettings.GlobalizePath(cacheResourcePath);
        if (!File.Exists(cachePath) || !File.ReadAllBytes(cachePath).SequenceEqual(resolution.Content!))
            WriteGeneratedWorldCacheAtomically(cachePath, resolution.Content!);

        var packed = ResourceLoader.Load<PackedScene>(cacheResourcePath)
            ?? throw new InvalidDataException(
                $"Verified generated scene '{resolution.RelativePath}' could not be loaded by Godot.");
        var instance = packed.Instantiate<Node3D>()
            ?? throw new InvalidDataException(
                $"Verified generated scene '{resolution.RelativePath}' has no Node3D root.");
        instance.Name = "PromotedGeneratedWorld";
        instance.SetMeta("openrpg_canonical_path", resolution.RelativePath!);
        instance.SetMeta("openrpg_canonical_sha256", resolution.Sha256!);
        if (instance.GetNodeOrNull<MeshInstance3D>("Terrain/Mesh") is { Mesh: { } terrainMesh })
        {
            var terrainCenter = terrainMesh.GetAabb().GetCenter();
            instance.Position = new Vector3(-terrainCenter.X, 0, -terrainCenter.Z);
            instance.SetMeta("openrpg_center_offset", instance.Position);
        }
        var suppressedTechnicalVisuals = SuppressTechnicalGeneratedVisuals(instance, resolution);
        if (!suppressedTechnicalVisuals &&
            instance.GetNodeOrNull<StaticBody3D>("Terrain/Collision") is { } terrainCollision)
            terrainCollision.SetMeta("openrpg_terrain_collision", true);
        (_areaWorldRoot ?? this).AddChild(instance);

        var meshes = instance.FindChildren("*", "MeshInstance3D", true, false).Count;
        var multiMeshes = instance.FindChildren("*", "MultiMeshInstance3D", true, false).Count;
        var collisions = instance.FindChildren("*", "CollisionShape3D", true, false).Count;
        var navigation = instance.FindChildren("*", "NavigationRegion3D", true, false).Count;
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_WORLD_GENERATION_PASS area={_activeArea.Id} " +
            $"sha256={resolution.Sha256} meshes={meshes} multimeshes={multiMeshes} " +
            $"collisions={collisions} navigation={navigation} offset={instance.Position} " +
            $"technical_visuals_suppressed={suppressedTechnicalVisuals} " +
            $"production_visuals={resolution.ProductionVisuals?.Fingerprint ?? "none"} " +
            $"source=canonical-promotion");
    }

    private bool SuppressTechnicalGeneratedVisuals(
        Node3D instance,
        WorldGenerationRuntimeSceneResolution resolution)
    {
        var hasMaterializedProductionWorld = resolution.ProductionVisuals is not null &&
                                             _activeTerrain?.Enabled == true &&
                                             _activeArea?.World.Biomes.Any(biome =>
                                                 !string.IsNullOrWhiteSpace(biome.ScatterResource)) == true;
        if (!hasMaterializedProductionWorld) return false;

        var removed = 0;
        foreach (var path in new[] { "Terrain", "GeneratedContent" })
        {
            if (instance.GetNodeOrNull<Node>(path) is not { } technical) continue;
            technical.GetParent()?.RemoveChild(technical);
            technical.Free();
            removed++;
        }
        instance.SetMeta("openrpg_technical_visuals_suppressed", true);
        instance.SetMeta("openrpg_production_visual_source_area",
            resolution.ProductionVisuals!.SourceAreaId.ToString("D"));
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_PRODUCTION_VISUALS_PASS area={_activeArea!.Id} " +
            $"source_area={resolution.ProductionVisuals.SourceAreaId:D} " +
            $"textured_layers={resolution.ProductionVisuals.TexturedTerrainLayerCount} " +
            $"painted_cells={resolution.ProductionVisuals.PaintedTerrainCellCount} " +
            $"painted_layers={resolution.ProductionVisuals.PaintedTerrainLayerCount} " +
            $"terrain_spacing={resolution.ProductionVisuals.TerrainSampleSpacingMeters:F2} " +
            $"scatter_resources={resolution.ProductionVisuals.ScatterResourceCount} " +
            $"strata={string.Join(',', resolution.ProductionVisuals.VegetationStrata)} " +
            $"production_features={resolution.ProductionVisuals.ProductionFeatureCount} " +
            $"textured_roads={resolution.ProductionVisuals.TexturedRoadCount} " +
            $"camera_spawns={resolution.ProductionVisuals.SpatialQuality.CameraSpawnCount} " +
            $"camera_relief_max={resolution.ProductionVisuals.SpatialQuality.MaximumCameraReliefMeters:F2} " +
            $"grounded_foundations={resolution.ProductionVisuals.SpatialQuality.GroundedFoundationCount} " +
            $"foundation_offset_max={resolution.ProductionVisuals.SpatialQuality.MaximumFoundationOffsetMeters:F2} " +
            $"technical_nodes_removed={removed}");
        return true;
    }

    private static void WriteGeneratedWorldCacheAtomically(string path, byte[] content)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new IOException("Generated-world cache path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, System.IO.FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
