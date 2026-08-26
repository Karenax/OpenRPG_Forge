using Forge.Application.Gameplay;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Runtime;

internal enum RuntimeMapPingKind
{
    Combat,
    Objective,
}

public partial class RuntimeModuleMap : CanvasLayer
{
    private RuntimeModulePresentation.MapView? _presentation;
    private PlayerController? _player;
    private Control? _miniMapRoot;
    private Control? _globalMapRoot;
    private LocalMapCanvas? _localCanvas;
    private LocalMapCanvas? _globalLocalCanvas;
    private GlobalMapCanvas? _globalCanvas;
    private Label? _explorationValue;
    private ProgressBar? _explorationProgress;
    private Label? _miniFloorValue;
    private Label? _floorValue;
    private Label? _workspaceCounts;
    private Label? _weatherValue;
    private Label? _objectiveValue;
    private bool _globalMapRequested;
    private bool _explorationUiRequested = true;

    internal RuntimeUiMotionPolicy MotionPolicy { get; init; } = RuntimeUiMotionPolicy.Create(null, []);

    public event Action? InventoryRequested;

    public event Action? CharacterRequested;

    public event Action? JournalRequested;

    public event Action<bool>? GlobalMapVisibilityChanged;

    public bool MiniMapVisible => _localCanvas?.IsVisibleInTree() == true;

    public bool GlobalMapVisible => _globalMapRoot?.Visible ?? _globalMapRequested;

    public bool GlobalMapUsesLocalAtlas => _globalLocalCanvas is not null;

    public int GlobalRenderedMarkerCount => _globalLocalCanvas?.RenderedMarkerCount ?? 0;

    public int GlobalMapLabelOverlapCount => _globalLocalCanvas?.LabelOverlapCount ?? 0;

    public int AreaCount => _presentation?.AreaCount ?? 0;

    public string CurrentAreaSlug => _presentation?.CurrentAreaSlug ?? string.Empty;

    public string DisplayedFloorText => _presentation is { FloorCount: > 1 } presentation
        ? $"{presentation.ActiveFloor + 1}/{presentation.FloorCount}"
        : "single";

    public Vector2 MiniMapLogicalSize => _localCanvas?.Size ?? Vector2.Zero;

    internal int ActivePingCount => _localCanvas?.ActivePingCount ?? 0;

    internal int PeakPingCount => _localCanvas?.PeakPingCount ?? 0;

    internal string LastPingAccessibleText => _localCanvas?.LastPingAccessibleText ?? string.Empty;

    public void Configure(RuntimeModulePresentation.MapView presentation, PlayerController player)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    public void UpdatePresentation(RuntimeModulePresentation.MapView presentation)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _localCanvas?.UpdatePresentation(presentation);
        _globalLocalCanvas?.UpdatePresentation(presentation);
        _globalCanvas?.UpdatePresentation(presentation);
        UpdatePresentationLabels();
    }

    public override void _Ready()
    {
        if (_presentation is null || _player is null)
            throw new InvalidOperationException("The runtime module map must be configured before entering the tree.");
        Layer = 25;
        BuildMiniMap();
        BuildGlobalMap();
        UpdatePresentationLabels();
        SetGlobalMapVisible(_globalMapRequested);
    }

    public override void _Process(double delta)
    {
        _localCanvas?.AdvancePings(delta);
        _localCanvas?.QueueRedraw();
        _globalLocalCanvas?.AdvancePings(delta);
        if (GlobalMapVisible)
        {
            _globalLocalCanvas?.QueueRedraw();
            _globalCanvas?.QueueRedraw();
        }
    }

    internal void ShowPing(Vector3Value position, RuntimeMapPingKind kind, string accessibleText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleText);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            throw new ArgumentOutOfRangeException(nameof(position));
        _localCanvas?.ShowPing(position, kind, accessibleText.Trim());
        _globalLocalCanvas?.ShowPing(position, kind, accessibleText.Trim());
    }

    internal void ClearPings()
    {
        _localCanvas?.ClearPings();
        _globalLocalCanvas?.ClearPings();
    }

    public void ToggleGlobalMap() => SetGlobalMapVisible(!GlobalMapVisible);

    public void SetGlobalMapVisible(bool visible)
    {
        var changed = GlobalMapVisible != visible;
        _globalMapRequested = visible;
        if (_globalMapRoot is not null) _globalMapRoot.Visible = visible;
        if (_miniMapRoot is not null) _miniMapRoot.Visible = !visible && _explorationUiRequested;
        if (changed) GlobalMapVisibilityChanged?.Invoke(visible);
    }

    public void SetExplorationUiVisible(bool visible)
    {
        _explorationUiRequested = visible;
        if (_miniMapRoot is not null) _miniMapRoot.Visible = visible && !GlobalMapVisible;
    }

    private void BuildMiniMap()
    {
        var margin = new MarginContainer
        {
            Name = "MiniMapFrame",
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -205,
            OffsetTop = 30,
            OffsetRight = -22,
            OffsetBottom = 199,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _miniMapRoot = margin;
        AddChild(margin);

        var root = new Control
        {
            CustomMinimumSize = new Vector2(183, 169),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        margin.AddChild(root);
        var compass = Label("N", 9, RuntimeRpgUiTheme.Gold, title: true);
        compass.Position = new Vector2(80, 0);
        compass.Size = new Vector2(24, 17);
        compass.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(compass);

        var mapShell = new Panel
        {
            Position = new Vector2(14, 14),
            Size = new Vector2(154, 154),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var shellStyle = RuntimeRpgUiTheme.Circle(new Color("0d0a08e8"), RuntimeRpgUiTheme.Bronze, 77);
        shellStyle.BorderWidthLeft = 2;
        shellStyle.BorderWidthTop = 2;
        shellStyle.BorderWidthRight = 2;
        shellStyle.BorderWidthBottom = 2;
        mapShell.AddThemeStyleboxOverride("panel", shellStyle);
        root.AddChild(mapShell);

        _localCanvas = new LocalMapCanvas(_presentation!, _player!, MotionPolicy)
        {
            Position = new Vector2(19, 19),
            Size = new Vector2(144, 144),
            CustomMinimumSize = new Vector2(144, 144),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(_localCanvas);

        _miniFloorValue = Label(string.Empty, 8, RuntimeRpgUiTheme.Gold, title: true);
        _miniFloorValue.Position = new Vector2(18, 142);
        _miniFloorValue.Size = new Vector2(75, 20);
        _miniFloorValue.HorizontalAlignment = HorizontalAlignment.Left;
        root.AddChild(_miniFloorValue);

        var open = new Button
        {
            Text = "M",
            TooltipText = "Ouvrir la carte globale du module",
            FocusMode = Control.FocusModeEnum.All,
            Position = new Vector2(145, 139),
            Size = new Vector2(27, 24),
        };
        open.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Circle(
            new Color("17110ef2"), RuntimeRpgUiTheme.Border, 12));
        open.AddThemeStyleboxOverride("hover", RuntimeRpgUiTheme.Circle(
            new Color("39291eea"), RuntimeRpgUiTheme.Gold, 12));
        open.AddThemeFontOverride("font", RuntimeRpgUiTheme.BodyFont);
        open.AddThemeFontSizeOverride("font_size", 8);
        open.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
        open.SetMeta("accessible_label", "Ouvrir la carte globale du module avec la touche M");
        open.Pressed += ToggleGlobalMap;
        root.AddChild(open);
    }

    private void BuildGlobalMap()
    {
        _globalMapRoot = new Control
        {
            Name = "GlobalModuleMap",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _globalMapRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_globalMapRoot);
        var veil = new MapVeil { MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _globalMapRoot.AddChild(veil);

        var top = new HBoxContainer
        {
            AnchorRight = 1,
            OffsetLeft = 34,
            OffsetTop = 17,
            OffsetRight = -34,
            OffsetBottom = 76,
        };
        top.AddThemeConstantOverride("separation", 16);
        _globalMapRoot.AddChild(top);
        var brand = new VBoxContainer { CustomMinimumSize = new Vector2(315, 0) };
        brand.AddChild(Label("ATLAS DU MODULE", 13, RuntimeRpgUiTheme.Bone, title: true));
        brand.AddChild(Label(_presentation!.CurrentAreaName, 11, RuntimeRpgUiTheme.Muted, title: true));
        top.AddChild(brand);

        var navCenter = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        top.AddChild(navCenter);
        var nav = new HBoxContainer();
        nav.AddThemeConstantOverride("separation", 6);
        navCenter.AddChild(nav);
        AddMapNavigation(nav, "INVENTAIRE", "I", () => InventoryRequested?.Invoke());
        AddMapNavigation(nav, "PERSONNAGE", "C", () => CharacterRequested?.Invoke());
        AddMapNavigation(nav, "JOURNAL", "J", () => JournalRequested?.Invoke());
        var activeMap = MapNavButton("CARTE", "M");
        activeMap.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.ButtonStyle(
            new Color("3b291ee8"), RuntimeRpgUiTheme.Gold));
        nav.AddChild(activeMap);

        var closeArea = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(315, 0),
            Alignment = BoxContainer.AlignmentMode.End,
        };
        top.AddChild(closeArea);
        var close = new Button { Text = "ESC  FERMER", CustomMinimumSize = new Vector2(104, 36) };
        RuntimeRpgUiTheme.StyleButton(close, quiet: true);
        close.Pressed += () => SetGlobalMapVisible(false);
        closeArea.AddChild(close);

        var body = new HBoxContainer
        {
            AnchorLeft = .028f,
            AnchorRight = .972f,
            AnchorTop = .105f,
            AnchorBottom = .955f,
        };
        body.AddThemeConstantOverride("separation", 14);
        _globalMapRoot.AddChild(body);
        BuildRegionPanel(body);
        BuildAtlasPanel(body);
        BuildDestinationPanel(body);
    }

    private void BuildRegionPanel(HBoxContainer body)
    {
        var panel = MapPanel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 21, 19);
        panel.CustomMinimumSize = new Vector2(320, 0);
        body.AddChild(panel);
        var region = new VBoxContainer();
        region.AddThemeConstantOverride("separation", 12);
        panel.AddChild(region);
        region.AddChild(Label("RÉGION ACTUELLE", 9, RuntimeRpgUiTheme.Player));
        region.AddChild(Label(_presentation!.CurrentAreaName, 30, RuntimeRpgUiTheme.Parchment, title: true));
        _floorValue = Label(string.Empty, 11, RuntimeRpgUiTheme.Gold, title: true);
        region.AddChild(_floorValue);
        var description = Label(_presentation.Description ?? "Aucune description renseignée.", 10,
            RuntimeRpgUiTheme.Muted);
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        region.AddChild(description);
        region.AddChild(MapDivider());
        var explored = new HBoxContainer();
        var exploredLabel = Label("EXPLORATION", 9, RuntimeRpgUiTheme.Quiet);
        exploredLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        explored.AddChild(exploredLabel);
        _explorationValue = Label(string.Empty, 13, RuntimeRpgUiTheme.Player, title: true);
        explored.AddChild(_explorationValue);
        region.AddChild(explored);
        _explorationProgress = MapProgress(0, RuntimeRpgUiTheme.Player);
        region.AddChild(_explorationProgress);
        region.AddChild(MapDivider());
        region.AddChild(Label("LÉGENDE", 9, RuntimeRpgUiTheme.Muted));
        foreach (var legend in new[]
                 {
                     ("◆", "Position actuelle", RuntimeRpgUiTheme.Player),
                     ("◇", "Portail", RuntimeRpgUiTheme.Gold),
                     ("●", "Pin ou waypoint", RuntimeRpgUiTheme.Green),
                     ("—", "Route authorée", RuntimeRpgUiTheme.Sky),
                 })
        {
            var legendRow = new HBoxContainer();
            legendRow.AddThemeConstantOverride("separation", 10);
            legendRow.AddChild(Label(legend.Item1, 13, legend.Item3, title: true));
            legendRow.AddChild(Label(legend.Item2, 10, RuntimeRpgUiTheme.Parchment));
            region.AddChild(legendRow);
        }
        region.AddChild(MapDivider());
        _workspaceCounts = Label(string.Empty, 9, RuntimeRpgUiTheme.Quiet);
        _workspaceCounts.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        region.AddChild(_workspaceCounts);
    }

    private void BuildAtlasPanel(HBoxContainer body)
    {
        var panel = MapPanel(new Color("1a120def"), RuntimeRpgUiTheme.Border, 16, 14);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(panel);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        panel.AddChild(stack);
        var header = new HBoxContainer();
        var usesLocalAtlas = _presentation!.Areas.Count == 1;
        var title = Label(usesLocalAtlas ? "CARTE DE LA ZONE" : "ZONES DU WORKSPACE", 10,
            RuntimeRpgUiTheme.Muted);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        header.AddChild(Label("N  ↑", 10, RuntimeRpgUiTheme.Player, title: true));
        stack.AddChild(header);
        if (usesLocalAtlas)
        {
            _globalLocalCanvas = new LocalMapCanvas(_presentation, _player!, MotionPolicy, showMarkerLabels: true)
            {
                CustomMinimumSize = new Vector2(720, 570),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stack.AddChild(_globalLocalCanvas);
        }
        else
        {
            _globalCanvas = new GlobalMapCanvas(_presentation)
            {
                CustomMinimumSize = new Vector2(720, 570),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stack.AddChild(_globalCanvas);
        }
        var hint = Label("M  REVENIR AU JEU", 9, RuntimeRpgUiTheme.Quiet);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(hint);
    }

    private void BuildDestinationPanel(HBoxContainer body)
    {
        var panel = MapPanel(new Color("2a1d15f2"), RuntimeRpgUiTheme.Border, 20, 19);
        panel.CustomMinimumSize = new Vector2(330, 0);
        body.AddChild(panel);
        var destination = new VBoxContainer();
        destination.AddThemeConstantOverride("separation", 12);
        panel.AddChild(destination);
        destination.AddChild(Label("ZONE ACTUELLE", 9, RuntimeRpgUiTheme.Gold));
        destination.AddChild(Label(_presentation!.CurrentAreaName, 27, RuntimeRpgUiTheme.Parchment, title: true));
        destination.AddChild(MapDivider());
        var description = Label(_presentation.Description ?? "Aucune description renseignée.", 11,
            RuntimeRpgUiTheme.Muted);
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        destination.AddChild(description);
        destination.AddChild(MapDivider());
        destination.AddChild(MapMetric("ZONES", _presentation.AreaCount.ToString(), RuntimeRpgUiTheme.Player));
        destination.AddChild(MapMetric("PORTAILS", _presentation.PortalCount.ToString(), RuntimeRpgUiTheme.Gold));
        destination.AddChild(MapMetric("PINS", _presentation.PinCount.ToString(), RuntimeRpgUiTheme.Green));
        destination.AddChild(MapMetric("WAYPOINTS", _presentation.WaypointCount.ToString(), RuntimeRpgUiTheme.Sky));
        _weatherValue = AddMutableMetric(destination, "MÉTÉO", RuntimeRpgUiTheme.Sky);
        _objectiveValue = AddMutableMetric(destination, "OBJECTIF", RuntimeRpgUiTheme.Violet);
        var emptyHint = Label("Les valeurs absentes restent explicitement non renseignées.", 9,
            RuntimeRpgUiTheme.Quiet);
        emptyHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        destination.AddChild(emptyHint);
    }

    private void AddMapNavigation(HBoxContainer parent, string text, string key, Action requested)
    {
        var button = MapNavButton(text, key);
        button.Pressed += () =>
        {
            SetGlobalMapVisible(false);
            requested();
        };
        parent.AddChild(button);
    }

    private void UpdatePresentationLabels()
    {
        if (_presentation is null) return;
        if (_explorationValue is not null)
            _explorationValue.Text = _presentation.ExplorationPercent is { } percent
                ? $"{percent:0.#}%"
                : "NON SUIVIE";
        if (_explorationProgress is not null)
        {
            _explorationProgress.Value = _presentation.ExplorationPercent ?? 0;
            _explorationProgress.Visible = _presentation.ExplorationPercent is not null;
        }
        var showFloor = _presentation.FloorCount > 1;
        var floorText = $"ÉTAGE {_presentation.ActiveFloor + 1} / {_presentation.FloorCount}";
        if (_miniFloorValue is not null)
        {
            _miniFloorValue.Text = floorText;
            _miniFloorValue.Visible = showFloor;
        }
        if (_floorValue is not null)
        {
            _floorValue.Text = floorText;
            _floorValue.Visible = showFloor;
        }
        if (_workspaceCounts is not null)
            _workspaceCounts.Text =
                $"{_presentation.AreaCount} ZONE(S) · {_presentation.PortalCount} PORTAIL(S) · " +
                $"{_presentation.PatrolRouteCount} ROUTE(S)";
        if (_weatherValue is not null)
            _weatherValue.Text = _presentation.Weather ?? "Non renseignée";
        if (_objectiveValue is not null)
            _objectiveValue.Text = _presentation.Objective ?? "Aucun objectif actif";
    }

    private static Label AddMutableMetric(VBoxContainer parent, string caption, Color color)
    {
        var row = new HBoxContainer();
        var captionLabel = Label(caption, 9, RuntimeRpgUiTheme.Quiet);
        captionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(captionLabel);
        var value = Label(string.Empty, 11, color, title: true);
        value.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        value.CustomMinimumSize = new Vector2(150, 0);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(value);
        parent.AddChild(row);
        return value;
    }

    private static Button MapNavButton(string text, string key)
    {
        var button = new Button
        {
            Text = $"{text}   {key}",
            CustomMinimumSize = new Vector2(128, 38),
            FocusMode = Control.FocusModeEnum.All,
        };
        RuntimeRpgUiTheme.StyleButton(button, quiet: true);
        button.AddThemeFontSizeOverride("font_size", 10);
        return button;
    }

    private static PanelContainer MapPanel(Color background, Color border, int horizontal, int vertical)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(background, border, horizontal, vertical));
        return panel;
    }

    private static ColorRect MapDivider() => new()
    {
        Color = RuntimeRpgUiTheme.BorderMuted,
        CustomMinimumSize = new Vector2(0, 1),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static ProgressBar MapProgress(double value, Color color)
    {
        var progress = new ProgressBar
        {
            MaxValue = 100,
            Value = value,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 7),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        progress.AddThemeStyleboxOverride("background", RuntimeRpgUiTheme.Bar(new Color("e5dac312"), 2));
        progress.AddThemeStyleboxOverride("fill", RuntimeRpgUiTheme.Bar(color, 2));
        return progress;
    }

    private static Control MapMetric(string caption, string value, Color color)
    {
        var row = new HBoxContainer();
        var captionLabel = Label(caption, 9, RuntimeRpgUiTheme.Quiet);
        captionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(captionLabel);
        row.AddChild(Label(value, 11, color, title: true));
        return row;
    }

    private static Label Label(string text, int size, Color color, bool title = false)
    {
        var label = new Label { Text = text };
        if (title) RuntimeRpgUiTheme.StyleTitle(label, size, color);
        else RuntimeRpgUiTheme.StyleBody(label, size, color);
        return label;
    }

    private sealed partial class MapVeil : Control
    {
        public override void _Draw()
        {
            var bounds = new Rect2(Vector2.Zero, Size);
            DrawRect(bounds, new Color("100b08fa"));
            DrawCircle(new Vector2(Size.X * .22f, Size.Y * .42f), Size.Y * .65f, new Color("7d56332d"));
            DrawCircle(new Vector2(Size.X * .82f, Size.Y * .58f), Size.Y * .7f, new Color("51351f2d"));
        }
    }

    private sealed partial class LocalMapCanvas : Control
    {
        private const int PingCapacity = 8;
        private readonly PlayerController player;
        private readonly RuntimeUiMotionPolicy motionPolicy;
        private readonly bool showMarkerLabels;
        private readonly MapPingEntry[] _pings = new MapPingEntry[PingCapacity];
        private RuntimeModulePresentation.MapView presentation;

        internal LocalMapCanvas(
            RuntimeModulePresentation.MapView presentation,
            PlayerController player,
            RuntimeUiMotionPolicy motionPolicy,
            bool showMarkerLabels = false)
        {
            this.presentation = presentation;
            this.player = player;
            this.motionPolicy = motionPolicy;
            this.showMarkerLabels = showMarkerLabels;
            for (var index = 0; index < _pings.Length; index++) _pings[index] = new MapPingEntry();
            SetMeta("accessible_marker_count", presentation.Markers.Count);
        }

        internal int RenderedMarkerCount => presentation.Markers.Count;

        internal int LabelOverlapCount { get; private set; }

        internal int ActivePingCount
        {
            get
            {
                var count = 0;
                foreach (var ping in _pings)
                {
                    if (ping.Active) count++;
                }

                return count;
            }
        }

        internal int PeakPingCount { get; private set; }

        internal string LastPingAccessibleText { get; private set; } = string.Empty;

        internal void UpdatePresentation(RuntimeModulePresentation.MapView value)
        {
            presentation = value;
            SetMeta("accessible_marker_count", presentation.Markers.Count);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var bounds = new Rect2(2, 2, Math.Max(1, Size.X - 4), Math.Max(1, Size.Y - 4));
            var center = bounds.GetCenter();
            var radius = Math.Min(bounds.Size.X, bounds.Size.Y) * .47f;
            DrawCircle(center, radius, new Color("0d0a08f2"));
            DrawGrid(bounds);
            DrawAuthoredRoutes(bounds);
            var markers = presentation.Markers.Select(marker =>
                (Marker: marker, Point: ClampToMapCircle(Project(marker.X, marker.Z, bounds), bounds))).ToArray();
            foreach (var entry in markers)
                DrawMarker(entry.Marker, entry.Point);
            if (showMarkerLabels) DrawMarkerLabels(bounds, markers);
            else LabelOverlapCount = 0;
            DrawPings(bounds);

            var playerPoint = ClampToMapCircle(Project(player.GlobalPosition.X, player.GlobalPosition.Z, bounds), bounds);
            var forward = -player.GlobalTransform.Basis.Z;
            var direction = new Vector2(forward.X, forward.Z).Normalized();
            if (direction.LengthSquared() < .01f) direction = Vector2.Up;
            var side = new Vector2(-direction.Y, direction.X);
            DrawCircle(playerPoint, 7, new Color("100c09"));
            DrawColoredPolygon([
                playerPoint + direction * 8,
                playerPoint - direction * 5 + side * 4,
                playerPoint - direction * 5 - side * 4,
            ], RuntimeRpgUiTheme.Bone);
            DrawArc(playerPoint, 9, 0, Mathf.Tau, 24, RuntimeRpgUiTheme.Gold, 1, true);
            DrawArc(center, radius, 0, Mathf.Tau, 96, new Color("a9855778"), 1, true);
        }

        internal void ShowPing(Vector3Value position, RuntimeMapPingKind kind, string accessibleText)
        {
            MapPingEntry? selected = null;
            foreach (var candidate in _pings)
            {
                if (candidate.Active && candidate.Kind == kind &&
                    Math.Abs(candidate.Position.X - position.X) < .05f &&
                    Math.Abs(candidate.Position.Z - position.Z) < .05f)
                {
                    selected = candidate;
                    break;
                }

                if (!candidate.Active && selected is null)
                    selected = candidate;
            }

            if (selected is null)
            {
                selected = _pings[0];
                foreach (var candidate in _pings)
                {
                    if (candidate.RemainingSeconds < selected.RemainingSeconds)
                        selected = candidate;
                }
            }
            selected.Active = true;
            selected.Position = position;
            selected.Kind = kind;
            selected.AccessibleText = accessibleText;
            selected.RemainingSeconds = motionPolicy.MapPingSeconds;
            LastPingAccessibleText = accessibleText;
            SetMeta("accessible_ping_text", accessibleText);
            PeakPingCount = Math.Max(PeakPingCount, ActivePingCount);
            QueueRedraw();
        }

        internal void AdvancePings(double delta)
        {
            foreach (var ping in _pings)
            {
                if (!ping.Active) continue;
                ping.RemainingSeconds = Math.Max(0, ping.RemainingSeconds - delta);
                if (ping.RemainingSeconds <= 0) ping.Active = false;
            }
        }

        internal void ClearPings()
        {
            foreach (var ping in _pings)
            {
                ping.Active = false;
                ping.RemainingSeconds = 0;
            }
            SetMeta("accessible_ping_text", string.Empty);
            QueueRedraw();
        }

        private void DrawGrid(Rect2 bounds)
        {
            for (var index = 1; index < 4; index++)
            {
                var x = bounds.Position.X + bounds.Size.X * index / 4;
                var y = bounds.Position.Y + bounds.Size.Y * index / 4;
                DrawLine(new Vector2(x, bounds.Position.Y), new Vector2(x, bounds.End.Y), new Color("a9855718"));
                DrawLine(new Vector2(bounds.Position.X, y), new Vector2(bounds.End.X, y), new Color("a9855718"));
            }
        }

        private void DrawAuthoredRoutes(Rect2 bounds)
        {
            foreach (var route in presentation.PatrolRoutes.Where(route => route.Points.Count > 1))
            {
                var points = route.Points.Select(point =>
                    ClampToMapCircle(Project(point.X, point.Z, bounds), bounds)).ToArray();
                DrawPolyline(points, RuntimeRpgUiTheme.Sky, 2, true);
            }
        }

        private void DrawMarker(RuntimeModulePresentation.MapMarker marker, Vector2 point)
        {
            var color = marker.Kind switch
            {
                "portal" => RuntimeRpgUiTheme.Gold,
                "waypoint" when marker.Tracked => RuntimeRpgUiTheme.Violet,
                "waypoint" => RuntimeRpgUiTheme.Sky,
                _ => RuntimeRpgUiTheme.Green,
            };
            if (string.Equals(marker.Kind, "portal", StringComparison.Ordinal))
                DrawDiamond(point, 5, color);
            else
                DrawCircle(point, marker.Tracked ? 4 : 3, color);
        }

        private void DrawMarkerLabels(
            Rect2 bounds,
            IReadOnlyList<(RuntimeModulePresentation.MapMarker Marker, Vector2 Point)> markers)
        {
            const float labelWidth = 170;
            const float minimumGap = 6;
            var center = bounds.GetCenter();
            var ordered = markers
                .OrderBy(entry => entry.Point.Y)
                .ThenBy(entry => entry.Point.X)
                .ThenBy(entry => entry.Marker.Label, StringComparer.Ordinal)
                .ToArray();
            var columns = new[]
            {
                ordered.Where((_, index) => index % 2 == 0).ToArray(),
                ordered.Where((_, index) => index % 2 == 1).ToArray(),
            };
            var placed = new List<Rect2>(ordered.Length);
            for (var column = 0; column < columns.Length; column++)
            {
                var previousBottom = bounds.Position.Y + 12;
                foreach (var entry in columns[column])
                {
                    var textSize = RuntimeRpgUiTheme.BodyFont.GetStringSize(
                        entry.Marker.Label, HorizontalAlignment.Left, labelWidth, 10);
                    var labelHeight = Math.Max(14, textSize.Y);
                    var x = column == 0 ? center.X - labelWidth - 28 : center.X + 28;
                    var y = Math.Max(entry.Point.Y - labelHeight / 2, previousBottom + minimumGap);
                    y = Math.Min(y, bounds.End.Y - labelHeight - 12);
                    var rectangle = new Rect2(new Vector2(x, y), new Vector2(labelWidth, labelHeight));
                    placed.Add(rectangle);
                    previousBottom = rectangle.End.Y;
                    var leaderEnd = column == 0
                        ? new Vector2(rectangle.End.X + 3, rectangle.GetCenter().Y)
                        : new Vector2(rectangle.Position.X - 3, rectangle.GetCenter().Y);
                    DrawLine(entry.Point, leaderEnd, new Color("a985575c"), 1, true);
                    DrawRect(rectangle.Grow(3), new Color("0b0806c8"));
                    DrawString(RuntimeRpgUiTheme.BodyFont,
                        rectangle.Position + new Vector2(0, labelHeight - 2),
                        entry.Marker.Label, HorizontalAlignment.Left, labelWidth, 10,
                        RuntimeRpgUiTheme.Parchment);
                }
            }

            LabelOverlapCount = 0;
            for (var first = 0; first < placed.Count; first++)
            {
                for (var second = first + 1; second < placed.Count; second++)
                {
                    if (placed[first].Intersects(placed[second])) LabelOverlapCount++;
                }
            }
            SetMeta("accessible_label_overlap_count", LabelOverlapCount);
        }

        private void DrawPings(Rect2 bounds)
        {
            foreach (var ping in _pings)
            {
                if (!ping.Active) continue;
                var point = ClampToMapCircle(Project(ping.Position.X, ping.Position.Z, bounds), bounds);
                var progress = 1 - ping.RemainingSeconds / Math.Max(.01, motionPolicy.MapPingSeconds);
                var radius = motionPolicy.ReducedMotion ? 8f : 6f + (float)progress * 10f;
                var color = ping.Kind == RuntimeMapPingKind.Combat
                    ? motionPolicy.ResolveColor(RuntimeUiSemanticColor.Damage)
                    : motionPolicy.ResolveColor(RuntimeUiSemanticColor.Objective);
                DrawArc(point, radius, 0, Mathf.Tau, 32, color, motionPolicy.HighContrast ? 3 : 2, true);
            }
        }

        private Vector2 Project(float x, float z, Rect2 bounds)
        {
            var normalizedX = Mathf.Clamp((x / Math.Max(1, presentation.Width)) + .5f, 0, 1);
            var normalizedZ = Mathf.Clamp((z / Math.Max(1, presentation.Depth)) + .5f, 0, 1);
            return new(bounds.Position.X + 7 + normalizedX * (bounds.Size.X - 14),
                bounds.Position.Y + 7 + normalizedZ * (bounds.Size.Y - 14));
        }

        private static Vector2 ClampToMapCircle(Vector2 point, Rect2 bounds)
        {
            var center = bounds.GetCenter();
            var delta = point - center;
            var radius = Math.Min(bounds.Size.X, bounds.Size.Y) * .42f;
            return delta.Length() > radius ? center + delta.Normalized() * radius : point;
        }

        private void DrawDiamond(Vector2 point, float radius, Color color) => DrawColoredPolygon([
            point + new Vector2(0, -radius), point + new Vector2(radius, 0),
            point + new Vector2(0, radius), point + new Vector2(-radius, 0),
        ], color);

        private sealed class MapPingEntry
        {
            internal bool Active { get; set; }
            internal Vector3Value Position { get; set; } = Vector3Value.Zero;
            internal RuntimeMapPingKind Kind { get; set; }
            internal string AccessibleText { get; set; } = string.Empty;
            internal double RemainingSeconds { get; set; }
        }
    }

    private sealed partial class GlobalMapCanvas : Control
    {
        private RuntimeModulePresentation.MapView presentation;

        internal GlobalMapCanvas(RuntimeModulePresentation.MapView presentation) => this.presentation = presentation;

        internal void UpdatePresentation(RuntimeModulePresentation.MapView value)
        {
            presentation = value;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var bounds = new Rect2(2, 2, Math.Max(1, Size.X - 4), Math.Max(1, Size.Y - 4));
            DrawBackdrop(bounds);
            var positions = ResolvePositions(bounds);
            foreach (var portal in presentation.Portals)
            {
                if (positions.TryGetValue(portal.SourceAreaId, out var source) &&
                    positions.TryGetValue(portal.DestinationAreaId, out var destination))
                    DrawRoute(source, destination);
            }
            foreach (var area in presentation.Areas)
                DrawArea(area, positions[area.Id]);
        }

        private Dictionary<string, Vector2> ResolvePositions(Rect2 bounds)
        {
            var positions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            if (presentation.Areas.Count == 1)
            {
                positions[presentation.Areas[0].Id] = bounds.GetCenter();
                return positions;
            }
            var radiusX = bounds.Size.X * .34f;
            var radiusY = bounds.Size.Y * .32f;
            for (var index = 0; index < presentation.Areas.Count; index++)
            {
                var angle = -Mathf.Pi / 2 + Mathf.Tau * index / presentation.Areas.Count;
                positions[presentation.Areas[index].Id] = bounds.GetCenter() +
                    new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            }
            return positions;
        }

        private void DrawBackdrop(Rect2 bounds)
        {
            DrawRect(bounds, new Color("21160f"), true);
            DrawCircle(bounds.GetCenter(), bounds.Size.Y * .42f, new Color("a276482c"));
            DrawRect(bounds, new Color("a9855780"), false, 1);
            for (var index = 1; index < 10; index++)
            {
                var x = bounds.Position.X + bounds.Size.X * index / 10;
                DrawLine(new Vector2(x, bounds.Position.Y), new Vector2(x, bounds.End.Y), new Color("c6a36a25"));
            }
            for (var index = 1; index < 6; index++)
            {
                var y = bounds.Position.Y + bounds.Size.Y * index / 6;
                DrawLine(new Vector2(bounds.Position.X, y), new Vector2(bounds.End.X, y), new Color("c6a36a25"));
            }
        }

        private void DrawRoute(Vector2 source, Vector2 destination)
        {
            DrawLine(source, destination, new Color("080503"), 7, true);
            DrawLine(source, destination, RuntimeRpgUiTheme.Bronze, 3, true);
        }

        private void DrawArea(RuntimeModulePresentation.MapArea area, Vector2 point)
        {
            var radius = area.IsCurrent ? 25f : 19f;
            if (area.IsCurrent) DrawCircle(point, radius + 10, new Color("c6a36a28"));
            DrawDiamond(point, radius, area.IsCurrent ? new Color("4b3524") : new Color("251b15"));
            DrawCircle(point, 5, area.IsCurrent ? RuntimeRpgUiTheme.Bone : RuntimeRpgUiTheme.Bronze, false, 2);
            var width = 300f;
            var label = new Vector2(point.X - width / 2, point.Y + radius + 27);
            DrawString(RuntimeRpgUiTheme.TitleFont, label, area.DisplayName.ToUpperInvariant(),
                HorizontalAlignment.Center, width, area.IsCurrent ? 16 : 14,
                area.IsCurrent ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.Parchment);
            if (area.IsCurrent)
                DrawString(RuntimeRpgUiTheme.BodyFont, label + new Vector2(0, 17), "VOUS ÊTES ICI",
                    HorizontalAlignment.Center, width, 10, RuntimeRpgUiTheme.Player);
        }

        private void DrawDiamond(Vector2 point, float radius, Color color) => DrawColoredPolygon([
            point + new Vector2(0, -radius), point + new Vector2(radius, 0),
            point + new Vector2(0, radius), point + new Vector2(-radius, 0),
        ], color);
    }
}
