using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.World;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed record StudioViewportCameraState(
    Vector3 Target,
    float Yaw,
    float Pitch,
    float Distance,
    EditorCameraProjection Projection = EditorCameraProjection.Perspective,
    float FieldOfView = 55,
    float OrthographicSize = 20);

internal sealed partial class StudioWorldViewport : SubViewportContainer
{
    private const float RotationPlaneInnerRadius = .32f;
    private const float RotationPlaneOuterRadius = 1.28f;
    private readonly SubViewport _viewport;
    private readonly Node3D _world;
    private readonly Camera3D _camera;
    private readonly DirectionalLight3D _sun;
    private readonly DirectionalLight3D _editorFill;
    private readonly OmniLight3D _fill;
    private readonly WorldEnvironment _environment;
    private readonly MeshInstance3D _selectionMarker;
    private readonly Node3D _moveGizmo;
    private readonly Dictionary<StudioMoveAxis, IReadOnlyList<StandardMaterial3D>> _gizmoMaterials = [];
    private readonly Dictionary<StudioRotationPlane, IReadOnlyList<StandardMaterial3D>> _rotationMaterials = [];
    private readonly Label _summary;
    private readonly Label _warning;
    private readonly Label _modeBadge;
    private readonly Label _interactionHint;
    private readonly PanelContainer _contextMenu;
    private readonly Panel _deleteMenu;
    private readonly Label _contextTitle;
    private Label _contextEyebrow = null!;
    private VBoxContainer _entityContextActions = null!;
    private VBoxContainer _placementContextActions = null!;
    private Label _deleteConfirmationText = null!;
    private readonly Dictionary<EntityId, Node3D> _entityNodes = [];
    private readonly HashSet<EntityId> _editableEntityIds = [];
    private readonly HashSet<string> _unavailableVisualAssets = new(StringComparer.Ordinal);
    private Node3D? _areaRoot;
    private GodotAssetModelLibrary? _modelLibrary;
    private StudioSessionController? _session;
    private EntityId? _selectedEntityId;
    private EntityId? _contextCandidateEntityId;
    private EntityId? _contextEntityId;
    private Vector3Value? _contextCandidatePosition;
    private Vector3Value? _contextPlacementPosition;
    private StudioMoveAxis? _dragAxis;
    private StudioRotationPlane? _dragRotationPlane;
    private TransformValue? _dragStartTransform;
    private TransformValue? _dragCandidateTransform;
    private Vector2 _dragStartPointer;
    private Vector3 _dragStartWorldPosition;
    private Vector3 _dragPreviousRotationVector;
    private float _dragAccumulatedRotationRadians;
    private Vector2 _rightPressPosition;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _cameraYaw = 0.72f;
    private float _cameraPitch = 0.58f;
    private float _cameraDistance = 28;
    private bool _orbiting;
    private bool _panning;
    private bool _rightDragging;
    private bool _readOnly;
    private AreaId? _viewAreaId;
    private EditorDisplayFilter _displayFilter = EditorDisplayFilter.ShowAll;

    internal event Action<EntityId>? EntitySelected;

    internal event Action<EntityId, StudioEntityContextAction>? EntityContextActionRequested;

    internal event Action<Vector3Value>? EntityPlacementRequested;

    internal event Action<Vector3Value, SemanticPasteMode>? ClipboardPasteRequested;

    internal event Action<string>? InteractionStatusChanged;

    internal int EntityCount { get; private set; }

    internal int AssetModelInstanceCount { get; private set; }

    internal int UnavailableAssetCount => _unavailableVisualAssets.Count;

    internal int VisibleEntityCount { get; private set; }

    internal int GhostedEntityCount { get; private set; }

    internal bool GizmoVisible => _moveGizmo.Visible;

    internal int RotationPlaneCount => _rotationMaterials.Count;

    private bool GizmoDragActive =>
        _dragAxis is not null || _dragRotationPlane is not null || _dragScaleAxis is not null;

    internal bool ContextMenuVisible => _contextMenu.Visible || _deleteMenu.Visible;

    internal bool DeleteConfirmationVisible => _deleteMenu.Visible;

    internal bool IsReadOnly => _readOnly;

    internal AreaId? ViewAreaId => _viewAreaId;

    private AreaDefinition? ViewArea => _viewAreaId is null
        ? _session?.ActiveArea
        : _session?.Project?.Areas.FirstOrDefault(area => area.Id == _viewAreaId);

    internal Vector3Value SuggestedPlacementPosition
    {
        get
        {
            var source = _selectedEntityId is not null && ViewArea is not null
                ? ViewArea.Entities.FirstOrDefault(entity => entity.Id == _selectedEntityId)?.Transform.Position
                : null;
            var position = source is null
                ? new Vector3Value(_cameraTarget.X, 0, _cameraTarget.Z)
                : new Vector3Value(source.Value.X + 1, source.Value.Y, source.Value.Z + 1);
            return SnapPlacement(position);
        }
    }

    internal StudioWorldViewport()
    {
        Name = "StudioWorldViewport";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(420, 300);
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;

        _viewport = new SubViewport
        {
            Name = "Studio3DViewport",
            Size = new Vector2I(1280, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = false,
        };
        AddChild(_viewport);
        _world = new Node3D { Name = "StudioWorld" };
        _viewport.AddChild(_world);

        _camera = new Camera3D
        {
            Name = "StudioCamera",
            Current = true,
            Fov = 52,
            Near = .08f,
            Far = 2000,
        };
        _world.AddChild(_camera);
        _sun = new DirectionalLight3D
        {
            Name = "StudioSun",
            RotationDegrees = new Vector3(-58, -32, 0),
            LightColor = new Color("ead9bd"),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        };
        _world.AddChild(_sun);
        _editorFill = new DirectionalLight3D
        {
            Name = "StudioNeutralFill",
            RotationDegrees = new Vector3(-38, 142, 0),
            LightColor = new Color("d7e3df"),
            LightEnergy = .62f,
            ShadowEnabled = false,
        };
        _world.AddChild(_editorFill);
        _fill = new OmniLight3D
        {
            Name = "StudioFill",
            Position = new Vector3(0, 7, -8),
            OmniRange = 120,
            LightColor = new Color("ffd2b8"),
            LightEnergy = 3.2f,
        };
        _world.AddChild(_fill);
        _environment = new WorldEnvironment { Name = "StudioEnvironment" };
        _world.AddChild(_environment);

        _selectionMarker = new MeshInstance3D
        {
            Name = "SelectionMarker",
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new CylinderMesh
            {
                TopRadius = .82f,
                BottomRadius = .82f,
                Height = .035f,
                RadialSegments = 48,
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(StudioStyle.Accent, .55f),
                EmissionEnabled = true,
                Emission = StudioStyle.Accent,
                EmissionEnergyMultiplier = 1.8f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        _world.AddChild(_selectionMarker);
        _moveGizmo = BuildMoveGizmo();
        _world.AddChild(_moveGizmo);

        var toolbar = StudioStyle.Panel("ViewportToolbar", new Color(StudioStyle.Chrome, .94f), 8);
        toolbar.Position = new Vector2(8, 8);
        toolbar.MouseFilter = MouseFilterEnum.Stop;
        AddChild(toolbar);
        var toolbarMargin = StudioStyle.Margin(6, 4, 6, 4);
        toolbar.AddChild(toolbarMargin);
        var toolbarRow = new HBoxContainer();
        toolbarRow.AddThemeConstantOverride("separation", 4);
        toolbarMargin.AddChild(toolbarRow);
        toolbarRow.AddChild(StudioStyle.Label("VUE 3D", 10, StudioStyle.Accent));
        _modeBadge = StudioStyle.Label("ÉDITION", 9, StudioStyle.Success);
        toolbarRow.AddChild(_modeBadge);
        var frame = StudioStyle.Button("Tout cadrer", compact: true);
        frame.Pressed += FrameAreaAndRecord;
        toolbarRow.AddChild(frame);
        var selection = StudioStyle.Button("Cadrer la sélection", compact: true);
        selection.Pressed += FrameSelection;
        toolbarRow.AddChild(selection);
        InitializeHumanTransformToolbar(toolbarRow);
        _interactionHint = StudioStyle.Label(
            "clic sélectionner · choisissez Déplacer, Tourner ou Redimensionner · clic droit éditer · glisser droit orbiter",
            10,
            StudioStyle.Muted);
        toolbarRow.AddChild(_interactionHint);

        var footer = StudioStyle.Panel("ViewportSummary", new Color(StudioStyle.Chrome, .92f), 8);
        footer.AnchorTop = 1;
        footer.AnchorBottom = 1;
        footer.OffsetLeft = 8;
        footer.OffsetTop = -35;
        footer.OffsetRight = 430;
        footer.OffsetBottom = -7;
        footer.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(footer);
        var footerRow = new HBoxContainer();
        _summary = StudioStyle.Label("Chargement de la zone…", 10, StudioStyle.Text);
        footerRow.AddChild(_summary);
        _warning = StudioStyle.Label(string.Empty, 10, StudioStyle.Warning);
        footerRow.AddChild(_warning);
        footer.AddChild(footerRow);

        _contextMenu = BuildContextMenu(out _contextTitle);
        AddChild(_contextMenu);
        _deleteMenu = BuildDeleteConfirmation();
        AddChild(_deleteMenu);

        UpdateCamera();
        InitializeUx0CViewport();
        InitializeUx0DViewport();
        InitializeHumanPicking();
        Resized += UpdateToolbarDensity;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (HandleUx0DPlacementInput(@event))
        {
            AcceptEvent();
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            if (GizmoDragActive)
            {
                CancelGizmoDrag();
            }
            else
            {
                HideContextMenu();
            }
            AcceptEvent();
            return;
        }

        if (HandleHumanTransformShortcut(@event))
        {
            AcceptEvent();
            return;
        }

        if (HandleUx0CCameraInput(@event))
        {
            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Right)
            {
                HandleRightButton(button);
                AcceptEvent();
                return;
            }
            if (button.ButtonIndex == MouseButton.Middle)
            {
                _panning = button.Pressed;
                AcceptEvent();
                return;
            }
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (!TryNavigateCamera(
                        EditorCameraOperation.Zoom,
                        x: button.ButtonIndex == MouseButton.WheelUp ? 1 : -1))
                {
                    _cameraDistance = Math.Clamp(
                        _cameraDistance * (button.ButtonIndex == MouseButton.WheelUp ? .88f : 1.14f),
                        2.5f,
                        600f);
                    UpdateCamera();
                }
                AcceptEvent();
                return;
            }
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    HideContextMenu();
                    if (TryBeginGizmoDrag(button.Position))
                    {
                        AcceptEvent();
                        return;
                    }

                    var entityId = FindPickedEntity(button.Position);
                    if (entityId is not null)
                    {
                        SelectPointerEntity(entityId, button.CtrlPressed || button.ShiftPressed);
                        if (button.DoubleClick)
                        {
                            FrameSelection();
                        }
                        AcceptEvent();
                    }
                }
                else if (GizmoDragActive)
                {
                    CommitGizmoDrag();
                    AcceptEvent();
                }
                return;
            }
        }

        if (@event is InputEventMouseMotion motion)
        {
            if (GizmoDragActive && (motion.ButtonMask & MouseButtonMask.Left) != 0)
            {
                PreviewGizmoDrag(motion);
                AcceptEvent();
                return;
            }

            if ((motion.ButtonMask & MouseButtonMask.Right) != 0)
            {
                ClearHumanPickingHover();
                if (!_orbiting && motion.Position.DistanceTo(_rightPressPosition) >= 4)
                {
                    _rightDragging = true;
                    _orbiting = true;
                    HideContextMenu();
                }

                if (_orbiting)
                {
                    if (!TryNavigateCamera(
                            EditorCameraOperation.Orbit,
                            x: motion.Relative.X,
                            y: -motion.Relative.Y))
                    {
                        _cameraYaw -= motion.Relative.X * .008f;
                        _cameraPitch = Math.Clamp(_cameraPitch + motion.Relative.Y * .006f, .12f, 1.35f);
                        UpdateCamera();
                    }
                    AcceptEvent();
                }
                return;
            }

            if (!_panning)
            {
                UpdateHumanPickingHover(motion.Position);
                return;
            }

            ClearHumanPickingHover();
            if (_orbiting)
            {
                if (TryNavigateCamera(
                        EditorCameraOperation.Orbit,
                        x: motion.Relative.X,
                        y: -motion.Relative.Y))
                {
                    AcceptEvent();
                    return;
                }
                _cameraYaw -= motion.Relative.X * .008f;
                _cameraPitch = Math.Clamp(_cameraPitch + motion.Relative.Y * .006f, .12f, 1.35f);
            }
            else
            {
                if (TryNavigateCamera(
                        EditorCameraOperation.Pan,
                        x: -motion.Relative.X,
                        y: motion.Relative.Y))
                {
                    AcceptEvent();
                    return;
                }
                var scale = _cameraDistance * .0018f;
                var right = _camera.GlobalTransform.Basis.X.Normalized();
                var forward = new Vector3(-right.Z, 0, right.X).Normalized();
                _cameraTarget += (right * -motion.Relative.X + forward * motion.Relative.Y) * scale;
            }
            UpdateCamera();
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!ContextMenuVisible)
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            HideContextMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton button ||
            !button.Pressed ||
            button.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        var pointer = GetGlobalMousePosition();
        var insideContextMenu = _contextMenu.Visible && _contextMenu.GetGlobalRect().HasPoint(pointer);
        var insideDeleteMenu = _deleteMenu.Visible && _deleteMenu.GetGlobalRect().HasPoint(pointer);
        if (!insideContextMenu && !insideDeleteMenu)
        {
            HideContextMenu();
        }
    }

    private void HandleRightButton(InputEventMouseButton button)
    {
        if (button.Pressed)
        {
            _rightPressPosition = button.Position;
            _rightDragging = false;
            _orbiting = false;
            HideContextMenu();
            _contextCandidateEntityId = FindPickedEntity(button.Position);
            _contextCandidatePosition = TryProjectToGround(button.Position, out var ground)
                ? SnapPlacement(new Vector3Value(ground.X, ground.Y, ground.Z))
                : null;
            if (_contextCandidateEntityId is not null)
            {
                SelectContextEntity(_contextCandidateEntityId);
            }
            return;
        }

        var entityId = _contextCandidateEntityId;
        var placementPosition = _contextCandidatePosition;
        var shouldOpen = !_rightDragging && (entityId is not null || placementPosition is not null);
        _contextCandidateEntityId = null;
        _contextCandidatePosition = null;
        _orbiting = false;
        _rightDragging = false;
        if (_readOnly)
        {
            if (shouldOpen)
            {
                InteractionStatusChanged?.Invoke("Comparaison protégée · aucune mutation dans cette vue");
            }
            return;
        }
        if (!shouldOpen)
        {
            return;
        }

        if (entityId is not null)
        {
            var entityPosition = ViewArea?.Entities
                .FirstOrDefault(entity => entity.Id == entityId)?.Transform.Position;
            ShowEntityContextMenu(
                entityId,
                placementPosition ?? (entityPosition is null ? Vector3Value.Zero : SnapPlacement(entityPosition.Value)),
                button.Position);
        }
        else if (placementPosition is not null)
        {
            ShowPlacementContextMenu(placementPosition.Value, button.Position);
        }
    }

    private bool TryBeginGizmoDrag(Vector2 pointer)
        => TryBeginConfiguredGizmoDrag(pointer);

    private void PreviewGizmoDrag(InputEventMouseMotion motion)
        => PreviewConfiguredGizmoDrag(motion);

    private void PreviewTranslationDrag(InputEventMouseMotion motion, Node3D node)
    {
        var axis = AxisVector(_dragAxis!.Value);
        var startTransform = _dragStartTransform!;
        var originScreen = _camera.UnprojectPosition(_dragStartWorldPosition);
        var scale = Math.Max(_moveGizmo.Scale.X, .001f);
        var worldAxisLength = 1.65f * scale;
        var endScreen = _camera.UnprojectPosition(_dragStartWorldPosition + axis * worldAxisLength);
        var screenAxis = endScreen - originScreen;
        if (screenAxis.LengthSquared() < 1)
        {
            return;
        }

        var pointerDelta = ToViewportPointer(motion.Position) - _dragStartPointer;
        var distance = pointerDelta.Dot(screenAxis.Normalized()) / screenAxis.Length() * worldAxisLength;
        var snap = motion.ShiftPressed ? .05f : .25f;
        distance = MathF.Round(distance / snap) * snap;
        var start = startTransform.Position;
        var candidatePosition = new Vector3Value(
            start.X + axis.X * distance,
            start.Y + axis.Y * distance,
            start.Z + axis.Z * distance);
        _dragCandidateTransform = new TransformValue(
            candidatePosition,
            startTransform.Rotation,
            startTransform.Scale);
        node.Position = new Vector3(candidatePosition.X, candidatePosition.Y, candidatePosition.Z);
        SetSelection(_selectedEntityId);
        InteractionStatusChanged?.Invoke(
            $"Translation {_dragAxis} · X {candidatePosition.X:0.##} · Y {candidatePosition.Y:0.##} · Z {candidatePosition.Z:0.##}");
    }

    private void PreviewRotationDrag(InputEventMouseMotion motion, Node3D node)
    {
        var plane = _dragRotationPlane!.Value;
        if (!TryPointerVectorOnRotationPlane(motion.Position, plane, out var currentVector, out _))
        {
            return;
        }

        var normal = AxisVector(RotationAxis(plane));
        var increment = MathF.Atan2(
            normal.Dot(_dragPreviousRotationVector.Cross(currentVector)),
            Math.Clamp(_dragPreviousRotationVector.Dot(currentVector), -1, 1));
        if (!float.IsFinite(increment))
        {
            return;
        }

        _dragAccumulatedRotationRadians += increment;
        _dragPreviousRotationVector = currentVector;
        var snap = motion.ShiftPressed ? 1f : 5f;
        var degrees = MathF.Round(
            _dragAccumulatedRotationRadians * 180f / MathF.PI / snap) * snap;
        var delta = new Quaternion(normal, degrees * MathF.PI / 180f);
        var startRotation = EngineValueConverter.ToEngine(_dragStartTransform!.Rotation).Normalized();
        var candidateRotation = (delta * startRotation).Normalized();
        _dragCandidateTransform = new TransformValue(
            _dragStartTransform.Position,
            new QuaternionValue(
                candidateRotation.X,
                candidateRotation.Y,
                candidateRotation.Z,
                candidateRotation.W),
            _dragStartTransform.Scale);
        node.Quaternion = candidateRotation;
        SetSelection(_selectedEntityId!);
        InteractionStatusChanged?.Invoke(
            $"Rotation {plane} · axe {RotationAxis(plane)} · {degrees:0.#}°");
    }

    private void CommitGizmoDrag()
        => CommitConfiguredGizmoDrag();

    private void CancelGizmoDrag()
        => CancelConfiguredGizmoDrag();

    private StudioMoveAxis? PickGizmoAxis(Vector2 pointer)
    {
        var screen = ToViewportPointer(pointer);
        var origin = _camera.UnprojectPosition(_moveGizmo.GlobalPosition);
        var scale = Math.Max(_moveGizmo.Scale.X, .001f);
        var threshold = 8f * _viewport.Size.X / Math.Max(Size.X, 1);
        StudioMoveAxis? nearest = null;
        var best = threshold;
        foreach (var axis in Enum.GetValues<StudioMoveAxis>())
        {
            var endpoint = _camera.UnprojectPosition(
                _moveGizmo.GlobalPosition + AxisVector(axis) * 1.65f * scale);
            var distance = DistanceToSegment(screen, origin, endpoint);
            if (distance < best)
            {
                best = distance;
                nearest = axis;
            }
        }
        return nearest;
    }

    private StudioRotationPlane? PickRotationPlane(Vector2 pointer)
    {
        StudioRotationPlane? nearest = null;
        var nearestDistance = float.PositiveInfinity;
        foreach (var plane in Enum.GetValues<StudioRotationPlane>())
        {
            if (!TryPointerVectorOnRotationPlane(pointer, plane, out var direction, out var distance))
            {
                continue;
            }

            var frame = RotationFrame(plane);
            var first = direction.Dot(frame.First);
            var second = direction.Dot(frame.Second);
            if (first < 0 || second < 0)
            {
                continue;
            }

            var scale = Math.Max(_moveGizmo.Scale.X, .001f);
            var rayOrigin = _camera.ProjectRayOrigin(ToViewportPointer(pointer));
            var hit = rayOrigin + _camera.ProjectRayNormal(ToViewportPointer(pointer)).Normalized() * distance;
            var radius = (hit - _moveGizmo.GlobalPosition).Length() / scale;
            if (radius < RotationPlaneInnerRadius || radius > RotationPlaneOuterRadius ||
                distance >= nearestDistance)
            {
                continue;
            }

            nearest = plane;
            nearestDistance = distance;
        }
        return nearest;
    }

    private bool TryPointerVectorOnRotationPlane(
        Vector2 pointer,
        StudioRotationPlane plane,
        out Vector3 direction,
        out float distance)
    {
        var screen = ToViewportPointer(pointer);
        var rayOrigin = _camera.ProjectRayOrigin(screen);
        var rayDirection = _camera.ProjectRayNormal(screen).Normalized();
        var normal = RotationFrame(plane).Normal;
        var denominator = normal.Dot(rayDirection);
        if (MathF.Abs(denominator) < .0001f)
        {
            direction = Vector3.Zero;
            distance = 0;
            return false;
        }

        distance = normal.Dot(_moveGizmo.GlobalPosition - rayOrigin) / denominator;
        if (!float.IsFinite(distance) || distance <= 0)
        {
            direction = Vector3.Zero;
            return false;
        }

        var hit = rayOrigin + rayDirection * distance;
        var offset = hit - _moveGizmo.GlobalPosition;
        offset -= normal * offset.Dot(normal);
        if (offset.LengthSquared() < .0001f)
        {
            direction = Vector3.Zero;
            return false;
        }

        direction = offset.Normalized();
        return true;
    }

    private void UpdateGizmo(Vector3 position)
    {
        _moveGizmo.Position = position;
        // Keep handles readable without letting a close-up selection cover the object itself.
        var scale = Math.Clamp(_cameraDistance * .045f, .18f, 8f);
        _moveGizmo.Scale = Vector3.One * scale;
        _moveGizmo.Visible = true;
    }

    private void RefreshGizmoMaterials(
        StudioMoveAxis? activeAxis,
        StudioRotationPlane? activePlane)
    {
        foreach (var pair in _gizmoMaterials)
        {
            var color = AxisColor(pair.Key);
            foreach (var material in pair.Value)
            {
                material.AlbedoColor = pair.Key == activeAxis ? color.Lightened(.28f) : color;
                material.Emission = material.AlbedoColor;
                material.EmissionEnergyMultiplier = pair.Key == activeAxis ? 2.8f : 1.35f;
            }
        }

        foreach (var pair in _rotationMaterials)
        {
            var active = pair.Key == activePlane;
            var color = PlaneColor(pair.Key);
            for (var index = 0; index < pair.Value.Count; index++)
            {
                var material = pair.Value[index];
                var alpha = index == 0
                    ? active ? .48f : .2f
                    : active ? 1f : .78f;
                var display = active ? color.Lightened(.24f) : color;
                display.A = alpha;
                material.AlbedoColor = display;
                material.Emission = new Color(display.R, display.G, display.B);
                material.EmissionEnergyMultiplier = active ? 2.4f : 1.15f;
            }
        }
    }

    private void ShowEntityContextMenu(EntityId entityId, Vector3Value placementPosition, Vector2 pointer)
    {
        _contextEntityId = entityId;
        _contextPlacementPosition = placementPosition;
        _contextEyebrow.Text = "MODIFIER CET OBJET";
        _contextTitle.Text = ResolveEntityTitle(entityId);
        _entityContextActions.Visible = true;
        _placementContextActions.Visible = false;
        _deleteMenu.Visible = false;
        ShowContextMenu(pointer, 430);
    }

    private void ShowPlacementContextMenu(Vector3Value placementPosition, Vector2 pointer)
    {
        _contextEntityId = null;
        _contextPlacementPosition = placementPosition;
        _contextEyebrow.Text = "AJOUTER DANS LA ZONE";
        _contextTitle.Text = $"X {placementPosition.X:0.##} · Z {placementPosition.Z:0.##}";
        _entityContextActions.Visible = false;
        _placementContextActions.Visible = true;
        _deleteMenu.Visible = false;
        ShowContextMenu(pointer, 145);
    }

    private void ShowContextMenu(Vector2 pointer, float expectedHeight)
    {
        const float width = 246;
        ResizeContextMenu(expectedHeight);
        _contextMenu.Position = new Vector2(
            Math.Clamp(pointer.X + 8, 4, Math.Max(4, Size.X - width - 4)),
            Math.Clamp(pointer.Y + 8, 4, Math.Max(4, Size.Y - expectedHeight - 4)));
        _contextMenu.Visible = true;
        _contextMenu.MoveToFront();
    }

    private void HideContextMenu()
    {
        _contextMenu.Visible = false;
        _deleteMenu.Visible = false;
        _contextEntityId = null;
        _contextPlacementPosition = null;
    }

    private void RequestContextAction(StudioEntityContextAction action)
    {
        var entityId = _contextEntityId;
        HideContextMenu();
        if (entityId is null)
        {
            return;
        }

        if (action == StudioEntityContextAction.FrameSelection)
        {
            FrameSelection();
            return;
        }
        EntityContextActionRequested?.Invoke(entityId, action);
    }

    private void RequestPlacement()
    {
        var position = _contextPlacementPosition;
        HideContextMenu();
        if (position is not null)
        {
            EntityPlacementRequested?.Invoke(position.Value);
        }
    }

    private void ShowDeleteConfirmation()
    {
        if (_contextEntityId is null)
        {
            return;
        }

        _deleteConfirmationText.Text = $"Supprimer {ResolveEntityTitle(_contextEntityId)} de cette zone ?";
        _deleteMenu.Position = _contextMenu.Position;
        _contextMenu.Visible = false;
        _deleteMenu.Visible = true;
        ResizeDeleteMenu();
        _deleteMenu.MoveToFront();
        InteractionStatusChanged?.Invoke("Confirmez la suppression · Ctrl+Z permettra de restaurer l'objet");
    }

    private void CancelDeleteConfirmation()
    {
        _deleteMenu.Visible = false;
        _contextMenu.Visible = _contextEntityId is not null;
        ResizeContextMenu(430);
        _contextMenu.MoveToFront();
    }

    private void ResizeContextMenu(float height)
    {
        _contextMenu.ResetSize();
        _contextMenu.Size = new Vector2(246, height);
        Callable.From(() =>
        {
            _contextMenu.ResetSize();
            _contextMenu.Size = new Vector2(246, height);
        }).CallDeferred();
    }

    private void ResizeDeleteMenu()
    {
        _deleteMenu.Size = new Vector2(246, 190);
        Callable.From(() =>
        {
            _deleteMenu.Size = new Vector2(246, 190);
        }).CallDeferred();
    }

    private string ResolveEntityTitle(EntityId entityId)
    {
        var entity = ViewArea?.Entities.FirstOrDefault(candidate => candidate.Id == entityId);
        var blueprint = entity is null
            ? null
            : _session?.Project?.Blueprints.FirstOrDefault(candidate => candidate.Id == entity.BlueprintId);
        return blueprint?.DisplayName ?? "Objet sélectionné";
    }

    private Vector2 ToViewportPointer(Vector2 pointer) => new(
        pointer.X * _viewport.Size.X / Math.Max(Size.X, 1),
        pointer.Y * _viewport.Size.Y / Math.Max(Size.Y, 1));

    private Vector2 FromViewportPointer(Vector2 pointer) => new(
        pointer.X * Math.Max(Size.X, 1) / _viewport.Size.X,
        pointer.Y * Math.Max(Size.Y, 1) / _viewport.Size.Y);

    private static Vector3 AxisVector(StudioMoveAxis axis) => axis switch
    {
        StudioMoveAxis.X => Vector3.Right,
        StudioMoveAxis.Y => Vector3.Up,
        _ => Vector3.Back,
    };

    private static Color AxisColor(StudioMoveAxis axis) => axis switch
    {
        StudioMoveAxis.X => new Color("ff5f56"),
        StudioMoveAxis.Y => new Color("62d26f"),
        _ => new Color("4f9cff"),
    };

    private static StudioMoveAxis RotationAxis(StudioRotationPlane plane) => plane switch
    {
        StudioRotationPlane.XY => StudioMoveAxis.Z,
        StudioRotationPlane.YZ => StudioMoveAxis.X,
        _ => StudioMoveAxis.Y,
    };

    private static Color PlaneColor(StudioRotationPlane plane) => AxisColor(RotationAxis(plane));

    private static (Vector3 First, Vector3 Second, Vector3 Normal) RotationFrame(
        StudioRotationPlane plane) => plane switch
        {
            StudioRotationPlane.XY => (Vector3.Right, Vector3.Up, Vector3.Back),
            StudioRotationPlane.YZ => (Vector3.Up, Vector3.Back, Vector3.Right),
            _ => (Vector3.Back, Vector3.Right, Vector3.Up),
        };

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        if (segment.LengthSquared() < .001f)
        {
            return point.DistanceTo(start);
        }
        var ratio = Math.Clamp((point - start).Dot(segment) / segment.LengthSquared(), 0, 1);
        return point.DistanceTo(start + segment * ratio);
    }

    internal void Rebuild(StudioSessionController session, bool frameArea = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        _viewAreaId = session.ActiveAreaId;
        _readOnly = false;
        RebuildCore(session, frameArea);
    }

    internal void ShowNoArea(StudioSessionController session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _viewAreaId = null;
        _readOnly = true;
        ClearArea();
        EntityCount = 0;
        VisibleEntityCount = 0;
        GhostedEntityCount = 0;
        AssetModelInstanceCount = 0;
        _unavailableVisualAssets.Clear();
        _warning.Text = "Aucune zone dans ce projet · utilisez Identité & configuration pour commencer";
        _modeBadge.Text = "PROJET SANS ZONE";
        _modeBadge.AddThemeColorOverride("font_color", StudioStyle.Warning);
        UpdateViewportModeSummary();
    }

    internal void RebuildArea(
        StudioSessionController session,
        AreaId areaId,
        bool readOnly,
        bool frameArea = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(areaId);
        _viewAreaId = areaId;
        _readOnly = readOnly;
        RebuildCore(session, frameArea);
    }

    private void RebuildCore(StudioSessionController session, bool frameArea)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        var project = session.Project ?? throw new InvalidOperationException("No project is open.");
        var area = ViewArea ?? throw new InvalidOperationException("No area is active.");
        ClearArea();
        _unavailableVisualAssets.Clear();
        EntityCount = 0;
        AssetModelInstanceCount = 0;
        _modelLibrary = session.AssetManifests.Count == 0
            ? null
            : new GodotAssetModelLibrary(
                session.Session!.RootDirectory,
                session.AssetManifests,
                allowRuntimeImport: !string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase));

        ApplyEnvironment(area);
        CaptureAuthoredLighting();
        _areaRoot = new Node3D { Name = "Area_" + SafeNodeName(area.DisplayName) };
        _world.AddChild(_areaRoot);
        try
        {
            var terrain = GodotTerrainAdapter.Build(
                area.World,
                _modelLibrary is null ? null : _modelLibrary.ResolveScatterMesh);
            _areaRoot.AddChild(terrain.Root);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_TERRAIN_FALLBACK {exception.Message}");
            _areaRoot.AddChild(BuildFallbackGround(area));
        }
        PrepareAuthoredArtKit(session, area);

        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        foreach (var source in area.Entities)
        {
            ResolvedModularTileEntity resolved;
            try
            {
                resolved = ModularTilePlacementResolver.Resolve(area, source);
            }
            catch (InvalidOperationException exception)
            {
                GD.PushWarning($"OPENRPG_FORGE_STUDIO_TILE_FALLBACK entity={source.Id} {exception.Message}");
                resolved = new ResolvedModularTileEntity(source, ModularTileMirrorMode.None, string.Empty, Guid.Empty);
            }
            if (!blueprints.TryGetValue(resolved.Entity.BlueprintId, out var blueprint))
            {
                continue;
            }

            var node = CreateRepresentation(resolved.Entity, blueprint);
            ApplyMirror(node, resolved.Mirror);
            ConfigureArtKitArchitectureProxy(node, resolved.Entity, blueprint);
            _areaRoot.AddChild(node);
            _entityNodes[source.Id] = node;
            EntityCount++;
        }

        if (frameArea)
        {
            FramePrimaryContent();
        }
        else
        {
            UpdateCamera();
        }
        ApplyDisplayFilter();
        _warning.Text = UnavailableAssetCount == 0 ? string.Empty : $" · {UnavailableAssetCount} aperçus de secours";
        SetViewportMode(session.WorkspaceProfile.Shell.ViewportMode);
        _modeBadge.Text = _readOnly ? "LECTURE SEULE" : "ÉDITION";
        _modeBadge.AddThemeColorOverride("font_color", _readOnly ? StudioStyle.Warning : StudioStyle.Success);
        UpdateToolbarDensity();
        SetSelections(
            _readOnly ? [] : session.SelectedEntityIds,
            _readOnly ? null : session.SelectedEntityId);
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_WORLD_READY area={area.Slug} entities={EntityCount} " +
            $"assets={AssetModelInstanceCount} fallback_assets={UnavailableAssetCount} " +
            $"art_kit={_artKitPassLoaded.ToString().ToLowerInvariant()} " +
            $"art_kit_models={_artKitModelInstanceCount} " +
            $"hidden_architecture_proxies={_hiddenArchitectureProxyCount} " +
            $"read_only={_readOnly.ToString().ToLowerInvariant()}");
    }

    internal StudioViewportCameraState CaptureCameraState() => new(
        _cameraTarget,
        _cameraYaw,
        _cameraPitch,
        _cameraDistance,
        _camera.Projection == Camera3D.ProjectionType.Orthogonal
            ? EditorCameraProjection.Orthographic
            : EditorCameraProjection.Perspective,
        _camera.Fov,
        _camera.Size);

    internal void RestoreCameraState(StudioViewportCameraState state)
        => RestoreConfiguredCameraState(state);

    internal void FocusSelection() => FrameSelection();

    private void UpdateToolbarDensity()
    {
        var narrow = Size.X > 0 && Size.X < 850;
        _interactionHint.Visible = !narrow;
        UpdateViewportModeBadge(narrow);
    }

    internal void SetSelection(EntityId? entityId)
        => SetSelections(entityId is null ? [] : [entityId], entityId);

    internal int SetDisplayFilter(EditorDisplayFilter filter)
    {
        _displayFilter = filter ?? throw new ArgumentNullException(nameof(filter));
        var visible = ApplyDisplayFilter();
        UpdateViewportModeSummary();
        return visible;
    }

    private int ApplyDisplayFilter()
    {
        VisibleEntityCount = 0;
        GhostedEntityCount = 0;
        _editableEntityIds.Clear();
        var project = _session?.Project;
        var area = ViewArea;
        if (project is null || area is null)
        {
            RefreshHumanPickingProxies();
            return 0;
        }

        var blueprints = project.Blueprints.ToDictionary(candidate => candidate.Id);
        var selectedLayerEntities = _displayFilter.VisibleLayerIds.Count == 0
            ? null
            : area.Authoring.Layers
                .Where(layer => _displayFilter.VisibleLayerIds.Contains(layer.Id))
                .SelectMany(layer => layer.EntityIds)
                .ToHashSet();
        foreach (var entity in area.Entities)
        {
            if (!_entityNodes.TryGetValue(entity.Id, out var node) ||
                !blueprints.TryGetValue(entity.BlueprintId, out var blueprint))
            {
                continue;
            }

            var matches = _displayFilter.VisibleKinds.Contains(blueprint.Kind) &&
                (selectedLayerEntities is null || selectedLayerEntities.Contains(entity.Id)) &&
                (blueprint.Kind != BlueprintKind.Trigger ||
                 _displayFilter.VisibleVolumeKinds.Contains(EditorVolumeKind.Trigger)) &&
                _displayFilter.RequiredTags.All(tag =>
                    entity.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(_displayFilter.Author) ||
                 blueprint.Provenance.Author.Contains(_displayFilter.Author, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(_displayFilter.State) ||
                 blueprint.PaletteOrigin.ToString().Contains(_displayFilter.State, StringComparison.OrdinalIgnoreCase)) &&
                (!_displayFilter.IsolateSelection || _selectedEntityIds.Contains(entity.Id));
            node.Visible = matches || _displayFilter.GhostHidden;
            SetGhosted(node, !matches && _displayFilter.GhostHidden);
            if (matches)
            {
                VisibleEntityCount++;
            }
            else if (_displayFilter.GhostHidden)
            {
                GhostedEntityCount++;
            }
            if (IsHumanPickable(matches, _displayFilter.GhostHidden, _readOnly))
            {
                _editableEntityIds.Add(entity.Id);
            }
        }

        RefreshHumanPickingProxies();
        RefreshSelectionVisuals();
        _summary.Text = $"{area.DisplayName} · {VisibleEntityCount}/{EntityCount} objets visibles" +
            (GhostedEntityCount == 0 ? string.Empty : $" · {GhostedEntityCount} fantômes");
        return VisibleEntityCount;
    }

    private static void SetGhosted(Node node, bool ghosted)
    {
        foreach (var child in node.GetChildren())
        {
            SetGhosted(child, ghosted);
        }
        if (node is GeometryInstance3D geometry)
        {
            geometry.Transparency = ghosted ? .48f : 0;
        }
    }

    internal void RefreshEntity(StudioSessionController session, EntityId entityId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(entityId);
        var entity = session.ActiveArea?.Entities.FirstOrDefault(candidate => candidate.Id == entityId);
        if (entity is null || !_entityNodes.TryGetValue(entityId, out var node))
        {
            Rebuild(session, frameArea: false);
            return;
        }

        node.Transform = EngineValueConverter.ToEngine(entity.Transform);
        RefreshHumanPickingProxies();
        SetSelections(session.SelectedEntityIds, session.SelectedEntityId);
    }

    internal bool PrepareEditingEvidence(StudioSessionController session, bool showContextMenu)
    {
        ArgumentNullException.ThrowIfNull(session);
        var blueprintKinds = session.Project?.Blueprints.ToDictionary(
            blueprint => blueprint.Id,
            blueprint => blueprint.Kind) ?? [];
        var entity = session.ActiveArea?.Entities
            .Where(candidate => _entityNodes.ContainsKey(candidate.Id))
            .OrderBy(candidate => blueprintKinds.TryGetValue(candidate.BlueprintId, out var kind)
                ? HumanKindPriority(kind)
                : int.MaxValue)
            .ThenBy(candidate => candidate.Id.Value)
            .FirstOrDefault();
        if (entity is null)
        {
            return false;
        }

        session.SelectEntity(entity.Id);
        SetSelection(entity.Id);
        EntitySelected?.Invoke(entity.Id);
        FrameSelection();
        if (showContextMenu)
        {
            ShowEntityContextMenu(
                entity.Id,
                SnapPlacement(entity.Transform.Position),
                new Vector2(Math.Max(12, Size.X * .55f), Math.Max(48, Size.Y * .28f)));
        }
        return GizmoVisible && (!showContextMenu || ContextMenuVisible);
    }

    internal bool PrepareRotationEvidence()
    {
        if (_selectedEntityId is null || _session?.ActiveArea is null ||
            !_entityNodes.TryGetValue(_selectedEntityId, out var node))
        {
            return false;
        }

        if (!SelectHumanTransformTool(EditorTransformTool.Rotate, reportStatus: false))
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_GIZMO_ROTATION_FAILED reason=tool_activation");
            return false;
        }

        var original = _session.ActiveArea.Entities
            .First(entity => entity.Id == _selectedEntityId)
            .Transform;
        StudioRotationPlane? selectedPlane = null;
        var pickablePlanes = new HashSet<StudioRotationPlane>();
        Vector2 startPointer = default;
        Vector2 endPointer = default;
        foreach (var sourcePlane in Enum.GetValues<StudioRotationPlane>())
        {
            var frame = ConfiguredRotationFrame(sourcePlane);
            var scale = Math.Max(_moveGizmo.Scale.X, .001f);
            foreach (var radiusRatio in new[] { .92f, .78f, .64f, .5f })
            {
                var radius = RotationPlaneInnerRadius +
                             (RotationPlaneOuterRadius - RotationPlaneInnerRadius) * radiusRatio;
                foreach (var angle in Enumerable.Range(2, 17).Select(index => index * 5f))
                {
                    var radians = angle * MathF.PI / 180f;
                    var startDirection = frame.First * MathF.Cos(radians) +
                                         frame.Second * MathF.Sin(radians);
                    var candidatePointer = FromViewportPointer(_camera.UnprojectPosition(
                        _moveGizmo.GlobalPosition + startDirection * (radius * scale)));
                    var pickedPlane = PickConfiguredRotationPlane(candidatePointer);
                    if (pickedPlane != sourcePlane ||
                        !TryPointerVectorOnConfiguredPlane(
                            candidatePointer,
                            pickedPlane.Value,
                            out var pickedDirection))
                    {
                        continue;
                    }

                    pickablePlanes.Add(sourcePlane);
                    if (selectedPlane is not null)
                    {
                        break;
                    }

                    selectedPlane = sourcePlane;
                    startPointer = candidatePointer;
                    var pickedFrame = ConfiguredRotationFrame(pickedPlane.Value);
                    var pickedAngle = MathF.Atan2(
                        pickedDirection.Dot(pickedFrame.Second),
                        pickedDirection.Dot(pickedFrame.First));
                    var endAngle = pickedAngle < MathF.PI * .25f
                        ? Math.Min(pickedAngle + MathF.PI / 6f, MathF.PI * .46f)
                        : Math.Max(pickedAngle - MathF.PI / 6f, MathF.PI * .04f);
                    var endDirection = pickedFrame.First * MathF.Cos(endAngle) +
                                       pickedFrame.Second * MathF.Sin(endAngle);
                    endPointer = FromViewportPointer(_camera.UnprojectPosition(
                        _moveGizmo.GlobalPosition + endDirection * (radius * scale)));
                    break;
                }
                if (pickablePlanes.Contains(sourcePlane))
                {
                    break;
                }
            }
        }

        if (selectedPlane is null || !TryBeginGizmoDrag(startPointer) ||
            _dragRotationPlane != selectedPlane)
        {
            CancelGizmoDrag();
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_GIZMO_ROTATION_FAILED reason=plane_pick " +
                        $"control={Size.X:0.#}x{Size.Y:0.#} viewport={_viewport.Size.X}x{_viewport.Size.Y}");
            return false;
        }

        PreviewGizmoDrag(new InputEventMouseMotion
        {
            Position = endPointer,
            ButtonMask = MouseButtonMask.Left,
        });
        var candidate = _dragCandidateTransform;
        var startRotation = EngineValueConverter.ToEngine(original.Rotation).Normalized();
        var candidateRotation = candidate is null
            ? startRotation
            : EngineValueConverter.ToEngine(candidate.Rotation).Normalized();
        var rotationChanged = MathF.Abs(startRotation.Dot(candidateRotation)) < .9999f;

        var xAxisEnd = _moveGizmo.GlobalPosition +
                       AxisVector(StudioMoveAxis.X) * (.82f * Math.Max(_moveGizmo.Scale.X, .001f));
        var axisPriority = PickGizmoAxis(FromViewportPointer(_camera.UnprojectPosition(xAxisEnd))) ==
                           StudioMoveAxis.X;
        CancelGizmoDrag();
        RefreshGizmoMaterials(null, selectedPlane);
        InteractionStatusChanged?.Invoke(
            $"Plans CATIA actifs · {selectedPlane} tourne autour de {RotationAxis(selectedPlane.Value)} · axes = translation");
        var valid = rotationChanged && axisPriority && RotationPlaneCount == 3 && pickablePlanes.Count == 3 &&
                    node.Transform == EngineValueConverter.ToEngine(original);
        GD.Print($"OPENRPG_FORGE_STUDIO_GIZMO_ROTATION_{(valid ? "PASS" : "FAILED")} " +
                 $"plane={selectedPlane} changed={rotationChanged.ToString().ToLowerInvariant()} " +
                 $"axis_priority={axisPriority.ToString().ToLowerInvariant()} planes={RotationPlaneCount} " +
                 $"pickable={pickablePlanes.Count}");
        return valid;
    }

    internal bool PrepareDeleteConfirmationEvidence()
    {
        if (_contextEntityId is null || !_contextMenu.Visible)
        {
            return false;
        }

        ShowDeleteConfirmation();
        return DeleteConfirmationVisible;
    }

    private void ClearArea()
    {
        ClearUx0CViewportArea();
        ClearArtKitState();
        ClearHumanPickingArea();
        _entityNodes.Clear();
        _editableEntityIds.Clear();
        _selectionMarker.Visible = false;
        _moveGizmo.Visible = false;
        HideContextMenu();
        if (_areaRoot is null)
        {
            return;
        }
        _world.RemoveChild(_areaRoot);
        _areaRoot.Free();
        _areaRoot = null;
    }

    private void ApplyEnvironment(AreaDefinition area)
    {
        try
        {
            var plan = AreaRenderPlanBuilder.Build(
                area.Environment,
                "desktop",
                observedDynamicLights: 2,
                observedShadowCastingLights: 1);
            var result = GodotEnvironmentAdapter.Build(plan);
            _environment.Environment = result.Environment;
            _environment.Environment.AmbientLightEnergy = Math.Max(
                _environment.Environment.AmbientLightEnergy,
                .9f);
            _environment.Environment.TonemapExposure = Math.Max(
                _environment.Environment.TonemapExposure,
                1.05f);
            _sun.LightColor = result.DirectionalLightColor;
            _sun.LightEnergy = Math.Max(result.DirectionalLightEnergy, .7f);
            _sun.ShadowEnabled = area.Environment.Lighting.Shadows;
            _viewport.PositionalShadowAtlasSize = result.ShadowAtlasSize;
            _viewport.UseHdr2D = area.Environment.Rendering.HdrOutput;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_ENVIRONMENT_FALLBACK {exception.Message}");
            _environment.Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("1f2b28"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("b8c2b8"),
                AmbientLightEnergy = .8f,
            };
        }
    }

    private Node3D CreateRepresentation(EntityInstance entity, BlueprintDefinition blueprint)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(blueprint.DisplayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        var explicitAssetId = entity.Overrides.Properties.FirstOrDefault(property =>
            string.Equals(property.Key, "visual.asset", StringComparison.Ordinal))?.Value;
        var assetId = explicitAssetId;
        if (!string.IsNullOrWhiteSpace(assetId) && _modelLibrary is not null &&
            !_unavailableVisualAssets.Contains(assetId))
        {
            try
            {
                var model = _modelLibrary.TryInstantiate(assetId);
                if (model is not null)
                {
                    root.AddChild(model);
                    AssetModelInstanceCount++;
                    return root;
                }
                _unavailableVisualAssets.Add(assetId);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException)
            {
                _unavailableVisualAssets.Add(assetId);
            }
        }

        root.AddChild(new MeshInstance3D
        {
            Name = FallbackPreviewNodeName,
            Mesh = CreateFallbackMesh(blueprint.Kind, entity.Tags),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = ColorFor(blueprint.Kind, entity.Tags),
                Roughness = .72f,
            },
        });
        return root;
    }

    private static PrimitiveMesh CreateFallbackMesh(BlueprintKind kind, IReadOnlyList<string> tags)
    {
        if (tags.Contains("pillar", StringComparer.OrdinalIgnoreCase))
        {
            return new CylinderMesh { TopRadius = .5f, BottomRadius = .5f, Height = 1.4f };
        }
        return kind switch
        {
            BlueprintKind.Creature or BlueprintKind.PlayerSpawn =>
                new CapsuleMesh { Radius = .42f, Height = 1.7f },
            BlueprintKind.Item => new SphereMesh { Radius = .35f, Height = .7f },
            BlueprintKind.Trigger => new CylinderMesh
            {
                TopRadius = .9f,
                BottomRadius = .9f,
                Height = .06f,
            },
            BlueprintKind.Door => new BoxMesh { Size = new Vector3(1.2f, 2.4f, .25f) },
            _ => new BoxMesh { Size = Vector3.One },
        };
    }

    private static Color ColorFor(BlueprintKind kind, IReadOnlyList<string> tags)
    {
        if (tags.Contains("boss", StringComparer.OrdinalIgnoreCase))
        {
            return new Color("d95b5b");
        }
        if (tags.Contains("npc", StringComparer.OrdinalIgnoreCase))
        {
            return new Color("d7a35d");
        }
        return kind switch
        {
            BlueprintKind.Creature => new Color("d8804c"),
            BlueprintKind.PlayerSpawn => new Color("68b6de"),
            BlueprintKind.Item => new Color("e4c45d"),
            BlueprintKind.Trigger => new Color(0.35f, 0.75f, 1f, .42f),
            BlueprintKind.Door => new Color("8f6749"),
            _ => new Color("849a72"),
        };
    }

    private Node3D BuildMoveGizmo()
    {
        var root = new Node3D
        {
            Name = "MoveGizmo",
            Visible = false,
        };
        foreach (var plane in Enum.GetValues<StudioRotationPlane>())
        {
            root.AddChild(BuildRotationPlane(plane));
        }
        foreach (var axis in Enum.GetValues<StudioMoveAxis>())
        {
            root.AddChild(BuildAxisHandle(axis));
        }

        root.AddChild(new MeshInstance3D
        {
            Name = "MoveGizmoOrigin",
            Mesh = new BoxMesh { Size = new Vector3(.18f, .18f, .18f) },
            MaterialOverride = GizmoMaterial(StudioStyle.Accent),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return root;
    }

    private Node3D BuildAxisHandle(StudioMoveAxis axis)
    {
        var color = AxisColor(axis);
        var materials = new List<StandardMaterial3D>();
        var root = new Node3D { Name = $"MoveGizmo{axis}" };
        var stemMaterial = GizmoMaterial(color);
        materials.Add(stemMaterial);
        root.AddChild(new MeshInstance3D
        {
            Name = "Stem",
            Position = AxisVector(axis) * .68f,
            Mesh = new BoxMesh
            {
                Size = axis switch
                {
                    StudioMoveAxis.X => new Vector3(1.28f, .055f, .055f),
                    StudioMoveAxis.Y => new Vector3(.055f, 1.28f, .055f),
                    _ => new Vector3(.055f, .055f, 1.28f),
                },
            },
            MaterialOverride = stemMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        var tipMaterial = GizmoMaterial(color);
        materials.Add(tipMaterial);
        root.AddChild(new MeshInstance3D
        {
            Name = "Arrow",
            Position = AxisVector(axis) * 1.42f,
            RotationDegrees = axis switch
            {
                StudioMoveAxis.X => new Vector3(0, 0, -90),
                StudioMoveAxis.Y => Vector3.Zero,
                _ => new Vector3(90, 0, 0),
            },
            Mesh = new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = .14f,
                Height = .36f,
                RadialSegments = 18,
            },
            MaterialOverride = tipMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _gizmoMaterials[axis] = materials;
        return root;
    }

    private Node3D BuildRotationPlane(StudioRotationPlane plane)
    {
        var color = PlaneColor(plane);
        var fillMaterial = GizmoPlaneMaterial(color, .2f, 1.15f);
        var rimMaterial = GizmoPlaneMaterial(color, .78f, 1.4f);
        var root = new Node3D { Name = $"MoveGizmoRotation{plane}" };
        root.AddChild(new MeshInstance3D
        {
            Name = "Plane",
            Mesh = BuildRotationSectorMesh(
                plane,
                RotationPlaneInnerRadius,
                RotationPlaneOuterRadius - .06f),
            MaterialOverride = fillMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        root.AddChild(new MeshInstance3D
        {
            Name = "Arc",
            Mesh = BuildRotationSectorMesh(
                plane,
                RotationPlaneOuterRadius - .06f,
                RotationPlaneOuterRadius),
            MaterialOverride = rimMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _rotationMaterials[plane] = [fillMaterial, rimMaterial];
        return root;
    }

    private static ImmediateMesh BuildRotationSectorMesh(
        StudioRotationPlane plane,
        float innerRadius,
        float outerRadius)
    {
        const int segments = 24;
        var frame = RotationFrame(plane);
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        mesh.SurfaceSetNormal(frame.Normal);
        for (var index = 0; index < segments; index++)
        {
            var firstAngle = index * MathF.PI * .5f / segments;
            var secondAngle = (index + 1) * MathF.PI * .5f / segments;
            var innerFirst = frame.First * (MathF.Cos(firstAngle) * innerRadius) +
                             frame.Second * (MathF.Sin(firstAngle) * innerRadius);
            var outerFirst = frame.First * (MathF.Cos(firstAngle) * outerRadius) +
                             frame.Second * (MathF.Sin(firstAngle) * outerRadius);
            var innerSecond = frame.First * (MathF.Cos(secondAngle) * innerRadius) +
                              frame.Second * (MathF.Sin(secondAngle) * innerRadius);
            var outerSecond = frame.First * (MathF.Cos(secondAngle) * outerRadius) +
                              frame.Second * (MathF.Sin(secondAngle) * outerRadius);
            mesh.SurfaceAddVertex(innerFirst);
            mesh.SurfaceAddVertex(outerFirst);
            mesh.SurfaceAddVertex(outerSecond);
            mesh.SurfaceAddVertex(innerFirst);
            mesh.SurfaceAddVertex(outerSecond);
            mesh.SurfaceAddVertex(innerSecond);
        }
        mesh.SurfaceEnd();
        return mesh;
    }

    private static StandardMaterial3D GizmoMaterial(Color color) => new()
    {
        AlbedoColor = color,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = 1.35f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
    };

    private static StandardMaterial3D GizmoPlaneMaterial(Color color, float alpha, float emission) => new()
    {
        AlbedoColor = new Color(color.R, color.G, color.B, alpha),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = emission,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
    };

    private PanelContainer BuildContextMenu(out Label title)
    {
        var panel = StudioStyle.Panel("EntityContextMenu", StudioStyle.Chrome, 7, 1);
        panel.Visible = false;
        panel.ZIndex = 100;
        panel.MouseFilter = MouseFilterEnum.Stop;
        panel.CustomMinimumSize = new Vector2(246, 0);
        var margin = StudioStyle.Margin(7, 7, 7, 7);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        _contextEyebrow = StudioStyle.SectionLabel(string.Empty);
        column.AddChild(_contextEyebrow);
        title = StudioStyle.Label(string.Empty, 13);
        column.AddChild(title);
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator", StudioStyle.Border);
        column.AddChild(separator);

        _entityContextActions = new VBoxContainer();
        _entityContextActions.AddThemeConstantOverride("separation", 3);
        column.AddChild(_entityContextActions);
        AddContextButton(_entityContextActions, "Tout modifier", StudioEntityContextAction.EditAll, primary: true);
        AddContextButton(_entityContextActions, "Position · rotation · échelle", StudioEntityContextAction.EditTransform);
        AddContextButton(_entityContextActions, "Propriétés locales", StudioEntityContextAction.EditProperties);
        AddContextButton(_entityContextActions, "Déclencheur au clic…", StudioEntityContextAction.EditClickTrigger);
        AddContextButton(_entityContextActions, "Tags et identité", StudioEntityContextAction.EditTags);
        _entityContextActions.AddChild(new HSeparator());
        AddContextButton(_entityContextActions, "Copier la sélection", StudioEntityContextAction.Copy);
        AddContextButton(_entityContextActions, "Couper la sélection", StudioEntityContextAction.Cut);
        AddContextButton(_entityContextActions, "Dupliquer à côté", StudioEntityContextAction.Duplicate);
        var addHere = StudioStyle.Button("Ajouter un autre objet ici…", compact: true);
        addHere.Alignment = HorizontalAlignment.Left;
        addHere.Pressed += RequestPlacement;
        _entityContextActions.AddChild(addHere);
        AddContextButton(_entityContextActions, "Cadrer la sélection", StudioEntityContextAction.FrameSelection);
        AddContextButton(_entityContextActions, "Trouver les références", StudioEntityContextAction.FindReferences);
        var delete = StudioStyle.Button("Supprimer…", compact: true);
        delete.Alignment = HorizontalAlignment.Left;
        delete.AddThemeColorOverride("font_color", StudioStyle.Danger);
        delete.AddThemeColorOverride("font_hover_color", StudioStyle.Danger.Lightened(.12f));
        delete.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Danger.Darkened(.28f), 5));
        delete.AddThemeStyleboxOverride("hover", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Danger, 5));
        delete.Pressed += ShowDeleteConfirmation;
        _entityContextActions.AddChild(delete);

        _placementContextActions = new VBoxContainer();
        _placementContextActions.Visible = false;
        _placementContextActions.AddThemeConstantOverride("separation", 3);
        column.AddChild(_placementContextActions);
        var add = StudioStyle.Button("Ajouter un objet ici…", primary: true, compact: true);
        add.Alignment = HorizontalAlignment.Left;
        add.Pressed += RequestPlacement;
        _placementContextActions.AddChild(add);
        var pasteInstances = StudioStyle.Button("Coller ici · instances", compact: true);
        pasteInstances.Alignment = HorizontalAlignment.Left;
        pasteInstances.Pressed += () => RequestClipboardPaste(SemanticPasteMode.Instance);
        _placementContextActions.AddChild(pasteInstances);
        var pasteIndependent = StudioStyle.Button("Coller ici · copies indépendantes", compact: true);
        pasteIndependent.Alignment = HorizontalAlignment.Left;
        pasteIndependent.Pressed += () => RequestClipboardPaste(SemanticPasteMode.IndependentCopy);
        _placementContextActions.AddChild(pasteIndependent);
        _placementContextActions.AddChild(StudioStyle.Label(
            "Une palette filtrable s'ouvrira dans l'inspecteur.",
            10,
            StudioStyle.Muted,
            wrap: true));
        return panel;
    }

    private void RequestClipboardPaste(SemanticPasteMode mode)
    {
        var position = _contextPlacementPosition;
        HideContextMenu();
        if (position is not null)
        {
            ClipboardPasteRequested?.Invoke(position.Value, mode);
        }
    }

    private Panel BuildDeleteConfirmation()
    {
        var panel = new Panel
        {
            Name = "EntityDeleteConfirmation",
            Visible = false,
            ZIndex = 101,
            MouseFilter = MouseFilterEnum.Stop,
            ClipContents = true,
            Size = new Vector2(246, 190),
        };
        panel.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Chrome, StudioStyle.Border, 7, 1));
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        panel.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel("CONFIRMER LA SUPPRESSION"));
        _deleteConfirmationText = StudioStyle.Label(string.Empty, 11, StudioStyle.Text, wrap: true);
        column.AddChild(_deleteConfirmationText);
        column.AddChild(StudioStyle.Label(
            "La suppression reste locale jusqu'à Enregistrer et peut être annulée avec Ctrl+Z.",
            10,
            StudioStyle.Warning,
            wrap: true));
        var confirmDelete = StudioStyle.Button("Supprimer cet objet", compact: true);
        confirmDelete.AddThemeColorOverride("font_color", StudioStyle.Danger);
        confirmDelete.AddThemeColorOverride("font_hover_color", StudioStyle.Danger.Lightened(.12f));
        confirmDelete.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Danger, 5));
        confirmDelete.AddThemeStyleboxOverride("hover", StudioStyle.Box(StudioStyle.Danger.Darkened(.5f), StudioStyle.Danger, 5));
        confirmDelete.Pressed += () => RequestContextAction(StudioEntityContextAction.Delete);
        column.AddChild(confirmDelete);
        var cancelDelete = StudioStyle.Button("Annuler", compact: true);
        cancelDelete.Pressed += CancelDeleteConfirmation;
        column.AddChild(cancelDelete);
        return panel;
    }

    private void AddContextButton(
        Container parent,
        string text,
        StudioEntityContextAction action,
        bool primary = false)
    {
        var button = StudioStyle.Button(text, primary, compact: true);
        button.Alignment = HorizontalAlignment.Left;
        button.Pressed += () => RequestContextAction(action);
        parent.AddChild(button);
    }

    private static void ApplyMirror(Node3D node, ModularTileMirrorMode mirror)
    {
        node.Scale = mirror switch
        {
            ModularTileMirrorMode.X => new Vector3(-node.Scale.X, node.Scale.Y, node.Scale.Z),
            ModularTileMirrorMode.Z => new Vector3(node.Scale.X, node.Scale.Y, -node.Scale.Z),
            _ => node.Scale,
        };
    }

    private static MeshInstance3D BuildFallbackGround(AreaDefinition area) => new()
    {
        Name = "FallbackGround",
        Mesh = new PlaneMesh
        {
            Size = new Vector2(area.Dimensions.Width, area.Dimensions.Depth),
            SubdivideWidth = Math.Clamp(area.Dimensions.Width / 2, 1, 64),
            SubdivideDepth = Math.Clamp(area.Dimensions.Depth / 2, 1, 64),
            Material = new StandardMaterial3D
            {
                AlbedoColor = new Color("40543b"),
                Roughness = .9f,
            },
        },
    };

    private EntityId? FindNearestEntity(Vector2 pointer)
    {
        if (Size.X <= 0 || Size.Y <= 0)
        {
            return null;
        }
        var scaled = new Vector2(
            pointer.X * _viewport.Size.X / Size.X,
            pointer.Y * _viewport.Size.Y / Size.Y);
        EntityId? nearest = null;
        var best = MathF.Pow(48f * _viewport.Size.X / Math.Max(Size.X, 1), 2);
        foreach (var pair in _entityNodes)
        {
            if (!_editableEntityIds.Contains(pair.Key))
            {
                continue;
            }
            if (_camera.IsPositionBehind(pair.Value.GlobalPosition))
            {
                continue;
            }
            var distance = scaled.DistanceSquaredTo(_camera.UnprojectPosition(pair.Value.GlobalPosition));
            if (distance < best)
            {
                best = distance;
                nearest = pair.Key;
            }
        }
        return nearest;
    }

    private bool TryProjectToGround(Vector2 pointer, out Vector3 ground)
    {
        ground = Vector3.Zero;
        if (Size.X <= 0 || Size.Y <= 0)
        {
            return false;
        }

        var screen = ToViewportPointer(pointer);
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        if (MathF.Abs(direction.Y) < .0001f)
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

    private static Vector3Value SnapPlacement(Vector3Value position) => new(
        MathF.Round(position.X * 4) / 4,
        MathF.Round(position.Y * 4) / 4,
        MathF.Round(position.Z * 4) / 4);

    private void FrameArea()
    {
        if (_entityNodes.Count == 0)
        {
            _cameraTarget = Vector3.Zero;
            _cameraDistance = 28;
        }
        else
        {
            var positions = _entityNodes.Values.Select(node => node.Position).ToArray();
            var minimum = positions.Aggregate((left, right) => left.Min(right));
            var maximum = positions.Aggregate((left, right) => left.Max(right));
            _cameraTarget = (minimum + maximum) * .5f;
            _cameraTarget.Y = Math.Max(_cameraTarget.Y, 0);
            var span = Math.Max(maximum.X - minimum.X, maximum.Z - minimum.Z);
            _cameraDistance = Math.Clamp(span * 1.05f + 12, 18, 420);
        }
        _cameraYaw = .72f;
        _cameraPitch = .58f;
        UpdateCamera();
    }

    private void FramePrimaryContent()
    {
        if (_entityNodes.Count < 12)
        {
            FrameArea();
            return;
        }

        var positions = _entityNodes.Values.Select(node => node.Position).ToArray();
        var x = positions.Select(position => position.X).Order().ToArray();
        var y = positions.Select(position => position.Y).Order().ToArray();
        var z = positions.Select(position => position.Z).Order().ToArray();
        var lowerQuartile = (positions.Length - 1) / 4;
        var upperQuartile = ((positions.Length - 1) * 3) / 4;
        var minimum = new Vector3(x[lowerQuartile], y[lowerQuartile], z[lowerQuartile]);
        var maximum = new Vector3(x[upperQuartile], y[upperQuartile], z[upperQuartile]);

        _cameraTarget = (minimum + maximum) * .5f;
        _cameraTarget.Y = Math.Max(_cameraTarget.Y, 0);
        var span = Math.Max(maximum.X - minimum.X, maximum.Z - minimum.Z);
        _cameraDistance = Math.Clamp(span * 1.05f + 12, 18, 420);
        _cameraYaw = .72f;
        _cameraPitch = .58f;
        UpdateCamera();
    }

    private void FrameSelection()
        => FrameCurrentSelection();

    private void UpdateCamera()
    {
        var horizontal = MathF.Cos(_cameraPitch);
        var direction = new Vector3(
            MathF.Sin(_cameraYaw) * horizontal,
            MathF.Sin(_cameraPitch),
            MathF.Cos(_cameraYaw) * horizontal);
        var position = _cameraTarget + direction * _cameraDistance;
        _camera.LookAtFromPosition(position, _cameraTarget, Vector3.Up);
        UpdateGizmoForSelection();
    }

    private static string SafeNodeName(string value) => value
        .Replace("/", "_", StringComparison.Ordinal)
        .Replace(":", "_", StringComparison.Ordinal)
        .Replace("@", "_", StringComparison.Ordinal);
}
