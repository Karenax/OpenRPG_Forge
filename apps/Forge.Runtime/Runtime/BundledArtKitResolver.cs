using Forge.EngineAdapter.Rendering;
using Godot;

namespace Forge.Runtime;

internal static class BundledArtKitResolver
{
    internal static GodotArtKitResult Build(string artKitId, string layoutJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artKitId);
        var layout = GodotArtKitLayoutJson.Deserialize(layoutJson);
        if (!string.Equals(layout.Id, artKitId, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Art-kit layout '{layout.Id}' does not match requested kit '{artKitId}'.");
        return GodotArtKitBuilder.Build(layout, fileName => InstantiateBundledModel(artKitId, fileName));
    }

    private static Node3D InstantiateBundledModel(string artKitId, string fileName)
    {
        var resourcePath = $"res://art/{artKitId}/{fileName}";
        var scene = ResourceLoader.Load<PackedScene>(resourcePath) ??
                    throw new InvalidOperationException($"Governed art-kit asset is unavailable: {resourcePath}");
        return scene.Instantiate<Node3D>();
    }
}
