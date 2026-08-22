using System;
using System.IO;
using Godot;

namespace Forge.Runtime;

internal sealed record CryptArtKitResult(
    Node3D Root,
    int ModelInstances,
    int FloorTiles,
    int WallSegments,
    int Props,
    int Zones);

internal static class CryptArtKitDecorator
{
    private const string ArtRoot = "res://art/crypt-kaykit";

    internal static CryptArtKitResult Build()
    {
        var root = new Node3D { Name = "CryptKayKitArtPass" };
        var floors = 0;
        var walls = 0;
        var props = 0;

        // Seven spatial beats instead of the historical single rectangular room.
        FillFloor(root, -8, 8, 29, 37, ref floors);       // Seuil des Braises
        FillFloor(root, -12, 12, 21, 29, ref floors);     // Sanctuaire d'Elyra
        FillFloor(root, -20, 20, 7, 19, ref floors);      // Catacombes
        FillFloor(root, -12, 12, -3, 7, ref floors);      // Salle de la Sentinelle
        FillFloor(root, -16, 16, -15, -3, ref floors);    // Ossuaire inondé
        FillFloor(root, -20, 20, -26, -15, ref floors);   // Grande Nef
        FillFloor(root, -16, 16, -37, -26, ref floors);   // Sanctuaire du Gardien

        // Visible side walls follow the room silhouettes, so the player reads actual spaces
        // instead of one decorated box.
        WallLineZ(root, -8.5f, 29, 37, 90, ref walls);
        WallLineZ(root, 8.5f, 29, 37, -90, ref walls);
        WallLineZ(root, -13.5f, 21, 29, 90, ref walls);
        WallLineZ(root, 13.5f, 21, 29, -90, ref walls);
        WallLineZ(root, -20.5f, 7, 19, 90, ref walls);
        WallLineZ(root, 20.5f, 7, 19, -90, ref walls);
        WallLineZ(root, -11.5f, -3, 7, 90, ref walls);
        WallLineZ(root, 11.5f, -3, 7, -90, ref walls);
        WallLineZ(root, -16.5f, -15, -3, 90, ref walls);
        WallLineZ(root, 16.5f, -15, -3, -90, ref walls);
        WallLineZ(root, -20.5f, -26, -15, 90, ref walls);
        WallLineZ(root, 20.5f, -26, -15, -90, ref walls);
        WallLineZ(root, -16.5f, -37, -26, 90, ref walls);
        WallLineZ(root, 16.5f, -37, -26, -90, ref walls);

        WallLineX(root, 38, -8, 8, 0, 0, false, ref walls);
        WallLineX(root, -38, -16, 16, 180, 0, false, ref walls);

        // Architectural thresholds keep a readable central passage.
        WallLineX(root, 20, -20, 20, 0, 4, true, ref walls);
        WallLineX(root, 6, -20, 20, 0, 2, true, ref walls);
        WallLineX(root, -15, -20, 20, 0, 4, true, ref walls);
        WallLineX(root, -26, -20, 20, 0, 4, true, ref walls);

        // Catacomb ribs create lateral exploration without obscuring the main axis.
        WallLineZ(root, -8, 11, 19, 90, ref walls);
        WallLineZ(root, 8, 11, 19, -90, ref walls);
        WallLineX(root, 11, -20, -10, 0, 0, false, ref walls);
        WallLineX(root, 11, 10, 20, 0, 0, false, ref walls);

        // The canonical area snapshot already carries the main gameplay walls. These safety
        // colliders close the narrower artistic silhouettes so there are no accidental void falls
        // while the generic collision authoring is being migrated to the richer dungeon layout.
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

        // Column rhythm: landmarks, cover and systemic silhouettes.
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
            Add(root, "column.gltf.glb", position);
            props++;
        }

        // Entrance elevation, reliquaries and environmental props.
        Add(root, "stairs.gltf.glb", new Vector3(0, 0, 35), 180);
        Add(root, "stairs.gltf.glb", new Vector3(0, 0, -27));
        Add(root, "chest.glb", new Vector3(14, 0, 15), -35);
        Add(root, "chest.glb", new Vector3(-14, 0, 15), 35);
        Add(root, "barrel_large_decorated.gltf.glb", new Vector3(7, 0, -10), -20);
        Add(root, "barrel_large_decorated.gltf.glb", new Vector3(-13, 0, -20), 25);
        Add(root, "barrel_large_decorated.gltf.glb", new Vector3(13, 0, -20), -25);
        // A CC0 lit prop visually anchors the hanging-brazier interaction.
        Add(root, "torch_lit.gltf.glb", new Vector3(0, 5, -29));
        props += 8;

        // Warm pools of light guide the critical path while leaving side lanes darker.
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
            Add(root, "torch_lit.gltf.glb", position, position.X < 0 ? 90 : -90);
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

        // Cold counter-light makes the water/givre route readable at a glance.
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
        return new CryptArtKitResult(root, instances, floors, walls, props, 7);
    }

    private static void FillFloor(Node3D root, float minX, float maxX, float minZ, float maxZ, ref int counter)
    {
        for (var x = minX; x <= maxX + .01f; x += 4)
        for (var z = minZ; z <= maxZ + .01f; z += 4)
        {
            Add(root, "floor_tile_large.gltf.glb", new Vector3(x, .015f, z));
            counter++;
        }
    }

    private static void WallLineZ(Node3D root, float x, float minZ, float maxZ, float yaw, ref int counter)
    {
        for (var z = minZ; z <= maxZ + .01f; z += 4)
        {
            Add(root, "wall.gltf.glb", new Vector3(x, 0, z), yaw);
            counter++;
        }
    }

    private static void WallLineX(
        Node3D root, float z, float minX, float maxX, float yaw, float halfGap, bool doorway, ref int counter)
    {
        for (var x = minX; x <= maxX + .01f; x += 4)
        {
            if (halfGap > 0 && MathF.Abs(x) <= halfGap) continue;
            Add(root, "wall.gltf.glb", new Vector3(x, 0, z), yaw);
            counter++;
        }
        if (!doorway) return;
        Add(root, "wall_doorway.glb", new Vector3(0, 0, z), yaw);
        counter++;
    }

    private static void AddSafetyWall(Node3D root, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D { Name = "CryptSafetyWall", Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(body);
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
