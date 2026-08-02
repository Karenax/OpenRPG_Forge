using Godot;

namespace Forge.Runtime;

public partial class RuntimeHud : CanvasLayer
{
    private Label? _objective;
    private Label? _message;
    private Label? _state;

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
        content.AddChild(_objective);
        content.AddChild(_message);
        content.AddChild(_state);

        var instructions = new Label
        {
            Text = CombatEnabled
                ? "ZQSD / flèches : déplacement   E : interagir   ESPACE : attaquer   F5/F9 : sauver/reprendre"
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
}
