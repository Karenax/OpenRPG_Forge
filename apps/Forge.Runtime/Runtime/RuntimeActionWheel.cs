using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeActionWheel : CanvasLayer
{
    private readonly List<Button> _buttons = [];
    private Control? _backdrop;
    private WheelOrnament? _ornament;
    private Label? _heading;
    private Label? _centerLabel;
    private Label? _accessibleSummary;
    private ModuleGameplayControlProfile? _profile;
    private string _parentKey = string.Empty;

    public event Action<string>? CommandRequested;

    public event Action<bool>? WheelVisibilityChanged;

    public bool IsOpen => _backdrop?.Visible == true;

    public override void _Ready()
    {
        Layer = 200;
        ProcessMode = ProcessModeEnum.Always;
        BuildWheel();
    }

    public void Configure(ModuleGameplayControlProfile profile) =>
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public void Toggle()
    {
        if (_profile?.RadialMenu.Enabled != true || _backdrop is null) return;
        if (_backdrop.Visible) Close(); else Open(string.Empty);
    }

    public void Close()
    {
        var wasOpen = IsOpen;
        if (_backdrop is not null) _backdrop.Visible = false;
        _parentKey = string.Empty;
        if (wasOpen) WheelVisibilityChanged?.Invoke(false);
    }

    private void BuildWheel()
    {
        _backdrop = new Control
        {
            Name = "ActionWheel",
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_backdrop);

        var veil = new WheelVeil { MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _backdrop.AddChild(veil);

        _heading = new Label
        {
            Text = "ROUE D’ACTIONS",
            AnchorLeft = .5f,
            AnchorRight = .5f,
            OffsetLeft = -320,
            OffsetTop = 58,
            OffsetRight = 320,
            OffsetBottom = 102,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        RuntimeRpgUiTheme.StyleTitle(_heading, 25, RuntimeRpgUiTheme.Bone);
        _backdrop.AddChild(_heading);

        var subtitle = new Label
        {
            Text = "CHOISISSEZ UNE INTENTION",
            AnchorLeft = .5f,
            AnchorRight = .5f,
            OffsetLeft = -220,
            OffsetTop = 98,
            OffsetRight = 220,
            OffsetBottom = 124,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        RuntimeRpgUiTheme.StyleBody(subtitle, 9, RuntimeRpgUiTheme.Gold);
        _backdrop.AddChild(subtitle);

        _ornament = new WheelOrnament
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = .5f,
            AnchorBottom = .5f,
            OffsetLeft = -285,
            OffsetTop = -285,
            OffsetRight = 285,
            OffsetBottom = 285,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _backdrop.AddChild(_ornament);

        var hub = new PanelContainer
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = .5f,
            AnchorBottom = .5f,
            OffsetLeft = -76,
            OffsetTop = -76,
            OffsetRight = 76,
            OffsetBottom = 76,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var hubStyle = RuntimeRpgUiTheme.Circle(new Color("17110ef2"), RuntimeRpgUiTheme.Gold, 76);
        hubStyle.BorderWidthLeft = 2;
        hubStyle.BorderWidthTop = 2;
        hubStyle.BorderWidthRight = 2;
        hubStyle.BorderWidthBottom = 2;
        hub.AddThemeStyleboxOverride("panel", hubStyle);
        _backdrop.AddChild(hub);
        var hubStack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        hubStack.Alignment = BoxContainer.AlignmentMode.Center;
        hubStack.AddThemeConstantOverride("separation", 3);
        hub.AddChild(hubStack);
        var mark = new Label { Text = "◆", HorizontalAlignment = HorizontalAlignment.Center };
        RuntimeRpgUiTheme.StyleTitle(mark, 18, RuntimeRpgUiTheme.Gold);
        hubStack.AddChild(mark);
        _centerLabel = new Label { Text = "ACTIONS", HorizontalAlignment = HorizontalAlignment.Center };
        RuntimeRpgUiTheme.StyleTitle(_centerLabel, 14, RuntimeRpgUiTheme.Bone);
        hubStack.AddChild(_centerLabel);
        var hint = new Label { Text = "TAB · FERMER", HorizontalAlignment = HorizontalAlignment.Center };
        RuntimeRpgUiTheme.StyleBody(hint, 8, RuntimeRpgUiTheme.Quiet);
        hubStack.AddChild(hint);

        _accessibleSummary = new Label
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -390,
            OffsetTop = -74,
            OffsetRight = 390,
            OffsetBottom = -31,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        RuntimeRpgUiTheme.StyleBody(_accessibleSummary, 9, RuntimeRpgUiTheme.Muted);
        _backdrop.AddChild(_accessibleSummary);
    }

    private void Open(string parentKey)
    {
        if (_profile is null || _backdrop is null) return;
        var wasOpen = IsOpen;
        _parentKey = parentKey;
        foreach (var button in _buttons) button.QueueFree();
        _buttons.Clear();
        var page = RadialActionResolver.Open(_profile.RadialMenu, parentKey);
        var entries = page.Actions.ToList();
        if (page.CanNavigateBack)
        {
            entries.Add(new RadialActionDefinition(Guid.NewGuid(), "back", "Retour", "Revenir au menu précédent",
                RadialActionKind.Navigation, "back", string.Empty, int.MaxValue, false, []));
        }

        var center = _backdrop.Size * .5f;
        var radius = Mathf.Clamp(Math.Min(_backdrop.Size.X, _backdrop.Size.Y) * .29f, 190, 244);
        for (var index = 0; index < entries.Count; index++)
        {
            var action = entries[index];
            var angle = -Mathf.Pi / 2 + Mathf.Tau * index / Math.Max(1, entries.Count);
            var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius - new Vector2(92, 31);
            var accent = WheelAccent(index);
            var button = new Button
            {
                Text = $"{index + 1:00}   {action.Label}",
                TooltipText = action.AccessibleLabel,
                Position = position,
                Size = new Vector2(184, 62),
                FocusMode = Control.FocusModeEnum.All,
                Alignment = HorizontalAlignment.Left,
            };
            button.AddThemeFontOverride("font", RuntimeRpgUiTheme.BodyFont);
            button.AddThemeFontSizeOverride("font_size", 11);
            button.AddThemeColorOverride("font_color", RuntimeRpgUiTheme.Bone);
            button.AddThemeColorOverride("font_hover_color", RuntimeRpgUiTheme.Bone);
            button.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
                new Color("17110ee8"), accent.Darkened(.2f), 13, 8));
            button.AddThemeStyleboxOverride("hover", RuntimeRpgUiTheme.Frame(
                new Color("3a291def"), accent, 13, 8));
            button.AddThemeStyleboxOverride("pressed", RuntimeRpgUiTheme.Frame(
                new Color("543621f4"), RuntimeRpgUiTheme.Bone, 13, 8));
            button.AddThemeStyleboxOverride("focus", RuntimeRpgUiTheme.Frame(
                new Color("302219ef"), RuntimeRpgUiTheme.Gold, 13, 8));
            button.Pressed += () => Select(action);
            _backdrop.AddChild(button);
            _buttons.Add(button);
        }

        if (_heading is not null)
            _heading.Text = parentKey.Length == 0 ? "ROUE D’ACTIONS" : parentKey.ToUpperInvariant();
        if (_centerLabel is not null) _centerLabel.Text = parentKey.Length == 0 ? "ACTIONS" : "SOUS-MENU";
        if (_accessibleSummary is not null)
            _accessibleSummary.Text = page.AccessibleSummary + " · Échap ou Tab pour fermer";
        if (_ornament is not null)
        {
            _ornament.EntryCount = entries.Count;
            _ornament.QueueRedraw();
        }
        _backdrop.Visible = true;
        if (!wasOpen) WheelVisibilityChanged?.Invoke(true);
        _buttons.FirstOrDefault()?.GrabFocus();
    }

    private void Select(RadialActionDefinition action)
    {
        if (action.Command == "back")
        {
            Open(string.Empty);
            return;
        }
        if (action.Command.StartsWith("open:", StringComparison.Ordinal))
        {
            Open(action.Command[5..]);
            return;
        }

        Close();
        CommandRequested?.Invoke(action.Command);
    }

    private static Color WheelAccent(int index) => (index % 6) switch
    {
        0 => RuntimeRpgUiTheme.Burgundy,
        1 => RuntimeRpgUiTheme.Gold,
        2 => RuntimeRpgUiTheme.Sage,
        3 => RuntimeRpgUiTheme.Violet,
        4 => RuntimeRpgUiTheme.FocusBlue,
        _ => RuntimeRpgUiTheme.Ember,
    };

    private sealed partial class WheelVeil : Control
    {
        public override void _Draw()
        {
            var bounds = new Rect2(Vector2.Zero, Size);
            DrawRect(bounds, new Color("070403c4"));
            DrawCircle(bounds.GetCenter(), Math.Min(Size.X, Size.Y) * .40f, new Color("2b1d142f"));
            DrawCircle(bounds.GetCenter(), Math.Min(Size.X, Size.Y) * .25f, new Color("0906048a"));
        }
    }

    private sealed partial class WheelOrnament : Control
    {
        public int EntryCount { get; set; }

        public override void _Draw()
        {
            var center = Size * .5f;
            var radius = Math.Min(Size.X, Size.Y) * .42f;
            DrawArc(center, radius, 0, Mathf.Tau, 96, new Color("a9855752"), 1.3f, true);
            DrawArc(center, radius * .48f, 0, Mathf.Tau, 72, new Color("a9855738"), 1, true);
            for (var index = 0; index < EntryCount; index++)
            {
                var angle = -Mathf.Pi / 2 + Mathf.Tau * index / Math.Max(1, EntryCount);
                var direction = Vector2.FromAngle(angle);
                DrawLine(center + direction * radius * .50f, center + direction * radius * .82f,
                    new Color("a9855733"), 1, true);
                DrawCircle(center + direction * radius, 3, RuntimeRpgUiTheme.Gold.Darkened(.1f));
            }
        }
    }
}
