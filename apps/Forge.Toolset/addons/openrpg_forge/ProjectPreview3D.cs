#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Assets;
using Forge.Application.Creator;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Domain.Authoring;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ProjectPreview3D : SubViewportContainer
{
    private readonly SubViewport _viewport;
    private readonly Node3D _world;
    private readonly BlueprintDragPalette _dragPalette;
    private readonly Dictionary<EntityId, Node3D> _entityNodes = [];
    private readonly HashSet<string> _animationClipNames = new(StringComparer.Ordinal);
    private readonly PlacementValidator _placementValidator = new();
    private readonly NavigateEditorCameraHandler _cameraHandler = new(new SystemClock());
    private Camera3D? _camera;
    private Node3D? _areaRoot;
    private Node3D? _gizmo;
    private MeshInstance3D? _ghost;
    private Node3D? _navigationOverlay;
    private Func<ProjectState?>? _getState;
    private Func<string?>? _getWorkspaceRoot;
    private Func<AssetPackageManifestState?>? _getManifestState;
    private Func<AreaId?>? _getActiveAreaId;
    private Func<float>? _getGridSize;
    private Action<EntityId>? _selectEntity;
    private Func<EntityId, TransformValue, bool>? _transformEntity;
    private Func<BlueprintId, Vector3Value, EntityId?>? _placeBlueprint;
    private Action<string, bool>? _showPlacementFeedback;
    private EntityId? _selectedEntityId;
    private EntityId? _draggedEntityId;
    private TransformValue? _dragCandidate;
    private bool _dragCandidateValid;
    private EditorCameraCommandHistory? _cameraHistory;
    private Action<EditorCameraProfile>? _persistCameraProfile;
    private bool _orbitingCamera;
    private bool _panningCamera;
    private Label? _cameraModeLabel;
    private Label? _cameraSpeedLabel;
    private Label? _bookmarkSummary;
    private EditorDisplayFilter _displayFilter = EditorDisplayFilter.ShowAll;
    private WorldEnvironment? _worldEnvironment;
    private DirectionalLight3D? _referenceSun;
    private OmniLight3D? _referenceFill;
    private AreaRenderPlan? _renderPlan;
    private AreaClimatePlan? _climatePlan;
    private Node3D? _climateEffects;
    private int _precipitationEmitters;
    private int _waterSurfaces;
    private string _reverbPreset = "unavailable";
    private int _terrainVertices;
    private int _terrainTriangles;
    private int _terrainCollisionShapes;
    private int _terrainFeatureSegments;
    private int _terrainScatterLayers;
    private int _terrainScatterInstances;
    private int _volumetricDebugVolumes;
    private int _flightRouteSegments;
    private GodotAssetModelLibrary? _modelLibrary;

    public int EntityCount { get; private set; }

    public int AssetModelInstanceCount { get; private set; }

    public int AnimatedAssetModelInstanceCount { get; private set; }

    public int AnimationClipCount => _animationClipNames.Count;

    public bool GizmoVisible => _gizmo?.Visible == true;

    public bool GhostVisible => _ghost?.Visible == true;

    public int GhostedEntityCount { get; private set; }

    public bool LastPlacementWasValid { get; private set; }

    public int NavigationOverlayCellCount { get; private set; }

    public EditorCameraProfile? CameraProfile => _cameraHistory?.State.Profile;

    public string RenderProfileSummary => _renderPlan is null
        ? "unavailable"
        : $"{_renderPlan.Rendering.MaterialWorkflow}:{_renderPlan.Rendering.ToneMapping}:{_renderPlan.Sky.Mode}";

    public int RenderDiagnosticCount => _renderPlan?.Diagnostics.Count ?? 0;

    public string ClimateProfileSummary => _climatePlan is null
        ? "unavailable"
        : $"{_climatePlan.Weather.Kind}:{(_climatePlan.ActiveWindZone?.Speed ?? _climatePlan.Wind.Speed):0.###}:{_waterSurfaces}:{_reverbPreset}";

    public int PrecipitationEmitterCount => _precipitationEmitters;

    public int WaterSurfaceCount => _waterSurfaces;

    public int TerrainVertexCount => _terrainVertices;

    public int TerrainTriangleCount => _terrainTriangles;

    public int TerrainCollisionShapeCount => _terrainCollisionShapes;

    public int TerrainFeatureSegmentCount => _terrainFeatureSegments;

    public int TerrainScatterLayerCount => _terrainScatterLayers;

    public int TerrainScatterInstanceCount => _terrainScatterInstances;

    public int VolumetricDebugVolumeCount => _volumetricDebugVolumes;

    public int FlightRouteSegmentCount => _flightRouteSegments;

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

        CreateCameraToolbar();
        CreateGizmoAndGhost();
    }

    public void Initialize(
        Func<ProjectState?> getState,
        Func<string?> getWorkspaceRoot,
        Func<AssetPackageManifestState?> getManifestState,
        Func<AreaId?> getActiveAreaId,
        Func<float> getGridSize,
        Action<EntityId> selectEntity,
        Func<EntityId, TransformValue, bool> transformEntity,
        Func<BlueprintId, Vector3Value, EntityId?> placeBlueprint,
        Action<string, bool> showPlacementFeedback)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _getWorkspaceRoot = getWorkspaceRoot ?? throw new ArgumentNullException(nameof(getWorkspaceRoot));
        _getManifestState = getManifestState ?? throw new ArgumentNullException(nameof(getManifestState));
        _getActiveAreaId = getActiveAreaId ?? throw new ArgumentNullException(nameof(getActiveAreaId));
        _getGridSize = getGridSize ?? throw new ArgumentNullException(nameof(getGridSize));
        _selectEntity = selectEntity ?? throw new ArgumentNullException(nameof(selectEntity));
        _transformEntity = transformEntity ?? throw new ArgumentNullException(nameof(transformEntity));
        _placeBlueprint = placeBlueprint ?? throw new ArgumentNullException(nameof(placeBlueprint));
        _showPlacementFeedback = showPlacementFeedback ?? throw new ArgumentNullException(nameof(showPlacementFeedback));
    }

    public void SetCameraProfile(
        EditorCameraProfile profile,
        Action<EditorCameraProfile> persistCameraProfile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(persistCameraProfile);
        _cameraHistory = new EditorCameraCommandHistory(profile);
        _persistCameraProfile = persistCameraProfile;
        ApplyCameraPose();
    }

    public void Rebuild(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Clear();

        var activeAreaId = _getActiveAreaId?.Invoke();
        var area = project.Areas.FirstOrDefault(candidate => candidate.Id == activeAreaId) ??
            project.Areas.FirstOrDefault()
            ?? throw new InvalidOperationException("The project does not contain an area to preview.");
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var workspaceRoot = _getWorkspaceRoot?.Invoke();
        var manifests = _getManifestState?.Invoke()?.Manifests ?? [];
        _modelLibrary = string.IsNullOrWhiteSpace(workspaceRoot) || manifests.Count == 0
            ? null
            : new GodotAssetModelLibrary(workspaceRoot, manifests);
        ApplyEnvironment(area.Environment);
        _dragPalette.SetBlueprints(project.Blueprints);
        _areaRoot = new Node3D { Name = "AreaPreview" };
        _world.AddChild(_areaRoot);
        ApplyAreaWorld(area.World, area.Navigation);

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
        ApplyPbrMaterials(_areaRoot);
        ApplyDisplayFilter();
    }

    public void SetDisplayFilter(EditorDisplayFilter filter)
    {
        _displayFilter = filter ?? throw new ArgumentNullException(nameof(filter));
        ApplyDisplayFilter();
    }

    public int ApplyDisplayFilter()
    {
        GhostedEntityCount = 0;
        var state = _getState?.Invoke();
        var areaId = _getActiveAreaId?.Invoke();
        var area = state?.Project.Areas.FirstOrDefault(candidate => candidate.Id == areaId);
        if (state is null || area is null) return 0;

        var blueprints = state.Project.Blueprints.ToDictionary(candidate => candidate.Id);
        var visible = 0;
        foreach (var entity in area.Entities)
        {
            if (!_entityNodes.TryGetValue(entity.Id, out var node) ||
                !blueprints.TryGetValue(entity.BlueprintId, out var blueprint)) continue;
            var matches = _displayFilter.VisibleKinds.Contains(blueprint.Kind) &&
                _displayFilter.RequiredTags.All(tag => entity.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(_displayFilter.Author) ||
                    blueprint.Provenance.Author.Contains(_displayFilter.Author, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(_displayFilter.State) ||
                    blueprint.PaletteOrigin.ToString().Contains(_displayFilter.State, StringComparison.OrdinalIgnoreCase)) &&
                (!_displayFilter.IsolateSelection || entity.Id == _selectedEntityId);
            node.Visible = matches || _displayFilter.GhostHidden;
            SetGhosted(node, !matches && _displayFilter.GhostHidden);
            if (!matches && _displayFilter.GhostHidden) GhostedEntityCount++;
            if (matches) visible++;
        }

        RefreshSelectedGizmo();
        return visible;
    }

    private static void SetGhosted(Node node, bool ghosted)
    {
        foreach (var child in node.GetChildren()) SetGhosted(child, ghosted);
        if (node is not GeometryInstance3D geometry) return;
        geometry.Transparency = ghosted ? 0.82f : 0f;
    }

    public void Clear()
    {
        ClearNavigationOverlay();
        EntityCount = 0;
        AssetModelInstanceCount = 0;
        AnimatedAssetModelInstanceCount = 0;
        _animationClipNames.Clear();
        _modelLibrary = null;
        _terrainVertices = 0;
        _terrainTriangles = 0;
        _terrainCollisionShapes = 0;
        _terrainFeatureSegments = 0;
        _terrainScatterLayers = 0;
        _terrainScatterInstances = 0;
        _volumetricDebugVolumes = 0;
        _flightRouteSegments = 0;
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

    public void ShowNavigationOverlay(NavigationSurface? surface)
    {
        ClearNavigationOverlay();
        if (surface is null)
        {
            return;
        }

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        foreach (var cell in surface.WalkableCells)
        {
            var center = surface.CenterOf(cell);
            var half = surface.CellSize * 0.43f;
            var color = new Color(0.25f, 0.92f, 0.72f, 0.46f);
            AddLine(center.X - half, center.Z - half, center.X + half, center.Z - half, color);
            AddLine(center.X + half, center.Z - half, center.X + half, center.Z + half, color);
            AddLine(center.X + half, center.Z + half, center.X - half, center.Z + half, color);
            AddLine(center.X - half, center.Z + half, center.X - half, center.Z - half, color);
        }

        foreach (var edge in surface.OffMeshEdges)
        {
            var from = surface.CenterOf(edge.From);
            var to = surface.CenterOf(edge.To);
            AddLine(from.X, from.Z, to.X, to.Z, new Color(0.78f, 0.42f, 1f, 0.95f), 0.12f);
        }

        mesh.SurfaceEnd();
        _navigationOverlay = new Node3D { Name = "NavigationOverlay" };
        _navigationOverlay.AddChild(new MeshInstance3D { Mesh = mesh });
        _world.AddChild(_navigationOverlay);
        NavigationOverlayCellCount = surface.WalkableCells.Count;

        void AddLine(float fromX, float fromZ, float toX, float toZ, Color color, float height = 0.06f)
        {
            mesh.SurfaceSetColor(color);
            mesh.SurfaceAddVertex(new Vector3(fromX, height, fromZ));
            mesh.SurfaceSetColor(color);
            mesh.SurfaceAddVertex(new Vector3(toX, height, toZ));
        }
    }

    private void ClearNavigationOverlay()
    {
        NavigationOverlayCellCount = 0;
        if (_navigationOverlay is null || !GodotObject.IsInstanceValid(_navigationOverlay))
        {
            _navigationOverlay = null;
            return;
        }

        _world.RemoveChild(_navigationOverlay);
        _navigationOverlay.Free();
        _navigationOverlay = null;
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
        if (_displayFilter.IsolateSelection) ApplyDisplayFilter();
    }

    public PlacementValidationResult PreviewEntityMoveForSmokeTest(EntityId entityId, Vector3Value position)
    {
        _draggedEntityId = entityId;
        return PreviewEntityMove(entityId, position);
    }

    public bool CommitPreviewMoveForSmokeTest() => CommitPreviewMove();

    public EntityId? DropBlueprintForSmokeTest(BlueprintId blueprintId, Vector3Value position) =>
        DropBlueprint(blueprintId, position);

    public bool NavigateCameraForSmokeTest(
        EditorCameraOperation operation,
        float x = 0,
        float y = 0,
        Vector3Value? target = null,
        float radius = 1,
        EditorCameraView view = EditorCameraView.Perspective,
        EditorCameraBookmarkId? bookmarkId = null,
        string? bookmarkName = null) =>
        ApplyCameraCommand(
            operation,
            x,
            y,
            target,
            radius,
            view,
            bookmarkId,
            bookmarkName,
            persist: true);

    public bool UndoCameraForSmokeTest()
    {
        if (_cameraHistory?.Undo() != true)
        {
            return false;
        }

        ApplyCameraPose();
        PersistCameraProfile();
        return true;
    }

    public bool RedoCameraForSmokeTest()
    {
        if (_cameraHistory?.Redo() != true)
        {
            return false;
        }

        ApplyCameraPose();
        PersistCameraProfile();
        return true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton cameraButton)
        {
            if (cameraButton.ButtonIndex == MouseButton.Right)
            {
                _orbitingCamera = cameraButton.Pressed;
                if (!cameraButton.Pressed)
                {
                    PersistCameraProfile();
                }
                AcceptEvent();
                return;
            }

            if (cameraButton.ButtonIndex == MouseButton.Middle)
            {
                _panningCamera = cameraButton.Pressed;
                if (!cameraButton.Pressed)
                {
                    PersistCameraProfile();
                }
                AcceptEvent();
                return;
            }

            if (cameraButton.Pressed && cameraButton.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (cameraButton.CtrlPressed && ScaleSelectedFromWheel(cameraButton.ButtonIndex == MouseButton.WheelUp))
                {
                    AcceptEvent();
                    return;
                }

                ApplyCameraCommand(
                    EditorCameraOperation.Zoom,
                    cameraButton.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                AcceptEvent();
                return;
            }
        }

        if (@event is InputEventMouseMotion cameraMotion && (_orbitingCamera || _panningCamera))
        {
            ApplyCameraCommand(
                _orbitingCamera ? EditorCameraOperation.Orbit : EditorCameraOperation.Pan,
                cameraMotion.Relative.X,
                cameraMotion.Relative.Y,
                persist: false);
            AcceptEvent();
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (HandleCameraKey(key.Keycode))
            {
                AcceptEvent();
                return;
            }
        }

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

    private bool ScaleSelectedFromWheel(bool increase)
    {
        var state = _getState?.Invoke();
        var entity = state?.Project.Areas.SelectMany(area => area.Entities)
            .FirstOrDefault(candidate => candidate.Id == _selectedEntityId);
        if (entity is null)
        {
            return false;
        }

        var factor = increase ? 1.1f : 0.9f;
        var current = entity.Transform.Scale;
        var scale = new Vector3Value(
            Math.Clamp(current.X * factor, ScaleEntitiesHandler.MinimumScale, ScaleEntitiesHandler.MaximumScale),
            Math.Clamp(current.Y * factor, ScaleEntitiesHandler.MinimumScale, ScaleEntitiesHandler.MaximumScale),
            Math.Clamp(current.Z * factor, ScaleEntitiesHandler.MinimumScale, ScaleEntitiesHandler.MaximumScale));
        return _transformEntity?.Invoke(entity.Id,
            new TransformValue(entity.Transform.Position, entity.Transform.Rotation, scale)) == true;
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

        var activeAreaId = _getActiveAreaId?.Invoke();
        var area = state.Project.Areas.FirstOrDefault(candidate => candidate.Id == activeAreaId) ??
            state.Project.Areas.First();
        return _placementValidator.Validate(
            state.Project,
            new PlacementValidationRequest(
                area.Id,
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

    private Node3D CreateRepresentation(
        EntityInstance entity,
        BlueprintDefinition blueprint)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(blueprint.DisplayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        var assetId = entity.Overrides.Properties
            .SingleOrDefault(property =>
                string.Equals(property.Key, "visual.asset", StringComparison.Ordinal))?.Value;
        var loadedAsset = false;
        if (!string.IsNullOrWhiteSpace(assetId) && _modelLibrary is not null)
        {
            try
            {
                var model = _modelLibrary.Instantiate(assetId);
                root.AddChild(model);
                AssetModelInstanceCount++;
                var animationProfile = entity.Overrides.Properties.SingleOrDefault(property =>
                    string.Equals(property.Key, "runtime.animationProfile", StringComparison.Ordinal))?.Value;
                string AnimationClip(string key, string fallback) =>
                    entity.Overrides.Properties.SingleOrDefault(property =>
                        string.Equals(property.Key, key, StringComparison.Ordinal))?.Value ?? fallback;
                var animation = animationProfile switch
                {
                    "humanoid-basic" => GodotAssetAnimationController.Configure(model),
                    "kaykit-adventurer" => GodotAssetAnimationController.Configure(
                        model,
                        idle: AnimationClip("runtime.animationIdle", "Idle"),
                        run: AnimationClip("runtime.animationRun", "Running_A"),
                        jump: AnimationClip("runtime.animationJump", "Jump_Full_Short"),
                        attack: AnimationClip("runtime.animationAttack", "1H_Melee_Attack_Slice_Horizontal"),
                        hit: AnimationClip("runtime.animationHit", "Hit_A"),
                        death: AnimationClip("runtime.animationDeath", "Death_A")),
                    _ => null,
                };
                if (animation is not null)
                {
                    GodotAssetAnimationController.Play(animation, animation.IdleClip, 0);
                    AnimatedAssetModelInstanceCount++;
                    _animationClipNames.UnionWith(animation.Clips);
                }
                loadedAsset = true;
            }
            catch (Exception exception) when (
                exception is IOException or ArgumentException or InvalidOperationException)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_TOOLSET_ASSET_VISUAL_FAILED entity={entity.Id} asset={assetId} " +
                    $"reason={exception.Message}");
            }
        }

        if (!loadedAsset)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = CreateMesh(blueprint.Kind, entity.Tags),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = ColorFor(blueprint.Kind, entity.Tags),
                    Roughness = 0.72f,
                },
            });
        }

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

    private void CreateCameraToolbar()
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(246, 14),
            CustomMinimumSize = new Vector2(458, 112),
            MouseFilter = MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.07f, 0.13f, 0.94f),
            BorderColor = new Color("47365e"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        });
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 8);
        column.AddChild(heading);
        _cameraModeLabel = new Label
        {
            Text = "CAMÉRA · PERSPECTIVE",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _cameraModeLabel.AddThemeColorOverride("font_color", new Color("ffb07c"));
        heading.AddChild(_cameraModeLabel);
        _cameraSpeedLabel = new Label { Text = "Vitesse 1×" };
        _cameraSpeedLabel.AddThemeColorOverride("font_color", new Color("b9aac8"));
        heading.AddChild(_cameraSpeedLabel);

        var views = new HBoxContainer();
        views.AddThemeConstantOverride("separation", 5);
        column.AddChild(views);
        AddCameraButton(views, "Cadrer", () => FocusCameraSelection());
        AddCameraButton(views, "3/4", () => SetCameraView(EditorCameraView.Perspective));
        AddCameraButton(views, "Dessus", () => SetCameraView(EditorCameraView.Top));
        AddCameraButton(views, "Face", () => SetCameraView(EditorCameraView.Front));
        AddCameraButton(views, "Gauche", () => SetCameraView(EditorCameraView.Left));
        AddCameraButton(views, "−", () => ChangeCameraSpeed(-0.25f));
        AddCameraButton(views, "+", () => ChangeCameraSpeed(0.25f));

        var helpRow = new HBoxContainer();
        helpRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(helpRow);
        var help = new Label
        {
            Text = "Clic droit orbite · milieu déplace · molette zoome · WASD pilote",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        help.AddThemeColorOverride("font_color", new Color("9587a5"));
        helpRow.AddChild(help);
        _bookmarkSummary = new Label { Text = "Aucun repère" };
        _bookmarkSummary.AddThemeColorOverride("font_color", new Color("b9aac8"));
        helpRow.AddChild(_bookmarkSummary);
        AddCameraButton(helpRow, "+ Repère", SaveCameraBookmark);
        AddCameraButton(helpRow, "Rappeler", RecallLatestCameraBookmark);
    }

    private static void AddCameraButton(Container parent, string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 30),
            FocusMode = FocusModeEnum.None,
        };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private bool HandleCameraKey(Key key)
    {
        const float keyboardStep = 24;
        return key switch
        {
            Key.F => FocusCameraSelection(),
            Key.W => ApplyCameraCommand(EditorCameraOperation.Pan, y: keyboardStep),
            Key.S => ApplyCameraCommand(EditorCameraOperation.Pan, y: -keyboardStep),
            Key.A => ApplyCameraCommand(EditorCameraOperation.Pan, x: keyboardStep),
            Key.D => ApplyCameraCommand(EditorCameraOperation.Pan, x: -keyboardStep),
            Key.Q => ApplyCameraCommand(EditorCameraOperation.Zoom, x: -1),
            Key.E => ApplyCameraCommand(EditorCameraOperation.Zoom, x: 1),
            _ => false,
        };
    }

    private bool FocusCameraSelection()
    {
        if (_selectedEntityId is not null && _entityNodes.TryGetValue(_selectedEntityId, out var selected))
        {
            var scale = selected.Scale;
            var radius = Math.Max(1, Math.Max(scale.X, Math.Max(scale.Y, scale.Z)));
            return ApplyCameraCommand(
                EditorCameraOperation.Focus,
                target: ToValue(selected.Position),
                radius: radius);
        }

        if (_entityNodes.Count == 0)
        {
            return ApplyCameraCommand(EditorCameraOperation.Focus, target: Vector3Value.Zero, radius: 5);
        }

        var center = new Vector3Value(
            _entityNodes.Values.Average(node => node.Position.X),
            _entityNodes.Values.Average(node => node.Position.Y),
            _entityNodes.Values.Average(node => node.Position.Z));
        var radiusFromCenter = _entityNodes.Values.Max(node => node.Position.DistanceTo(EngineValueConverter.ToEngine(center)));
        return ApplyCameraCommand(
            EditorCameraOperation.Focus,
            target: center,
            radius: Math.Max(2, radiusFromCenter));
    }

    private void SetCameraView(EditorCameraView view) =>
        ApplyCameraCommand(EditorCameraOperation.SetView, view: view);

    private void ChangeCameraSpeed(float delta)
    {
        if (_cameraHistory is null)
        {
            return;
        }

        ApplyCameraCommand(
            EditorCameraOperation.SetSpeed,
            x: Math.Clamp(_cameraHistory.State.Profile.NavigationSpeed + delta, 0.1f, 20f));
    }

    private void SaveCameraBookmark()
    {
        if (_cameraHistory is null)
        {
            return;
        }

        var index = _cameraHistory.State.Profile.Bookmarks.Count + 1;
        ApplyCameraCommand(
            EditorCameraOperation.SaveBookmark,
            bookmarkId: EditorCameraBookmarkId.From(Guid.NewGuid()),
            bookmarkName: $"Repère {index}");
    }

    private void RecallLatestCameraBookmark()
    {
        var bookmark = _cameraHistory?.State.Profile.Bookmarks.LastOrDefault();
        if (bookmark is not null)
        {
            ApplyCameraCommand(EditorCameraOperation.RecallBookmark, bookmarkId: bookmark.Id);
        }
    }

    private bool ApplyCameraCommand(
        EditorCameraOperation operation,
        float x = 0,
        float y = 0,
        Vector3Value? target = null,
        float radius = 1,
        EditorCameraView view = EditorCameraView.Perspective,
        EditorCameraBookmarkId? bookmarkId = null,
        string? bookmarkName = null,
        bool persist = true)
    {
        if (_cameraHistory is null)
        {
            return false;
        }

        var command = new NavigateEditorCameraCommand(
            CommandId.From(Guid.NewGuid()),
            _cameraHistory.State.Revision,
            operation,
            x,
            y,
            target,
            radius,
            view,
            bookmarkId,
            bookmarkName,
            DryRun: false);
        var preview = _cameraHandler.Handle(_cameraHistory.State, command with { DryRun = true });
        var applied = preview.Succeeded
            ? _cameraHandler.Handle(_cameraHistory.State, command)
            : preview;
        if (!applied.Succeeded)
        {
            _showPlacementFeedback?.Invoke(
                $"{applied.Diagnostics[0].Code} · {applied.Diagnostics[0].Message}",
                false);
            return false;
        }

        _cameraHistory.Record(applied);
        ApplyCameraPose();
        if (persist)
        {
            PersistCameraProfile();
        }

        return true;
    }

    private void ApplyCameraPose()
    {
        if (_camera is null || _cameraHistory is null)
        {
            return;
        }

        var profile = _cameraHistory.State.Profile;
        var pose = profile.Pose;
        var yaw = pose.YawDegrees * (MathF.PI / 180f);
        var pitch = pose.PitchDegrees * (MathF.PI / 180f);
        var horizontal = pose.Distance * MathF.Cos(pitch);
        var offset = new Vector3(
            horizontal * MathF.Sin(yaw),
            pose.Distance * MathF.Sin(pitch),
            horizontal * MathF.Cos(yaw));
        var pivot = EngineValueConverter.ToEngine(pose.Pivot);
        _camera.Projection = pose.Projection == EditorCameraProjection.Perspective
            ? Camera3D.ProjectionType.Perspective
            : Camera3D.ProjectionType.Orthogonal;
        _camera.Fov = pose.FieldOfView;
        _camera.Size = pose.OrthographicSize;
        _camera.LookAtFromPosition(pivot + offset, pivot, Vector3.Up);
        _cameraModeLabel!.Text = pose.Projection == EditorCameraProjection.Perspective
            ? "CAMÉRA · PERSPECTIVE"
            : "CAMÉRA · ORTHOGRAPHIQUE";
        _cameraSpeedLabel!.Text = $"Vitesse {profile.NavigationSpeed:0.##}×";
        _bookmarkSummary!.Text = profile.Bookmarks.Count == 0
            ? "Aucun repère"
            : $"{profile.Bookmarks.Count} repère(s)";
    }

    private void PersistCameraProfile()
    {
        if (_cameraHistory is not null)
        {
            _persistCameraProfile?.Invoke(_cameraHistory.State.Profile);
        }
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

        _referenceSun = new DirectionalLight3D
        {
            Name = "PreviewSun",
            RotationDegrees = new Vector3(-58, -32, 0),
            LightColor = new Color("e8d6bc"),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        };
        _world.AddChild(_referenceSun);
        _referenceFill = new OmniLight3D
        {
            Name = "PreviewFill",
            Position = new Vector3(0, 5, -7),
            OmniRange = 18,
            LightColor = new Color("ff7043"),
            LightEnergy = 4,
        };
        _world.AddChild(_referenceFill);
        _worldEnvironment = new WorldEnvironment { Name = "PreviewEnvironment" };
        _world.AddChild(_worldEnvironment);
    }

    private void ApplyEnvironment(AreaEnvironmentProfile profile)
    {
        _renderPlan = AreaRenderPlanBuilder.Build(profile, "desktop", observedDynamicLights: 2, observedShadowCastingLights: 1);
        var result = GodotEnvironmentAdapter.Build(_renderPlan);
        _worldEnvironment!.Environment = result.Environment;
        _viewport.PositionalShadowAtlasSize = result.ShadowAtlasSize;
        _referenceSun!.LightColor = result.DirectionalLightColor;
        _referenceSun.LightEnergy = result.DirectionalLightEnergy;
        _referenceSun.ShadowEnabled = profile.Lighting.Shadows;
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_TOOLSET_RENDER_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_RENDER_PROFILE_APPLIED pbr={profile.Rendering.MaterialWorkflow.ToString().ToLowerInvariant()} " +
            $"tone={profile.Rendering.ToneMapping.ToString().ToLowerInvariant()} hdr={profile.Rendering.HdrOutput.ToString().ToLowerInvariant()} " +
            $"perpixel={profile.Rendering.PerPixelLighting.ToString().ToLowerInvariant()} sky={profile.Sky.Mode.ToString().ToLowerInvariant()} " +
            $"lights=2/{_renderPlan.LightBudget.MaximumDynamicLights}");
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_RENDER_BACKEND method={RenderingServer.GetCurrentRenderingMethod()} " +
            $"driver={RenderingServer.GetCurrentRenderingDriverName()}");
        _climatePlan = AreaClimatePlanBuilder.Build(
            profile,
            profile.Weather.InitialStateId,
            Vector3Value.Zero,
            "desktop");
        if (_climateEffects is not null)
        {
            _world.RemoveChild(_climateEffects);
            _climateEffects.QueueFree();
        }

        var climate = GodotClimateAdapter.Build(_climatePlan, configureRuntimeAudio: false);
        _climateEffects = climate.Root;
        _precipitationEmitters = climate.PrecipitationEmitters;
        _waterSurfaces = climate.WaterSurfaces;
        _reverbPreset = climate.ReverbPreset;
        _world.AddChild(_climateEffects);
        foreach (var diagnostic in climate.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_TOOLSET_CLIMATE_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        var effectiveWindSpeed = _climatePlan.ActiveWindZone?.Speed ?? _climatePlan.Wind.Speed;
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_CLIMATE_APPLIED wind={effectiveWindSpeed:0.###} " +
            $"weather={_climatePlan.Weather.Kind.ToString().ToLowerInvariant()} particles={_precipitationEmitters} " +
            $"water={_waterSurfaces} reverb={_reverbPreset}");
    }

    private void ApplyAreaWorld(AreaWorldProfile profile, AreaNavigationDefinition navigation)
    {
        var result = GodotTerrainAdapter.Build(
            profile,
            _modelLibrary is null ? null : _modelLibrary.ResolveScatterMesh);
        var surfaceNavigation = GodotSurfaceNavigationAdapter.Build(profile, navigation);
        result.Root.AddChild(surfaceNavigation.Root);
        _areaRoot!.AddChild(result.Root);
        _terrainVertices = result.Vertices;
        _terrainTriangles = result.Triangles;
        _terrainCollisionShapes = result.CollisionShapes;
        _terrainFeatureSegments = result.FeatureSegments;
        _terrainScatterLayers = result.ScatterLayers;
        _terrainScatterInstances = result.ScatterInstances;
        _volumetricDebugVolumes = result.DebugVolumes;
        _flightRouteSegments = result.FlightRouteSegments;
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_TOOLSET_TERRAIN_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }
        foreach (var diagnostic in surfaceNavigation.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_TOOLSET_NAVIGATION_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_TERRAIN_APPLIED provider={profile.Terrain.Provider.ToString().ToLowerInvariant()} " +
            $"vertices={result.Vertices} triangles={result.Triangles} collisions={result.CollisionShapes} " +
            $"features={result.FeatureSegments} volumes={result.DebugVolumes} routes={result.FlightRouteSegments} " +
            $"scatter_layers={result.ScatterLayers} scatter_instances={result.ScatterInstances} " +
            $"water_surfaces={result.WaterSurfaces} water_volumes={result.WaterVolumes} " +
            $"nav_regions={surfaceNavigation.Regions} nav_polygons={surfaceNavigation.Polygons} " +
            $"nav_blocked={surfaceNavigation.BlockedTriangles} nav_links={surfaceNavigation.Links}");
    }

    private void ApplyPbrMaterials(Node node)
    {
        if (_renderPlan is null) return;
        if (node is MeshInstance3D { MaterialOverride: StandardMaterial3D material } &&
            material.ShadingMode != BaseMaterial3D.ShadingModeEnum.Unshaded)
        {
            GodotEnvironmentAdapter.ApplyPbrDefaults(material, _renderPlan);
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyPbrMaterials(child);
        }
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
