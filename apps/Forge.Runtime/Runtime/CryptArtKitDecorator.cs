using Godot;

namespace Forge.Runtime;

internal sealed record CryptArtKitResult(Node3D Root, int ModelInstances, int FloorTiles, int WallSegments, int Props);

internal static class CryptArtKitDecorator
{
    private const string ArtRoot = "res://art/crypt-kaykit";

    internal static CryptArtKitResult Build()
    {
        var root = new Node3D { Name = "CryptKayKitArtPass" };
        var instances = 0;
        var floors = 0;
        var walls = 0;
        var props = 0;

        for (var x = -8; x <= 8; x += 4)
        {
            for (var z = -12; z <= 12; z += 4)
            {
                Add(root, "floor_tile_large.gltf.glb", new Vector3(x, .015f, z));
                floors++;
            }
        }

        for (var z = -12; z <= 12; z += 4)
        {
            Add(root, "wall.gltf.glb", new Vector3(-9, 0, z), 90);
            Add(root, "wall.gltf.glb", new Vector3(9, 0, z), -90);
            walls += 2;
        }

        for (var x = -8; x <= 8; x += 4)
        {
            Add(root, "wall.gltf.glb", new Vector3(x, 0, -14), 0);
            walls++;
            if (x != 0)
            {
                Add(root, "wall.gltf.glb", new Vector3(x, 0, 14), 180);
                walls++;
            }
        }

        Add(root, "wall_doorway.glb", new Vector3(0, 0, 14), 180);
        walls++;
        foreach (var position in new[]
                 {
                     new Vector3(-7.5f, 0, 9), new Vector3(7.5f, 0, 9),
                     new Vector3(-7.5f, 0, 1), new Vector3(7.5f, 0, 1),
                     new Vector3(-7.5f, 0, -8), new Vector3(7.5f, 0, -8),
                 })
        {
            Add(root, "column.gltf.glb", position);
            props++;
        }

        Add(root, "stairs.gltf.glb", new Vector3(0, 0, 11), 180);
        Add(root, "chest.glb", new Vector3(-5.8f, 0, -10.5f), 25);
        Add(root, "barrel_large_decorated.gltf.glb", new Vector3(6.2f, 0, -10.5f), -20);
        props += 3;
        foreach (var position in new[]
                 {
                     new Vector3(-8.65f, 1.5f, 8), new Vector3(8.65f, 1.5f, 8),
                     new Vector3(-8.65f, 1.5f, 1), new Vector3(8.65f, 1.5f, 1),
                     new Vector3(-8.65f, 1.5f, -7), new Vector3(8.65f, 1.5f, -7),
                 })
        {
            Add(root, "torch_lit.gltf.glb", position, position.X < 0 ? 90 : -90);
            props++;
        }

        instances = floors + walls + props;
        return new CryptArtKitResult(root, instances, floors, walls, props);
    }

    private static void Add(Node3D root, string fileName, Vector3 position, float yawDegrees = 0)
    {
        var resourcePath = $"{ArtRoot}/{fileName}";
        var scene = ResourceLoader.Load<PackedScene>(resourcePath) ??
                    throw new InvalidOperationException($"Governed crypt art asset is unavailable: {resourcePath}");
        var instance = scene.Instantiate<Node3D>();
        instance.Name = $"Art_{Path.GetFileNameWithoutExtension(fileName)}";
        instance.Position = position;
        instance.RotationDegrees = new Vector3(0, yawDegrees, 0);
        root.AddChild(instance);
    }
}
