using Forge.Domain.RuntimeExtensions;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeHud : CanvasLayer
{
    private Label? _objective;
    private Label? _message;
    private Label? _state;
    private Label? _tactical;
    private Label? _rules;
    private Label? _caption;
    private Label? _playerIdentity;
    private Label? _healthText;
    private ProgressBar? _healthBar;
    private Label? _fps;

    public string ModuleTitle { get; init; } = "OpenRPG Forge";

    public bool CombatEnabled { get; init; }

    public AccessibilityQualityProfile? Accessibility { get; init; }

    public bool FpsVisible => _fps?.Visible == true;

    public override void _Ready()
    {
        BuildPlayerFrame();
        BuildObjectiveTracker();
        BuildMessageToast();
        BuildPerformanceCounter();
        ApplyAccessibility();
    }

    private void BuildPlayerFrame()
    {
        var margin = new MarginContainer
        {
            OffsetLeft = 18,
            OffsetTop = 18,
            OffsetRight = 330,
            OffsetBottom = 118,
        };
        AddChild(margin);

        var panel = Panel(new Color("141827e6"), new Color("52718f"));
        margin.AddChild(panel);
        var content = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        content.AddThemeConstantOverride("separation", 4);
        panel.AddChild(content);

        _playerIdentity = CompactLabel($"{ModuleTitle} · Niv. 1", 14, new Color("f0d89b"));
        content.AddChild(_playerIdentity);

        var healthRow = new HBoxContainer();
        healthRow.AddThemeConstantOverride("separation", 8);
        content.AddChild(healthRow);
        _healthBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(190, 12),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _healthBar.AddThemeStyleboxOverride("background", BarStyle(new Color("0c101b")));
        _healthBar.AddThemeStyleboxOverride("fill", BarStyle(new Color("3db66b")));
        healthRow.AddChild(_healthBar);
        _healthText = CompactLabel("100 / 100", 11, new Color("dce9df"));
        healthRow.AddChild(_healthText);

        _tactical = CompactLabel("Temps réel · Sol", 11, new Color("99b9cf"));
        _rules = CompactLabel("Standard · 0 XP", 11, new Color("8fc8ad"));
        content.AddChild(_tactical);
        content.AddChild(_rules);
    }

    private void BuildObjectiveTracker()
    {
        var margin = new MarginContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -370,
            OffsetTop = 18,
            OffsetRight = -18,
            OffsetBottom = 145,
        };
        AddChild(margin);
        var panel = Panel(new Color("111522d9"), new Color("70674e"));
        margin.AddChild(panel);
        var content = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        content.AddThemeConstantOverride("separation", 4);
        panel.AddChild(content);
        content.AddChild(CompactLabel("OBJECTIF", 11, new Color("c9aa67")));
        _objective = CompactLabel("Parler au guide [E]", 14, Colors.White, wrap: true);
        _state = CompactLabel(string.Empty, 11, new Color("aab6c5"), wrap: true);
        content.AddChild(_objective);
        content.AddChild(_state);
        _caption = CompactLabel(string.Empty, 11, new Color("fff1cf"), wrap: true);
        _caption.Visible = Accessibility?.RuntimeCaptions == true || Accessibility?.RuntimeSubtitles == true;
        content.AddChild(_caption);
    }

    private void BuildMessageToast()
    {
        var margin = new MarginContainer
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -290,
            OffsetTop = -158,
            OffsetRight = 290,
            OffsetBottom = -122,
        };
        AddChild(margin);
        var panel = Panel(new Color("0b0e17c7"), new Color("3a4558"));
        margin.AddChild(panel);
        _message = CompactLabel("Explorez le monde.", 12, new Color("dce5ef"), wrap: true);
        _message.HorizontalAlignment = HorizontalAlignment.Center;
        _message.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(_message);
    }

    private void BuildPerformanceCounter()
    {
        _fps = CompactLabel("FPS 0", 12, new Color("9fe3aa"));
        _fps.OffsetLeft = 18;
        _fps.OffsetTop = 126;
        _fps.OffsetRight = 110;
        _fps.OffsetBottom = 151;
        _fps.Visible = false;
        _fps.AddThemeStyleboxOverride("normal", BarStyle(new Color("0b0e17c7")));
        AddChild(_fps);
    }

    private void ApplyAccessibility()
    {
        var scale = (float)(Accessibility?.RuntimeTextScale ?? 1);
        foreach (var label in new[] { _objective, _message, _state, _tactical, _rules, _caption, _playerIdentity,
                     _healthText, _fps }.OfType<Label>())
        {
            var currentSize = label.GetThemeFontSize("font_size");
            label.AddThemeFontSizeOverride("font_size", Math.Max(11, (int)Math.Round(currentSize * scale)));
            label.SetMeta("accessible_label", label.Text);
        }
    }

    public void UpdatePlayerStatus(string displayName, int health, int maximumHealth, int level)
    {
        if (_playerIdentity is not null) _playerIdentity.Text = $"{displayName} · Niv. {level}";
        if (_healthBar is not null)
        {
            _healthBar.MaxValue = Math.Max(1, maximumHealth);
            _healthBar.Value = Math.Clamp(health, 0, Math.Max(1, maximumHealth));
        }
        if (_healthText is not null) _healthText.Text = $"{health} / {maximumHealth}";
    }

    public void UpdateState(string objective, string message, string state)
    {
        if (_objective is not null) _objective.Text = objective;
        if (_message is not null) _message.Text = message;
        if (_state is not null) _state.Text = state;
        if (_caption is not null && Accessibility?.RuntimeSubtitles == true)
        {
            _caption.Text = message;
            _caption.SetMeta("accessible_label", _caption.Text);
        }
    }

    public void UpdateTactical(string timeState, string combatMode, int queuedActions,
        string movementCapability, float breath, float stamina)
    {
        if (_tactical is null) return;
        _tactical.Text = $"{ShortTimeState(timeState)} · {movementCapability} · File {queuedActions} · " +
                         $"Souffle {breath:0} · Endurance {stamina:0}";
        _tactical.TooltipText = $"Combat : {combatMode}";
    }

    public void UpdateRules(string difficulty, int level, double experience, double? nextLevel,
        string pvpMode, int perceptionModifiers)
    {
        if (_rules is null) return;
        var progression = nextLevel is null ? $"{experience:0} XP" : $"{experience:0}/{nextLevel:0} XP";
        _rules.Text = $"{difficulty} · Niv. {level} · {progression}";
        _rules.TooltipText = $"PvP {pvpMode} · Perception {perceptionModifiers}";
    }

    public void SetFpsVisible(bool visible)
    {
        if (_fps is not null) _fps.Visible = visible;
    }

    public void UpdatePerformance(double framesPerSecond)
    {
        if (_fps is not null && _fps.Visible) _fps.Text = $"FPS {framesPerSecond:0}";
    }

    private static string ShortTimeState(string value) => value switch
    {
        "Paused" => "Pause",
        "SlowMotion" => "Ralenti",
        _ => "Temps réel",
    };

    private static Label CompactLabel(string text, int size, Color color, bool wrap = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            ClipText = !wrap,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private PanelContainer Panel(Color background, Color border)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = Accessibility?.RuntimeHighContrast == true ? new Color("070910") : background,
            BorderColor = Accessibility?.RuntimeHighContrast == true ? new Color("fff1cf") : border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 12,
            ContentMarginTop = 8,
            ContentMarginRight = 12,
            ContentMarginBottom = 8,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static StyleBoxFlat BarStyle(Color color) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = 5,
        ContentMarginRight = 5,
    };
}
