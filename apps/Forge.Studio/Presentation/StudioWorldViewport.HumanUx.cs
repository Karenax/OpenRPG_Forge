using Forge.Domain.Authoring;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorldViewport
{
    private readonly Dictionary<EditorTransformTool, Button> _humanTransformButtons = [];

    internal event Action? TransformToolChanged;

    internal int HumanTransformToolButtonCount => _humanTransformButtons.Count;

    internal int HumanActiveTransformToolButtonCount =>
        _humanTransformButtons.Values.Count(button => button.ButtonPressed);

    private void InitializeHumanTransformToolbar(Container toolbar)
    {
        ArgumentNullException.ThrowIfNull(toolbar);

        toolbar.AddChild(StudioStyle.Label("MANIPULER", 9, StudioStyle.Muted));
        AddHumanTransformButton(toolbar, EditorTransformTool.Translate, "Déplacer", "1");
        AddHumanTransformButton(toolbar, EditorTransformTool.Rotate, "Tourner", "2");
        AddHumanTransformButton(toolbar, EditorTransformTool.Scale, "Redimensionner", "3");
        RefreshHumanTransformToolbar();
    }

    private void AddHumanTransformButton(
        Container toolbar,
        EditorTransformTool tool,
        string label,
        string shortcut)
    {
        var button = StudioStyle.Button($"{label} [{shortcut}]", compact: true);
        button.Name = $"HumanTransform{tool}";
        button.ToggleMode = true;
        button.FocusMode = FocusModeEnum.None;
        button.TooltipText = tool switch
        {
            EditorTransformTool.Rotate =>
                "Tourner la sélection avec les surfaces colorées du manipulateur. Raccourci : 2.",
            EditorTransformTool.Scale =>
                "Redimensionner la sélection avec les poignées X, Y, Z ou uniforme. Raccourci : 3.",
            _ => "Déplacer la sélection avec les flèches X, Y ou Z. Raccourci : 1.",
        };
        button.Pressed += () =>
        {
            SelectHumanTransformTool(tool);
            GrabFocus();
        };
        toolbar.AddChild(button);
        _humanTransformButtons[tool] = button;
    }

    private bool HandleHumanTransformShortcut(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo ||
            key.CtrlPressed || key.AltPressed || key.MetaPressed ||
            _readOnly || _viewportMode != EditorViewportMode.Editing)
        {
            return false;
        }

        var shortcut = key.PhysicalKeycode is Key.Key1 or Key.Key2 or Key.Key3
            ? key.PhysicalKeycode
            : key.Keycode;
        var tool = shortcut switch
        {
            Key.Key1 => EditorTransformTool.Translate,
            Key.Key2 => EditorTransformTool.Rotate,
            Key.Key3 => EditorTransformTool.Scale,
            _ => (EditorTransformTool?)null,
        };
        return tool is not null && SelectHumanTransformTool(tool.Value);
    }

    private bool SelectHumanTransformTool(EditorTransformTool tool, bool reportStatus = true)
    {
        if (_readOnly || _viewportMode != EditorViewportMode.Editing || _session is null)
        {
            RefreshHumanTransformToolbar();
            return false;
        }

        var source = _session.WorkspaceProfile.Shell.DirectManipulation;
        if (source.Transform.Tool != tool)
        {
            var transform = new EditorTransformToolConfiguration(
                source.Transform.Id,
                source.Transform.SchemaVersion,
                "studio:human-transform-toolbar",
                tool,
                source.Transform.CoordinateSpace,
                source.Transform.PivotMode,
                source.Transform.TranslationSnap,
                source.Transform.RotationSnapDegrees,
                source.Transform.ScaleSnap,
                source.Transform.NumericInputEnabled,
                source.Transform.AlignmentEnabled);
            var direct = new EditorDirectManipulationProfile(
                source.Id,
                source.SchemaVersion,
                "studio:human-transform-toolbar",
                source.Selection,
                transform);
            var result = _session.ConfigureDirectManipulation(direct, persist: false);
            if (!result.Succeeded)
            {
                RefreshHumanTransformToolbar();
                if (reportStatus)
                {
                    InteractionStatusChanged?.Invoke(result.Message);
                }
                return false;
            }
        }

        RefreshManipulationProfile();
        if (reportStatus)
        {
            TransformToolChanged?.Invoke();
            InteractionStatusChanged?.Invoke(HumanTransformToolStatus(tool));
        }
        return true;
    }

    private void RefreshHumanTransformToolbar()
    {
        var active = ActiveTransformTool;
        var disabled = _readOnly || _viewportMode != EditorViewportMode.Editing;
        foreach (var pair in _humanTransformButtons)
        {
            var selected = pair.Key == active;
            pair.Value.ButtonPressed = selected;
            pair.Value.Disabled = disabled;
            pair.Value.Text = $"{(selected ? "● " : string.Empty)}{HumanTransformToolLabel(pair.Key)} " +
                              $"[{HumanTransformToolShortcut(pair.Key)}]";
        }
    }

    internal bool RunHumanTransformToolbarEvidence()
    {
        if (_session is null || _readOnly || _viewportMode != EditorViewportMode.Editing ||
            _humanTransformButtons.Count != 3)
        {
            return false;
        }

        var original = ActiveTransformTool;
        var valid = Enum.GetValues<EditorTransformTool>().All(tool =>
            SelectHumanTransformTool(tool, reportStatus: false) &&
            ActiveTransformTool == tool &&
            _humanTransformButtons[tool].ButtonPressed &&
            HumanActiveTransformToolButtonCount == 1);
        var restored = SelectHumanTransformTool(original, reportStatus: false);
        return valid && restored && ActiveTransformTool == original &&
               HumanActiveTransformToolButtonCount == 1;
    }

    private static string HumanTransformToolLabel(EditorTransformTool tool) => tool switch
    {
        EditorTransformTool.Rotate => "Tourner",
        EditorTransformTool.Scale => "Redimensionner",
        _ => "Déplacer",
    };

    private static string HumanTransformToolShortcut(EditorTransformTool tool) => tool switch
    {
        EditorTransformTool.Rotate => "2",
        EditorTransformTool.Scale => "3",
        _ => "1",
    };

    private static string HumanTransformToolStatus(EditorTransformTool tool) => tool switch
    {
        EditorTransformTool.Rotate =>
            "Outil actif · Tourner [2] · glissez une surface colorée autour de la sélection",
        EditorTransformTool.Scale =>
            "Outil actif · Redimensionner [3] · glissez X, Y, Z ou la poignée uniforme",
        _ => "Outil actif · Déplacer [1] · glissez une flèche X, Y ou Z",
    };
}
