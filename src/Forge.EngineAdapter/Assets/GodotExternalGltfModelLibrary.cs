using Godot;

namespace Forge.EngineAdapter.Assets;

/// <summary>
/// Loads a small, trusted GLB/GLTF library located outside the active Godot project.
/// Paths remain contained under the configured root and packed scenes are cached per file.
/// </summary>
public sealed class GodotExternalGltfModelLibrary
{
    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly Dictionary<string, PackedScene> _scenes = new(StringComparer.OrdinalIgnoreCase);

    public GodotExternalGltfModelLibrary(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(_root))
            throw new DirectoryNotFoundException($"External GLTF library does not exist: {_root}");
        _rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                      Path.DirectorySeparatorChar;
    }

    public Node3D Instantiate(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(normalized) || normalized.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException($"External GLTF path '{relativePath}' is unsafe.");
        var extension = Path.GetExtension(normalized);
        if (!extension.Equals(".glb", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"External model '{relativePath}' must be GLB or GLTF.");

        var absolute = Path.GetFullPath(Path.Combine(_root, normalized));
        if (!absolute.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"External GLTF path '{relativePath}' escapes its library root.");
        if (!File.Exists(absolute))
            throw new FileNotFoundException($"External GLTF model is missing: {relativePath}", absolute);

        if (!_scenes.TryGetValue(normalized, out var scene))
        {
            scene = GodotAssetModelLibrary.LoadRuntimeGltf(absolute, absolute);
            _scenes[normalized] = scene;
        }
        return scene.Instantiate<Node3D>();
    }
}
