#if TOOLS
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.EngineAdapter.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ProjectPreview3D : SubViewportContainer
{
    private readonly SubViewport _viewport;
    private readonly Node3D _world;
    private Node3D? _areaRoot;

    public int EntityCount { get; private set; }

    public ProjectPreview3D()
    {
        Name = "ProjectPreview3D";
        CustomMinimumSize = new Vector2(720, 420);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Stretch = true;

        _viewport = new SubViewport
        {
            Name = "PreviewViewport",
            Size = new Vector2I(960, 540),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_viewport);

        _world = new Node3D { Name = "PreviewWorld" };
        _viewport.AddChild(_world);
        ConfigureCameraAndLighting();
        CreateGrid();
    }

    public void Rebuild(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Clear();

        var area = project.Areas.FirstOrDefault()
            ?? throw new InvalidOperationException("The project does not contain an area to preview.");
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        _areaRoot = new Node3D { Name = "AreaPreview" };
        _world.AddChild(_areaRoot);

        foreach (var entity in area.Entities)
        {
            if (!blueprints.TryGetValue(entity.BlueprintId, out var blueprint))
            {
                continue;
            }

            _areaRoot.AddChild(CreateRepresentation(entity, blueprint));
            EntityCount++;
        }
    }

    public void Clear()
    {
        EntityCount = 0;
        if (_areaRoot is null || !GodotObject.IsInstanceValid(_areaRoot))
        {
            _areaRoot = null;
            return;
        }

        _world.RemoveChild(_areaRoot);
        _areaRoot.Free();
        _areaRoot = null;
    }

    private static Node3D CreateRepresentation(
        EntityInstance entity,
        BlueprintDefinition blueprint)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(blueprint.DisplayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        root.AddChild(new MeshInstance3D
        {
            Mesh = CreateMesh(blueprint.Kind, entity.Tags),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = ColorFor(blueprint.Kind, entity.Tags),
                Roughness = 0.72f,
            },
        });

        if (blueprint.Kind is BlueprintKind.Creature or BlueprintKind.Item or BlueprintKind.Door)
        {
            root.AddChild(new Label3D
            {
                Text = blueprint.DisplayName,
                Position = new Vector3(0, 1.25f, 0),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 26,
                OutlineSize = 6,
            });
        }

        return root;
    }

    private static PrimitiveMesh CreateMesh(BlueprintKind kind, IReadOnlyList<string> tags)
    {
        if (tags.Contains("pillar", StringComparer.Ordinal))
        {
            return new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1 };
        }

        return kind switch
        {
            BlueprintKind.Creature or BlueprintKind.PlayerSpawn =>
                new CapsuleMesh { Radius = 0.45f, Height = 1.7f },
            BlueprintKind.Item => new SphereMesh { Radius = 0.4f, Height = 0.8f },
            BlueprintKind.Trigger => new CylinderMesh
            {
                TopRadius = 0.9f,
                BottomRadius = 0.9f,
                Height = 0.08f,
            },
            _ => new BoxMesh { Size = Vector3.One },
        };
    }

    private static Color ColorFor(BlueprintKind kind, IReadOnlyList<string> tags)
    {
        if (tags.Contains("boss", StringComparer.Ordinal))
        {
            return new Color("d34f4f");
        }

        if (tags.Contains("npc", StringComparer.Ordinal))
        {
            return new Color("4ec9b0");
        }

        if (tags.Contains("pillar", StringComparer.Ordinal))
        {
            return new Color("665565");
        }

        return kind switch
        {
            BlueprintKind.PlayerSpawn => new Color("74b9ff"),
            BlueprintKind.Creature => new Color("d68a3a"),
            BlueprintKind.Item => new Color("ffca55"),
            BlueprintKind.Door => new Color("8f5738"),
            BlueprintKind.Trigger => new Color("55efc4"),
            _ => new Color("514858"),
        };
    }

    private void ConfigureCameraAndLighting()
    {
        var camera = new Camera3D
        {
            Name = "PreviewCamera",
            Position = new Vector3(13, 15, 17),
            Current = true,
            Fov = 55,
        };
        _world.AddChild(camera);
        camera.LookAtFromPosition(camera.Position, Vector3.Zero, Vector3.Up);

        _world.AddChild(new DirectionalLight3D
        {
            Name = "PreviewSun",
            RotationDegrees = new Vector3(-58, -32, 0),
            LightColor = new Color("e8d6bc"),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        });
        _world.AddChild(new OmniLight3D
        {
            Name = "PreviewFill",
            Position = new Vector3(0, 5, -7),
            OmniRange = 18,
            LightColor = new Color("ff7043"),
            LightEnergy = 4,
        });
    }

    private void CreateGrid()
    {
        var grid = new Node3D { Name = "PlacementGrid" };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.42f, 0.34f, 0.45f, 0.6f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        for (var coordinate = -10; coordinate <= 10; coordinate++)
        {
            grid.AddChild(CreateGridLine(
                new Vector3(coordinate, 0.01f, 0),
                new Vector3(0.025f, 0.01f, 20),
                material));
            grid.AddChild(CreateGridLine(
                new Vector3(0, 0.01f, coordinate),
                new Vector3(20, 0.01f, 0.025f),
                material));
        }

        _world.AddChild(grid);
    }

    private static MeshInstance3D CreateGridLine(
        Vector3 position,
        Vector3 size,
        Material material) =>
        new()
        {
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
        };

    private static string SafeNodeName(string value) =>
        new(value.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());
}
#endif
