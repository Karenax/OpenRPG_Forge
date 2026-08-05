using Godot;

namespace Forge.Runtime;

public partial class RuntimeHud : CanvasLayer
{
    private Label? _objective;
    private Label? _message;
    private Label? _state;
    private Label? _tactical;
    private Label? _rules;

    public string ModuleTitle { get; init; } = "OpenRPG Forge";

    public bool CombatEnabled { get; init; }

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
        margin.AddChild(panel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        content.AddChild(new Label
        {
            Text = ModuleTitle.ToUpperInvariant(),
            ThemeTypeVariation = "HeaderSmall",
        });

        _objective = new Label { Text = "Objectif : parler à Elyra [E]" };
        _message = new Label { Text = "Explorez la crypte." };
        _state = new Label { Text = "Quête : inactive | Clé : non | Boss : 5/5" };
        _tactical = new Label { Text = "Temps : réel | Combat : temps réel avec pause" };
        _rules = new Label { Text = "Difficulté : standard | Niveau 1 · 0 XP | PvP désactivé" };
        _tactical.AddThemeColorOverride("font_color", new Color("ffb06f"));
        _rules.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        content.AddChild(_objective);
        content.AddChild(_message);
        content.AddChild(_state);
        content.AddChild(_tactical);
        content.AddChild(_rules);

        var instructions = new Label
        {
            Text = CombatEnabled
                ? "ZQSD : bouger · E : interagir · ESPACE : attaquer · TAB : roue · P : pause tactique · T : ralenti · M : mode combat · F5/F9 : sauver/reprendre"
                : "ZQSD / flèches : déplacement   E : interagir",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.15f,
            AnchorRight = 0.85f,
            AnchorTop = 0.93f,
            AnchorBottom = 0.99f,
        };
        AddChild(instructions);
    }

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

        if (_state is not null)
        {
            _state.Text = state;
        }
    }

    public void UpdateTactical(string timeState, string combatMode, int queuedActions,
        string movementCapability, float breath, float stamina)
    {
        if (_tactical is null) return;
        _tactical.Text = $"Temps : {timeState} | Combat : {combatMode} | File : {queuedActions} | " +
                         $"Mouvement : {movementCapability} · souffle {breath:0}/{stamina:0} endurance";
    }

    public void UpdateRules(string difficulty, int level, double experience, double? nextLevel,
        string pvpMode, int perceptionModifiers)
    {
        if (_rules is null) return;
        var progression = nextLevel is null ? $"Niveau {level} · {experience:0} XP" :
            $"Niveau {level} · {experience:0}/{nextLevel:0} XP";
        _rules.Text = $"Difficulté : {difficulty} | {progression} | PvP : {pvpMode} | " +
                      $"Perception : {perceptionModifiers} modificateur(s)";
    }
}
