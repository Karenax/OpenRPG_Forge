using Godot;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Runtime;

public partial class RuntimeHud : CanvasLayer
{
    private Label? _objective;
    private Label? _message;
    private Label? _state;
    private Label? _tactical;
    private Label? _rules;
    private Label? _caption;

    public string ModuleTitle { get; init; } = "OpenRPG Forge";

    public bool CombatEnabled { get; init; }

    public AccessibilityQualityProfile? Accessibility { get; init; }

    public override void _Ready()
    {
        var margin = new MarginContainer
        {
            OffsetLeft = 20,
            OffsetTop = 20,
            OffsetRight = 470,
            OffsetBottom = 190,
        };
        AddChild(margin);

        var panel = new PanelContainer();
        if (Accessibility?.RuntimeHighContrast == true)
        {
            panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color("08060d"),
                BorderColor = new Color("fff1cf"),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
            });
        }
        margin.AddChild(panel);

        var content = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(430, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        var heading = new Label
        {
            Text = ModuleTitle.ToUpperInvariant(),
            ThemeTypeVariation = "HeaderSmall",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        content.AddChild(heading);

        _objective = WrappedLabel("Objectif : parler à Elyra [E]");
        _message = WrappedLabel("Explorez la crypte.");
        _state = WrappedLabel("Quête : inactive | Clé : non | Boss : 5/5");
        _tactical = WrappedLabel("Temps : réel | Combat : temps réel avec pause");
        _rules = WrappedLabel("Difficulté : standard | Niveau 1 · 0 XP | PvP désactivé");
        _tactical.AddThemeColorOverride("font_color", new Color("ffb06f"));
        _rules.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        content.AddChild(_objective);
        content.AddChild(_message);
        content.AddChild(_state);
        content.AddChild(_tactical);
        content.AddChild(_rules);
        _caption = new Label
        {
            Text = Accessibility?.RuntimeCaptions == true
                ? "[Description sonore] Ambiance du lieu · aucun danger immédiat."
                : string.Empty,
            Visible = Accessibility?.RuntimeCaptions == true || Accessibility?.RuntimeSubtitles == true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _caption.AddThemeColorOverride("font_color", new Color("fff1cf"));
        content.AddChild(_caption);

        var instructions = new Label
        {
            Text = CombatEnabled
                ? "ZQSD : bouger · clic droit/milieu : orbite · molette : zoom · Début/Fin : tourner · ESPACE : attaquer · TAB : roue · P : pause · F5/F9 : sauver/reprendre"
                : "ZQSD / flèches : déplacement · clic droit/milieu : orbite · molette : zoom · Début/Fin : tourner",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.15f,
            AnchorRight = 0.85f,
            AnchorTop = 0.93f,
            AnchorBottom = 0.99f,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(instructions);

        var scale = (float)(Accessibility?.RuntimeTextScale ?? 1);
        heading.AddThemeFontSizeOverride("font_size", Math.Max(18, (int)Math.Round(20 * scale)));
        foreach (var label in new[] { _objective, _message, _state, _tactical, _rules, _caption, instructions })
        {
            label.AddThemeFontSizeOverride("font_size", Math.Max(14, (int)Math.Round(16 * scale)));
            label.SetMeta("accessible_label", label.Text);
        }
    }

    private static Label WrappedLabel(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    public void UpdateState(string objective, string message, string state)
    {
        if (_objective is not null)
        {
            _objective.Text = $"Objectif : {objective}";
        }

        if (_message is not null)
        {
            _message.Text = message;
        }

        if (_caption is not null && Accessibility?.RuntimeSubtitles == true)
        {
            _caption.Text = $"[Sous-titre] {message}";
            _caption.SetMeta("accessible_label", _caption.Text);
        }

        if (_state is not null)
        {
            _state.Text = state;
        }
    }

    public void UpdateTactical(string timeState, string combatMode, int queuedActions,
        string movementCapability, float breath, float stamina)
    {
        if (_tactical is null) return;
        _tactical.Text = $"Temps : {timeState} · Combat : {combatMode} · Actions : {queuedActions} · " +
                         $"{movementCapability} {breath:0}/{stamina:0}";
    }

    public void UpdateRules(string difficulty, int level, double experience, double? nextLevel,
        string pvpMode, int perceptionModifiers)
    {
        if (_rules is null) return;
        var progression = nextLevel is null ? $"Niveau {level} · {experience:0} XP" :
            $"Niveau {level} · {experience:0}/{nextLevel:0} XP";
        _rules.Text = $"{difficulty} · {progression} · PvP {pvpMode} · Perception {perceptionModifiers}";
    }
}
