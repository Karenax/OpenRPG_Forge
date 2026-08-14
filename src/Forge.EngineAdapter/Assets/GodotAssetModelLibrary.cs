using System.Security.Cryptography;
using Forge.Domain.Assets;
using Godot;

namespace Forge.EngineAdapter.Assets;

public sealed class GodotAssetModelLibrary
{
    private static readonly object ImporterMeshConversionLock = new();
    private static GltfDocumentExtensionConvertImporterMesh? _importerMeshConversion;
    private readonly string _contentRoot;
    private readonly string _contentRootPrefix;
    private readonly IReadOnlyDictionary<AssetCatalogItemId, AssetPackageManifest> _manifests;
    private readonly Dictionary<AssetCatalogItemId, PackedScene> _scenes = [];
    private readonly HashSet<AssetCatalogItemId> _unavailableScenes = [];
    private readonly bool _allowRuntimeImport;

    public GodotAssetModelLibrary(
        string contentRoot,
        IReadOnlyList<AssetPackageManifest> manifests,
        bool allowRuntimeImport = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentNullException.ThrowIfNull(manifests);
        _contentRoot = Path.GetFullPath(contentRoot);
        _contentRootPrefix = _contentRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        _manifests = manifests.ToDictionary(manifest => manifest.AssetId);
        _allowRuntimeImport = allowRuntimeImport;
    }

    public int Count => _manifests.Count;

    public int LodAssetCount => _manifests.Values.Count(manifest => manifest.Lod?.Levels.Count > 0);

    public int LodLevelCount => _manifests.Values.Sum(manifest => manifest.Lod?.Levels.Count ?? 0);

    public long GovernedStoredBytes => _manifests.Values
        .SelectMany(manifest => manifest.Chunks)
        .Sum(chunk => chunk.StoredBytes);

    public Node3D Instantiate(string assetId)
    {
        var id = AssetCatalogItemId.Parse(assetId);
        var scene = LoadScene(id);
        var instance = scene.Instantiate<Node3D>();
        PruneImportHelperMeshes(instance);
        return instance;
    }

    public Node3D? TryInstantiate(string assetId)
    {
        AssetCatalogItemId id;
        try
        {
            id = AssetCatalogItemId.Parse(assetId);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (_unavailableScenes.Contains(id)) return null;
        try
        {
            var scene = LoadScene(id);
            var instance = scene.Instantiate<Node3D>();
            PruneImportHelperMeshes(instance);
            return instance;
        }
        catch (Exception exception) when (
            exception is IOException or ArgumentException or InvalidOperationException)
        {
            _unavailableScenes.Add(id);
            return null;
        }
    }

    public Mesh? ResolveScatterMesh(string resource)
    {
        const string prefix = "asset:";
        if (!resource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var instance = TryInstantiate(resource[prefix.Length..]);
        if (instance is null) return null;
        try
        {
            return FindPrimaryMesh(instance)?.Mesh;
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
        PackedScene? scene;
        if (_allowRuntimeImport)
        {
            VerifyContentHash(id, chunk, absolute);
            scene = TryLoadImportedScene(absolute) ?? LoadRuntimeGltf(absolute);
        }
        else
        {
            scene = TryLoadImportedScene(absolute);
            if (scene is null)
            {
                throw new InvalidOperationException(
                    $"Visual asset '{id}' is outside the Godot project and uses the safe editor fallback.");
            }

            VerifyContentHash(id, chunk, absolute);
        }
        _scenes[id] = scene;
        return scene;
    }

    private static void VerifyContentHash(AssetCatalogItemId id, AssetManifestChunk chunk, string absolute)
    {
        using var stream = File.OpenRead(absolute);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(actual, chunk.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Visual asset '{id}' model hash does not match its governed package manifest.");
        }
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
        EnsureImporterMeshConversionRegistered();
        var document = new GltfDocument();
        var state = new GltfState();
        // Godot resolves external GLTF textures by appending their URI to the model directory.
        // A Windows path with backslashes otherwise becomes a mixed `E:\dir/subdir` path that
        // the editor importer reports as missing even though the texture exists.
        var godotPath = absolute.Replace('\\', '/');
        var error = document.AppendFromFile(godotPath, state);
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

    private static void EnsureImporterMeshConversionRegistered()
    {
        lock (ImporterMeshConversionLock)
        {
            if (_importerMeshConversion is not null) return;
            var extension = new GltfDocumentExtensionConvertImporterMesh();
            GltfDocument.RegisterGltfDocumentExtension(extension, firstPriority: true);
            _importerMeshConversion = extension;
        }
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

    private static MeshInstance3D? FindPrimaryMesh(Node root)
    {
        return EnumerateMeshes(root)
            .OrderByDescending(VertexCount)
            .ThenByDescending(candidate => candidate.Mesh?.GetSurfaceCount() ?? 0)
            .FirstOrDefault();
    }

    private static void PruneImportHelperMeshes(Node root)
    {
        var meshes = EnumerateMeshes(root).ToArray();
        if (meshes.Length < 2) return;
        var primary = meshes
            .OrderByDescending(VertexCount)
            .ThenByDescending(candidate => candidate.Mesh?.GetSurfaceCount() ?? 0)
            .First();
        var primaryVertices = VertexCount(primary);
        foreach (var candidate in meshes)
        {
            if (candidate == primary ||
                !candidate.Name.ToString().Equals("Cube", StringComparison.OrdinalIgnoreCase) ||
                VertexCount(candidate) > 36 ||
                primaryVertices < VertexCount(candidate) * 4)
                continue;
            candidate.GetParent()?.RemoveChild(candidate);
            candidate.Free();
        }
    }

    private static IEnumerable<MeshInstance3D> EnumerateMeshes(Node root)
    {
        if (root is MeshInstance3D { Mesh: not null } mesh) yield return mesh;
        foreach (Node child in root.GetChildren())
            foreach (var descendant in EnumerateMeshes(child))
                yield return descendant;
    }

    private static int VertexCount(MeshInstance3D candidate)
    {
        if (candidate.Mesh is null) return 0;
        var count = 0;
        for (var surface = 0; surface < candidate.Mesh.GetSurfaceCount(); surface++)
            count += candidate.Mesh is ArrayMesh arrayMesh
                ? arrayMesh.SurfaceGetArrayLen(surface)
                : 1;
        return count;
    }
}
