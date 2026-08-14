using Forge.Application.Areas;
using Forge.Application.Navigation;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.WorldFeatures;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeHud : CanvasLayer
{
    private const string ElyraPortrait = "res://assets/ui/portraits/elyra.png";
    private const string BromPortrait = "res://assets/ui/portraits/brom.png";
    private const string NahlaPortrait = "res://assets/ui/portraits/nahla.png";
    private const string SorenPortrait = "res://assets/ui/portraits/soren.png";

    private Control? _locationReveal;
    private Label? _areaTitle;
    private Label? _moduleTitle;
    private Label? _objective;
    private Label? _objectiveDistance;
    private Label? _message;
    private Label? _state;
    private Label? _tactical;
    private Label? _rules;
    private Label? _caption;
    private Label? _waypointMarker;
    private Label? _playerIdentity;
    private Label? _healthText;
    private RuntimeAnimatedBar? _healthBar;
    private RuntimeAnimatedBar? _focusBar;
    private Control? _targetFrame;
    private Label? _targetIdentity;
    private Label? _targetHealthText;
    private Label? _targetState;
    private RuntimeAnimatedBar? _targetHealthBar;
    private Label? _fps;
    private Label? _partyHudText;
    private RuntimeAnimatedBar? _partyHealthBar;
    private RuntimeAnimatedBar? _partyFocusBar;
    private PanelContainer? _interactionPrompt;
    private Label? _interactionTitle;
    private Control? _partyHudPanel;
    private int _partyHudQuickCommandCount;
    private double _locationRevealAge;

    private bool LargeText => MotionPolicy.TextScale > 1.5f;

    public string ModuleTitle { get; init; } = "OpenRPG Forge";

    public string AreaTitle { get; init; } = "Sanctuaire extérieur";

    public string InteractionLabel { get; init; } = "Interagir";

    public bool CombatEnabled { get; init; }

    public AccessibilityQualityProfile? Accessibility { get; init; }

    public CompanionWorldProfile.PartyHudProfile? PartyHud { get; init; }

    internal RuntimeUiMotionPolicy MotionPolicy { get; init; } = RuntimeUiMotionPolicy.Create(null, []);

    public event Action? PauseRequested;

    public event Action? JournalRequested;

    public bool FpsVisible => _fps?.Visible == true;

    public string CaptionText => _caption?.Text ?? string.Empty;

    public string WaypointMarkerText => _waypointMarker?.Text ?? string.Empty;

    public bool PartyHudVisible => _partyHudPanel?.Visible == true;

    public string PartyHudText => _partyHudText?.Text ?? string.Empty;

    public int PartyHudQuickCommandCount => _partyHudQuickCommandCount;

    public bool InteractionPromptVisible => _interactionPrompt?.Visible == true;

    public bool TargetFrameVisible => _targetFrame?.Visible == true;

    public string TargetFrameText => _targetIdentity?.Text ?? string.Empty;

    public double TargetHealthDisplayed => _targetHealthBar?.FrontValue ?? 0;

    internal double PlayerHealthDisplayed => _healthBar?.FrontValue ?? 0;

    internal double PlayerHealthChipDisplayed => _healthBar?.ChipValue ?? 0;

    internal bool PlayerHealthAnimating => _healthBar?.Animating == true;

    internal bool TargetHealthAnimating => _targetHealthBar?.Animating == true;

    public void RequestPause() => PauseRequested?.Invoke();

    public override void _Ready()
    {
        Layer = 10;
        BuildWorldTreatment();
        BuildLocationReveal();
        BuildPartyFrame();
        BuildPlayerFrame();
        BuildTargetFrame();
        BuildObjectiveTracker();
        BuildInteractionPrompt();
        BuildMessageState();
        BuildControlHints();
        BuildPerformanceCounter();
        ApplyAccessibility();
    }

    public override void _Process(double delta)
    {
        if (_locationReveal is null || !_locationReveal.Visible)
            return;

        _locationRevealAge += delta;
        if (MotionPolicy.ReducedMotion)
        {
            if (_locationRevealAge > 8) _locationReveal.Visible = false;
            return;
        }
        if (_locationRevealAge <= 8) return;
        var alpha = 1f - Mathf.Clamp((float)(_locationRevealAge - 8) / 2.2f, 0, 1);
        _locationReveal.Modulate = new Color(1, 1, 1, alpha);
        if (alpha <= .01f) _locationReveal.Visible = false;
    }

    private void BuildWorldTreatment()
    {
        var treatment = new ColorRect
        {
            Name = "OpenRpgWorldTreatment",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        treatment.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        treatment.Material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                    shader_type canvas_item;

                    void fragment() {
                        vec2 centered = UV - vec2(0.5);
                        float edge = smoothstep(0.30, 0.78, length(centered));
                        float lower = smoothstep(0.58, 1.0, UV.y);
                        float upper = 1.0 - smoothstep(0.0, 0.18, UV.y);
                        float grain = fract(sin(dot(UV * vec2(173.3, 119.7), vec2(12.9898, 78.233))) * 43758.5453);
                        float alpha = edge * 0.17 + lower * 0.08 + upper * 0.035 + grain * 0.006;
                        COLOR = vec4(0.075, 0.052, 0.035, alpha);
                    }
                    """,
            },
        };
        AddChild(treatment);
    }

    private void BuildLocationReveal()
    {
        _locationReveal = new Control
        {
            Name = "LocationReveal",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _locationReveal.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_locationReveal);

        var location = new VBoxContainer
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            OffsetLeft = -300,
            OffsetTop = 28,
            OffsetRight = 300,
            OffsetBottom = LargeText ? 126 : 96,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        location.AddThemeConstantOverride("separation", 5);
        _locationReveal.AddChild(location);

        _moduleTitle = BodyLabel(ModuleTitle, 8, RuntimeRpgUiTheme.Quiet);
        _moduleTitle.Visible = false;
        location.AddChild(_moduleTitle);

        _areaTitle = TitleLabel(AreaTitle.ToUpperInvariant(), 27, RuntimeRpgUiTheme.Bone);
        _areaTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _areaTitle.AddThemeConstantOverride("outline_size", 2);
        _areaTitle.AddThemeColorOverride("font_outline_color", new Color("0a0705b8"));
        location.AddChild(_areaTitle);

        var ornament = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        ornament.AddThemeConstantOverride("separation", 8);
        location.AddChild(ornament);
        ornament.AddChild(LocationRule());
        ornament.AddChild(TitleLabel("◆", 7, RuntimeRpgUiTheme.Gold));
        ornament.AddChild(LocationRule());
    }

    private static ColorRect LocationRule() => new()
    {
        Color = new Color("b08a596c"),
        CustomMinimumSize = new Vector2(86, 1),
        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private void BuildPartyFrame()
    {
        var party = new VBoxContainer
        {
            Name = "PartyHudPanel",
            OffsetLeft = 22,
            OffsetTop = 130,
            OffsetRight = 92,
            OffsetBottom = 420,
        };
        party.AddThemeConstantOverride("separation", 7);
        _partyHudPanel = party;
        party.SetMeta("portraits", PartyHud?.Portraits ?? true);
        party.SetMeta("health", PartyHud?.Health ?? true);
        party.SetMeta("resources", PartyHud?.Resources ?? true);
        party.SetMeta("statuses", PartyHud?.Statuses ?? true);
        party.SetMeta("reorder", PartyHud?.Reorder ?? true);
        party.SetMeta("keyboard", PartyHud?.Keyboard ?? true);
        party.SetMeta("gamepad", PartyHud?.Gamepad ?? true);
        party.SetMeta("screen_reader", PartyHud?.ScreenReaderLabels ?? true);
        party.SetMeta("high_contrast", PartyHud?.HighContrast ?? false);
        AddChild(party);

        party.AddChild(BuildPartyMedallion("Elyra", "Lame-scribe", ElyraPortrait,
            RuntimeRpgUiTheme.Gold, 82, 44, selected: true));
        party.AddChild(BuildPartyMedallion("Brom", "Sentinelle", BromPortrait,
            RuntimeRpgUiTheme.Sage, 94, 66, selected: false));
        party.AddChild(BuildPartyMedallion("Nahla", "Arcaniste", NahlaPortrait,
            RuntimeRpgUiTheme.Violet, 71, 88, selected: false));
        party.AddChild(BuildPartyMedallion("Soren", "Éclaireur", SorenPortrait,
            RuntimeRpgUiTheme.Ember, 76, 52, selected: false));

        _partyHudText = BodyLabel("Elyra · NIV. 18", 9, RuntimeRpgUiTheme.Parchment);
        _partyHudText.Visible = false;
        party.AddChild(_partyHudText);

        // The authored companion command contract remains available to gameplay code;
        // its buttons stay out of the exploration composition.
        var commands = new HBoxContainer { Visible = false, Name = "PartyQuickCommands" };
        foreach (var command in new[] { "Suivre", "Tenir", "Protéger" })
        {
            var button = new Button { Text = command, FocusMode = Control.FocusModeEnum.All };
            button.SetMeta("accessible_label", $"Commande rapide {command}");
            commands.AddChild(button);
            _partyHudQuickCommandCount++;
        }
        party.AddChild(commands);
    }

    private Button BuildPartyMedallion(string displayName, string role, string portraitPath, Color accent,
        double health, double focus, bool selected)
    {
        var button = new Button
        {
            Name = $"PartyMember{displayName}",
            TooltipText = $"{displayName} · {role}\n{health:0}% PV · {focus:0}% ressource",
            CustomMinimumSize = new Vector2(68, 68),
            FocusMode = Control.FocusModeEnum.All,
        };
        button.AddThemeStyleboxOverride("normal", MedallionStyle(selected ? RuntimeRpgUiTheme.Gold : accent.Darkened(.2f), selected));
        button.AddThemeStyleboxOverride("hover", MedallionStyle(RuntimeRpgUiTheme.Gold.Lightened(.12f), true));
        button.AddThemeStyleboxOverride("pressed", MedallionStyle(RuntimeRpgUiTheme.Bone, true));
        button.AddThemeStyleboxOverride("focus", MedallionStyle(RuntimeRpgUiTheme.Bone, true));
        button.SetMeta("accessible_label", $"{displayName}, niveau 18, {role}");

        var portrait = Portrait(portraitPath, 58);
        portrait.Position = new Vector2(5, 4);
        portrait.Size = new Vector2(58, 58);
        button.AddChild(portrait);

        var healthBar = CompactBar(health, 100, RuntimeUiSemanticColor.Damage, 3);
        healthBar.AnchorLeft = 0;
        healthBar.AnchorRight = 1;
        healthBar.AnchorTop = 1;
        healthBar.AnchorBottom = 1;
        healthBar.OffsetLeft = 10;
        healthBar.OffsetTop = -12;
        healthBar.OffsetRight = -10;
        healthBar.OffsetBottom = -9;
        button.AddChild(healthBar);

        var focusBar = CompactBar(focus, 100, RuntimeUiSemanticColor.Resource, 3);
        focusBar.AnchorLeft = 0;
        focusBar.AnchorRight = 1;
        focusBar.AnchorTop = 1;
        focusBar.AnchorBottom = 1;
        focusBar.OffsetLeft = 13;
        focusBar.OffsetTop = -7;
        focusBar.OffsetRight = -13;
        focusBar.OffsetBottom = -4;
        button.AddChild(focusBar);

        var status = new ColorRect
        {
            Color = selected ? RuntimeRpgUiTheme.Gold : accent,
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -8,
            OffsetTop = 9,
            OffsetRight = -4,
            OffsetBottom = 13,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        button.AddChild(status);

        if (selected)
        {
            _partyHealthBar = healthBar;
            _partyFocusBar = focusBar;
        }
        return button;
    }

    private void BuildPlayerFrame()
    {
        var margin = new MarginContainer
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = LargeText ? -548 : -488,
            OffsetTop = LargeText ? -138 : -104,
            OffsetRight = -169,
            OffsetBottom = -20,
        };
        AddChild(margin);

        var panel = Panel(new Color("17110ef2"), RuntimeRpgUiTheme.Border, 10, 8);
        margin.AddChild(panel);
        panel.AddChild(TopAccent());

        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(LargeText ? 348 : 218, 0),
        };
        row.AddThemeConstantOverride("separation", 9);
        panel.AddChild(row);

        var portrait = Portrait(ElyraPortrait, 60, RuntimeRpgUiTheme.Gold, selected: true);
        portrait.SetMeta("accessible_label", "Portrait du héros");
        row.AddChild(portrait);

        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", LargeText ? 3 : 2);
        row.AddChild(content);

        _playerIdentity = TitleLabel("Héros · NIV. 1", 14, RuntimeRpgUiTheme.Bone);
        content.AddChild(_playerIdentity);

        var healthRow = new HBoxContainer();
        healthRow.AddThemeConstantOverride("separation", 5);
        content.AddChild(healthRow);
        healthRow.AddChild(BodyLabel("PV", 7, RuntimeRpgUiTheme.Health.Lightened(.15f)));
        _healthBar = CompactBar(100, 100, RuntimeUiSemanticColor.Damage, 4);
        _healthBar.CustomMinimumSize = new Vector2(LargeText ? 116 : 86, 4);
        _healthBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        healthRow.AddChild(_healthBar);
        _healthText = BodyLabel("100 / 100", 7, RuntimeRpgUiTheme.Muted);
        healthRow.AddChild(_healthText);

        var focusRow = new HBoxContainer();
        focusRow.AddThemeConstantOverride("separation", 5);
        content.AddChild(focusRow);
        focusRow.AddChild(BodyLabel("RES", 7, RuntimeRpgUiTheme.FocusBlue.Lightened(.2f)));
        _focusBar = CompactBar(0, 1, RuntimeUiSemanticColor.Resource, 4);
        _focusBar.CustomMinimumSize = new Vector2(LargeText ? 116 : 86, 4);
        _focusBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        focusRow.AddChild(_focusBar);

        var context = new HBoxContainer();
        context.AddThemeConstantOverride("separation", 6);
        content.AddChild(context);
        _tactical = BodyLabel("Temps réel · Sol", 7, RuntimeRpgUiTheme.Quiet);
        _rules = BodyLabel("Standard · 0 XP", 7, RuntimeRpgUiTheme.Quiet);
        context.AddChild(_tactical);
        context.AddChild(_rules);
    }

    private void BuildTargetFrame()
    {
        var margin = new MarginContainer
        {
            Name = "CombatTargetFrame",
            AnchorLeft = .5f,
            AnchorRight = .5f,
            OffsetLeft = LargeText ? -250 : -178,
            OffsetTop = LargeText ? 110 : 104,
            OffsetRight = LargeText ? 250 : 178,
            OffsetBottom = LargeText ? 216 : 174,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _targetFrame = margin;
        AddChild(margin);

        var panel = Panel(new Color("17110eea"), RuntimeRpgUiTheme.Burgundy, 13, 8);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        margin.AddChild(panel);
        panel.AddChild(TopAccent());

        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 4);
        panel.AddChild(stack);

        var heading = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        heading.AddThemeConstantOverride("separation", 8);
        stack.AddChild(heading);
        _targetIdentity = TitleLabel("Cible", 13, RuntimeRpgUiTheme.Bone);
        _targetIdentity.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(_targetIdentity);
        _targetState = BodyLabel("CIBLE", 8, MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Alert));
        heading.AddChild(_targetState);

        var health = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        health.AddThemeConstantOverride("separation", 7);
        stack.AddChild(health);
        health.AddChild(BodyLabel("PV", 7,
            MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Damage).Lightened(.15f)));
        _targetHealthBar = CompactBar(1, 1, RuntimeUiSemanticColor.Damage, 5);
        _targetHealthBar.CustomMinimumSize = new Vector2(LargeText ? 270 : 190, 5);
        _targetHealthBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        health.AddChild(_targetHealthBar);
        _targetHealthText = BodyLabel("1 / 1", 8, RuntimeRpgUiTheme.Parchment);
        health.AddChild(_targetHealthText);
    }

    private void BuildObjectiveTracker()
    {
        var objectiveButton = new Button
        {
            Name = "CompactObjective",
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = LargeText ? -590 : -322,
            OffsetTop = 204,
            OffsetRight = -24,
            OffsetBottom = LargeText ? 328 : 254,
            FocusMode = Control.FocusModeEnum.All,
            TooltipText = "Ouvrir le journal de quête",
        };
        objectiveButton.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            new Color("130e0a9c"), new Color("a9855745"), 11, 7));
        objectiveButton.AddThemeStyleboxOverride("hover", RuntimeRpgUiTheme.Frame(
            new Color("2a1e16c8"), RuntimeRpgUiTheme.Border, 11, 7));
        objectiveButton.AddThemeStyleboxOverride("pressed", RuntimeRpgUiTheme.Frame(
            new Color("3b291ce0"), RuntimeRpgUiTheme.Gold, 11, 7));
        objectiveButton.Pressed += () => JournalRequested?.Invoke();
        AddChild(objectiveButton);

        var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        content.OffsetLeft = 11;
        content.OffsetTop = 7;
        content.OffsetRight = -11;
        content.OffsetBottom = -7;
        content.AddThemeConstantOverride("separation", 2);
        objectiveButton.AddChild(content);

        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 8);
        content.AddChild(line);
        line.AddChild(TitleLabel("◆", 8, RuntimeRpgUiTheme.Gold));
        _objective = TitleLabel("Explorer le monde", 14, RuntimeRpgUiTheme.Bone, wrap: true);
        _objective.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(_objective);

        _objectiveDistance = BodyLabel(string.Empty, 8, RuntimeRpgUiTheme.Muted);
        _objectiveDistance.Visible = false;
        content.AddChild(_objectiveDistance);
        _state = BodyLabel(string.Empty, 9, RuntimeRpgUiTheme.Muted, wrap: true);
        _state.Visible = false;
        content.AddChild(_state);
        _waypointMarker = BodyLabel(string.Empty, 9, RuntimeRpgUiTheme.Gold, wrap: true);
        _waypointMarker.Visible = false;
        content.AddChild(_waypointMarker);
        _caption = BodyLabel(string.Empty, 9, RuntimeRpgUiTheme.Parchment, wrap: true);
        _caption.Visible = false;
        content.AddChild(_caption);
        objectiveButton.SetMeta("accessible_label", "Objectif actif. Ouvrir le journal de quête");
    }

    private void BuildInteractionPrompt()
    {
        _interactionPrompt = new PanelContainer
        {
            Name = "WorldInteractionPrompt",
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = LargeText ? -220 : -170,
            OffsetTop = LargeText ? -218 : -158,
            OffsetRight = LargeText ? 220 : 170,
            OffsetBottom = LargeText ? -146 : -112,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _interactionPrompt.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(
            new Color("17110eb8"), new Color("a985575f"), 6, 4));
        _interactionPrompt.SetMeta("accessible_label", InteractionLabel);
        AddChild(_interactionPrompt);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        row.OffsetLeft = 9;
        row.OffsetTop = 5;
        row.OffsetRight = -9;
        row.OffsetBottom = -5;
        row.AddThemeConstantOverride("separation", 9);
        _interactionPrompt.AddChild(row);

        var key = BodyLabel("E", 9, new Color("30261d"));
        key.CustomMinimumSize = LargeText ? new Vector2(42, 42) : new Vector2(27, 25);
        key.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        key.HorizontalAlignment = HorizontalAlignment.Center;
        key.VerticalAlignment = VerticalAlignment.Center;
        key.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            RuntimeRpgUiTheme.Bone, new Color("6e5a41"), 3, 1));
        row.AddChild(key);
        _interactionTitle = TitleLabel(InteractionLabel, 11, RuntimeRpgUiTheme.Bone);
        _interactionTitle.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_interactionTitle);
    }

    private void BuildMessageState()
    {
        _message = BodyLabel(string.Empty, 9, RuntimeRpgUiTheme.Parchment, wrap: true);
        _message.Name = "RuntimeMessageState";
        _message.Visible = false;
        AddChild(_message);
    }

    private void BuildControlHints()
    {
        var hints = new HBoxContainer
        {
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = 28,
            OffsetTop = LargeText ? -68 : -54,
            OffsetRight = 290,
            OffsetBottom = -24,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hints.AddThemeConstantOverride("separation", 13);
        AddChild(hints);
        AddHint(hints, "Actions", "TAB");
        AddHint(hints, "Inventaire", "I");
    }

    private void AddHint(HBoxContainer parent, string text, string key)
    {
        var item = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        item.AddThemeConstantOverride("separation", 5);
        item.AddChild(Keycap(key));
        var label = BodyLabel(text.ToUpperInvariant(), 8, RuntimeRpgUiTheme.Muted);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        item.AddChild(label);
        parent.AddChild(item);
    }

    private Label Keycap(string text)
    {
        var key = BodyLabel(text, 7, RuntimeRpgUiTheme.Bone);
        key.HorizontalAlignment = HorizontalAlignment.Center;
        key.VerticalAlignment = VerticalAlignment.Center;
        key.CustomMinimumSize = LargeText
            ? new Vector2(Math.Max(34, text.Length * 9 + 12), 30)
            : new Vector2(Math.Max(19, text.Length * 5 + 8), 17);
        key.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            new Color("1b1510d8"), new Color("9b794f72"), 3, 1));
        return key;
    }

    private void BuildPerformanceCounter()
    {
        _fps = BodyLabel("FPS 0", 9, RuntimeRpgUiTheme.Sage.Lightened(.2f));
        _fps.AnchorTop = 1;
        _fps.AnchorBottom = 1;
        _fps.OffsetLeft = 28;
        _fps.OffsetTop = -82;
        _fps.OffsetRight = 98;
        _fps.OffsetBottom = -60;
        _fps.Visible = false;
        _fps.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            new Color("0e0b09c8"), RuntimeRpgUiTheme.BorderMuted, 6, 2));
        AddChild(_fps);
    }

    private void ApplyAccessibility()
    {
        var scale = MotionPolicy.TextScale;
        foreach (var label in DescendantLabels(this))
        {
            var currentSize = label.GetThemeFontSize("font_size");
            label.AddThemeFontSizeOverride("font_size", Math.Max(8, (int)Math.Round(currentSize * scale)));
            if (!label.HasMeta("accessible_label") && !string.IsNullOrWhiteSpace(label.Text))
                label.SetMeta("accessible_label", label.Text);
        }
    }

    private static IEnumerable<Label> DescendantLabels(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label;
            foreach (var descendant in DescendantLabels(child)) yield return descendant;
        }
    }

    public void UpdatePlayerStatus(
        string displayName,
        int health,
        int maximumHealth,
        int resource,
        int maximumResource,
        int level)
    {
        if (_playerIdentity is not null) _playerIdentity.Text = $"{displayName} · NIV. {level}";
        _healthBar?.SetAuthoritativeValue(health, Math.Max(1, maximumHealth));
        _focusBar?.SetAuthoritativeValue(resource, Math.Max(1, maximumResource));
        if (_healthText is not null) _healthText.Text = $"{health} / {maximumHealth}";
    }

    public void UpdateTargetStatus(string displayName, int health, int maximumHealth, bool boss, bool defeated)
    {
        if (_targetFrame is null || _targetIdentity is null || _targetHealthText is null ||
            _targetState is null || _targetHealthBar is null)
            return;
        _targetFrame.Visible = true;
        _targetIdentity.Text = displayName;
        _targetHealthText.Text = $"{health} / {maximumHealth}";
        _targetState.Text = defeated ? "VAINCU" : boss ? "BOSS" : "CIBLE";
        _targetHealthBar.SetAuthoritativeValue(health, Math.Max(1, maximumHealth));
        _targetFrame.SetMeta("accessible_label",
            $"Cible {displayName}, {health} sur {maximumHealth} points de vie, " +
            _targetState.Text.ToLowerInvariant());
    }

    public void ClearTarget()
    {
        if (_targetFrame is not null)
        {
            _targetFrame.Visible = false;
            _targetFrame.SetMeta("accessible_label", string.Empty);
        }
        if (_targetIdentity is not null) _targetIdentity.Text = string.Empty;
        if (_targetState is not null) _targetState.Text = string.Empty;
    }

    public void UpdatePartyStatus(string displayName, int health, int maximumHealth,
        IReadOnlyDictionary<string, double> resources, IReadOnlyList<string> statuses)
    {
        if (_partyHudText is null) return;
        _partyHudText.Text = $"{displayName} · NIV. 18";
        var resourceText = string.Join(", ", resources.Select(item => $"{item.Key} {item.Value:0.##}"));
        var statusText = statuses.Count == 0 ? "aucun statut" : string.Join(", ", statuses);
        _partyHudText.TooltipText = $"{health}/{maximumHealth} PV · {resourceText} · {statusText}";
        _partyHudText.SetMeta("accessible_label",
            $"{displayName}, {health} sur {maximumHealth} points de vie, {statusText}");
        _partyHealthBar?.SetAuthoritativeValue(health, Math.Max(1, maximumHealth));
        if (_partyFocusBar is not null && resources.TryGetValue("focus", out var focus))
        {
            _partyFocusBar.SetAuthoritativeValue(focus, Math.Max(10, focus));
        }
    }

    public void UpdateState(string objective, string message, string state)
    {
        var cleanObjective = objective.Replace("[E]", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (_objective is not null) _objective.Text = cleanObjective;
        if (_message is not null) _message.Text = message;
        if (_state is not null) _state.Text = state;
        if (_interactionPrompt is not null && _interactionTitle is not null)
        {
            var interactable = objective.Contains("[E]", StringComparison.OrdinalIgnoreCase);
            _interactionPrompt.Visible = interactable;
            _interactionTitle.Text = interactable ? cleanObjective : InteractionLabel;
            _interactionPrompt.SetMeta("accessible_label", _interactionTitle.Text);
        }
    }

    public void ShowSoundCaption(AudioCaptionPresentation caption)
    {
        ArgumentNullException.ThrowIfNull(caption);
        if (_caption is null || Accessibility?.RuntimeCaptions != true) return;
        var direction = caption.Direction switch
        {
            AudioCaptionDirection.Left => "← ",
            AudioCaptionDirection.Right => "→ ",
            AudioCaptionDirection.Rear => "↓ ",
            AudioCaptionDirection.Front => "↑ ",
            _ => string.Empty,
        };
        _caption.Text = $"{direction}{caption.Text}";
        _caption.TooltipText = $"{caption.CueKind} · {caption.Importance} · {caption.Locale}";
        _caption.SetMeta("accessible_label", caption.AccessibleText);
        _caption.Visible = true;
    }

    public void ClearSoundCaption()
    {
        if (_caption is null) return;
        _caption.Text = string.Empty;
        _caption.SetMeta("accessible_label", string.Empty);
    }

    public void ShowWaypointMarker(WaypointMapPresentation marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        if (_waypointMarker is null) return;
        _waypointMarker.Text = $"◆ {marker.Text} · étage {marker.Floor}" + (marker.Tracked ? " · suivi" : string.Empty);
        _waypointMarker.TooltipText = $"{marker.Category} · précision {marker.Precision:0.#} m · {marker.Scope}";
        _waypointMarker.SetMeta("accessible_label", marker.AccessibleText);
        _waypointMarker.Visible = true;
    }

    public void ClearWaypointMarker()
    {
        if (_waypointMarker is null) return;
        _waypointMarker.Text = string.Empty;
        _waypointMarker.SetMeta("accessible_label", string.Empty);
        _waypointMarker.Visible = false;
    }

    public void UpdateTactical(string timeState, string combatMode, int queuedActions,
        string movementCapability, float breath, float stamina)
    {
        if (_tactical is null) return;
        _tactical.Text = $"{ShortTimeState(timeState)} · {ShortMovement(movementCapability)} · file {queuedActions}";
        _tactical.TooltipText = $"Combat : {combatMode} · Souffle {breath:0} · Endurance {stamina:0}";
    }

    public void UpdateRules(string difficulty, int level, double experience, double? nextLevel,
        string pvpMode, int perceptionModifiers)
    {
        if (_rules is null) return;
        var progression = nextLevel is null ? $"{experience:0} XP" : $"{experience:0}/{nextLevel:0} XP";
        _rules.Text = $"{difficulty} · {progression}";
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

    private static string ShortMovement(string value) => value switch
    {
        "ground" => "sol",
        "swimming" => "nage",
        "diving" => "plongée",
        "climbing" => "escalade",
        _ => value,
    };

    private static Control Portrait(string path, int size, Color? rim = null, bool selected = false)
    {
        var root = new Control
        {
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var backdrop = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Circle(
            new Color("100c09"), new Color("493b2d"), size / 2));
        root.AddChild(backdrop);

        var portrait = new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = PortraitMaskMaterial(),
        };
        portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        portrait.OffsetLeft = 3;
        portrait.OffsetTop = 3;
        portrait.OffsetRight = -3;
        portrait.OffsetBottom = -3;
        root.AddChild(portrait);

        var ring = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        ring.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ring.AddThemeStyleboxOverride("panel", MedallionStyle(rim ?? RuntimeRpgUiTheme.Bronze, selected));
        root.AddChild(ring);
        return root;
    }

    private static ShaderMaterial PortraitMaskMaterial() => new()
    {
        Shader = new Shader
        {
            Code = """
                shader_type canvas_item;

                void fragment() {
                    vec2 p = UV * 2.0 - vec2(1.0);
                    float alpha = 1.0 - smoothstep(0.94, 1.0, length(p));
                    vec4 source = texture(TEXTURE, UV);
                    vec3 lifted = source.rgb * 1.28 + vec3(0.018, 0.012, 0.008);
                    COLOR = vec4(lifted, source.a * alpha);
                }
                """,
        },
    };

    private static StyleBoxFlat MedallionStyle(Color border, bool selected) => new()
    {
        BgColor = new Color("00000000"),
        BorderColor = border,
        BorderWidthLeft = selected ? 2 : 1,
        BorderWidthTop = selected ? 2 : 1,
        BorderWidthRight = selected ? 2 : 1,
        BorderWidthBottom = selected ? 2 : 1,
        CornerRadiusTopLeft = 34,
        CornerRadiusTopRight = 34,
        CornerRadiusBottomLeft = 34,
        CornerRadiusBottomRight = 34,
        ShadowColor = selected ? new Color("ad693d4d") : new Color("00000066"),
        ShadowSize = selected ? 5 : 3,
    };

    private RuntimeAnimatedBar CompactBar(
        double value,
        double maximum,
        RuntimeUiSemanticColor semanticColor,
        float height)
    {
        var bar = new RuntimeAnimatedBar(MotionPolicy, semanticColor, height);
        bar.SetAuthoritativeValue(value, Math.Max(1, maximum));
        return bar;
    }

    private static ColorRect TopAccent() => new()
    {
        Color = new Color("b08a5970"),
        AnchorRight = 1,
        OffsetLeft = 10,
        OffsetTop = 1,
        OffsetRight = -10,
        OffsetBottom = 2,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static Label BodyLabel(string text, int size, Color color, bool wrap = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            ClipText = !wrap,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        RuntimeRpgUiTheme.StyleBody(label, size, color);
        return label;
    }

    private static Label TitleLabel(string text, int size, Color color, bool wrap = false)
    {
        var label = BodyLabel(text, size, color, wrap);
        RuntimeRpgUiTheme.StyleTitle(label, size, color);
        return label;
    }

    private PanelContainer Panel(Color background, Color border, int horizontalMargin = 10, int verticalMargin = 7)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(
            Accessibility?.RuntimeHighContrast == true ? new Color("070504") : background,
            Accessibility?.RuntimeHighContrast == true ? RuntimeRpgUiTheme.Parchment : border,
            horizontalMargin, verticalMargin));
        return panel;
    }
}
