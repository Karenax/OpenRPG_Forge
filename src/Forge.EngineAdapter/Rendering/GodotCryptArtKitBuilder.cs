using Godot;

namespace Forge.EngineAdapter.Rendering;

public sealed record GodotCryptArtKitResult(
    Node3D Root,
    int ModelInstances,
    int FloorTiles,
    int WallSegments,
    int Props,
    int Zones);

/// <summary>
/// Builds the final Crypt of Ashes art pass from a caller-owned model resolver.
/// Runtime can resolve imported res:// scenes while Studio can import the same external GLBs.
/// </summary>
public static class GodotCryptArtKitBuilder
{
    public const int ZoneCount = 7;

    public static GodotCryptArtKitResult Build(Func<string, Node3D> instantiateModel)
    {
        ArgumentNullException.ThrowIfNull(instantiateModel);
        var root = new Node3D { Name = "CryptKayKitArtPass" };
        var floors = 0;
        var walls = 0;
        var props = 0;

        try
        {
            FillFloor(root, -8, 8, 29, 37, instantiateModel, ref floors);
            FillFloor(root, -12, 12, 21, 29, instantiateModel, ref floors);
            FillFloor(root, -20, 20, 7, 19, instantiateModel, ref floors);
            FillFloor(root, -12, 12, -3, 7, instantiateModel, ref floors);
            FillFloor(root, -16, 16, -15, -3, instantiateModel, ref floors);
            FillFloor(root, -20, 20, -26, -15, instantiateModel, ref floors);
            FillFloor(root, -16, 16, -37, -26, instantiateModel, ref floors);

            WallLineZ(root, -8.5f, 29, 37, 90, instantiateModel, ref walls);
            WallLineZ(root, 8.5f, 29, 37, -90, instantiateModel, ref walls);
            WallLineZ(root, -13.5f, 21, 29, 90, instantiateModel, ref walls);
            WallLineZ(root, 13.5f, 21, 29, -90, instantiateModel, ref walls);
            WallLineZ(root, -20.5f, 7, 19, 90, instantiateModel, ref walls);
            WallLineZ(root, 20.5f, 7, 19, -90, instantiateModel, ref walls);
            WallLineZ(root, -11.5f, -3, 7, 90, instantiateModel, ref walls);
            WallLineZ(root, 11.5f, -3, 7, -90, instantiateModel, ref walls);
            WallLineZ(root, -16.5f, -15, -3, 90, instantiateModel, ref walls);
            WallLineZ(root, 16.5f, -15, -3, -90, instantiateModel, ref walls);
            WallLineZ(root, -20.5f, -26, -15, 90, instantiateModel, ref walls);
            WallLineZ(root, 20.5f, -26, -15, -90, instantiateModel, ref walls);
            WallLineZ(root, -16.5f, -37, -26, 90, instantiateModel, ref walls);
            WallLineZ(root, 16.5f, -37, -26, -90, instantiateModel, ref walls);

            WallLineX(root, 38, -8, 8, 0, 0, false, instantiateModel, ref walls);
            WallLineX(root, -38, -16, 16, 180, 0, false, instantiateModel, ref walls);
            WallLineX(root, 20, -20, 20, 0, 4, true, instantiateModel, ref walls);
            WallLineX(root, 6, -20, 20, 0, 2, true, instantiateModel, ref walls);
            WallLineX(root, -15, -20, 20, 0, 4, true, instantiateModel, ref walls);
            WallLineX(root, -26, -20, 20, 0, 4, true, instantiateModel, ref walls);

            WallLineZ(root, -8, 11, 19, 90, instantiateModel, ref walls, doorwayZ: 15);
            WallLineZ(root, 8, 11, 19, -90, instantiateModel, ref walls, doorwayZ: 15);
            WallLineX(root, 11, -20, -10, 0, 0, false, instantiateModel, ref walls);
            WallLineX(root, 11, 10, 20, 0, 0, false, instantiateModel, ref walls);

            AddSafetyWall(root, new Vector3(-8.5f, 1.5f, 33), new Vector3(1, 4, 10));
            AddSafetyWall(root, new Vector3(8.5f, 1.5f, 33), new Vector3(1, 4, 10));
            AddSafetyWall(root, new Vector3(-13.5f, 1.5f, 25), new Vector3(1, 4, 10));
            AddSafetyWall(root, new Vector3(13.5f, 1.5f, 25), new Vector3(1, 4, 10));
            AddSafetyWall(root, new Vector3(-16.5f, 1.5f, -9), new Vector3(1, 4, 12));
            AddSafetyWall(root, new Vector3(16.5f, 1.5f, -9), new Vector3(1, 4, 12));
            AddSafetyWall(root, new Vector3(-20.5f, 2, -20), new Vector3(1, 5, 12));
            AddSafetyWall(root, new Vector3(20.5f, 2, -20), new Vector3(1, 5, 12));
            AddSafetyWall(root, new Vector3(-16.5f, 2, -31), new Vector3(1, 5, 12));
            AddSafetyWall(root, new Vector3(16.5f, 2, -31), new Vector3(1, 5, 12));

            foreach (var position in new[]
                     {
                         new Vector3(-6, 0, 25), new Vector3(6, 0, 25),
                         new Vector3(-12, 0, 15), new Vector3(12, 0, 15),
                         new Vector3(-7, 0, -8), new Vector3(7, 0, -8),
                         new Vector3(-14, 0, -20), new Vector3(-7, 0, -20),
                         new Vector3(7, 0, -20), new Vector3(14, 0, -20),
                         new Vector3(-7, 0, -29), new Vector3(7, 0, -29),
                     })
            {
                Add(root, "column.gltf.glb", position, instantiateModel);
                props++;
            }

            Add(root, "stairs.gltf.glb", new Vector3(0, 0, -27), instantiateModel);
            Add(root, "chest.glb", new Vector3(14, 0, 15), instantiateModel, -35);
            Add(root, "chest.glb", new Vector3(-14, 0, 15), instantiateModel, 35);
            Add(root, "barrel_large_decorated.gltf.glb", new Vector3(7, 0, -10), instantiateModel, -20);
            Add(root, "barrel_large_decorated.gltf.glb", new Vector3(-13, 0, -20), instantiateModel, 25);
            Add(root, "barrel_large_decorated.gltf.glb", new Vector3(13, 0, -20), instantiateModel, -25);
            Add(root, "torch_lit.gltf.glb", new Vector3(0, 5, -29), instantiateModel);
            props += 7;

            var torches = new[]
            {
                new Vector3(-7.5f, 1.5f, 33), new Vector3(7.5f, 1.5f, 33),
                new Vector3(-11.5f, 1.5f, 24), new Vector3(11.5f, 1.5f, 24),
                new Vector3(-18.5f, 1.5f, 15), new Vector3(18.5f, 1.5f, 15),
                new Vector3(-10.5f, 1.5f, 1), new Vector3(10.5f, 1.5f, 1),
                new Vector3(-15.5f, 1.5f, -9), new Vector3(15.5f, 1.5f, -9),
                new Vector3(-18.5f, 1.5f, -20), new Vector3(18.5f, 1.5f, -20),
                new Vector3(-14.5f, 1.5f, -31), new Vector3(14.5f, 1.5f, -31),
            };
            for (var index = 0; index < torches.Length; index++)
            {
                var position = torches[index];
                Add(root, "torch_lit.gltf.glb", position, instantiateModel, position.X < 0 ? 90 : -90);
                props++;

                if ((index & 1) != 0) continue;
                root.AddChild(new OmniLight3D
                {
                    Name = $"CryptTorchLight_{index:00}",
                    Position = position + new Vector3(0, .45f, 0),
                    LightColor = new Color("ff8a50"),
                    LightEnergy = 1.55f,
                    OmniRange = 7.5f,
                    ShadowEnabled = index is 0 or 6 or 12,
                });
            }

            root.AddChild(new OmniLight3D
            {
                Name = "FloodedOssuaryColdLight",
                Position = new Vector3(0, 3.2f, -9),
                LightColor = new Color("8fc9e8"),
                LightEnergy = 1.25f,
                OmniRange = 12,
                ShadowEnabled = false,
            });

            var instances = floors + walls + props;
            return new GodotCryptArtKitResult(root, instances, floors, walls, props, ZoneCount);
        }
        catch
        {
            root.Free();
            throw;
        }
    }

    private static void FillFloor(
        Node3D root,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        Func<string, Node3D> instantiateModel,
        ref int counter)
    {
        for (var x = minX; x <= maxX + .01f; x += 4)
            for (var z = minZ; z <= maxZ + .01f; z += 4)
            {
                Add(root, "floor_tile_large.gltf.glb", new Vector3(x, .015f, z), instantiateModel);
                counter++;
            }
    }

    private static void WallLineZ(
        Node3D root,
        float x,
        float minZ,
        float maxZ,
        float yaw,
        Func<string, Node3D> instantiateModel,
        ref int counter,
        float? doorwayZ = null)
    {
        for (var z = minZ; z <= maxZ + .01f; z += 4)
        {
            if (doorwayZ is { } opening && MathF.Abs(z - opening) <= .01f) continue;
            Add(root, "wall.gltf.glb", new Vector3(x, 0, z), instantiateModel, yaw);
            counter++;
        }
        if (doorwayZ is not { } doorway) return;
        Add(root, "wall_doorway.glb", new Vector3(x, 0, doorway), instantiateModel, yaw);
        counter++;
    }

    private static void WallLineX(
        Node3D root,
        float z,
        float minX,
        float maxX,
        float yaw,
        float halfGap,
        bool doorway,
        Func<string, Node3D> instantiateModel,
        ref int counter)
    {
        for (var x = minX; x <= maxX + .01f; x += 4)
        {
            if (halfGap > 0 && MathF.Abs(x) <= halfGap) continue;
            Add(root, "wall.gltf.glb", new Vector3(x, 0, z), instantiateModel, yaw);
            counter++;
        }
        if (!doorway) return;
        Add(root, "wall_doorway.glb", new Vector3(0, 0, z), instantiateModel, yaw);
        counter++;
    }

    private static void AddSafetyWall(Node3D root, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D { Name = "CryptSafetyWall", Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(body);
    }

    private static void Add(
        Node3D root,
        string fileName,
        Vector3 position,
        Func<string, Node3D> instantiateModel,
        float yawDegrees = 0)
    {
        var instance = instantiateModel(fileName) ??
                       throw new InvalidOperationException($"Crypt art resolver returned no model for '{fileName}'.");
        instance.Name = $"Art_{Path.GetFileNameWithoutExtension(fileName)}";
        instance.Position = position;
        instance.RotationDegrees = new Vector3(0, yawDegrees, 0);
        root.AddChild(instance);
    }
}
