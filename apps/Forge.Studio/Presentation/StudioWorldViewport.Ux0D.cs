using Forge.Studio.Session;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldViewport
{
    private Node3D _placementGhostRoot = null!;
    private PanelContainer _placementFeedbackPanel = null!;
    private Label _placementFeedback = null!;
    private StudioRepeatedPlacementRequest? _armedPlacement;
    private readonly List<Forge.Domain.Spatial.Vector3Value> _brushSamples = [];
    private bool _brushDragging;

    internal event Action<StudioRepeatedPlacementRequest>? RepeatedPlacementRequested;

    internal bool IsPlacementToolArmed => _armedPlacement is not null;

    internal int PlacementGhostCount => _placementGhostRoot.GetChildCount();

    internal bool PlacementFeedbackVisible => _placementFeedbackPanel.Visible;

    private void InitializeUx0DViewport()
    {
        _placementGhostRoot = new Node3D { Name = "PlacementPreviewGhosts" };
        _world.AddChild(_placementGhostRoot);

        _placementFeedbackPanel = StudioStyle.Panel(
            "PlacementPreviewFeedback",
            new Color(StudioStyle.Chrome, .94f),
            7,
            1);
        _placementFeedbackPanel.Position = new Vector2(8, 48);
        _placementFeedbackPanel.CustomMinimumSize = new Vector2(410, 0);
        _placementFeedbackPanel.MouseFilter = MouseFilterEnum.Ignore;
        _placementFeedbackPanel.Visible = false;
        var margin = StudioStyle.Margin(7, 5, 7, 5);
        _placementFeedbackPanel.AddChild(margin);
        _placementFeedback = StudioStyle.Label(string.Empty, 10, StudioStyle.Text, wrap: true);
        margin.AddChild(_placementFeedback);
        AddChild(_placementFeedbackPanel);
    }

    internal void ShowPlacementPreview(StudioPlacementPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ClearPlacementGhosts();
        foreach (var pose in preview.Poses)
        {
            var color = pose.IsValid ? StudioStyle.Success : StudioStyle.Danger;
            var authored = pose.Transform ?? new TransformValue(
                pose.Position, QuaternionValue.Identity, Vector3Value.One);
            var ghost = new MeshInstance3D
            {
                Name = pose.IsValid ? "ValidPlacementGhost" : "InvalidPlacementGhost",
                Position = new Vector3(pose.Position.X, pose.Position.Y + .045f, pose.Position.Z),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Mesh = new CylinderMesh
                {
                    TopRadius = .58f,
                    BottomRadius = .58f,
                    Height = .07f,
                    RadialSegments = 32,
                },
                MaterialOverride = PlacementGhostMaterial(color),
            };
            ghost.Quaternion = EngineValueConverter.ToEngine(authored.Rotation).Normalized();
            ghost.Scale = EngineValueConverter.ToEngine(authored.Scale);
            _placementGhostRoot.AddChild(ghost);
        }

        _placementGhostRoot.Visible = preview.Poses.Count > 0;
        _placementFeedbackPanel.Visible = preview.Poses.Count > 0 || _armedPlacement is not null;
        _placementFeedback.Text = preview.Message +
            (preview.Poses.Count == 0 ? string.Empty : " · vert = valide, rouge = refusé");
        _placementFeedback.AddThemeColorOverride(
            "font_color",
            preview.IsValid ? StudioStyle.Success : StudioStyle.Danger);
    }

    internal void HidePlacementPreview()
    {
        ClearPlacementGhosts();
        _placementFeedbackPanel.Visible = false;
    }

    internal void FramePlacementPreview(StudioPlacementPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.Poses.Count == 0)
        {
            return;
        }
        var center = new Vector3(
            preview.Poses.Average(pose => pose.Position.X),
            preview.Poses.Average(pose => pose.Position.Y),
            preview.Poses.Average(pose => pose.Position.Z));
        var radius = preview.Poses.Max(pose => MathF.Sqrt(
            MathF.Pow(pose.Position.X - center.X, 2) +
            MathF.Pow(pose.Position.Z - center.Z, 2)));
        _cameraTarget = center;
        _cameraDistance = Math.Clamp(8 + (radius * 2.4f), 8, 36);
        UpdateCamera();
    }

    internal void ArmPlacementTool(StudioRepeatedPlacementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _armedPlacement = request;
        _brushDragging = false;
        _brushSamples.Clear();
        HideContextMenu();
        _modeBadge.Text = request.Profile.BrushMode ? "PINCEAU" : "PLACEMENT";
        _modeBadge.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _interactionHint.Text = request.Profile.BrushMode
            ? "maintenir clic gauche pour tracer · relâcher pour appliquer · Échap annuler"
            : "clic gauche pour placer · Échap annuler";
        InteractionStatusChanged?.Invoke(request.Profile.BrushMode
            ? "Pinceau actif · tracez dans la vue 3D · un tracé = une annulation"
            : "Placement actif · choisissez le point dans la vue 3D");
    }

    internal void CancelPlacementTool(bool notify = true)
    {
        if (_armedPlacement is null)
        {
            HidePlacementPreview();
            return;
        }
        _armedPlacement = null;
        _brushDragging = false;
        _brushSamples.Clear();
        HidePlacementPreview();
        if (_session is not null)
        {
            SetViewportMode(_session.WorkspaceProfile.Shell.ViewportMode);
        }
        if (notify)
        {
            InteractionStatusChanged?.Invoke("Outil de placement fermé · aucune donnée ajoutée");
        }
    }

    private bool HandleUx0DPlacementInput(InputEvent @event)
    {
        if (_armedPlacement is null || _session is null)
        {
            return false;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            CancelPlacementTool();
            return true;
        }

        if (@event is InputEventMouseMotion motion)
        {
            if (!TryPlacementPoint(motion.Position, out var point))
            {
                return true;
            }

            if (_armedPlacement.Profile.BrushMode && _brushDragging &&
                (motion.ButtonMask & MouseButtonMask.Left) != 0)
            {
                AddBrushSample(point);
                PreviewArmedPlacement(point, _brushSamples);
            }
            else
            {
                PreviewArmedPlacement(point, null);
            }
            return true;
        }

        if (@event is not InputEventMouseButton button)
        {
            return true;
        }
        if (button.ButtonIndex == MouseButton.Right && button.Pressed)
        {
            CancelPlacementTool();
            return true;
        }
        if (button.ButtonIndex != MouseButton.Left || !TryPlacementPoint(button.Position, out var placement))
        {
            return true;
        }

        if (_armedPlacement.Profile.BrushMode)
        {
            if (button.Pressed)
            {
                _brushDragging = true;
                _brushSamples.Clear();
                AddBrushSample(placement);
                PreviewArmedPlacement(placement, _brushSamples);
                return true;
            }
            if (!_brushDragging)
            {
                return true;
            }

            _brushDragging = false;
            AddBrushSample(placement);
            var request = _armedPlacement with
            {
                Origin = _brushSamples[0],
                BrushSamples = _brushSamples.ToArray(),
            };
            var preview = _session.PreviewRepeatedPlacement(request);
            ShowPlacementPreview(preview);
            if (!preview.IsValid)
            {
                InteractionStatusChanged?.Invoke(preview.Message);
                return true;
            }

            var keepActive = request.Profile.ContinuousStroke;
            if (!keepActive)
            {
                CancelPlacementTool(notify: false);
            }
            else
            {
                _brushSamples.Clear();
            }
            RepeatedPlacementRequested?.Invoke(request);
            return true;
        }

        if (!button.Pressed)
        {
            return true;
        }
        var single = _armedPlacement with { Origin = placement, BrushSamples = null };
        var singlePreview = _session.PreviewRepeatedPlacement(single);
        ShowPlacementPreview(singlePreview);
        if (!singlePreview.IsValid)
        {
            InteractionStatusChanged?.Invoke(singlePreview.Message);
            return true;
        }
        CancelPlacementTool(notify: false);
        RepeatedPlacementRequested?.Invoke(single);
        return true;
    }

    private void PreviewArmedPlacement(
        Forge.Domain.Spatial.Vector3Value point,
        IReadOnlyList<Forge.Domain.Spatial.Vector3Value>? samples)
    {
        var request = _armedPlacement! with
        {
            Origin = samples is { Count: > 0 } ? samples[0] : point,
            BrushSamples = samples,
        };
        ShowPlacementPreview(_session!.PreviewRepeatedPlacement(request));
    }

    private void AddBrushSample(Forge.Domain.Spatial.Vector3Value point)
    {
        if (_brushSamples.Count >= 256)
        {
            return;
        }
        if (_brushSamples.Count == 0 || GroundDistance(_brushSamples[^1], point) >= .15f)
        {
            _brushSamples.Add(point);
        }
    }

    private bool TryPlacementPoint(Vector2 pointer, out Forge.Domain.Spatial.Vector3Value point)
    {
        if (TryProjectToGround(pointer, out var ground))
        {
            point = SnapPlacement(new Forge.Domain.Spatial.Vector3Value(ground.X, ground.Y, ground.Z));
            return true;
        }
        point = Forge.Domain.Spatial.Vector3Value.Zero;
        return false;
    }

    private static float GroundDistance(
        Forge.Domain.Spatial.Vector3Value left,
        Forge.Domain.Spatial.Vector3Value right)
    {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt((x * x) + (z * z));
    }

    private void ClearPlacementGhosts()
    {
        foreach (Node child in _placementGhostRoot.GetChildren())
        {
            _placementGhostRoot.RemoveChild(child);
            child.QueueFree();
        }
        _placementGhostRoot.Visible = false;
    }

    private static StandardMaterial3D PlacementGhostMaterial(Color color) => new()
    {
        AlbedoColor = new Color(color.R, color.G, color.B, .48f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = 2.1f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
    };
}
