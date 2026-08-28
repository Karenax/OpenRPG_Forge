using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Forge.EngineAdapter.Rendering;

public enum ArtKitWallAxis
{
    X,
    Z,
}

public sealed record ArtKitVector3(float X, float Y, float Z)
{
    internal Vector3 ToGodot() => new(X, Y, Z);
}

public sealed record ArtKitFloorFill(
    string Model,
    float MinimumX,
    float MaximumX,
    float MinimumZ,
    float MaximumZ,
    float Step = 4,
    float Y = .015f);

public sealed record ArtKitWallLine(
    string Model,
    ArtKitWallAxis Axis,
    float Fixed,
    float Minimum,
    float Maximum,
    float YawDegrees,
    float Step = 4,
    float? DoorwayAt = null,
    float HalfGap = 0,
    string DoorwayModel = "wall_doorway.glb");

public sealed record ArtKitModelInstance(string Model, ArtKitVector3 Position, float YawDegrees = 0);

public sealed record ArtKitCollisionBox(string Name, ArtKitVector3 Position, ArtKitVector3 Size);

public sealed record ArtKitLight(
    string Name,
    ArtKitVector3 Position,
    string Color,
    float Energy,
    float Range,
    bool Shadows);

public sealed record ArtKitDisc(
    string Name,
    ArtKitVector3 Position,
    float Radius,
    float Height,
    string Color,
    bool Emission);

public sealed record GodotArtKitLayout(
    int SchemaVersion,
    string Id,
    string RootName,
    int Zones,
    IReadOnlyList<ArtKitFloorFill> FloorFills,
    IReadOnlyList<ArtKitWallLine> WallLines,
    IReadOnlyList<ArtKitModelInstance> Instances,
    IReadOnlyList<ArtKitCollisionBox> CollisionBoxes,
    IReadOnlyList<ArtKitLight> Lights,
    IReadOnlyList<ArtKitDisc> Discs);

public sealed record GodotArtKitResult(
    Node3D Root,
    int ModelInstances,
    int FloorTiles,
    int WallSegments,
    int Props,
    int Zones);

public static class GodotArtKitLayoutJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static GodotArtKitLayout Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var layout = JsonSerializer.Deserialize<GodotArtKitLayout>(json, Options) ??
                     throw new JsonException("The art-kit layout is empty.");
        Validate(layout);
        return layout;
    }

    private static void Validate(GodotArtKitLayout layout)
    {
        if (layout.SchemaVersion != 1 || string.IsNullOrWhiteSpace(layout.Id) ||
            string.IsNullOrWhiteSpace(layout.RootName) || layout.Zones < 1)
            throw new InvalidDataException("Art-kit layouts require schema v1, an identity, a root name and zones.");
        ArgumentNullException.ThrowIfNull(layout.FloorFills);
        ArgumentNullException.ThrowIfNull(layout.WallLines);
        ArgumentNullException.ThrowIfNull(layout.Instances);
        ArgumentNullException.ThrowIfNull(layout.CollisionBoxes);
        ArgumentNullException.ThrowIfNull(layout.Lights);
        ArgumentNullException.ThrowIfNull(layout.Discs);
        var models = layout.FloorFills.Select(item => item.Model)
            .Concat(layout.WallLines.SelectMany(item => new[] { item.Model, item.DoorwayModel }))
            .Concat(layout.Instances.Select(item => item.Model));
        if (models.Any(model => string.IsNullOrWhiteSpace(model) ||
                                !string.Equals(Path.GetFileName(model), model, StringComparison.Ordinal) ||
                                !string.Equals(Path.GetExtension(model), ".glb", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Art-kit models must be leaf .glb file names.");
        if (layout.FloorFills.Any(item => item.Step <= 0 || item.MaximumX < item.MinimumX ||
                                          item.MaximumZ < item.MinimumZ) ||
            layout.WallLines.Any(item => item.Step <= 0 || item.Maximum < item.Minimum || item.HalfGap < 0) ||
            layout.CollisionBoxes.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                                               item.Size.X <= 0 || item.Size.Y <= 0 || item.Size.Z <= 0) ||
            layout.Lights.Any(item => string.IsNullOrWhiteSpace(item.Name) || item.Energy < 0 || item.Range <= 0) ||
            layout.Discs.Any(item => string.IsNullOrWhiteSpace(item.Name) || item.Radius <= 0 || item.Height <= 0))
            throw new InvalidDataException("Art-kit layout ranges and dimensions must be finite and positive.");
        if (layout.Lights.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != layout.Lights.Count ||
            layout.Discs.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != layout.Discs.Count ||
            layout.CollisionBoxes.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() !=
            layout.CollisionBoxes.Count)
            throw new InvalidDataException("Art-kit node names must be unique within their category.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public static class GodotArtKitBuilder
{
    public static GodotArtKitResult Build(GodotArtKitLayout layout, Func<string, Node3D> instantiateModel)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(instantiateModel);
        var root = new Node3D { Name = layout.RootName };
        var floors = 0;
        var walls = 0;
        var props = 0;
        try
        {
            foreach (var fill in layout.FloorFills)
            {
                for (var x = fill.MinimumX; x <= fill.MaximumX + .01f; x += fill.Step)
                {
                    for (var z = fill.MinimumZ; z <= fill.MaximumZ + .01f; z += fill.Step)
                    {
                        Add(root, fill.Model, new Vector3(x, fill.Y, z), 0, instantiateModel);
                        floors++;
                    }
                }
            }

            foreach (var line in layout.WallLines)
            {
                for (var coordinate = line.Minimum; coordinate <= line.Maximum + .01f; coordinate += line.Step)
                {
                    if (line.DoorwayAt is { } doorway &&
                        (line.HalfGap > 0
                            ? MathF.Abs(coordinate - doorway) <= line.HalfGap
                            : MathF.Abs(coordinate - doorway) <= .01f))
                        continue;
                    var position = line.Axis == ArtKitWallAxis.X
                        ? new Vector3(coordinate, 0, line.Fixed)
                        : new Vector3(line.Fixed, 0, coordinate);
                    Add(root, line.Model, position, line.YawDegrees, instantiateModel);
                    walls++;
                }
                if (line.DoorwayAt is not { } opening) continue;
                var doorwayPosition = line.Axis == ArtKitWallAxis.X
                    ? new Vector3(opening, 0, line.Fixed)
                    : new Vector3(line.Fixed, 0, opening);
                Add(root, line.DoorwayModel, doorwayPosition, line.YawDegrees, instantiateModel);
                walls++;
            }

            foreach (var instance in layout.Instances)
            {
                Add(root, instance.Model, instance.Position.ToGodot(), instance.YawDegrees, instantiateModel);
                props++;
            }
            foreach (var collision in layout.CollisionBoxes)
            {
                var body = new StaticBody3D { Name = collision.Name, Position = collision.Position.ToGodot() };
                body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = collision.Size.ToGodot() } });
                root.AddChild(body);
            }
            foreach (var light in layout.Lights)
            {
                root.AddChild(new OmniLight3D
                {
                    Name = light.Name,
                    Position = light.Position.ToGodot(),
                    LightColor = new Color(light.Color),
                    LightEnergy = light.Energy,
                    OmniRange = light.Range,
                    ShadowEnabled = light.Shadows,
                });
            }
            foreach (var disc in layout.Discs)
            {
                var material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(disc.Color),
                    Roughness = .72f,
                };
                if (disc.Emission)
                {
                    material.EmissionEnabled = true;
                    material.Emission = new Color(disc.Color);
                    material.EmissionEnergyMultiplier = 2.2f;
                }
                root.AddChild(new MeshInstance3D
                {
                    Name = disc.Name,
                    Position = disc.Position.ToGodot(),
                    Mesh = new CylinderMesh
                    {
                        TopRadius = disc.Radius,
                        BottomRadius = disc.Radius,
                        Height = disc.Height,
                    },
                    MaterialOverride = material,
                });
            }

            return new GodotArtKitResult(root, floors + walls + props, floors, walls, props, layout.Zones);
        }
        catch
        {
            root.Free();
            throw;
        }
    }

    private static void Add(Node3D root, string fileName, Vector3 position, float yawDegrees,
        Func<string, Node3D> instantiateModel)
    {
        var instance = instantiateModel(fileName) ??
                       throw new InvalidOperationException($"Art-kit resolver returned no model for '{fileName}'.");
        instance.Name = $"Art_{Path.GetFileNameWithoutExtension(fileName)}";
        instance.Position = position;
        instance.RotationDegrees = new Vector3(0, yawDegrees, 0);
        root.AddChild(instance);
    }
}
