using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
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
    private ProjectDefinition? _project;
    private AreaDefinition? _area;
    private PlayerController? _player;
    private Control? _miniMapRoot;
    private Control? _globalMapRoot;
    private Label? _miniMapArea;
    private LocalMapCanvas? _localCanvas;
    private GlobalMapCanvas? _globalCanvas;
    private bool _globalMapRequested;
    private bool _explorationUiRequested = true;

    internal RuntimeUiMotionPolicy MotionPolicy { get; init; } = RuntimeUiMotionPolicy.Create(null, []);

    public event Action? InventoryRequested;

    public event Action? CharacterRequested;

    public event Action? JournalRequested;

    public event Action<bool>? GlobalMapVisibilityChanged;

    public bool MiniMapVisible => _localCanvas?.IsVisibleInTree() == true;

    public bool GlobalMapVisible => _globalMapRoot?.Visible ?? _globalMapRequested;

    public int AreaCount => _project?.Areas.Count ?? 0;

    public string CurrentAreaSlug => _area?.Slug ?? string.Empty;

    public Vector2 MiniMapLogicalSize => _localCanvas?.Size ?? Vector2.Zero;

    internal int ActivePingCount => _localCanvas?.ActivePingCount ?? 0;

    internal int PeakPingCount => _localCanvas?.PeakPingCount ?? 0;

    internal string LastPingAccessibleText => _localCanvas?.LastPingAccessibleText ?? string.Empty;

    public void Configure(ProjectDefinition project, AreaDefinition area, PlayerController player)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    public override void _Ready()
    {
        if (_project is null || _area is null || _player is null)
            throw new InvalidOperationException("The runtime module map must be configured before entering the tree.");

        Layer = 25;
        BuildMiniMap();
        BuildGlobalMap();
        SetGlobalMapVisible(_globalMapRequested);
    }

    public override void _Process(double delta)
    {
        _localCanvas?.AdvancePings(delta);
        _localCanvas?.QueueRedraw();
        if (GlobalMapVisible) _globalCanvas?.QueueRedraw();
    }

    internal void ShowPing(Vector3Value position, RuntimeMapPingKind kind, string accessibleText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessibleText);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            throw new ArgumentOutOfRangeException(nameof(position));
        _localCanvas?.ShowPing(position, kind, accessibleText.Trim());
    }

    internal void ClearPings() => _localCanvas?.ClearPings();

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

        _miniMapArea = Label(_area!.DisplayName, 8, RuntimeRpgUiTheme.Parchment, title: true);
        _miniMapArea.Visible = false;
        root.AddChild(_miniMapArea);

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

        _localCanvas = new LocalMapCanvas(_area!, _player!, MotionPolicy)
        {
            Position = new Vector2(19, 19),
            Size = new Vector2(144, 144),
            CustomMinimumSize = new Vector2(144, 144),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(_localCanvas);

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
        open.AddThemeStyleboxOverride("pressed", RuntimeRpgUiTheme.Circle(
            new Color("4a3423ef"), RuntimeRpgUiTheme.Bone, 12));
        open.AddThemeFontOverride("font", RuntimeRpgUiTheme.BodyFont);
        open.AddThemeFontSizeOverride("font_size", 8);
        open.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
        open.AddThemeColorOverride("font_hover_color", RuntimeRpgUiTheme.Gold);
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

        var topShade = new ColorRect
        {
            Color = new Color("120d09f4"),
            AnchorRight = 1,
            OffsetBottom = 92,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _globalMapRoot.AddChild(topShade);
        var topRule = new ColorRect
        {
            Color = RuntimeRpgUiTheme.BorderMuted,
            AnchorRight = 1,
            OffsetTop = 91,
            OffsetBottom = 92,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _globalMapRoot.AddChild(topRule);

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
        var brand = new HBoxContainer { CustomMinimumSize = new Vector2(315, 0) };
        brand.AddThemeConstantOverride("separation", 12);
        top.AddChild(brand);
        var sigil = Label("◇", 28, RuntimeRpgUiTheme.Player, title: true);
        sigil.CustomMinimumSize = new Vector2(44, 44);
        sigil.HorizontalAlignment = HorizontalAlignment.Center;
        sigil.VerticalAlignment = VerticalAlignment.Center;
        sigil.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            new Color("241a14df"), RuntimeRpgUiTheme.Bronze, 0, 0));
        brand.AddChild(sigil);
        var brandCopy = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        brandCopy.AddThemeConstantOverride("separation", 1);
        brandCopy.AddChild(Label("ATLAS DU VOYAGEUR", 13, RuntimeRpgUiTheme.Bone));
        brandCopy.AddChild(Label(_area!.DisplayName, 11, RuntimeRpgUiTheme.Muted, title: true));
        brand.AddChild(brandCopy);

        var navCenter = new CenterContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        top.AddChild(navCenter);
        var nav = new HBoxContainer();
        nav.AddThemeConstantOverride("separation", 6);
        navCenter.AddChild(nav);
        var inventory = MapNavButton("INVENTAIRE", "I");
        inventory.Pressed += () =>
        {
            SetGlobalMapVisible(false);
            InventoryRequested?.Invoke();
        };
        nav.AddChild(inventory);
        var character = MapNavButton("PERSONNAGE", "C");
        character.Pressed += () =>
        {
            SetGlobalMapVisible(false);
            CharacterRequested?.Invoke();
        };
        nav.AddChild(character);
        var journal = MapNavButton("JOURNAL", "J");
        journal.Pressed += () =>
        {
            SetGlobalMapVisible(false);
            JournalRequested?.Invoke();
        };
        nav.AddChild(journal);
        var activeMap = MapNavButton("CARTE", "M");
        activeMap.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
        activeMap.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.ButtonStyle(
            new Color("3b291ee8"), RuntimeRpgUiTheme.Gold));
        nav.AddChild(activeMap);

        var closeArea = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(315, 0),
            Alignment = BoxContainer.AlignmentMode.End,
        };
        closeArea.AddThemeConstantOverride("separation", 11);
        top.AddChild(closeArea);
        closeArea.AddChild(Label("◆  2 480", 11, RuntimeRpgUiTheme.Gold));
        closeArea.AddChild(Label("◇  12", 11, RuntimeRpgUiTheme.Player));
        var close = new Button
        {
            Text = "ESC  FERMER",
            CustomMinimumSize = new Vector2(104, 36),
            FocusMode = Control.FocusModeEnum.All,
        };
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

        var regionPanel = MapPanel(new Color("21170ff2"), RuntimeRpgUiTheme.BorderMuted, 21, 19);
        regionPanel.CustomMinimumSize = new Vector2(320, 0);
        body.AddChild(regionPanel);
        var region = new VBoxContainer();
        region.AddThemeConstantOverride("separation", 12);
        regionPanel.AddChild(region);
        region.AddChild(Label("RÉGION ACTUELLE", 9, RuntimeRpgUiTheme.Player));
        region.AddChild(Label(_area!.DisplayName, 31, RuntimeRpgUiTheme.Parchment, title: true));
        var regionDescription = Label(
            "Des routes noyées de lumière traversent des terres anciennes et encore inexplorées.",
            10, RuntimeRpgUiTheme.Muted);
        regionDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        region.AddChild(regionDescription);
        region.AddChild(MapDivider());
        var explored = new HBoxContainer();
        var exploredLabel = Label("EXPLORATION", 9, RuntimeRpgUiTheme.Quiet);
        exploredLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        explored.AddChild(exploredLabel);
        explored.AddChild(Label("68%", 13, RuntimeRpgUiTheme.Player, title: true));
        region.AddChild(explored);
        region.AddChild(MapProgress(68, RuntimeRpgUiTheme.Player));
        region.AddChild(MapDivider());
        region.AddChild(Label("LÉGENDE", 9, RuntimeRpgUiTheme.Muted));
        foreach (var legend in new[]
                 {
                     ("◆", "Position actuelle", RuntimeRpgUiTheme.Player),
                     ("◇", "Passage de zone", RuntimeRpgUiTheme.Gold),
                     ("●", "Lieu découvert", RuntimeRpgUiTheme.Green),
                     ("✦", "Objectif suivi", RuntimeRpgUiTheme.Violet),
                     ("—", "Route praticable", RuntimeRpgUiTheme.Sky),
                 })
        {
            var legendRow = new HBoxContainer();
            legendRow.AddThemeConstantOverride("separation", 10);
            var glyph = Label(legend.Item1, 13, legend.Item3, title: true);
            glyph.CustomMinimumSize = new Vector2(26, 22);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            legendRow.AddChild(glyph);
            legendRow.AddChild(Label(legend.Item2, 10, RuntimeRpgUiTheme.Parchment));
            region.AddChild(legendRow);
        }
        region.AddChild(MapDivider());
        region.AddChild(Label($"{_project!.Areas.Count} LIEUX CONNUS   ·   3 ROUTES ACTIVES", 9,
            RuntimeRpgUiTheme.Quiet));

        var mapPanel = MapPanel(new Color("1a120deF"), RuntimeRpgUiTheme.Border, 16, 14);
        mapPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(mapPanel);
        var mapStack = new VBoxContainer();
        mapStack.AddThemeConstantOverride("separation", 8);
        mapPanel.AddChild(mapStack);
        var mapHeader = new HBoxContainer();
        var mapTitle = Label("LES TERRES CONNUES", 10, RuntimeRpgUiTheme.Muted);
        mapTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mapHeader.AddChild(mapTitle);
        mapHeader.AddChild(Label("N  ↑", 10, RuntimeRpgUiTheme.Player, title: true));
        mapStack.AddChild(mapHeader);
        _globalCanvas = new GlobalMapCanvas(_project!, _area!)
        {
            CustomMinimumSize = new Vector2(720, 570),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        mapStack.AddChild(_globalCanvas);
        var mapHint = Label("MOLETTE  ZOOMER     ·     GLISSER  DÉPLACER     ·     M  REVENIR AU JEU", 9,
            RuntimeRpgUiTheme.Quiet);
        mapHint.HorizontalAlignment = HorizontalAlignment.Center;
        mapStack.AddChild(mapHint);

        var destinationPanel = MapPanel(new Color("2a1d15f2"), RuntimeRpgUiTheme.Border, 20, 19);
        destinationPanel.CustomMinimumSize = new Vector2(330, 0);
        body.AddChild(destinationPanel);
        var destination = new VBoxContainer();
        destination.AddThemeConstantOverride("separation", 12);
        destinationPanel.AddChild(destination);
        destination.AddChild(Label("DESTINATION SÉLECTIONNÉE", 9, RuntimeRpgUiTheme.Gold));
        destination.AddChild(Label(_area.DisplayName, 27, RuntimeRpgUiTheme.Parchment, title: true));
        destination.AddChild(Label("Zone actuelle", 11, RuntimeRpgUiTheme.Player, title: true));
        destination.AddChild(MapDivider());
        var destinationDescription = Label(
            "Un territoire de sentiers anciens. Les marqueurs indiquent les passages, " +
            "alliés et objectifs déjà découverts.", 11, RuntimeRpgUiTheme.Muted);
        destinationDescription.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        destination.AddChild(destinationDescription);
        destination.AddChild(MapDivider());
        destination.AddChild(MapMetric("TEMPS DE TRAJET", "Sur place", RuntimeRpgUiTheme.Player));
        destination.AddChild(MapMetric("DANGER", "Modéré", RuntimeRpgUiTheme.Gold));
        destination.AddChild(MapMetric("MÉTÉO", "Brume claire", RuntimeRpgUiTheme.Sky));
        destination.AddChild(MapDivider());
        var travel = new Button
        {
            Text = "PLACER UN REPÈRE  ◆",
            CustomMinimumSize = new Vector2(0, 44),
            FocusMode = Control.FocusModeEnum.All,
        };
        RuntimeRpgUiTheme.StyleButton(travel);
        destination.AddChild(travel);
        var travelHint = Label("Les voyages rapides se débloquent après la découverte d'un relais.", 9,
            RuntimeRpgUiTheme.Quiet);
        travelHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        destination.AddChild(travelHint);
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
            DrawCircle(new Vector2(Size.X * .22f, Size.Y * .42f), Size.Y * .65f,
                new Color("7d56332d"));
            DrawCircle(new Vector2(Size.X * .82f, Size.Y * .58f), Size.Y * .7f,
                new Color("51351f2d"));
            for (var index = 1; index < 11; index++)
            {
                var x = Size.X * index / 11;
                DrawLine(new Vector2(x, 92), new Vector2(x, Size.Y), new Color("a9855708"));
            }
        }
    }

    private sealed partial class LocalMapCanvas : Control
    {
        private const int PingCapacity = 8;

        private readonly AreaDefinition area;
        private readonly PlayerController player;
        private readonly RuntimeUiMotionPolicy motionPolicy;
        private readonly MapPingEntry[] _pings = new MapPingEntry[PingCapacity];
        private readonly HashSet<EntityId> _portalEntities;

        internal LocalMapCanvas(AreaDefinition area, PlayerController player, RuntimeUiMotionPolicy motionPolicy)
        {
            this.area = area ?? throw new ArgumentNullException(nameof(area));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.motionPolicy = motionPolicy ?? throw new ArgumentNullException(nameof(motionPolicy));
            _portalEntities = area.Portals.Select(portal => portal.SourceEntityId).ToHashSet();
            for (var index = 0; index < _pings.Length; index++) _pings[index] = new MapPingEntry();
        }

        internal int ActivePingCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _pings.Length; index++)
                    if (_pings[index].Active) count++;
                return count;
            }
        }

        internal int PeakPingCount { get; private set; }

        internal string LastPingAccessibleText { get; private set; } = string.Empty;

        public override void _Draw()
        {
            var bounds = new Rect2(2, 2, Math.Max(1, Size.X - 4), Math.Max(1, Size.Y - 4));
            var center = bounds.GetCenter();
            var radius = Math.Min(bounds.Size.X, bounds.Size.Y) * .47f;
            DrawCircle(center, radius, new Color("0d0a08f2"));
            DrawAreaBackdrop(bounds);
            DrawArc(center, radius, 0, Mathf.Tau, 96, new Color("a9855778"), 1, true);

            foreach (var entity in area.Entities.Where(IsMapMarker))
                DrawEntity(entity, ClampToMapCircle(Project(entity, bounds), bounds));

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
        }

        internal void ShowPing(Vector3Value position, RuntimeMapPingKind kind, string accessibleText)
        {
            MapPingEntry? selected = null;
            for (var index = 0; index < _pings.Length; index++)
            {
                var candidate = _pings[index];
                if (candidate.Active && candidate.Kind == kind &&
                    Math.Abs(candidate.Position.X - position.X) < .05f &&
                    Math.Abs(candidate.Position.Z - position.Z) < .05f)
                {
                    selected = candidate;
                    break;
                }
                if (!candidate.Active && selected is null) selected = candidate;
            }

            if (selected is null)
            {
                selected = _pings[0];
                for (var index = 1; index < _pings.Length; index++)
                    if (_pings[index].RemainingSeconds < selected.RemainingSeconds) selected = _pings[index];
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
            for (var index = 0; index < _pings.Length; index++)
            {
                var ping = _pings[index];
                if (!ping.Active) continue;
                ping.RemainingSeconds = Math.Max(0, ping.RemainingSeconds - delta);
                if (ping.RemainingSeconds > 0) continue;
                ping.Active = false;
            }
        }

        internal void ClearPings()
        {
            for (var index = 0; index < _pings.Length; index++)
            {
                _pings[index].Active = false;
                _pings[index].RemainingSeconds = 0;
            }
            SetMeta("accessible_ping_text", string.Empty);
            QueueRedraw();
        }

        private void DrawPings(Rect2 bounds)
        {
            for (var index = 0; index < _pings.Length; index++)
            {
                var ping = _pings[index];
                if (!ping.Active) continue;
                var point = ClampToMapCircle(Project(ping.Position.X, ping.Position.Z, bounds), bounds);
                var progress = 1 - ping.RemainingSeconds / Math.Max(.01, motionPolicy.MapPingSeconds);
                var radius = motionPolicy.ReducedMotion ? 8f : 6f + (float)progress * 10f;
                var alpha = motionPolicy.ReducedMotion
                    ? 1f
                    : (float)Math.Clamp(ping.RemainingSeconds / .45, 0, 1);
                var color = ping.Kind == RuntimeMapPingKind.Combat
                    ? motionPolicy.ResolveColor(RuntimeUiSemanticColor.Damage)
                    : motionPolicy.ResolveColor(RuntimeUiSemanticColor.Objective);
                color.A = alpha;
                DrawCircle(point, radius, new Color(0, 0, 0, .5f), false,
                    motionPolicy.HighContrast ? 4 : 2, true);
                DrawArc(point, radius, 0, Mathf.Tau, 32, color,
                    motionPolicy.HighContrast ? 3 : 2, true);
                if (ping.Kind == RuntimeMapPingKind.Combat)
                {
                    DrawLine(point + new Vector2(-4, -4), point + new Vector2(4, 4), color, 2, true);
                    DrawLine(point + new Vector2(4, -4), point + new Vector2(-4, 4), color, 2, true);
                }
                else
                {
                    DrawDiamond(point, 4, color);
                }
            }
        }

        private static Vector2 ClampToMapCircle(Vector2 point, Rect2 bounds)
        {
            var center = bounds.GetCenter();
            var delta = point - center;
            var radius = Math.Min(bounds.Size.X, bounds.Size.Y) * .42f;
            return delta.Length() > radius ? center + delta.Normalized() * radius : point;
        }

        private void DrawAreaBackdrop(Rect2 bounds)
        {
            for (var index = 1; index < 4; index++)
            {
                var x = bounds.Position.X + bounds.Size.X * index / 4;
                var y = bounds.Position.Y + bounds.Size.Y * index / 4;
                DrawLine(new Vector2(x, bounds.Position.Y), new Vector2(x, bounds.End.Y), new Color("a9855718"));
                DrawLine(new Vector2(bounds.Position.X, y), new Vector2(bounds.End.X, y), new Color("a9855718"));
            }

            var center = bounds.GetCenter();
            switch (area.Slug)
            {
                case "clairval":
                    DrawLine(new Vector2(center.X, bounds.Position.Y + 7), new Vector2(center.X, bounds.End.Y - 7),
                        new Color("617d8980"), 7, true);
                    DrawLine(new Vector2(bounds.Position.X + 10, center.Y), new Vector2(bounds.End.X - 10, center.Y),
                        new Color("617d8980"), 5, true);
                    break;
                case "ember-inn":
                    DrawRect(bounds.Grow(-10), new Color("ad693d8c"), false, 5);
                    DrawLine(new Vector2(center.X, bounds.Position.Y + 12), new Vector2(center.X, bounds.End.Y - 12),
                        new Color("9b794f9c"), 2);
                    break;
                case "gnoll-fortress":
                    DrawRect(bounds.Grow(-12), new Color("9f373594"), false, 6);
                    DrawCircle(center, Math.Min(bounds.Size.X, bounds.Size.Y) * .18f, new Color("ad693d90"), false, 4);
                    break;
                case "amber-desert":
                    for (var radius = 15; radius < Math.Min(bounds.Size.X, bounds.Size.Y) / 2; radius += 20)
                        DrawArc(center, radius, -.6f, 2.6f, 24, new Color("c6a36a38"), 1, true);
                    break;
                case "snow-pass":
                    DrawPolyline([
                        new Vector2(bounds.Position.X + 16, bounds.End.Y - 12),
                        center + new Vector2(-30, 17), center + new Vector2(9, -4),
                        new Vector2(bounds.End.X - 16, bounds.Position.Y + 10),
                    ], new Color("91a7aa68"), 5, true);
                    break;
            }
        }

        private bool IsMapMarker(EntityInstance entity) =>
            _portalEntities.Contains(entity.Id) || entity.Tags.Any(tag => tag is
                "npc" or "guide" or "hostile" or "boss" or "quest" or "exit" or "landmark" or
                "blacksmith" or "temple" or "healing" or "innkeeper");

        private void DrawEntity(EntityInstance entity, Vector2 point)
        {
            if (_portalEntities.Contains(entity.Id))
            {
                DrawDiamond(point, 5, RuntimeRpgUiTheme.Gold);
                return;
            }

            var color = entity.Tags.Contains("boss", StringComparer.OrdinalIgnoreCase) ? new Color("b43c39") :
                entity.Tags.Contains("hostile", StringComparer.OrdinalIgnoreCase) ? new Color("ad693d") :
                entity.Tags.Any(tag => tag is "blacksmith" or "temple" or "healing") ? new Color("738563") :
                entity.Tags.Any(tag => tag is "quest" or "exit") ? RuntimeRpgUiTheme.Gold :
                new Color("8e8777");
            DrawCircle(point, 3, color);
        }

        private void DrawDiamond(Vector2 point, float radius, Color color) => DrawColoredPolygon([
            point + new Vector2(0, -radius), point + new Vector2(radius, 0),
            point + new Vector2(0, radius), point + new Vector2(-radius, 0),
        ], color);

        private Vector2 Project(EntityInstance entity, Rect2 bounds) =>
            Project(entity.Transform.Position.X, entity.Transform.Position.Z, bounds);

        private Vector2 Project(float x, float z, Rect2 bounds)
        {
            var width = Math.Max(1, area.Dimensions.Width);
            var depth = Math.Max(1, area.Dimensions.Depth);
            var normalizedX = Mathf.Clamp((x / width) + .5f, 0, 1);
            var normalizedZ = Mathf.Clamp((z / depth) + .5f, 0, 1);
            return new(bounds.Position.X + 7 + normalizedX * (bounds.Size.X - 14),
                bounds.Position.Y + 7 + normalizedZ * (bounds.Size.Y - 14));
        }

        private sealed class MapPingEntry
        {
            internal bool Active { get; set; }

            internal Vector3Value Position { get; set; } = Vector3Value.Zero;

            internal RuntimeMapPingKind Kind { get; set; }

            internal string AccessibleText { get; set; } = string.Empty;

            internal double RemainingSeconds { get; set; }
        }
    }

    private sealed partial class GlobalMapCanvas(ProjectDefinition project, AreaDefinition currentArea) : Control
    {
        private static readonly IReadOnlyDictionary<string, Vector2> AuthoredPositions =
            new Dictionary<string, Vector2>(StringComparer.Ordinal)
            {
                ["ember-inn"] = new(.20f, .20f),
                ["clairval"] = new(.36f, .36f),
                ["gnoll-fortress"] = new(.79f, .32f),
                ["amber-desert"] = new(.81f, .76f),
                ["snow-pass"] = new(.20f, .77f),
            };

        public override void _Draw()
        {
            var bounds = new Rect2(2, 2, Math.Max(1, Size.X - 4), Math.Max(1, Size.Y - 4));
            DrawBackdrop(bounds);
            var positions = project.Areas.ToDictionary(area => area.Id, area => ResolvePosition(area, bounds));
            var routes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var owner in project.Areas)
                foreach (var portal in owner.Portals)
                {
                    var key = string.CompareOrdinal(owner.Id.ToString(), portal.DestinationAreaId.ToString()) < 0
                        ? $"{owner.Id}:{portal.DestinationAreaId}"
                        : $"{portal.DestinationAreaId}:{owner.Id}";
                    if (!routes.Add(key) || !positions.TryGetValue(portal.DestinationAreaId, out var destination)) continue;
                    DrawRoute(positions[owner.Id], destination);
                }

            foreach (var area in project.Areas) DrawArea(area, positions[area.Id], area.Id == currentArea.Id);
        }

        private void DrawBackdrop(Rect2 bounds)
        {
            DrawRect(bounds, new Color("21160f"), true);
            DrawCircle(bounds.GetCenter() + new Vector2(-bounds.Size.X * .28f, -bounds.Size.Y * .18f),
                bounds.Size.Y * .42f, new Color("a2764832"));
            DrawCircle(bounds.GetCenter() + new Vector2(bounds.Size.X * .30f, bounds.Size.Y * .14f),
                bounds.Size.Y * .50f, new Color("72543830"));
            DrawRect(bounds, new Color("0a07051f"), true);
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
            var center = bounds.GetCenter();
            DrawArc(center, Math.Min(bounds.Size.X, bounds.Size.Y) * .37f, 0, Mathf.Tau, 96,
                new Color("a985574a"), 1.5f, true);
            DrawArc(center, Math.Min(bounds.Size.X, bounds.Size.Y) * .22f, 0, Mathf.Tau, 72,
                new Color("a9855732"), 1, true);
            for (var index = 0; index < 46; index++)
            {
                var x = bounds.Position.X + 13 + ((index * 83) % Math.Max(1, (int)bounds.Size.X - 26));
                var y = bounds.Position.Y + 13 + ((index * 47) % Math.Max(1, (int)bounds.Size.Y - 26));
                DrawCircle(new Vector2(x, y), index % 7 == 0 ? 1.4f : .75f, new Color("e5dac33d"));
            }
        }

        private void DrawRoute(Vector2 source, Vector2 destination)
        {
            DrawLine(source, destination, new Color("080503"), 7, true);
            DrawLine(source, destination, RuntimeRpgUiTheme.Bronze, 3, true);
            DrawRouteMarker((source + destination) * .5f);
        }

        private static Vector2 ResolvePosition(AreaDefinition area, Rect2 bounds)
        {
            var normalized = AuthoredPositions.TryGetValue(area.Slug, out var authored) ? authored : new Vector2(.5f, .5f);
            return bounds.Position + new Vector2(normalized.X * bounds.Size.X, normalized.Y * bounds.Size.Y);
        }

        private void DrawArea(AreaDefinition area, Vector2 point, bool current)
        {
            var radius = current ? 25f : 19f;
            if (current)
            {
                DrawCircle(point, radius + 12, new Color("c6a36a28"));
                DrawCircle(point, radius + 7, new Color("ad693d35"));
            }
            DrawDiamond(point, radius + 5, new Color("0b0705"));
            DrawDiamond(point, radius, current ? new Color("4b3524") : new Color("251b15"));
            DrawDiamondOutline(point, radius + 1, current ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.Border, current ? 2 : 1);
            DrawAreaSymbol(area.Slug, point, current ? RuntimeRpgUiTheme.Bone : new Color("b99b6b"));

            var scale = Mathf.Clamp(Size.X / 1500f, .78f, 1.18f);
            var labelWidth = 330f * scale;
            var fontSize = current ? (int)(17 * scale) : (int)(15 * scale);
            var label = new Vector2(point.X - labelWidth / 2, point.Y + radius + 27 * scale);
            DrawString(RuntimeRpgUiTheme.TitleFont, label, area.DisplayName.ToUpperInvariant(),
                HorizontalAlignment.Center, labelWidth, Math.Max(11, fontSize),
                current ? RuntimeRpgUiTheme.Gold : RuntimeRpgUiTheme.Parchment);
            if (current)
                DrawString(RuntimeRpgUiTheme.BodyFont, label + new Vector2(0, 17 * scale), "VOUS ÊTES ICI",
                    HorizontalAlignment.Center, labelWidth, Math.Max(10, (int)(11 * scale)), RuntimeRpgUiTheme.Player);
        }

        private void DrawAreaSymbol(string slug, Vector2 point, Color color)
        {
            switch (slug)
            {
                case "ember-inn":
                    DrawPolyline([
                        point + new Vector2(-9, 1), point + new Vector2(0, -7), point + new Vector2(9, 1),
                        point + new Vector2(7, 1), point + new Vector2(7, 8), point + new Vector2(-7, 8),
                        point + new Vector2(-7, 1),
                    ], color, 1.5f, true);
                    break;
                case "clairval":
                    DrawLine(point + new Vector2(-8, 7), point + new Vector2(-8, -5), color, 2);
                    DrawLine(point + new Vector2(0, 7), point + new Vector2(0, -9), color, 2);
                    DrawLine(point + new Vector2(8, 7), point + new Vector2(8, -3), color, 2);
                    DrawLine(point + new Vector2(-11, 7), point + new Vector2(11, 7), color, 2);
                    break;
                case "gnoll-fortress":
                    DrawRect(new Rect2(point + new Vector2(-9, -7), new Vector2(18, 15)), color, false, 2);
                    DrawLine(point + new Vector2(-9, -7), point + new Vector2(-4, -11), color, 2);
                    DrawLine(point + new Vector2(9, -7), point + new Vector2(4, -11), color, 2);
                    DrawLine(point + new Vector2(-4, -11), point + new Vector2(4, -11), color, 2);
                    break;
                case "amber-desert":
                    DrawCircle(point, 5, color, false, 2);
                    for (var index = 0; index < 8; index++)
                    {
                        var direction = Vector2.FromAngle(index * Mathf.Pi / 4);
                        DrawLine(point + direction * 8, point + direction * 12, color, 1.5f);
                    }
                    break;
                case "snow-pass":
                    DrawPolyline([
                        point + new Vector2(-12, 8), point + new Vector2(-3, -8), point + new Vector2(2, 0),
                        point + new Vector2(6, -5), point + new Vector2(13, 8),
                    ], color, 2, true);
                    break;
            }
        }

        private void DrawRouteMarker(Vector2 point) => DrawDiamond(point, 5, RuntimeRpgUiTheme.Ember);

        private void DrawDiamond(Vector2 point, float radius, Color color) => DrawColoredPolygon([
            point + new Vector2(0, -radius), point + new Vector2(radius, 0),
            point + new Vector2(0, radius), point + new Vector2(-radius, 0),
        ], color);

        private void DrawDiamondOutline(Vector2 point, float radius, Color color, float width)
        {
            var points = new[]
            {
                point + new Vector2(0, -radius), point + new Vector2(radius, 0),
                point + new Vector2(0, radius), point + new Vector2(-radius, 0),
                point + new Vector2(0, -radius),
            };
            DrawPolyline(points, color, width, true);
        }
    }
}
