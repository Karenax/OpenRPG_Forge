using Godot;

namespace Forge.Runtime;

public partial class RuntimeDialogueOverlay : CanvasLayer
{
    private Control? _root;
    private HBoxContainer? _header;
    private Control? _portrait;
    private Label? _speaker;
    private Label? _role;
    private Label? _line;
    private VBoxContainer? _choices;

    public bool IsOpen => _root?.Visible == true;

    public event Action<int>? ChoiceSelected;

    public event Action? Closed;

    public override void _Ready()
    {
        Layer = 35;
        BuildOverlay();
        Close();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventKey { Pressed: true, Echo: false } key || _choices is null) return;
        var index = key.PhysicalKeycode switch
        {
            Key.Key1 => 0,
            Key.Key2 => 1,
            Key.Key3 => 2,
            _ => -1,
        };
        if (index < 0 || index >= _choices.GetChildCount() || _choices.GetChild(index) is not Button button) return;
        button.EmitSignal(BaseButton.SignalName.Pressed);
        GetViewport().SetInputAsHandled();
    }

    public void Show(
        string speaker,
        string role,
        string line,
        IReadOnlyList<string> choices,
        string? portraitPath = null,
        string? portraitAlt = null)
    {
        if (_root is null || _speaker is null || _role is null || _line is null || _choices is null) return;
        var speakerName = string.IsNullOrWhiteSpace(speaker) ? "Inconnu" : speaker.Split(',', 2)[0].Trim();
        UpdatePortrait(portraitPath, portraitAlt, speakerName);
        _speaker.Text = speakerName;
        _role.Text = string.IsNullOrWhiteSpace(role) ? "VOYAGEUR" : role.ToUpperInvariant();
        _line.Text = string.IsNullOrWhiteSpace(line) ? "…" : $"« {line.Trim()} »";
        foreach (var child in _choices.GetChildren()) child.QueueFree();
        var visibleChoices = choices.Count == 0 ? ["Continuer"] : choices.Take(3).ToArray();
        for (var index = 0; index < visibleChoices.Length; index++)
        {
            var choice = new Button
            {
                Text = $"{index + 1:00}    {visibleChoices[index]}",
                CustomMinimumSize = new Vector2(0, 44),
                FocusMode = Control.FocusModeEnum.All,
                Alignment = HorizontalAlignment.Left,
            };
            RuntimeRpgUiTheme.StyleButton(choice, quiet: true);
            choice.AddThemeFontSizeOverride("font_size", 11);
            choice.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.ButtonStyle(
                new Color("17110ed6"), RuntimeRpgUiTheme.BorderMuted));
            choice.AddThemeStyleboxOverride("hover", RuntimeRpgUiTheme.ButtonStyle(
                new Color("3b291ee8"), RuntimeRpgUiTheme.Gold));
            choice.AddThemeStyleboxOverride("pressed", RuntimeRpgUiTheme.ButtonStyle(
                new Color("543621ee"), RuntimeRpgUiTheme.Bone));
            choice.SetMeta("accessible_label", $"Réponse {index + 1}: {visibleChoices[index]}");
            var selectedIndex = index;
            choice.Pressed += () =>
            {
                ChoiceSelected?.Invoke(selectedIndex);
                Close();
            };
            _choices.AddChild(choice);
        }
        _root.Visible = true;
        CallDeferred(MethodName.FocusFirstChoice);
    }

    public void Close()
    {
        if (_root is null || !_root.Visible) return;
        _root.Visible = false;
        Closed?.Invoke();
    }

    private void FocusFirstChoice()
    {
        if (_choices?.GetChildCount() > 0 && _choices.GetChild(0) is Button button) button.GrabFocus();
    }

    private void BuildOverlay()
    {
        _root = new Control
        {
            Name = "DialogueOverlay",
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var dim = new DialogueVeil { MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);

        var panelMargin = new MarginContainer
        {
            AnchorLeft = .5f,
            AnchorRight = .5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -470,
            OffsetTop = -414,
            OffsetRight = 470,
            OffsetBottom = -34,
        };
        _root.AddChild(panelMargin);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", RuntimeRpgUiTheme.Frame(
            new Color("120d0af3"), RuntimeRpgUiTheme.Border, 28, 22));
        panelMargin.AddChild(panel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 11);
        panel.AddChild(content);
        var accent = new ColorRect
        {
            Color = RuntimeRpgUiTheme.Gold,
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        content.AddChild(accent);

        _header = new HBoxContainer();
        _header.AddThemeConstantOverride("separation", 12);
        content.AddChild(_header);
        _portrait = RuntimeRpgUiTheme.Portrait(null, 72, RuntimeRpgUiTheme.Gold, selected: true,
            fallbackText: "?");
        _header.AddChild(_portrait);

        var identity = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        identity.AddThemeConstantOverride("separation", 2);
        _header.AddChild(identity);
        _speaker = Label("Inconnu", 21, RuntimeRpgUiTheme.Parchment, true);
        identity.AddChild(_speaker);
        _role = Label("VOYAGEUR", 9, RuntimeRpgUiTheme.Gold);
        identity.AddChild(_role);

        var badge = Label("◆  PAROLE", 9, RuntimeRpgUiTheme.Gold);
        badge.CustomMinimumSize = new Vector2(112, 28);
        badge.HorizontalAlignment = HorizontalAlignment.Center;
        badge.VerticalAlignment = VerticalAlignment.Center;
        badge.AddThemeStyleboxOverride("normal", RuntimeRpgUiTheme.Frame(
            new Color("2a1d15d4"), RuntimeRpgUiTheme.Border, 8, 4));
        _header.AddChild(badge);
        var close = new Button
        {
            Text = "×",
            CustomMinimumSize = new Vector2(34, 34),
            FocusMode = Control.FocusModeEnum.All,
        };
        RuntimeRpgUiTheme.StyleIconButton(close);
        close.Pressed += Close;
        _header.AddChild(close);

        _line = Label("…", 21,
            RuntimeRpgUiTheme.Parchment, true, wrap: true);
        _line.CustomMinimumSize = new Vector2(0, 74);
        _line.VerticalAlignment = VerticalAlignment.Center;
        content.AddChild(_line);
        var divider = new ColorRect
        {
            Color = RuntimeRpgUiTheme.BorderMuted,
            CustomMinimumSize = new Vector2(0, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        content.AddChild(divider);
        _choices = new VBoxContainer();
        _choices.AddThemeConstantOverride("separation", 7);
        content.AddChild(_choices);

        var hint = Label("1–3  CHOISIR      ESC  QUITTER LE DIALOGUE", 8, RuntimeRpgUiTheme.Quiet);
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        content.AddChild(hint);
    }

    private void UpdatePortrait(string? portraitPath, string? portraitAlt, string speakerName)
    {
        if (_header is null || _portrait is null) return;
        var index = _portrait.GetIndex();
        _header.RemoveChild(_portrait);
        _portrait.QueueFree();
        _portrait = RuntimeRpgUiTheme.Portrait(
            portraitPath,
            72,
            RuntimeRpgUiTheme.Gold,
            selected: true,
            fallbackText: Initials(speakerName));
        _portrait.SetMeta("accessible_label",
            string.IsNullOrWhiteSpace(portraitAlt) ? $"Portrait de {speakerName}" : portraitAlt);
        _header.AddChild(_portrait);
        _header.MoveChild(_portrait, index);
    }

    private static string Initials(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(part => part[0])).ToUpperInvariant();
    }

    private static Label Label(string text, int size, Color color, bool title = false, bool wrap = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            ClipText = false,
        };
        if (title) RuntimeRpgUiTheme.StyleTitle(label, size, color);
        else RuntimeRpgUiTheme.StyleBody(label, size, color);
        return label;
    }

    private sealed partial class DialogueVeil : Control
    {
        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color("09060458"));
            var bands = 14;
            for (var index = 0; index < bands; index++)
            {
                var normalized = (float)index / bands;
                var y = Size.Y * (.38f + normalized * .62f);
                var height = Size.Y * .62f / bands + 2;
                DrawRect(new Rect2(0, y, Size.X, height), new Color(0.035f, 0.022f, 0.014f,
                    Mathf.Lerp(.02f, .90f, normalized)));
            }
        }
    }
}
