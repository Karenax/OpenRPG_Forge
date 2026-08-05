using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Godot;

namespace Forge.Runtime;

public partial class RuntimeActionWheel : CanvasLayer
{
    private readonly List<Button> _buttons = [];
    private Control? _backdrop;
    private Label? _heading;
    private Label? _accessibleSummary;
    private ModuleGameplayControlProfile? _profile;
    private string _parentKey = string.Empty;

    public event Action<string>? CommandRequested;

    public bool IsOpen => _backdrop?.Visible == true;

    public override void _Ready()
    {
        Layer = 200;
        ProcessMode = ProcessModeEnum.Always;
        _backdrop = new ColorRect
        {
            Color = new Color(0.025f, 0.018f, 0.045f, .88f),
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        AddChild(_backdrop);
        _heading = new Label
        {
            Text = "ROUE D’ACTIONS",
            Position = new Vector2(0, 92),
            Size = new Vector2(1280, 40),
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = "HeaderSmall",
        };
        _heading.AddThemeColorOverride("font_color", new Color("ff9858"));
        _backdrop.AddChild(_heading);
        _accessibleSummary = new Label
        {
            Position = new Vector2(250, 600),
            Size = new Vector2(780, 80),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("c7b3dc"),
        };
        _backdrop.AddChild(_accessibleSummary);
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
        if (_backdrop is not null) _backdrop.Visible = false;
        _parentKey = string.Empty;
    }

    private void Open(string parentKey)
    {
        if (_profile is null || _backdrop is null) return;
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

        var center = new Vector2(640, 355);
        for (var index = 0; index < entries.Count; index++)
        {
            var action = entries[index];
            var angle = -Mathf.Pi / 2 + Mathf.Tau * index / Math.Max(1, entries.Count);
            var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 210 - new Vector2(85, 28);
            var button = new Button
            {
                Text = action.Label,
                TooltipText = action.AccessibleLabel,
                Position = position,
                Size = new Vector2(170, 56),
                FocusMode = Control.FocusModeEnum.All,
            };
            button.Pressed += () => Select(action);
            _backdrop.AddChild(button);
            _buttons.Add(button);
        }

        _heading!.Text = parentKey.Length == 0 ? "ROUE D’ACTIONS" : parentKey.ToUpperInvariant();
        _accessibleSummary!.Text = page.AccessibleSummary + " · Échap/Tab : fermer";
        _backdrop.Visible = true;
        _buttons.FirstOrDefault()?.GrabFocus();
    }

    private void Select(RadialActionDefinition action)
    {
        if (action.Command == "back") { Open(string.Empty); return; }
        if (action.Command.StartsWith("open:", StringComparison.Ordinal))
        {
            Open(action.Command[5..]);
            return;
        }

        Close();
        CommandRequested?.Invoke(action.Command);
    }
}
