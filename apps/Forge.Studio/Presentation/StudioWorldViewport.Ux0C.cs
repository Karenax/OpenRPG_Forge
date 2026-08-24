using Forge.Application.Creator;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldViewport
{
    private readonly HashSet<EntityId> _selectedEntityIds = [];
    private readonly Dictionary<EntityId, MeshInstance3D> _secondarySelectionMarkers = [];
    private readonly Dictionary<StudioScaleAxis, MeshInstance3D> _scaleHandles = [];
    private readonly Dictionary<StudioScaleAxis, StandardMaterial3D> _scaleMaterials = [];
    private readonly Dictionary<EntityId, TransformValue> _dragStartTransforms = [];
    private Node3D _scaleGizmo = null!;
    private Node3D? _diagnosticOverlay;
    private StudioScaleAxis? _dragScaleAxis;
    private EditorTransformTool? _dragTool;
    private Vector3Value? _dragCommandValue;
    private EditorViewportMode _viewportMode = EditorViewportMode.Editing;
    private float _authoredSunEnergy = 1;
    private bool _authoredSunShadows = true;
    private float _authoredEditorFillEnergy = .62f;
    private float _authoredFillEnergy = 3.2f;

    internal event Action<IReadOnlyList<EntityId>, EntityId?>? EntitiesSelected;

    internal event Action<EditorTransformTool, Vector3Value, EditorTransformAlignment>?
        EntityManipulationRequested;

    internal int SelectedEntityCount => _selectedEntityIds.Count;

    internal EditorViewportMode ViewportMode => _viewportMode;

    internal EditorTransformTool ActiveTransformTool =>
        _session?.WorkspaceProfile.Shell.DirectManipulation.Transform.Tool ?? EditorTransformTool.Translate;

    internal int ScaleGizmoHandleCount => _scaleHandles.Count;

    internal string? DiagnosticOverlayName => _diagnosticOverlay?.Name.ToString();

    internal EditorCameraProjection CameraProjection =>
        _camera.Projection == Camera3D.ProjectionType.Perspective
            ? EditorCameraProjection.Perspective
            : EditorCameraProjection.Orthographic;

    internal void FrameAreaForEvidence() => FrameAreaAndRecord();

    private void InitializeUx0CViewport()
    {
        _scaleGizmo = BuildScaleGizmo();
        _moveGizmo.AddChild(_scaleGizmo);
        RefreshConfiguredGizmoVisibility();
    }

    internal void ApplyCameraProfile(EditorCameraProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var pose = profile.Pose;
        _cameraTarget = new Vector3(pose.Pivot.X, pose.Pivot.Y, pose.Pivot.Z);
        _cameraYaw = Mathf.DegToRad(pose.YawDegrees);
        _cameraPitch = Mathf.DegToRad(pose.PitchDegrees);
        _cameraDistance = pose.Distance;
        _camera.Projection = pose.Projection == EditorCameraProjection.Perspective
            ? Camera3D.ProjectionType.Perspective
            : Camera3D.ProjectionType.Orthogonal;
        _camera.Fov = pose.FieldOfView;
        _camera.Size = pose.OrthographicSize;
        UpdateCamera();
    }

    internal EditorCameraPose CaptureCameraPose() => new(
        new Vector3Value(_cameraTarget.X, _cameraTarget.Y, _cameraTarget.Z),
        Mathf.RadToDeg(_cameraYaw),
        Mathf.RadToDeg(_cameraPitch),
        _cameraDistance,
        _camera.Projection == Camera3D.ProjectionType.Perspective
            ? EditorCameraProjection.Perspective
            : EditorCameraProjection.Orthographic,
        _camera.Fov,
        _camera.Size);

    private bool HandleUx0CCameraInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo ||
            key.CtrlPressed || key.AltPressed || key.MetaPressed || _readOnly || _session is null)
        {
            return false;
        }

        var pressed = key.Keycode.ToString().ToUpperInvariant();
        var profile = _session.CameraProfile;
        var action = profile.Input.Bindings.FirstOrDefault(pair => pair.Value == pressed).Key;
        const float step = 22;
        return action switch
        {
            "focus" => FrameCurrentSelectionHandled(),
            "forward" => profile.Input.PilotMode == EditorCameraPilotMode.Fly
                ? TryNavigateCamera(EditorCameraOperation.Pilot, z: step)
                : TryNavigateCamera(EditorCameraOperation.Pan, y: step),
            "back" => profile.Input.PilotMode == EditorCameraPilotMode.Fly
                ? TryNavigateCamera(EditorCameraOperation.Pilot, z: -step)
                : TryNavigateCamera(EditorCameraOperation.Pan, y: -step),
            "left" => profile.Input.PilotMode == EditorCameraPilotMode.Fly
                ? TryNavigateCamera(EditorCameraOperation.Pilot, x: -step)
                : TryNavigateCamera(EditorCameraOperation.Pan, x: step),
            "right" => profile.Input.PilotMode == EditorCameraPilotMode.Fly
                ? TryNavigateCamera(EditorCameraOperation.Pilot, x: step)
                : TryNavigateCamera(EditorCameraOperation.Pan, x: -step),
            "up" => TryNavigateCamera(EditorCameraOperation.Pilot, y: step),
            "down" => TryNavigateCamera(EditorCameraOperation.Pilot, y: -step),
            _ => false,
        };
    }

    private bool FrameCurrentSelectionHandled()
    {
        FrameCurrentSelection();
        return true;
    }

    private bool TryNavigateCamera(
        EditorCameraOperation operation,
        float x = 0,
        float y = 0,
        float z = 0)
    {
        if (_readOnly || _session is null)
        {
            return false;
        }

        var result = _session.NavigateCamera(operation, x, y, z);
        InteractionStatusChanged?.Invoke(result.Message);
        if (result.Succeeded)
        {
            ApplyCameraProfile(_session.CameraProfile);
        }
        return true;
    }

    private void RestoreConfiguredCameraState(StudioViewportCameraState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _cameraTarget = state.Target;
        _cameraYaw = state.Yaw;
        _cameraPitch = state.Pitch;
        _cameraDistance = state.Distance;
        _camera.Projection = state.Projection == EditorCameraProjection.Perspective
            ? Camera3D.ProjectionType.Perspective
            : Camera3D.ProjectionType.Orthogonal;
        _camera.Fov = state.FieldOfView;
        _camera.Size = state.OrthographicSize;
        UpdateCamera();
        if (!_readOnly && _session is not null)
        {
            var result = _session.SetCameraPose(CaptureCameraPose());
            if (!result.Succeeded)
            {
                InteractionStatusChanged?.Invoke(result.Message);
            }
        }
    }

    private void FrameAreaAndRecord()
    {
        FrameArea();
        if (!_readOnly && _session is not null)
        {
            var result = _session.SetCameraPose(CaptureCameraPose());
            InteractionStatusChanged?.Invoke(result.Message);
        }
    }

    private void FrameCurrentSelection()
    {
        var nodes = _selectedEntityIds
            .Where(_entityNodes.ContainsKey)
            .Select(id => _entityNodes[id])
            .ToArray();
        if (nodes.Length == 0)
        {
            return;
        }

        var center = new Vector3(
            nodes.Average(node => node.GlobalPosition.X),
            nodes.Average(node => node.GlobalPosition.Y),
            nodes.Average(node => node.GlobalPosition.Z));
        var radius = Math.Max(1, nodes.Max(node => node.GlobalPosition.DistanceTo(center)) + 1);
        if (!_readOnly && _session is not null)
        {
            var result = _session.NavigateCamera(
                EditorCameraOperation.Focus,
                target: new Vector3Value(center.X, center.Y, center.Z),
                radius: radius);
            InteractionStatusChanged?.Invoke(result.Message);
            if (result.Succeeded)
            {
                ApplyCameraProfile(_session.CameraProfile);
                return;
            }
        }

        _cameraTarget = center + new Vector3(0, .8f, 0);
        _cameraDistance = Math.Clamp(radius * 2.5f, 6, 80);
        UpdateCamera();
    }

    private void SelectPointerEntity(EntityId entityId, bool additive)
    {
        if (_readOnly || _session is null)
        {
            return;
        }

        var selection = _session.WorkspaceProfile.Shell.DirectManipulation.Selection;
        if (selection.LockSelection)
        {
            InteractionStatusChanged?.Invoke("Sélection verrouillée · déverrouillez-la dans Outils 3D");
            return;
        }

        var candidate = _selectedEntityIds.ToList();
        if (!additive || !selection.MultiSelectionEnabled)
        {
            candidate = [entityId];
        }
        else if (!candidate.Remove(entityId))
        {
            candidate.Add(entityId);
        }

        var active = candidate.Contains(entityId) ? entityId : candidate.LastOrDefault();
        SetSelections(candidate, active);
        EntitiesSelected?.Invoke(candidate, active);
    }

    private void SelectContextEntity(EntityId entityId)
    {
        if (_readOnly || _session is null)
        {
            return;
        }

        var candidate = _selectedEntityIds.Contains(entityId)
            ? _selectedEntityIds.ToArray()
            : [entityId];
        SetSelections(candidate, entityId);
        EntitiesSelected?.Invoke(candidate, entityId);
    }

    internal void SetSelections(IReadOnlyList<EntityId> entityIds, EntityId? activeEntityId)
    {
        ArgumentNullException.ThrowIfNull(entityIds);
        var candidate = _readOnly
            ? []
            : entityIds.Where(_entityNodes.ContainsKey).Distinct().ToArray();
        var active = activeEntityId is not null && candidate.Contains(activeEntityId)
            ? activeEntityId
            : candidate.LastOrDefault();
        var changed = !_selectedEntityIds.SetEquals(candidate) || _selectedEntityId != active;
        _selectedEntityIds.Clear();
        _selectedEntityIds.UnionWith(candidate);
        _selectedEntityId = active;
        if (changed && _displayFilter.IsolateSelection)
        {
            ApplyDisplayFilter();
            return;
        }

        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        ClearSecondarySelectionMarkers();
        if (_selectedEntityId is null || !_entityNodes.TryGetValue(_selectedEntityId, out var activeNode) ||
            !_editableEntityIds.Contains(_selectedEntityId) || _readOnly)
        {
            _selectionMarker.Visible = false;
            _moveGizmo.Visible = false;
            HideHumanSelectionBounds();
            HideContextMenu();
            return;
        }

        PlaceHumanSelectionMarker(_selectionMarker, _selectedEntityId, activeNode, primary: true);
        _selectionMarker.Visible = true;
        RefreshHumanSelectionBounds(_selectedEntityId);
        foreach (var entityId in _selectedEntityIds.Where(id => id != _selectedEntityId && _editableEntityIds.Contains(id)))
        {
            var marker = BuildSecondarySelectionMarker();
            PlaceHumanSelectionMarker(marker, entityId, _entityNodes[entityId], primary: false);
            _world.AddChild(marker);
            _secondarySelectionMarkers[entityId] = marker;
        }
        UpdateGizmoForSelection();
    }

    private MeshInstance3D BuildSecondarySelectionMarker() => new()
    {
        Name = "MultiSelectionMarker",
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Mesh = new CylinderMesh
        {
            TopRadius = .66f,
            BottomRadius = .66f,
            Height = .025f,
            RadialSegments = 40,
        },
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(StudioStyle.Accent, .34f),
            EmissionEnabled = true,
            Emission = StudioStyle.Accent,
            EmissionEnergyMultiplier = 1.25f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        },
    };

    private void ClearSecondarySelectionMarkers()
    {
        foreach (var marker in _secondarySelectionMarkers.Values)
        {
            if (GodotObject.IsInstanceValid(marker))
            {
                marker.GetParent()?.RemoveChild(marker);
                marker.Free();
            }
        }
        _secondarySelectionMarkers.Clear();
    }

    private void UpdateGizmoForSelection()
    {
        if (!_selectionMarker.Visible || _selectedEntityId is null ||
            !_entityNodes.TryGetValue(_selectedEntityId, out var activeNode) ||
            _viewportMode != EditorViewportMode.Editing)
        {
            _moveGizmo.Visible = false;
            return;
        }

        var transform = _session?.WorkspaceProfile.Shell.DirectManipulation.Transform;
        var pivot = transform?.PivotMode == EditorPivotMode.SelectionCenter && _selectedEntityIds.Count > 1
            ? new Vector3(
                _selectedEntityIds.Where(_entityNodes.ContainsKey).Average(id => _entityNodes[id].GlobalPosition.X),
                _selectedEntityIds.Where(_entityNodes.ContainsKey).Average(id => _entityNodes[id].GlobalPosition.Y),
                _selectedEntityIds.Where(_entityNodes.ContainsKey).Average(id => _entityNodes[id].GlobalPosition.Z))
            : activeNode.GlobalPosition;
        UpdateGizmo(pivot);
        _moveGizmo.Quaternion = transform?.CoordinateSpace == EditorCoordinateSpace.Local
            ? activeNode.GlobalTransform.Basis.GetRotationQuaternion()
            : Quaternion.Identity;
        RefreshConfiguredGizmoVisibility();
    }

    internal void RefreshManipulationProfile()
    {
        RefreshConfiguredGizmoVisibility();
        RefreshSelectionVisuals();
    }

    private void RefreshConfiguredGizmoVisibility()
    {
        if (_scaleGizmo is null)
        {
            return;
        }
        var tool = ActiveTransformTool;
        _interactionHint.Text = tool switch
        {
            EditorTransformTool.Rotate =>
                "Tourner [2] · glissez une surface colorée · clic droit éditer · glisser droit orbiter",
            EditorTransformTool.Scale =>
                "Redimensionner [3] · glissez X, Y, Z ou uniforme · clic droit éditer · glisser droit orbiter",
            _ =>
                "Déplacer [1] · glissez une flèche X, Y ou Z · clic droit éditer · glisser droit orbiter",
        };
        foreach (var child in _moveGizmo.GetChildren().OfType<Node3D>())
        {
            if (child == _scaleGizmo)
            {
                child.Visible = tool == EditorTransformTool.Scale;
            }
            else if (child.Name.ToString().StartsWith("MoveGizmoRotation", StringComparison.Ordinal))
            {
                child.Visible = tool == EditorTransformTool.Rotate;
            }
            else if (child.Name.ToString() is "MoveGizmoX" or "MoveGizmoY" or "MoveGizmoZ")
            {
                child.Visible = tool == EditorTransformTool.Translate;
            }
            else
            {
                child.Visible = true;
            }
        }
        RefreshHumanTransformToolbar();
    }

    private bool TryBeginConfiguredGizmoDrag(Vector2 pointer)
    {
        var area = ViewArea;
        if (_readOnly || _selectedEntityId is null || !_moveGizmo.Visible || area is null || _session is null)
        {
            return false;
        }

        var tool = ActiveTransformTool;
        var axis = tool == EditorTransformTool.Translate ? PickConfiguredMoveAxis(pointer) : null;
        var plane = tool == EditorTransformTool.Rotate ? PickConfiguredRotationPlane(pointer) : null;
        var scaleAxis = tool == EditorTransformTool.Scale ? PickScaleAxis(pointer) : null;
        if (axis is null && plane is null && scaleAxis is null)
        {
            return false;
        }

        var active = area.Entities.FirstOrDefault(entity => entity.Id == _selectedEntityId);
        if (active is null)
        {
            return false;
        }

        var rotationVector = Vector3.Zero;
        if (plane is not null && !TryPointerVectorOnConfiguredPlane(pointer, plane.Value, out rotationVector))
        {
            return false;
        }

        _dragStartTransforms.Clear();
        foreach (var entity in area.Entities.Where(entity => _selectedEntityIds.Contains(entity.Id)))
        {
            _dragStartTransforms[entity.Id] = entity.Transform;
        }
        _dragTool = tool;
        _dragAxis = axis;
        _dragRotationPlane = plane;
        _dragScaleAxis = scaleAxis;
        _dragStartTransform = active.Transform;
        _dragCandidateTransform = active.Transform;
        _dragCommandValue = null;
        _dragStartPointer = ToViewportPointer(pointer);
        _dragStartWorldPosition = _moveGizmo.GlobalPosition;
        _dragPreviousRotationVector = rotationVector;
        _dragAccumulatedRotationRadians = 0;
        RefreshGizmoMaterials(axis, plane);
        RefreshScaleGizmoMaterials(scaleAxis);

        var config = _session.WorkspaceProfile.Shell.DirectManipulation.Transform;
        InteractionStatusChanged?.Invoke(tool switch
        {
            EditorTransformTool.Translate =>
                $"Translation {axis} · pas {config.TranslationSnap:0.###} m · Maj pour affiner",
            EditorTransformTool.Rotate =>
                $"Rotation {plane} · pas {config.RotationSnapDegrees:0.###}° · Maj pour affiner",
            _ => $"Échelle {scaleAxis} · pas {config.ScaleSnap:0.###} · Maj pour affiner",
        });
        return true;
    }

    private void PreviewConfiguredGizmoDrag(InputEventMouseMotion motion)
    {
        if (!GizmoDragActive || _dragStartTransforms.Count == 0 || _session is null)
        {
            CancelConfiguredGizmoDrag();
            return;
        }

        switch (_dragTool)
        {
            case EditorTransformTool.Translate:
                PreviewConfiguredTranslation(motion);
                break;
            case EditorTransformTool.Rotate:
                PreviewConfiguredRotation(motion);
                break;
            case EditorTransformTool.Scale:
                PreviewConfiguredScale(motion);
                break;
        }
    }

    private void PreviewConfiguredTranslation(InputEventMouseMotion motion)
    {
        var axis = _dragAxis!.Value;
        var localAxis = AxisVector(axis);
        var worldAxis = ConfiguredWorldAxis(localAxis);
        var scale = Math.Max(_moveGizmo.Scale.X, .001f);
        var endScreen = _camera.UnprojectPosition(_dragStartWorldPosition + worldAxis * 1.65f * scale);
        var originScreen = _camera.UnprojectPosition(_dragStartWorldPosition);
        var screenAxis = endScreen - originScreen;
        if (screenAxis.LengthSquared() < 1)
        {
            return;
        }

        var pointerDelta = ToViewportPointer(motion.Position) - _dragStartPointer;
        var distance = pointerDelta.Dot(screenAxis.Normalized()) / screenAxis.Length() * 1.65f * scale;
        var configured = _session!.WorkspaceProfile.Shell.DirectManipulation.Transform;
        var snap = motion.ShiftPressed
            ? Math.Max(.001f, configured.TranslationSnap / 5f)
            : configured.TranslationSnap;
        distance = MathF.Round(distance / snap) * snap;
        _dragCommandValue = new Vector3Value(
            localAxis.X * distance,
            localAxis.Y * distance,
            localAxis.Z * distance);
        ApplyPreviewTransforms(_dragCommandValue.Value, EditorTransformTool.Translate);
        InteractionStatusChanged?.Invoke(
            $"Translation {axis} · Δ {distance:0.###} m · {_selectedEntityIds.Count} objet(s)");
    }

    private void PreviewConfiguredRotation(InputEventMouseMotion motion)
    {
        var plane = _dragRotationPlane!.Value;
        if (!TryPointerVectorOnConfiguredPlane(motion.Position, plane, out var currentVector))
        {
            return;
        }

        var normal = ConfiguredWorldAxis(AxisVector(RotationAxis(plane)));
        var increment = MathF.Atan2(
            normal.Dot(_dragPreviousRotationVector.Cross(currentVector)),
            Math.Clamp(_dragPreviousRotationVector.Dot(currentVector), -1, 1));
        if (!float.IsFinite(increment))
        {
            return;
        }

        _dragAccumulatedRotationRadians += increment;
        _dragPreviousRotationVector = currentVector;
        var configured = _session!.WorkspaceProfile.Shell.DirectManipulation.Transform;
        var snap = motion.ShiftPressed
            ? Math.Max(.1f, configured.RotationSnapDegrees / 5f)
            : configured.RotationSnapDegrees;
        var degrees = MathF.Round(_dragAccumulatedRotationRadians * 180f / MathF.PI / snap) * snap;
        var axis = RotationAxis(plane);
        _dragCommandValue = axis switch
        {
            StudioMoveAxis.X => new Vector3Value(degrees, 0, 0),
            StudioMoveAxis.Y => new Vector3Value(0, degrees, 0),
            _ => new Vector3Value(0, 0, degrees),
        };
        ApplyPreviewTransforms(_dragCommandValue.Value, EditorTransformTool.Rotate);
        InteractionStatusChanged?.Invoke(
            $"Rotation {axis} · {degrees:0.###}° · {_selectedEntityIds.Count} objet(s)");
    }

    private void PreviewConfiguredScale(InputEventMouseMotion motion)
    {
        var axis = _dragScaleAxis!.Value;
        var screenDirection = axis == StudioScaleAxis.Uniform
            ? new Vector2(1, -1).Normalized()
            : (_camera.UnprojectPosition(_scaleHandles[axis].GlobalPosition) -
               _camera.UnprojectPosition(_dragStartWorldPosition)).Normalized();
        var pointerDelta = ToViewportPointer(motion.Position) - _dragStartPointer;
        var raw = 1 + pointerDelta.Dot(screenDirection) / 120f;
        var configured = _session!.WorkspaceProfile.Shell.DirectManipulation.Transform;
        var snap = motion.ShiftPressed
            ? Math.Max(.005f, configured.ScaleSnap / 5f)
            : configured.ScaleSnap;
        var factor = Math.Clamp(MathF.Round(raw / snap) * snap, .05f, 20f);
        _dragCommandValue = axis switch
        {
            StudioScaleAxis.X => new Vector3Value(factor, 1, 1),
            StudioScaleAxis.Y => new Vector3Value(1, factor, 1),
            StudioScaleAxis.Z => new Vector3Value(1, 1, factor),
            _ => new Vector3Value(factor, factor, factor),
        };
        ApplyPreviewTransforms(_dragCommandValue.Value, EditorTransformTool.Scale);
        InteractionStatusChanged?.Invoke(
            $"Échelle {axis} · ×{factor:0.###} · {_selectedEntityIds.Count} objet(s)");
    }

    private void ApplyPreviewTransforms(Vector3Value value, EditorTransformTool tool)
    {
        var config = _session!.WorkspaceProfile.Shell.DirectManipulation.Transform;
        var activeStart = _selectedEntityId is not null && _dragStartTransforms.TryGetValue(_selectedEntityId, out var active)
            ? active
            : _dragStartTransforms.Values.First();
        var pivot = config.PivotMode switch
        {
            EditorPivotMode.ActiveObject => activeStart.Position,
            EditorPivotMode.SelectionCenter => new Vector3Value(
                _dragStartTransforms.Values.Average(transform => transform.Position.X),
                _dragStartTransforms.Values.Average(transform => transform.Position.Y),
                _dragStartTransforms.Values.Average(transform => transform.Position.Z)),
            _ => Vector3Value.Zero,
        };

        var localValue = new Vector3(value.X, value.Y, value.Z);
        var localRotation = Quaternion.FromEuler(new Vector3(
            Mathf.DegToRad(value.X),
            Mathf.DegToRad(value.Y),
            Mathf.DegToRad(value.Z)));
        var activeQuaternion = EngineValueConverter.ToEngine(activeStart.Rotation).Normalized();
        var worldRotation = config.CoordinateSpace == EditorCoordinateSpace.Local
            ? activeQuaternion * localRotation * activeQuaternion.Inverse()
            : localRotation;

        foreach (var pair in _dragStartTransforms)
        {
            if (!_entityNodes.TryGetValue(pair.Key, out var node))
            {
                continue;
            }

            var source = pair.Value;
            var position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
            var rotation = EngineValueConverter.ToEngine(source.Rotation).Normalized();
            var scale = new Vector3(source.Scale.X, source.Scale.Y, source.Scale.Z);
            switch (tool)
            {
                case EditorTransformTool.Translate:
                    var delta = config.CoordinateSpace == EditorCoordinateSpace.Local
                        ? rotation * localValue
                        : localValue;
                    position += delta;
                    break;
                case EditorTransformTool.Rotate:
                    rotation = config.CoordinateSpace == EditorCoordinateSpace.Local
                        ? (rotation * localRotation).Normalized()
                        : (localRotation * rotation).Normalized();
                    if (config.PivotMode != EditorPivotMode.Individual)
                    {
                        var pivotVector = new Vector3(pivot.X, pivot.Y, pivot.Z);
                        position = pivotVector + worldRotation * (position - pivotVector);
                    }
                    break;
                case EditorTransformTool.Scale:
                    var factor = localValue;
                    scale *= factor;
                    if (config.PivotMode != EditorPivotMode.Individual)
                    {
                        var pivotVector = new Vector3(pivot.X, pivot.Y, pivot.Z);
                        position = pivotVector + (position - pivotVector) * factor;
                    }
                    break;
            }

            var candidate = new TransformValue(
                new Vector3Value(position.X, position.Y, position.Z),
                new QuaternionValue(rotation.X, rotation.Y, rotation.Z, rotation.W),
                new Vector3Value(scale.X, scale.Y, scale.Z));
            node.Transform = EngineValueConverter.ToEngine(candidate);
            if (pair.Key == _selectedEntityId)
            {
                _dragCandidateTransform = candidate;
            }
        }
        RefreshPreviewSelectionVisuals();
    }

    private void RefreshPreviewSelectionVisuals()
    {
        if (_selectedEntityId is not null && _entityNodes.TryGetValue(_selectedEntityId, out var active))
        {
            _selectionMarker.Position = active.GlobalPosition + new Vector3(0, .035f, 0);
        }
        foreach (var pair in _secondarySelectionMarkers)
        {
            if (_entityNodes.TryGetValue(pair.Key, out var node))
            {
                pair.Value.Position = node.GlobalPosition + new Vector3(0, .03f, 0);
            }
        }
        UpdateGizmoForSelection();
    }

    private void CommitConfiguredGizmoDrag()
    {
        var tool = _dragTool;
        var value = _dragCommandValue;
        ResetConfiguredGizmoDrag(restoreVisuals: false);
        if (tool is null || value is null)
        {
            return;
        }

        EntityManipulationRequested?.Invoke(tool.Value, value.Value, EditorTransformAlignment.None);
    }

    private void CancelConfiguredGizmoDrag() => ResetConfiguredGizmoDrag(restoreVisuals: true);

    private void ResetConfiguredGizmoDrag(bool restoreVisuals)
    {
        if (restoreVisuals)
        {
            foreach (var pair in _dragStartTransforms)
            {
                if (_entityNodes.TryGetValue(pair.Key, out var node))
                {
                    node.Transform = EngineValueConverter.ToEngine(pair.Value);
                }
            }
        }

        _dragAxis = null;
        _dragRotationPlane = null;
        _dragScaleAxis = null;
        _dragTool = null;
        _dragStartTransform = null;
        _dragCandidateTransform = null;
        _dragCommandValue = null;
        _dragPreviousRotationVector = Vector3.Zero;
        _dragAccumulatedRotationRadians = 0;
        _dragStartTransforms.Clear();
        RefreshGizmoMaterials(null, null);
        RefreshScaleGizmoMaterials(null);
        RefreshSelectionVisuals();
    }

    private StudioMoveAxis? PickConfiguredMoveAxis(Vector2 pointer)
    {
        var screen = ToViewportPointer(pointer);
        var origin = _camera.UnprojectPosition(_moveGizmo.GlobalPosition);
        var scale = Math.Max(_moveGizmo.Scale.X, .001f);
        StudioMoveAxis? best = null;
        var bestDistance = 18f;
        foreach (var axis in Enum.GetValues<StudioMoveAxis>())
        {
            var end = _camera.UnprojectPosition(
                _moveGizmo.GlobalPosition + ConfiguredWorldAxis(AxisVector(axis)) * 1.62f * scale);
            var distance = DistanceToSegment(screen, origin, end);
            if (distance < bestDistance)
            {
                best = axis;
                bestDistance = distance;
            }
        }
        return best;
    }

    private StudioRotationPlane? PickConfiguredRotationPlane(Vector2 pointer)
    {
        var screen = ToViewportPointer(pointer);
        var rayOrigin = _camera.ProjectRayOrigin(screen);
        var rayDirection = _camera.ProjectRayNormal(screen).Normalized();
        var scale = Math.Max(_moveGizmo.Scale.X, .001f);
        StudioRotationPlane? best = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var plane in Enum.GetValues<StudioRotationPlane>())
        {
            var frame = ConfiguredRotationFrame(plane);
            var denominator = frame.Normal.Dot(rayDirection);
            if (MathF.Abs(denominator) < .0001f)
            {
                continue;
            }
            var distance = frame.Normal.Dot(_moveGizmo.GlobalPosition - rayOrigin) / denominator;
            if (!float.IsFinite(distance) || distance <= 0 || distance >= bestDistance)
            {
                continue;
            }
            var offset = rayOrigin + rayDirection * distance - _moveGizmo.GlobalPosition;
            offset -= frame.Normal * offset.Dot(frame.Normal);
            var radius = offset.Length() / scale;
            if (radius < RotationPlaneInnerRadius || radius > RotationPlaneOuterRadius)
            {
                continue;
            }
            var direction = offset.Normalized();
            if (direction.Dot(frame.First) < 0 || direction.Dot(frame.Second) < 0)
            {
                continue;
            }
            best = plane;
            bestDistance = distance;
        }
        return best;
    }

    private bool TryPointerVectorOnConfiguredPlane(
        Vector2 pointer,
        StudioRotationPlane plane,
        out Vector3 direction)
    {
        var viewportPointer = ToViewportPointer(pointer);
        var rayOrigin = _camera.ProjectRayOrigin(viewportPointer);
        var rayDirection = _camera.ProjectRayNormal(viewportPointer);
        var normal = ConfiguredRotationFrame(plane).Normal;
        var denominator = normal.Dot(rayDirection);
        if (MathF.Abs(denominator) < .0001f)
        {
            direction = Vector3.Zero;
            return false;
        }
        var distance = normal.Dot(_moveGizmo.GlobalPosition - rayOrigin) / denominator;
        if (!float.IsFinite(distance) || distance <= 0)
        {
            direction = Vector3.Zero;
            return false;
        }
        var offset = rayOrigin + rayDirection * distance - _moveGizmo.GlobalPosition;
        offset -= normal * offset.Dot(normal);
        if (offset.LengthSquared() < .0001f)
        {
            direction = Vector3.Zero;
            return false;
        }
        direction = offset.Normalized();
        return true;
    }

    private (Vector3 First, Vector3 Second, Vector3 Normal) ConfiguredRotationFrame(
        StudioRotationPlane plane)
    {
        var source = RotationFrame(plane);
        return (
            ConfiguredWorldAxis(source.First),
            ConfiguredWorldAxis(source.Second),
            ConfiguredWorldAxis(source.Normal));
    }

    private Vector3 ConfiguredWorldAxis(Vector3 localAxis) =>
        (_moveGizmo.GlobalTransform.Basis * localAxis).Normalized();

    private Node3D BuildScaleGizmo()
    {
        var root = new Node3D { Name = "ScaleGizmo", Visible = false };
        foreach (var axis in Enum.GetValues<StudioScaleAxis>())
        {
            var vector = ScaleAxisVector(axis);
            var color = axis == StudioScaleAxis.Uniform ? StudioStyle.Accent : AxisColor((StudioMoveAxis)axis);
            var material = GizmoMaterial(color);
            var handle = new MeshInstance3D
            {
                Name = $"Scale{axis}",
                Position = vector * (axis == StudioScaleAxis.Uniform ? .72f : 1.42f),
                Mesh = new BoxMesh
                {
                    Size = axis == StudioScaleAxis.Uniform
                        ? new Vector3(.22f, .22f, .22f)
                        : new Vector3(.18f, .18f, .18f),
                },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            root.AddChild(handle);
            _scaleHandles[axis] = handle;
            _scaleMaterials[axis] = material;
            if (axis == StudioScaleAxis.Uniform)
            {
                continue;
            }
            root.AddChild(new MeshInstance3D
            {
                Name = $"ScaleStem{axis}",
                Position = vector * .68f,
                Mesh = new BoxMesh
                {
                    Size = axis switch
                    {
                        StudioScaleAxis.X => new Vector3(1.28f, .045f, .045f),
                        StudioScaleAxis.Y => new Vector3(.045f, 1.28f, .045f),
                        _ => new Vector3(.045f, .045f, 1.28f),
                    },
                },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        return root;
    }

    private StudioScaleAxis? PickScaleAxis(Vector2 pointer)
    {
        var screen = ToViewportPointer(pointer);
        return _scaleHandles
            .Select(pair => (pair.Key, Distance: screen.DistanceTo(_camera.UnprojectPosition(pair.Value.GlobalPosition))))
            .Where(pair => pair.Distance <= 24)
            .OrderBy(pair => pair.Distance)
            .Select(pair => (StudioScaleAxis?)pair.Key)
            .FirstOrDefault();
    }

    private void RefreshScaleGizmoMaterials(StudioScaleAxis? active)
    {
        foreach (var pair in _scaleMaterials)
        {
            var color = pair.Key == StudioScaleAxis.Uniform ? StudioStyle.Accent : AxisColor((StudioMoveAxis)pair.Key);
            pair.Value.AlbedoColor = pair.Key == active ? color.Lightened(.28f) : color;
            pair.Value.Emission = pair.Value.AlbedoColor;
            pair.Value.EmissionEnergyMultiplier = pair.Key == active ? 2.8f : 1.35f;
        }
    }

    private static Vector3 ScaleAxisVector(StudioScaleAxis axis) => axis switch
    {
        StudioScaleAxis.X => Vector3.Right,
        StudioScaleAxis.Y => Vector3.Up,
        StudioScaleAxis.Z => Vector3.Back,
        _ => Vector3.One.Normalized(),
    };

    private void CaptureAuthoredLighting()
    {
        _authoredSunEnergy = _sun.LightEnergy;
        _authoredSunShadows = _sun.ShadowEnabled;
        _authoredEditorFillEnergy = _editorFill.LightEnergy;
        _authoredFillEnergy = _fill.LightEnergy;
    }

    internal void SetViewportMode(EditorViewportMode mode)
    {
        _viewportMode = mode;
        _sun.LightEnergy = _authoredSunEnergy;
        _sun.ShadowEnabled = _authoredSunShadows;
        _editorFill.LightEnergy = _authoredEditorFillEnergy;
        _fill.LightEnergy = _authoredFillEnergy;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        switch (mode)
        {
            case EditorViewportMode.Lighting:
                _editorFill.LightEnergy = 0;
                _fill.LightEnergy = 0;
                break;
            case EditorViewportMode.Performance:
                _sun.ShadowEnabled = false;
                _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible;
                break;
        }
        RebuildDiagnosticOverlay();
        UpdateViewportModeSummary();
        UpdateViewportModeBadge(Size.X > 0 && Size.X < 850);
        RefreshSelectionVisuals();
        RefreshHumanTransformToolbar();
    }

    private void UpdateViewportModeBadge(bool narrow)
    {
        if (_readOnly)
        {
            _modeBadge.Text = narrow ? "LECTURE SEULE" : "COMPARAISON · LECTURE SEULE";
            return;
        }
        _modeBadge.Text = _viewportMode switch
        {
            EditorViewportMode.Simulation => "SIMULATION",
            EditorViewportMode.Lighting => "ÉCLAIRAGE",
            EditorViewportMode.Navigation => "NAVIGATION",
            EditorViewportMode.Collisions => "COLLISIONS",
            EditorViewportMode.Performance => "PERFORMANCE",
            _ => "ÉDITION",
        };
    }

    private void RebuildDiagnosticOverlay()
    {
        RemoveDiagnosticOverlay();
        if (_viewportMode is not (EditorViewportMode.Navigation or EditorViewportMode.Collisions))
        {
            return;
        }
        var area = ViewArea;
        if (area is null)
        {
            return;
        }

        var color = _viewportMode == EditorViewportMode.Navigation
            ? new Color(.2f, .92f, .62f, .62f)
            : new Color(1f, .52f, .2f, .78f);
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            NoDepthTest = true,
        };
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        if (_viewportMode == EditorViewportMode.Navigation)
        {
            var halfWidth = area.Dimensions.Width / 2f;
            var halfDepth = area.Dimensions.Depth / 2f;
            var step = Math.Max(area.Navigation.CellSize, Math.Max(area.Dimensions.Width, area.Dimensions.Depth) / 48f);
            for (var x = -halfWidth; x <= halfWidth + .001f; x += step)
            {
                AddOverlayLine(mesh, new Vector3(x, .06f, -halfDepth), new Vector3(x, .06f, halfDepth), color);
            }
            for (var z = -halfDepth; z <= halfDepth + .001f; z += step)
            {
                AddOverlayLine(mesh, new Vector3(-halfWidth, .06f, z), new Vector3(halfWidth, .06f, z), color);
            }
        }
        else
        {
            foreach (var entity in area.Entities.Take(120))
            {
                if (!_entityNodes.TryGetValue(entity.Id, out var node))
                {
                    continue;
                }
                AddOverlayBox(
                    mesh,
                    node.GlobalPosition,
                    new Vector3(
                        MathF.Abs(entity.Transform.Scale.X),
                        MathF.Abs(entity.Transform.Scale.Y),
                        MathF.Abs(entity.Transform.Scale.Z)),
                    color);
            }
        }
        mesh.SurfaceEnd();
        var instance = new MeshInstance3D
        {
            Name = _viewportMode + "Overlay",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _world.AddChild(instance);
        _diagnosticOverlay = instance;
    }

    private void UpdateViewportModeSummary()
    {
        var area = ViewArea;
        if (area is null)
        {
            return;
        }
        var suffix = _viewportMode switch
        {
            EditorViewportMode.Simulation => "aperçu scène · gizmos masqués",
            EditorViewportMode.Lighting => "lumières de confort désactivées",
            EditorViewportMode.Navigation => $"grille navigation · pas {area.Navigation.CellSize:0.###} m",
            EditorViewportMode.Collisions => $"{Math.Min(120, area.Entities.Count)} emprises canoniques",
            EditorViewportMode.Performance =>
                $"{VisibleEntityCount} visibles · {AssetModelInstanceCount} modèles · ombres diagnostic coupées",
            _ => "édition directe",
        };
        _summary.Text = $"{area.DisplayName} · {VisibleEntityCount}/{EntityCount} objets visibles" +
                        (GhostedEntityCount == 0 ? string.Empty : $" · {GhostedEntityCount} fantômes") +
                        $" · {suffix}";
    }

    private static void AddOverlayLine(ImmediateMesh mesh, Vector3 from, Vector3 to, Color color)
    {
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(from);
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(to);
    }

    private static void AddOverlayBox(ImmediateMesh mesh, Vector3 center, Vector3 size, Color color)
    {
        var half = size * .5f;
        var points = new[]
        {
            center + new Vector3(-half.X, 0, -half.Z),
            center + new Vector3(half.X, 0, -half.Z),
            center + new Vector3(half.X, 0, half.Z),
            center + new Vector3(-half.X, 0, half.Z),
            center + new Vector3(-half.X, size.Y, -half.Z),
            center + new Vector3(half.X, size.Y, -half.Z),
            center + new Vector3(half.X, size.Y, half.Z),
            center + new Vector3(-half.X, size.Y, half.Z),
        };
        var edges = new[]
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7),
        };
        foreach (var edge in edges)
        {
            AddOverlayLine(mesh, points[edge.Item1], points[edge.Item2], color);
        }
    }

    private void RemoveDiagnosticOverlay()
    {
        if (_diagnosticOverlay is null || !GodotObject.IsInstanceValid(_diagnosticOverlay))
        {
            _diagnosticOverlay = null;
            return;
        }
        _diagnosticOverlay.GetParent()?.RemoveChild(_diagnosticOverlay);
        _diagnosticOverlay.Free();
        _diagnosticOverlay = null;
    }

    private void ClearUx0CViewportArea()
    {
        ClearSecondarySelectionMarkers();
        RemoveDiagnosticOverlay();
        _selectedEntityIds.Clear();
        _dragStartTransforms.Clear();
        _dragScaleAxis = null;
        _dragTool = null;
        _dragCommandValue = null;
    }
}
