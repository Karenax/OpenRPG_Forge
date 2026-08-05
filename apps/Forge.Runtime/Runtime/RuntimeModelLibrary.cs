using Forge.Domain.Assets;
using Godot;

namespace Forge.Runtime;

internal sealed class RuntimeModelLibrary
{
    private readonly string _contentRoot;
    private readonly string _contentRootPrefix;
    private readonly IReadOnlyDictionary<AssetCatalogItemId, AssetPackageManifest> _manifests;
    private readonly Dictionary<AssetCatalogItemId, PackedScene> _scenes = [];

    internal RuntimeModelLibrary(string contentRoot, IReadOnlyList<AssetPackageManifest> manifests)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentNullException.ThrowIfNull(manifests);
        _contentRoot = Path.GetFullPath(contentRoot);
        _contentRootPrefix = _contentRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        _manifests = manifests.ToDictionary(manifest => manifest.AssetId);
    }

    internal int Count => _manifests.Count;

    internal Node3D Instantiate(string assetId)
    {
        var id = AssetCatalogItemId.Parse(assetId);
        var scene = LoadScene(id);
        return scene.Instantiate<Node3D>();
    }

    internal Mesh? ResolveScatterMesh(string resource)
    {
        const string prefix = "asset:";
        if (!resource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var instance = Instantiate(resource[prefix.Length..]);
        try
        {
            return FindFirstMesh(instance)?.Mesh;
        }
        finally
        {
            instance.Free();
        }
    }

    private PackedScene LoadScene(AssetCatalogItemId id)
    {
        if (_scenes.TryGetValue(id, out var cached)) return cached;
        if (!_manifests.TryGetValue(id, out var manifest))
            throw new InvalidOperationException($"Visual asset '{id}' has no package manifest in this module.");
        var chunk = manifest.Chunks
            .Where(candidate => candidate.Role == AssetChunkRole.Model)
            .OrderByDescending(candidate => candidate.Required)
            .ThenBy(candidate => candidate.RelativePath, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Visual asset '{id}' has no model chunk.");
        var absolute = ResolveContained(chunk.RelativePath);
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"Visual asset '{id}' is missing its model chunk.", absolute);

        var scene = TryLoadImportedScene(absolute) ?? LoadRuntimeGltf(absolute);
        _scenes[id] = scene;
        return scene;
    }

    private PackedScene? TryLoadImportedScene(string absolute)
    {
        var resourceRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        var relative = Path.GetRelativePath(resourceRoot, absolute);
        if (relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative)) return null;
        var uri = $"res://{relative.Replace('\\', '/')}";
        return ResourceLoader.Exists(uri) ? ResourceLoader.Load<PackedScene>(uri) : null;
    }

    private static PackedScene LoadRuntimeGltf(string absolute)
    {
        var document = new GltfDocument();
        var state = new GltfState();
        var error = document.AppendFromFile(absolute, state);
        if (error != Error.Ok)
            throw new InvalidDataException($"GLB runtime import failed for '{absolute}' with error {error}.");
        var generated = document.GenerateScene(state)
            ?? throw new InvalidDataException($"GLB runtime import produced no scene for '{absolute}'.");
        var packed = new PackedScene();
        error = packed.Pack(generated);
        generated.Free();
        if (error != Error.Ok)
            throw new InvalidDataException($"GLB runtime scene packing failed for '{absolute}' with error {error}.");
        return packed;
    }

    private string ResolveContained(string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath) || relativePath.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException($"Asset chunk path '{relativePath}' is unsafe.");
        var absolute = Path.GetFullPath(Path.Combine(
            _contentRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolute.StartsWith(_contentRootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Asset chunk path '{relativePath}' escapes the module content root.");
        return absolute;
    }

    private static MeshInstance3D? FindFirstMesh(Node root)
    {
        if (root is MeshInstance3D mesh && mesh.Mesh is not null) return mesh;
        foreach (Node child in root.GetChildren())
        {
            var found = FindFirstMesh(child);
            if (found is not null) return found;
        }
        return null;
    }
}
