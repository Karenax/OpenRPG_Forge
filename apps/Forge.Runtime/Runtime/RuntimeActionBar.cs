using Forge.Domain.Combat;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeActionBar : CanvasLayer
{
    private static readonly (string Key, string Label, string Command, string Tooltip)[] Slots =
    [
        ("1", "Attaque", "attack", "Attaquer la cible hostile la plus proche"),
        ("2", "Interagir", "interact", "Interagir avec la cible proche"),
        ("3", "Actions", "action_wheel", "Ouvrir la roue d’actions"),
        ("4", "Repos", "rest", "Se reposer lorsque la zone est sûre"),
        ("F5", "Sauver", "quick_save", "Créer une sauvegarde rapide"),
        ("F9", "Charger", "quick_load", "Recharger la sauvegarde rapide"),
        ("M", "Monde", "module_map", "Ouvrir la carte globale du module"),
        ("P", "Pause", "tactical_pause", "Activer la pause tactique"),
    ];

    private readonly List<Button> _buttons = [];
    private readonly double[] _feedbackRemaining = new double[Slots.Length];
    private AbilityDefinition? _primaryAbility;
    private RuntimeCooldownSweep? _primaryCooldown;
    private Label? _primaryState;
    private double _previousPrimaryRemaining;
    private bool _primaryStateInitialized;
    private string _primaryStateText = string.Empty;

    public event Action<string>? CommandRequested;

    public event Action? PrimaryAbilityReady;

    public bool CombatEnabled { get; init; }

    internal RuntimeUiMotionPolicy MotionPolicy { get; init; } = RuntimeUiMotionPolicy.Create(null, []);

    private bool LargeText => MotionPolicy.TextScale > 1.5f;

    public int SlotCount => Slots.Length;

    internal double PrimaryCooldownRemaining => _primaryCooldown?.RemainingSeconds ?? 0;

    internal bool PrimaryCooldownVisible => _primaryCooldown?.Active == true;

    internal string PrimaryStateText => _primaryStateText;

    public override void _Ready()
    {
        Layer = 14;
        ProcessMode = ProcessModeEnum.Always;
        var margin = new MarginContainer
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -170,
            OffsetTop = LargeText ? -124 : -104,
            OffsetRight = LargeText ? 450 : 389,
            OffsetBottom = -20,
        };
        AddChild(margin);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(
            new Color("17110ef2"), RuntimeRpgUiTheme.Border, 9, 8));
        margin.AddChild(panel);

        var accent = new ColorRect
        {
            Color = new Color("b08a5970"),
            AnchorRight = 1,
            OffsetLeft = 10,
            OffsetTop = 1,
            OffsetRight = -10,
            OffsetBottom = 2,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddChild(accent);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        panel.AddChild(row);

        for (var index = 0; index < Slots.Length; index++)
        {
            var slot = Slots[index];
            var button = new Button
            {
                Name = $"ActionSlot{index + 1}",
                TooltipText = $"{slot.Key} · {slot.Label}\n{slot.Tooltip}",
                CustomMinimumSize = LargeText ? new Vector2(70, 72) : new Vector2(59, 60),
                FocusMode = Control.FocusModeEnum.All,
                Disabled = slot.Command == "attack" && !CombatEnabled,
            };
            var slotColor = SlotAccent(slot.Command);
            button.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Slot(slotColor, button.Disabled));
            button.AddThemeStyleboxOverride("hover", RuntimeRpgUiTheme.Frame(
                new Color("35271de8"), slotColor.Lightened(.14f), 0, 0));
            button.AddThemeStyleboxOverride("pressed", RuntimeRpgUiTheme.Frame(
                new Color("4b3524ef"), RuntimeRpgUiTheme.Bone, 0, 0));
            button.AddThemeStyleboxOverride("focus", RuntimeRpgUiTheme.Frame(
                new Color("2a1e17e8"), RuntimeRpgUiTheme.Gold, 0, 0));

            var glyph = new ActionGlyph(slot.Command,
                button.Disabled ? new Color("51483d") : slotColor.Lightened(.16f))
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            glyph.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            glyph.OffsetTop = 5;
            button.AddChild(glyph);

            if (slot.Command == "attack")
            {
                _primaryCooldown = new RuntimeCooldownSweep { MotionPolicy = MotionPolicy };
                _primaryCooldown.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _primaryCooldown.OffsetLeft = 2;
                _primaryCooldown.OffsetTop = 2;
                _primaryCooldown.OffsetRight = -2;
                _primaryCooldown.OffsetBottom = -2;
                _primaryCooldown.Configure(_primaryAbility?.CooldownSeconds ?? 0);
                button.AddChild(_primaryCooldown);
            }

            var key = new Label
            {
                Text = slot.Key,
                OffsetLeft = 5,
                OffsetTop = 3,
                OffsetRight = LargeText ? 42 : 31,
                OffsetBottom = LargeText ? 27 : 17,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            RuntimeRpgUiTheme.StyleBody(key, Math.Max(8, Mathf.RoundToInt(8 * MotionPolicy.TextScale)),
                button.Disabled ? new Color("5f5548") : RuntimeRpgUiTheme.Muted);
            button.AddChild(key);

            if (slot.Command == "attack")
            {
                _primaryState = new Label
                {
                    AnchorTop = 1,
                    AnchorRight = 1,
                    AnchorBottom = 1,
                    OffsetLeft = 2,
                    OffsetTop = LargeText ? -27 : -17,
                    OffsetRight = -2,
                    OffsetBottom = -4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                RuntimeRpgUiTheme.StyleTitle(_primaryState,
                    Math.Max(7, Mathf.RoundToInt(8 * MotionPolicy.TextScale)), RuntimeRpgUiTheme.Bone);
                button.AddChild(_primaryState);
            }

            var lowerRule = new ColorRect
            {
                Color = button.Disabled ? new Color("51483d") : slotColor.Darkened(.08f),
                AnchorTop = 1,
                AnchorRight = 1,
                AnchorBottom = 1,
                OffsetLeft = 8,
                OffsetTop = -3,
                OffsetRight = -8,
                OffsetBottom = -1,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            button.AddChild(lowerRule);

            button.SetMeta("accessible_label", $"Action {slot.Key} : {slot.Label}. {slot.Tooltip}");
            var capturedIndex = index;
            button.Pressed += () => ActivateSlot(capturedIndex);
            _buttons.Add(button);
            row.AddChild(button);
        }

        RefreshPrimaryPresentation(0, 0, false);
    }

    public override void _Process(double delta)
    {
        for (var index = 0; index < _feedbackRemaining.Length && index < _buttons.Count; index++)
        {
            if (_feedbackRemaining[index] <= 0) continue;
            _feedbackRemaining[index] = Math.Max(0, _feedbackRemaining[index] - delta);
            if (MotionPolicy.ReducedMotion || _feedbackRemaining[index] <= 0)
            {
                _buttons[index].SelfModulate = Colors.White;
                continue;
            }
            var intensity = (float)Math.Clamp(_feedbackRemaining[index] / .12, 0, 1);
            _buttons[index].SelfModulate = Colors.White.Lerp(RuntimeRpgUiTheme.Gold.Lightened(.25f), intensity);
        }
    }

    internal void ConfigurePrimaryAbility(AbilityDefinition ability)
    {
        _primaryAbility = ability ?? throw new ArgumentNullException(nameof(ability));
        _primaryCooldown?.Configure(ability.CooldownSeconds);
        RefreshPrimaryPresentation(_primaryCooldown?.RemainingSeconds ?? 0, 0, false);
    }

    internal void UpdatePrimaryAbilityState(double remaining, int resource, bool targetAvailable)
    {
        if (!double.IsFinite(remaining) || remaining < 0)
            throw new ArgumentOutOfRangeException(nameof(remaining));
        ArgumentOutOfRangeException.ThrowIfNegative(resource);
        _primaryCooldown?.SetRemaining(remaining);
        if (_primaryStateInitialized && _primaryAbility is { CooldownSeconds: > 0 } &&
            _previousPrimaryRemaining > 0 && remaining <= 0)
        {
            NotifyCommandAccepted("attack");
            PrimaryAbilityReady?.Invoke();
        }
        _primaryStateInitialized = true;
        _previousPrimaryRemaining = remaining;
        RefreshPrimaryPresentation(remaining, resource, targetAvailable);
    }

    internal void NotifyCastAccepted() => NotifyCommandAccepted("attack");

    internal void NotifyCommandAccepted(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        for (var index = 0; index < Slots.Length; index++)
        {
            if (!string.Equals(Slots[index].Command, command, StringComparison.Ordinal)) continue;
            _feedbackRemaining[index] = MotionPolicy.ReducedMotion ? 0 : .12;
            return;
        }
    }

    internal bool ActivateSlot(int index)
    {
        if (index < 0 || index >= Slots.Length || _buttons[index].Disabled) return false;
        NotifyCommandAccepted(Slots[index].Command);
        CommandRequested?.Invoke(Slots[index].Command);
        return true;
    }

    private void RefreshPrimaryPresentation(double remaining, int resource, bool targetAvailable)
    {
        var ability = _primaryAbility;
        var nextState = !CombatEnabled || ability is null
            ? "INDISPO"
            : !targetAvailable
                ? "CIBLE"
                : resource < ability.ResourceCost
                    ? $"RES {ability.ResourceCost}"
                    : remaining > 0
                        ? $"{remaining:0.0}s"
                        : "PRÊT";
        if (string.Equals(nextState, _primaryStateText, StringComparison.Ordinal) &&
            string.Equals(_primaryState?.Text, nextState, StringComparison.Ordinal))
            return;
        _primaryStateText = nextState;
        if (_primaryState is not null)
        {
            _primaryState.Text = nextState;
            var color = nextState == "PRÊT"
                ? MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Healing)
                : nextState == "CIBLE" || nextState.StartsWith("RES", StringComparison.Ordinal)
                    ? MotionPolicy.ResolveColor(RuntimeUiSemanticColor.Alert)
                    : RuntimeRpgUiTheme.Bone;
            _primaryState.AddThemeColorOverride("font_color", color);
        }
        if (_buttons.Count == 0) return;
        var description = ability is null
            ? "Attaque indisponible"
            : $"{ability.DisplayName} · coût {ability.ResourceCost} ressource · {nextState.ToLowerInvariant()}";
        _buttons[0].TooltipText = $"1 · {description}";
        _buttons[0].SetMeta("accessible_label", $"Action 1 : {description}");
    }

    private static Color SlotAccent(string command) => command switch
    {
        "attack" => RuntimeRpgUiTheme.Health,
        "interact" => RuntimeRpgUiTheme.Gold,
        "action_wheel" => RuntimeRpgUiTheme.Violet,
        "rest" => RuntimeRpgUiTheme.Ember,
        "quick_save" => RuntimeRpgUiTheme.Sage,
        "quick_load" => RuntimeRpgUiTheme.FocusBlue,
        "module_map" => RuntimeRpgUiTheme.Gold,
        "tactical_pause" => RuntimeRpgUiTheme.Muted,
        _ => RuntimeRpgUiTheme.Parchment,
    };

    private sealed partial class ActionGlyph(string command, Color color) : Control
    {
        public override void _Draw()
        {
            var center = new Vector2(Size.X * .5f, Size.Y * .55f);
            switch (command)
            {
                case "attack": DrawAttack(center); break;
                case "interact": DrawInteract(center); break;
                case "action_wheel": DrawActionWheel(center); break;
                case "rest": DrawRest(center); break;
                case "quick_save": DrawSave(center); break;
                case "quick_load": DrawLoad(center); break;
                case "module_map": DrawMap(center); break;
                case "tactical_pause": DrawPause(center); break;
            }
        }

        private void DrawAttack(Vector2 center)
        {
            DrawLine(center + new Vector2(-10, 10), center + new Vector2(10, -10), color, 2.4f, true);
            DrawLine(center + new Vector2(-10, -10), center + new Vector2(10, 10), color, 2.4f, true);
            DrawLine(center + new Vector2(-12, 5), center + new Vector2(-5, 12), color, 2, true);
            DrawLine(center + new Vector2(5, 12), center + new Vector2(12, 5), color, 2, true);
            DrawCircle(center + new Vector2(-11, 11), 2, color);
            DrawCircle(center + new Vector2(11, 11), 2, color);
        }

        private void DrawInteract(Vector2 center)
        {
            DrawColoredPolygon([
                center + new Vector2(0, -12), center + new Vector2(12, 0),
                center + new Vector2(0, 12), center + new Vector2(-12, 0),
            ], color.Darkened(.5f));
            DrawPolyline([
                center + new Vector2(0, -12), center + new Vector2(12, 0),
                center + new Vector2(0, 12), center + new Vector2(-12, 0),
                center + new Vector2(0, -12),
            ], color, 1.6f, true);
            DrawCircle(center, 3.5f, color);
        }

        private void DrawActionWheel(Vector2 center)
        {
            DrawCircle(center, 7, color.Darkened(.45f));
            DrawArc(center, 7, 0, Mathf.Tau, 32, color, 1.8f, true);
            for (var index = 0; index < 8; index++)
            {
                var direction = Vector2.FromAngle(index * Mathf.Pi / 4);
                DrawLine(center + direction * 9, center + direction * 14, color, 1.8f, true);
            }
            DrawCircle(center, 2.2f, color);
        }

        private void DrawRest(Vector2 center)
        {
            DrawArc(center + new Vector2(-2, 1), 12, -.9f, 4.2f, 32, color, 2.3f, true);
            DrawCircle(center + new Vector2(6, -7), 7, new Color("17110e"));
            DrawCircle(center + new Vector2(10, -9), 1.4f, RuntimeRpgUiTheme.Gold);
        }

        private void DrawSave(Vector2 center)
        {
            DrawRect(new Rect2(center + new Vector2(-11, -12), new Vector2(22, 24)), color.Darkened(.58f), true);
            DrawRect(new Rect2(center + new Vector2(-11, -12), new Vector2(22, 24)), color, false, 1.8f, true);
            DrawRect(new Rect2(center + new Vector2(-6, -9), new Vector2(10, 7)), color, false, 1.5f, true);
            DrawRect(new Rect2(center + new Vector2(-6, 4), new Vector2(12, 8)), color, false, 1.5f, true);
        }

        private void DrawLoad(Vector2 center)
        {
            DrawArc(center, 11, -.55f, 4.8f, 32, color, 2.2f, true);
            DrawColoredPolygon([
                center + new Vector2(-12, -7), center + new Vector2(-4, -8),
                center + new Vector2(-8, -1),
            ], color);
            DrawLine(center + new Vector2(0, -7), center + new Vector2(0, 5), color, 2, true);
            DrawLine(center + new Vector2(-4, 1), center + new Vector2(0, 5), color, 2, true);
            DrawLine(center + new Vector2(4, 1), center + new Vector2(0, 5), color, 2, true);
        }

        private void DrawMap(Vector2 center)
        {
            var left = center + new Vector2(-13, -10);
            DrawPolyline([
                left, left + new Vector2(8, -3), left + new Vector2(17, 1),
                left + new Vector2(26, -3), left + new Vector2(26, 19),
                left + new Vector2(17, 22), left + new Vector2(8, 18), left + new Vector2(0, 22), left,
            ], color, 1.7f, true);
            DrawLine(left + new Vector2(8, -3), left + new Vector2(8, 18), color, 1.2f, true);
            DrawLine(left + new Vector2(17, 1), left + new Vector2(17, 22), color, 1.2f, true);
        }

        private void DrawPause(Vector2 center)
        {
            DrawRect(new Rect2(center + new Vector2(-9, -12), new Vector2(6, 24)), color, true);
            DrawRect(new Rect2(center + new Vector2(3, -12), new Vector2(6, 24)), color, true);
        }
    }
}
