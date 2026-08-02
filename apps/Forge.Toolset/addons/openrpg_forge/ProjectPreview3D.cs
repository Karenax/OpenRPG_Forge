#if TOOLS
using Forge.Application.Projects;
using Forge.Application.Spatial;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ProjectPreview3D : SubViewportContainer
{
    private readonly SubViewport _viewport;
    private readonly Node3D _world;
    private readonly BlueprintDragPalette _dragPalette;
    private readonly Dictionary<EntityId, Node3D> _entityNodes = [];
    private readonly PlacementValidator _placementValidator = new();
    private Camera3D? _camera;
    private Node3D? _areaRoot;
    private Node3D? _gizmo;
    private MeshInstance3D? _ghost;
    private Func<ProjectState?>? _getState;
    private Func<float>? _getGridSize;
    private Action<EntityId>? _selectEntity;
    private Func<EntityId, TransformValue, bool>? _transformEntity;
    private Func<BlueprintId, Vector3Value, EntityId?>? _placeBlueprint;
    private Action<string, bool>? _showPlacementFeedback;
    private EntityId? _selectedEntityId;
    private EntityId? _draggedEntityId;
    private TransformValue? _dragCandidate;
    private bool _dragCandidateValid;

    public int EntityCount { get; private set; }

    public bool GizmoVisible => _gizmo?.Visible == true;

    public bool GhostVisible => _ghost?.Visible == true;

    public bool LastPlacementWasValid { get; private set; }

    public ProjectPreview3D()
    {
        Name = "ProjectPreview3D";
        CustomMinimumSize = new Vector2(720, 420);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;

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

        var palettePanel = new PanelContainer
        {
            Position = new Vector2(14, 14),
            CustomMinimumSize = new Vector2(220, 175),
            MouseFilter = MouseFilterEnum.Stop,
        };
        palettePanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.07f, 0.13f, 0.94f),
            BorderColor = new Color("62458a"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        });
        AddChild(palettePanel);
        var paletteMargin = new MarginContainer();
        paletteMargin.AddThemeConstantOverride("margin_left", 10);
        paletteMargin.AddThemeConstantOverride("margin_top", 8);
        paletteMargin.AddThemeConstantOverride("margin_right", 10);
        paletteMargin.AddThemeConstantOverride("margin_bottom", 10);
        palettePanel.AddChild(paletteMargin);
        var paletteColumn = new VBoxContainer();
        paletteColumn.AddChild(new Label { Text = "GLISSER DANS LE MONDE" });
        _dragPalette = new BlueprintDragPalette();
        paletteColumn.AddChild(_dragPalette);
        paletteMargin.AddChild(paletteColumn);

        CreateGizmoAndGhost();
    }

    public void Initialize(
        Func<ProjectState?> getState,
        Func<float> getGridSize,
        Action<EntityId> selectEntity,
        Func<EntityId, TransformValue, bool> transformEntity,
        Func<BlueprintId, Vector3Value, EntityId?> placeBlueprint,
        Action<string, bool> showPlacementFeedback)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _getGridSize = getGridSize ?? throw new ArgumentNullException(nameof(getGridSize));
        _selectEntity = selectEntity ?? throw new ArgumentNullException(nameof(selectEntity));
        _transformEntity = transformEntity ?? throw new ArgumentNullException(nameof(transformEntity));
        _placeBlueprint = placeBlueprint ?? throw new ArgumentNullException(nameof(placeBlueprint));
        _showPlacementFeedback = showPlacementFeedback ?? throw new ArgumentNullException(nameof(showPlacementFeedback));
    }

    public void Rebuild(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Clear();

        var area = project.Areas.FirstOrDefault()
            ?? throw new InvalidOperationException("The project does not contain an area to preview.");
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        _dragPalette.SetBlueprints(project.Blueprints);
        _areaRoot = new Node3D { Name = "AreaPreview" };
        _world.AddChild(_areaRoot);

        foreach (var entity in area.Entities)
        {
            if (!blueprints.TryGetValue(entity.BlueprintId, out var blueprint))
            {
                continue;
            }

            var representation = CreateRepresentation(entity, blueprint);
            _areaRoot.AddChild(representation);
            _entityNodes[entity.Id] = representation;
            EntityCount++;
        }

        RefreshSelectedGizmo();
    }

    public void Clear()
    {
        EntityCount = 0;
        _entityNodes.Clear();
        _gizmo?.Hide();
        _ghost?.Hide();
        if (_areaRoot is null || !GodotObject.IsInstanceValid(_areaRoot))
        {
            _areaRoot = null;
            return;
        }

        _world.RemoveChild(_areaRoot);
        _areaRoot.Free();
        _areaRoot = null;
    }

    public bool SelectEntityForSmokeTest(EntityId entityId)
    {
        if (!_entityNodes.ContainsKey(entityId))
        {
            return false;
        }

        SelectEntity(entityId);
        return GizmoVisible;
    }

    public void ShowSelection(EntityId entityId)
    {
        _selectedEntityId = entityId;
        RefreshSelectedGizmo();
    }

    public PlacementValidationResult PreviewEntityMoveForSmokeTest(EntityId entityId, Vector3Value position)
    {
        _draggedEntityId = entityId;
        return PreviewEntityMove(entityId, position);
    }

    public bool CommitPreviewMoveForSmokeTest() => CommitPreviewMove();

    public EntityId? DropBlueprintForSmokeTest(BlueprintId blueprintId, Vector3Value position) =>
        DropBlueprint(blueprintId, position);

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            if (button.Pressed && TryProjectToGround(button.Position, out var ground))
            {
                var entityId = FindNearestEntity(ground);
                if (entityId is not null)
                {
                    SelectEntity(entityId);
                    _draggedEntityId = entityId;
                    AcceptEvent();
                }
            }
            else if (!button.Pressed && _draggedEntityId is not null)
            {
                CommitPreviewMove();
                AcceptEvent();
            }

            return;
        }

        if (@event is InputEventMouseMotion motion &&
            _draggedEntityId is not null &&
            (motion.ButtonMask & MouseButtonMask.Left) != 0 &&
            TryProjectToGround(motion.Position, out var position))
        {
            PreviewEntityMove(_draggedEntityId, ToValue(position));
            AcceptEvent();
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadBlueprintPayload(data, out var blueprintId) ||
            !TryProjectToGround(atPosition, out var ground))
        {
            return false;
        }

        var validation = Validate(blueprintId, ToValue(ground));
        ShowGhost(validation.CorrectedPosition, validation.IsValid);
        Report(validation);
        return true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadBlueprintPayload(data, out var blueprintId) ||
            !TryProjectToGround(atPosition, out var ground))
        {
            return;
        }

        DropBlueprint(blueprintId, ToValue(ground));
        _ghost?.Hide();
    }

    private void SelectEntity(EntityId entityId)
    {
        _selectedEntityId = entityId;
        RefreshSelectedGizmo();
        _selectEntity?.Invoke(entityId);
    }

    private PlacementValidationResult PreviewEntityMove(EntityId entityId, Vector3Value position)
    {
        var state = _getState?.Invoke();
        var entity = state?.Project.Areas.SelectMany(area => area.Entities)
            .FirstOrDefault(candidate => candidate.Id == entityId);
        if (state is null || entity is null)
        {
            var missing = new PlacementValidationResult(
                false,
                PlacementValidator.MissingAreaCode,
                "L’entité sélectionnée n’existe plus.",
                position,
                position);
            Report(missing);
            return missing;
        }

        var area = state.Project.Areas.First(candidate => candidate.Entities.Any(item => item.Id == entity.Id));
        var validation = _placementValidator.Validate(
            state.Project,
            new PlacementValidationRequest(
                area.Id,
                entity.BlueprintId,
                position,
                Math.Max(0.25f, _getGridSize?.Invoke() ?? 1),
                entity.Id));
        _dragCandidate = new TransformValue(
            validation.CorrectedPosition,
            entity.Transform.Rotation,
            entity.Transform.Scale);
        _dragCandidateValid = validation.IsValid;
        LastPlacementWasValid = validation.IsValid;
        ShowGhost(validation.CorrectedPosition, validation.IsValid);
        Report(validation);
        return validation;
    }

    private bool CommitPreviewMove()
    {
        var entityId = _draggedEntityId;
        _draggedEntityId = null;
        _ghost?.Hide();
        if (entityId is null || _dragCandidate is null || !_dragCandidateValid)
        {
            _dragCandidate = null;
            return false;
        }

        var candidate = _dragCandidate;
        _dragCandidate = null;
        return _transformEntity?.Invoke(entityId, candidate) == true;
    }

    private EntityId? DropBlueprint(BlueprintId blueprintId, Vector3Value position)
    {
        var validation = Validate(blueprintId, position);
        LastPlacementWasValid = validation.IsValid;
        ShowGhost(validation.CorrectedPosition, validation.IsValid);
        Report(validation);
        if (!validation.IsValid)
        {
            return null;
        }

        var entityId = _placeBlueprint?.Invoke(blueprintId, validation.CorrectedPosition);
        if (entityId is not null)
        {
            _selectedEntityId = entityId;
        }

        return entityId;
    }

    private PlacementValidationResult Validate(BlueprintId blueprintId, Vector3Value position)
    {
        var state = _getState?.Invoke();
        if (state is null || state.Project.Areas.Count == 0)
        {
            return new PlacementValidationResult(
                false,
                PlacementValidator.MissingAreaCode,
                "Ouvrez un projet avec une zone avant de placer un blueprint.",
                position,
                position);
        }

        return _placementValidator.Validate(
            state.Project,
            new PlacementValidationRequest(
                state.Project.Areas.First().Id,
                blueprintId,
                position,
                Math.Max(0.25f, _getGridSize?.Invoke() ?? 1)));
    }

    private void Report(PlacementValidationResult validation) =>
        _showPlacementFeedback?.Invoke($"{validation.Code} · {validation.Message}", validation.IsValid);

    private void ShowGhost(Vector3Value position, bool valid)
    {
        if (_ghost?.MaterialOverride is not StandardMaterial3D material)
        {
            return;
        }

        _ghost.Position = EngineValueConverter.ToEngine(position);
        material.AlbedoColor = valid
            ? new Color(0.35f, 0.95f, 0.55f, 0.48f)
            : new Color(1f, 0.25f, 0.25f, 0.58f);
        _ghost.Show();
    }

    private void RefreshSelectedGizmo()
    {
        if (_gizmo is null || _selectedEntityId is null ||
            !_entityNodes.TryGetValue(_selectedEntityId, out var selected))
        {
            _gizmo?.Hide();
            return;
        }

        _gizmo.Position = selected.Position + new Vector3(0, 0.08f, 0);
        _gizmo.Show();
    }

    private EntityId? FindNearestEntity(Vector3 ground)
    {
        EntityId? nearest = null;
        var bestDistance = 1.1f;
        foreach (var pair in _entityNodes)
        {
            var distance = new Vector2(pair.Value.Position.X - ground.X, pair.Value.Position.Z - ground.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = pair.Key;
            }
        }

        return nearest;
    }

    private bool TryProjectToGround(Vector2 localPosition, out Vector3 ground)
    {
        ground = Vector3.Zero;
        if (_camera is null || Size.X <= 0 || Size.Y <= 0)
        {
            return false;
        }

        var screen = new Vector2(
            localPosition.X * _viewport.Size.X / Size.X,
            localPosition.Y * _viewport.Size.Y / Size.Y);
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        if (MathF.Abs(direction.Y) < 0.0001f)
        {
            return false;
        }

        var distance = -origin.Y / direction.Y;
        if (distance < 0)
        {
            return false;
        }

        ground = origin + (direction * distance);
        return true;
    }

    private static bool TryReadBlueprintPayload(Variant data, out BlueprintId blueprintId)
    {
        blueprintId = null!;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dictionary = data.AsGodotDictionary();
        if (!dictionary.ContainsKey("kind") || !dictionary.ContainsKey("blueprintId") ||
            !string.Equals(dictionary["kind"].AsString(), BlueprintDragPalette.PayloadKind, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            blueprintId = BlueprintId.Parse(dictionary["blueprintId"].AsString());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Vector3Value ToValue(Vector3 value) => new(value.X, 0, value.Z);

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

    private void CreateGizmoAndGhost()
    {
        _gizmo = new Node3D { Name = "SelectionGizmo", Visible = false };
        _gizmo.AddChild(CreateGizmoAxis(
            new Vector3(1.15f, 0.04f, 0),
            new Vector3(2.3f, 0.08f, 0.08f),
            new Color("ff5f56")));
        _gizmo.AddChild(CreateGizmoAxis(
            new Vector3(0, 0.04f, 1.15f),
            new Vector3(0.08f, 0.08f, 2.3f),
            new Color("4f9cff")));
        _gizmo.AddChild(CreateGizmoAxis(
            new Vector3(0, 0.06f, 0),
            new Vector3(0.34f, 0.12f, 0.34f),
            new Color("ffb35c")));
        _world.AddChild(_gizmo);

        _ghost = new MeshInstance3D
        {
            Name = "PlacementGhost",
            Mesh = new BoxMesh { Size = new Vector3(0.92f, 0.12f, 0.92f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 0.95f, 0.55f, 0.48f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
            },
            Visible = false,
        };
        _world.AddChild(_ghost);
    }

    private static MeshInstance3D CreateGizmoAxis(Vector3 position, Vector3 size, Color color) => new()
    {
        Position = position,
        Mesh = new BoxMesh { Size = size },
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true,
        },
    };

    private void ConfigureCameraAndLighting()
    {
        _camera = new Camera3D
        {
            Name = "PreviewCamera",
            Position = new Vector3(13, 15, 17),
            Current = true,
            Fov = 55,
        };
        _world.AddChild(_camera);
        _camera.LookAtFromPosition(_camera.Position, Vector3.Zero, Vector3.Up);

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
