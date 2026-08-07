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
        ("5", "Sauver", "quick_save", "Créer une sauvegarde rapide"),
        ("6", "Reprendre", "quick_load", "Recharger la sauvegarde rapide"),
    ];

    private readonly List<Button> _buttons = [];

    public event Action<string>? CommandRequested;

    public bool CombatEnabled { get; init; }

    public int SlotCount => Slots.Length;

    public override void _Ready()
    {
        var margin = new MarginContainer
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -292,
            OffsetTop = -112,
            OffsetRight = 292,
            OffsetBottom = -24,
        };
        AddChild(margin);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        margin.AddChild(panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);

        for (var index = 0; index < Slots.Length; index++)
        {
            var slot = Slots[index];
            var button = new Button
            {
                Text = $"{slot.Key}\n{slot.Label}",
                TooltipText = slot.Tooltip,
                CustomMinimumSize = new Vector2(82, 58),
                FocusMode = Control.FocusModeEnum.All,
                Disabled = slot.Command == "attack" && !CombatEnabled,
            };
            button.AddThemeFontSizeOverride("font_size", 12);
            button.AddThemeColorOverride("font_color", new Color("e5edf4"));
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeStyleboxOverride("normal", SlotStyle(new Color("20283a"), new Color("52627a")));
            button.AddThemeStyleboxOverride("hover", SlotStyle(new Color("2d3a52"), new Color("c9aa67")));
            button.AddThemeStyleboxOverride("pressed", SlotStyle(new Color("394965"), new Color("e4c77f")));
            button.SetMeta("accessible_label", $"Action {slot.Key} : {slot.Label}. {slot.Tooltip}");
            var capturedIndex = index;
            button.Pressed += () => ActivateSlot(capturedIndex);
            _buttons.Add(button);
            row.AddChild(button);
        }
    }

    internal bool ActivateSlot(int index)
    {
        if (index < 0 || index >= Slots.Length || _buttons[index].Disabled) return false;
        CommandRequested?.Invoke(Slots[index].Command);
        return true;
    }

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = new Color("0b0f19e8"),
        BorderColor = new Color("59677b"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 9,
        CornerRadiusTopRight = 9,
        CornerRadiusBottomLeft = 9,
        CornerRadiusBottomRight = 9,
        ContentMarginLeft = 10,
        ContentMarginTop = 9,
        ContentMarginRight = 10,
        ContentMarginBottom = 9,
    };

    private static StyleBoxFlat SlotStyle(Color background, Color border) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };
}
