using Forge.EngineAdapter.Rendering;
using Godot;

namespace Forge.Runtime;

internal static class CryptArtKitDecorator
{
    private const string ArtRoot = "res://art/crypt-kaykit";

    internal static GodotCryptArtKitResult Build()
    {
        return GodotCryptArtKitBuilder.Build(InstantiateModel);
    }

    private static Node3D InstantiateModel(string fileName)
    {
        var resourcePath = $"{ArtRoot}/{fileName}";
        var scene = ResourceLoader.Load<PackedScene>(resourcePath) ??
                    throw new InvalidOperationException($"Governed crypt art asset is unavailable: {resourcePath}");
        return scene.Instantiate<Node3D>();
    }
}
