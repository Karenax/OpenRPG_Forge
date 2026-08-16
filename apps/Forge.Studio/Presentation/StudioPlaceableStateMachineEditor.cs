using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableStateMachineEditor : VBoxContainer
{
    private sealed class StateRow(
        StudioPlaceableVisualStateDraft source,
        PanelContainer card,
        LineEdit animation,
        LineEdit visualOverride,
        CheckBox interactable)
    {
        internal StudioPlaceableVisualStateDraft Source { get; } = source;
        internal PanelContainer Card { get; } = card;
        internal LineEdit Animation { get; } = animation;
        internal LineEdit VisualOverride { get; } = visualOverride;
        internal CheckBox Interactable { get; } = interactable;
    }

    private sealed class TransitionRow(
        StudioPlaceableVisualTransitionDraft source,
        PanelContainer card,
        OptionButton from,
        OptionButton to,
        LineEdit trigger,
        SpinBox duration,
        LineEdit condition,
        Button remove)
    {
        internal StudioPlaceableVisualTransitionDraft Source { get; } = source;
        internal PanelContainer Card { get; } = card;
        internal OptionButton From { get; } = from;
        internal OptionButton To { get; } = to;
        internal LineEdit Trigger { get; } = trigger;
        internal SpinBox Duration { get; } = duration;
        internal LineEdit Condition { get; } = condition;
        internal Button Remove { get; } = remove;
    }

    private static readonly string[] RequiredStateIds =
        ["idle", "open", "closed", "broken", "active", "custom"];

    private readonly CheckBox _enabled;
    private readonly Label _state;
    private readonly VBoxContainer _content;
    private readonly OptionButton _initial;
    private readonly GridContainer _statesHost;
    private readonly VBoxContainer _transitionsHost;
    private readonly Label _transitionsEmpty;
    private readonly Button _addTransition;
    private readonly Button _previewTransition;
    private readonly Label _transitionPreview;
    private readonly CheckBox _runtimeRequired;
    private readonly List<StateRow> _states = [];
    private readonly List<TransitionRow> _transitions = [];
    private readonly List<string> _stateIds = [];
    private bool _existingProfile;
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal bool Enabled => _enabled.ButtonPressed;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal bool IsReady => Enabled && _states.Count >= RequiredStateIds.Length &&
                             RequiredStateIds.All(required => _states.Any(row =>
                                 string.Equals(row.Source.StateId, required, StringComparison.Ordinal))) &&
                             _states.All(row => !string.IsNullOrWhiteSpace(row.Animation.Text) &&
                                               !string.IsNullOrWhiteSpace(row.VisualOverride.Text));

    internal bool TransitionPreviewReady =>
        _transitionPreview.Text.Contains('→') &&
        _transitionPreview.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase);

    internal string HumanText => string.Join(' ',
        new[]
        {
            _state.Text,
            "État initial Animation Apparence Interactions Transitions Déclencheur Durée Condition Runtime",
            _transitionPreview.Text,
        }.Concat(_states.Select(row => HumanStateLabel(row.Source.StateId)))
            .Concat(_transitions.Select(row => row.Trigger.Text)));

    internal StudioPlaceableStateMachineEditor()
    {
        Name = "StudioPlaceableStateMachineEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _enabled = AddCheck(this, "Activer les états visuels");
        _enabled.TooltipText =
            "Ajouter une machine d’état canonique à ce placeable. Une machine existante reste protégée contre la suppression accidentelle.";
        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 7);
        AddChild(_content);

        _initial = AddOption(_content, "État affiché au chargement");

        _content.AddChild(StudioStyle.Label("ÉTATS VISUELS", 10, StudioStyle.Accent));
        _statesHost = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _statesHost.AddThemeConstantOverride("h_separation", 7);
        _statesHost.AddThemeConstantOverride("v_separation", 7);
        _content.AddChild(_statesHost);

        _content.AddChild(new HSeparator());
        _content.AddChild(StudioStyle.Label("TRANSITIONS", 10, StudioStyle.Accent));
        _transitionsEmpty = StudioStyle.Label(
            "État vide · ajoutez une transition pour prévisualiser le passage entre deux apparences.",
            10,
            StudioStyle.Warning,
            wrap: true);
        _content.AddChild(_transitionsEmpty);
        _transitionsHost = new VBoxContainer();
        _transitionsHost.AddThemeConstantOverride("separation", 6);
        _content.AddChild(_transitionsHost);
        _addTransition = StudioStyle.Button("Ajouter une transition", compact: true);
        _addTransition.TooltipText = "Définir un déclencheur sans exposer l’identifiant interne de la transition";
        _content.AddChild(_addTransition);
        _previewTransition = StudioStyle.Button("Prévisualiser la première transition", compact: true);
        _previewTransition.TooltipText = "Afficher le passage, sa durée et sa condition sans modifier le module";
        _content.AddChild(_previewTransition);
        _transitionPreview = StudioStyle.Label(
            "Aucune transition prévisualisée.", 10, StudioStyle.Muted, wrap: true);
        _transitionPreview.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        _content.AddChild(_transitionPreview);

        _runtimeRequired = AddCheck(_content, "Exiger ce profil d’états au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, l’absence des opérations produit un diagnostic au lieu d’un repli optionnel.";

        _enabled.Toggled += value =>
        {
            if (_existingProfile && !value)
            {
                _loading = true;
                _enabled.ButtonPressed = true;
                _loading = false;
                return;
            }
            UpdateEnabledState();
            NotifyChanged();
        };
        _initial.ItemSelected += _ => NotifyChanged();
        _runtimeRequired.Toggled += value =>
        {
            if (!_loading)
            {
                RuntimeRequirementChanged?.Invoke(value);
                Changed?.Invoke();
            }
        };
        _addTransition.Pressed += () =>
        {
            var from = _stateIds.Contains("closed", StringComparer.Ordinal) ? "closed" : _stateIds.FirstOrDefault() ?? "idle";
            var to = _stateIds.Contains("active", StringComparer.Ordinal) ? "active" : _stateIds.Skip(1).FirstOrDefault() ?? from;
            AddTransitionRow(new(Guid.NewGuid(), from, to, "activer", .5, string.Empty));
            UpdateTransitionState();
            Changed?.Invoke();
        };
        _previewTransition.Pressed += PreviewFirstTransition;
        UpdateEnabledState();
    }

    internal void Load(StudioPlaceableStateMachineDraft draft, bool enabled, bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _enabled.ButtonPressed = enabled;
        _enabled.Disabled = draft.ExistingProfile;
        _state.Text = draft.ExistingProfile
            ? "Profil canonique existant · les états supplémentaires et les transitions voisines seront conservés."
            : "État vide · activez cette section pour préparer les six apparences sûres avant validation.";
        _state.AddThemeColorOverride("font_color", draft.ExistingProfile ? StudioStyle.Success : StudioStyle.Warning);

        ClearRows();
        foreach (var state in OrderStates(draft.States))
        {
            AddStateRow(state);
        }
        PopulateInitial(draft.InitialStateId);
        foreach (var transition in draft.Transitions)
        {
            AddTransitionRow(transition);
        }
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _transitionPreview.Text = "Aucune transition prévisualisée.";
        _transitionPreview.AddThemeColorOverride("font_color", StudioStyle.Muted);
        UpdateTransitionState();
        UpdateEnabledState();
        _loading = false;
    }

    internal StudioPlaceableStateMachineDraft Capture(StudioPlaceableStateMachineDraft source)
    {
        var initialIndex = _initial.Selected;
        var initial = initialIndex >= 0 && initialIndex < _stateIds.Count
            ? _stateIds[initialIndex]
            : source.InitialStateId;
        return source with
        {
            InitialStateId = initial,
            States = _states.Select(row => row.Source with
            {
                AnimationId = row.Animation.Text,
                VisualOverrideId = row.VisualOverride.Text,
                Interactable = row.Interactable.ButtonPressed,
            }).ToArray(),
            Transitions = _transitions.Select(row => row.Source with
            {
                FromStateId = StateValue(row.From),
                ToStateId = StateValue(row.To),
                Trigger = row.Trigger.Text,
                DurationSeconds = row.Duration.Value,
                Condition = row.Condition.Text,
            }).ToArray(),
        };
    }

    internal void EnsureEnabled()
    {
        _enabled.ButtonPressed = true;
        UpdateEnabledState();
    }

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusInitialState() => _initial.GrabFocus();

    internal void FocusFirstTransition() => _transitions.FirstOrDefault()?.Trigger.GrabFocus();

    internal void SetEvidenceValues()
    {
        EnsureEnabled();
        SelectState(_initial, "active");
        foreach (var row in _states)
        {
            row.Animation.Text = $"animation:studio-{row.Source.StateId}";
            row.VisualOverride.Text = $"visual:studio-{row.Source.StateId}";
            row.Interactable.ButtonPressed = !string.Equals(row.Source.StateId, "broken", StringComparison.Ordinal);
        }
        ClearTransitions();
        AddTransitionRow(new(Guid.NewGuid(), "closed", "active", "power-on", .65, "power.available"));
        AddTransitionRow(new(Guid.NewGuid(), "active", "closed", "power-off", .35, string.Empty));
        _runtimeRequired.ButtonPressed = true;
        UpdateTransitionState();
        PreviewFirstTransition();
    }

    private void AddStateRow(StudioPlaceableVisualStateDraft source)
    {
        _stateIds.Add(source.StateId);
        var card = StudioStyle.Panel("PlaceableVisualState", StudioStyle.SurfaceRaised, 6);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _statesHost.AddChild(card);
        var margin = StudioStyle.Margin(9, 7, 9, 7);
        card.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label(HumanStateLabel(source.StateId).ToUpperInvariant(), 10, StudioStyle.Accent));
        var animation = AddLine(column, "Animation", "animation:idle");
        var visualOverride = AddLine(column, "Apparence", "visual:idle");
        var interactable = AddCheck(column, "Interactions autorisées");
        animation.Text = source.AnimationId;
        visualOverride.Text = source.VisualOverrideId;
        interactable.ButtonPressed = source.Interactable;
        var row = new StateRow(source, card, animation, visualOverride, interactable);
        _states.Add(row);
        foreach (var line in new[] { animation, visualOverride })
        {
            line.TextChanged += _ => NotifyChanged();
        }
        interactable.Toggled += _ => NotifyChanged();
    }

    private void AddTransitionRow(StudioPlaceableVisualTransitionDraft source)
    {
        var card = StudioStyle.Panel("PlaceableVisualTransition", StudioStyle.SurfaceRaised, 6);
        _transitionsHost.AddChild(card);
        var margin = StudioStyle.Margin(9, 7, 9, 7);
        card.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        column.AddChild(header);
        var title = StudioStyle.Label("PASSAGE VISUEL", 10, StudioStyle.Accent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var remove = StudioStyle.Button("Retirer", compact: true);
        header.AddChild(remove);

        var endpoints = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        endpoints.AddThemeConstantOverride("h_separation", 7);
        column.AddChild(endpoints);
        var from = AddOption(endpoints, "Depuis");
        var to = AddOption(endpoints, "Vers");
        PopulateStates(from, source.FromStateId);
        PopulateStates(to, source.ToStateId);
        var trigger = AddLine(column, "Déclencheur", "power-on");
        trigger.Text = source.Trigger;
        var duration = AddSpin(column, "Durée de l’aperçu (s)", 0, 3_600, .05);
        duration.Value = source.DurationSeconds;
        var condition = AddLine(column, "Condition facultative", "power.available");
        condition.Text = source.Condition;
        var row = new TransitionRow(source, card, from, to, trigger, duration, condition, remove);
        _transitions.Add(row);
        from.ItemSelected += _ => NotifyChanged();
        to.ItemSelected += _ => NotifyChanged();
        trigger.TextChanged += _ => NotifyChanged();
        duration.ValueChanged += _ => NotifyChanged();
        condition.TextChanged += _ => NotifyChanged();
        remove.Pressed += () =>
        {
            _transitions.Remove(row);
            _transitionsHost.RemoveChild(card);
            card.QueueFree();
            UpdateTransitionState();
            Changed?.Invoke();
        };
    }

    private void PopulateInitial(string selected)
    {
        _initial.Clear();
        foreach (var stateId in _stateIds)
        {
            _initial.AddItem(HumanStateLabel(stateId));
            _initial.SetItemTooltip(_initial.ItemCount - 1, "État visuel canonique");
        }
        SelectState(_initial, selected);
    }

    private void PopulateStates(OptionButton option, string selected)
    {
        option.Clear();
        foreach (var stateId in _stateIds)
        {
            option.AddItem(HumanStateLabel(stateId));
        }
        SelectState(option, selected);
    }

    private void SelectState(OptionButton option, string stateId)
    {
        var index = _stateIds.FindIndex(value => string.Equals(value, stateId, StringComparison.Ordinal));
        option.Select(index >= 0 ? index : 0);
    }

    private string StateValue(OptionButton option)
    {
        var index = option.Selected;
        return index >= 0 && index < _stateIds.Count ? _stateIds[index] : string.Empty;
    }

    private void PreviewFirstTransition()
    {
        var row = _transitions.FirstOrDefault();
        if (row is null)
        {
            _transitionPreview.Text = "Aucune transition à prévisualiser.";
            _transitionPreview.AddThemeColorOverride("font_color", StudioStyle.Warning);
            return;
        }
        var condition = string.IsNullOrWhiteSpace(row.Condition.Text)
            ? "sans condition"
            : $"si {row.Condition.Text.Trim()}";
        _transitionPreview.Text =
            $"Aperçu · {HumanStateLabel(StateValue(row.From))} → {HumanStateLabel(StateValue(row.To))} · " +
            $"{row.Duration.Value:0.##} s · {condition}";
        _transitionPreview.AddThemeColorOverride("font_color", StudioStyle.Success);
    }

    private void ClearRows()
    {
        foreach (var row in _states)
        {
            _statesHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _states.Clear();
        _stateIds.Clear();
        ClearTransitions();
    }

    private void ClearTransitions()
    {
        foreach (var row in _transitions)
        {
            _transitionsHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _transitions.Clear();
    }

    private void UpdateEnabledState()
    {
        _content.Visible = _enabled.ButtonPressed;
        _state.Visible = true;
        if (!_existingProfile)
        {
            _state.Text = _enabled.ButtonPressed
                ? "Nouveau profil prêt · les six états restent dans le brouillon jusqu’à l’application validée."
                : "État vide · activez cette section pour préparer les six apparences sûres avant validation.";
            _state.AddThemeColorOverride(
                "font_color", _enabled.ButtonPressed ? StudioStyle.Success : StudioStyle.Warning);
        }
    }

    private void UpdateTransitionState()
    {
        _transitionsEmpty.Visible = _transitions.Count == 0;
        _previewTransition.Disabled = _transitions.Count == 0;
    }

    private void NotifyChanged()
    {
        if (!_loading)
        {
            Changed?.Invoke();
        }
    }

    private static IEnumerable<StudioPlaceableVisualStateDraft> OrderStates(
        IReadOnlyList<StudioPlaceableVisualStateDraft> states) => states
        .OrderBy(state => Array.IndexOf(RequiredStateIds, state.StateId) is var index && index >= 0
            ? index
            : RequiredStateIds.Length)
        .ThenBy(state => state.StateId, StringComparer.Ordinal);

    private static string HumanStateLabel(string stateId) => stateId switch
    {
        "idle" => "Au repos",
        "open" => "Ouvert",
        "closed" => "Fermé",
        "broken" => "Brisé",
        "active" => "Actif",
        "custom" => "Personnalisé",
        _ => string.Join(' ', stateId.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..])),
    };

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
    }

    private static OptionButton AddOption(Container parent, string label)
    {
        var column = parent is GridContainer ? new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill } : null;
        var host = (Container?)column ?? parent;
        if (column is not null)
        {
            parent.AddChild(column);
        }
        host.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        host.AddChild(option);
        return option;
    }

    private static SpinBox AddSpin(Container parent, string label, double minimum, double maximum, double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        parent.AddChild(spin);
        return spin;
    }

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }
}
