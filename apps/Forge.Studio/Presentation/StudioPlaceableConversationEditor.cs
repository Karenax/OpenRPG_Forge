using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableConversationEditor : VBoxContainer
{
    private sealed class BindingRow(
        StudioPlaceableConversationBindingDraft source,
        PanelContainer card,
        OptionButton dialogue,
        OptionButton trigger,
        OptionButton entryPoint,
        LineEdit selector,
        OptionButton item,
        SpinBox priority,
        CheckBox oncePerUser,
        Button remove)
    {
        internal StudioPlaceableConversationBindingDraft Source { get; } = source;
        internal PanelContainer Card { get; } = card;
        internal OptionButton Dialogue { get; } = dialogue;
        internal OptionButton Trigger { get; } = trigger;
        internal OptionButton EntryPoint { get; } = entryPoint;
        internal LineEdit Selector { get; } = selector;
        internal OptionButton Item { get; } = item;
        internal SpinBox Priority { get; } = priority;
        internal CheckBox OncePerUser { get; } = oncePerUser;
        internal Button Remove { get; } = remove;
    }

    private readonly Label _state;
    private readonly VBoxContainer _bindingsHost;
    private readonly Label _empty;
    private readonly Button _addBinding;
    private readonly OptionButton _previewBinding;
    private readonly CheckBox _previewAlreadyUsed;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private readonly List<BindingRow> _rows = [];
    private IReadOnlyList<StudioPlaceableDialogueOption> _dialogues = [];
    private IReadOnlyList<StudioPlaceableItemOption> _items = [];
    private readonly List<Guid> _previewBindingIds = [];
    private bool _loading;
    private bool _existingProfile;
    private bool _importedLegacy;

    internal event Action? Changed;

    internal event Action? PreviewRequested;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal Guid? PreviewBindingId =>
        _previewBinding.Selected >= 0 && _previewBinding.Selected < _previewBindingIds.Count
            ? _previewBindingIds[_previewBinding.Selected]
            : null;

    internal bool PreviewAlreadyUsed => _previewAlreadyUsed.ButtonPressed;

    internal int BindingCount => _rows.Count;

    internal int TriggerCount => _rows.Select(row => TriggerValue(row.Trigger)).Distinct().Count();

    internal bool IsReady => Visible && _rows.Count > 0 && _rows.All(RowIsReady);

    internal bool PreviewReady =>
        _previewResult.Text.Contains("Conversation", StringComparison.OrdinalIgnoreCase) &&
        (_previewResult.Text.Contains("lancée", StringComparison.OrdinalIgnoreCase) ||
         _previewResult.Text.Contains("déjà", StringComparison.OrdinalIgnoreCase));

    internal string HumanText => string.Join(' ',
        new[]
        {
            _state.Text,
            _empty.Text,
            "Conversation Dialogue Déclencheur Interaction État Utilisateur Objet Événement Point d’entrée Priorité Une fois par utilisateur Aperçu sans écriture Runtime",
            _previewResult.Text,
        }.Concat(_rows.Select(row =>
            $"{DialogueLabel(row.Dialogue)} {HumanTrigger(TriggerValue(row.Trigger))} {EntryPointValue(row)}")));

    internal StudioPlaceableConversationEditor()
    {
        Name = "StudioPlaceableConversationEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("LIAISONS DE DIALOGUE", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Chaque liaison choisit un dialogue, son point d’entrée et la situation qui le déclenche. Les priorités départagent plusieurs correspondances.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _empty = StudioStyle.Label(
            "État vide · ajoutez une liaison pour choisir quand ce placeable parle.",
            10,
            StudioStyle.Warning,
            wrap: true);
        AddChild(_empty);
        _bindingsHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _bindingsHost.AddThemeConstantOverride("separation", 6);
        AddChild(_bindingsHost);
        _addBinding = StudioStyle.Button("Ajouter une liaison", compact: true);
        _addBinding.TooltipText = "Ajouter une situation de conversation sans exposer d’identifiant technique";
        AddChild(_addBinding);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewBinding = AddOption(this, "Liaison à simuler");
        _previewAlreadyUsed = AddCheck(this, "Simuler une liaison déjà utilisée par ce joueur");
        _preview = StudioStyle.Button("Prévisualiser la conversation", compact: true);
        _preview.TooltipText = "Résoudre la liaison par le même service que le runtime, sans modifier le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label(
            "Aucune conversation prévisualisée.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger les liaisons de conversation au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, un profil absent produit un diagnostic au lieu d’un repli optionnel.";

        _addBinding.Pressed += AddDefaultBinding;
        _preview.Pressed += () => PreviewRequested?.Invoke();
        _previewBinding.ItemSelected += _ => ClearPreview();
        _previewAlreadyUsed.Toggled += _ => ClearPreview();
        _runtimeRequired.Toggled += value =>
        {
            if (!_loading)
            {
                RuntimeRequirementChanged?.Invoke(value);
                Changed?.Invoke();
            }
        };
    }

    internal void Load(
        StudioPlaceableConversationDraft draft,
        IReadOnlyList<StudioPlaceableDialogueOption> dialogues,
        IReadOnlyList<StudioPlaceableItemOption> items,
        bool enabled,
        bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _importedLegacy = draft.ImportedLegacyDialogue;
        _dialogues = dialogues;
        _items = items;
        ClearRows();
        foreach (var binding in draft.Bindings)
        {
            AddBindingRow(binding);
        }
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewAlreadyUsed.ButtonPressed = false;
        _previewResult.Text = "Aucune conversation prévisualisée.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RebuildPreviewChoices();
        UpdateEnabledState(enabled);
        _loading = false;
    }

    internal StudioPlaceableConversationDraft Capture(StudioPlaceableConversationDraft source) =>
        source with
        {
            Bindings = _rows.Select(row => new StudioPlaceableConversationBindingDraft(
                row.Source.Id,
                DialogueValue(row.Dialogue),
                TriggerValue(row.Trigger),
                TriggerValue(row.Trigger) == PlaceableConversationTrigger.Item
                    ? string.Empty
                    : row.Selector.Text,
                TriggerValue(row.Trigger) == PlaceableConversationTrigger.Item
                    ? ItemValue(row.Item)
                    : null,
                EntryPointValue(row),
                (int)row.Priority.Value,
                row.OncePerUser.ButtonPressed)).ToArray(),
        };

    internal void SetEnabledState(bool enabled) => UpdateEnabledState(enabled);

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusBindings() => _rows.FirstOrDefault()?.Dialogue.GrabFocus();

    internal void FocusPreview() => _previewBinding.GrabFocus();

    internal void SetEvidenceValues(bool previewUsed)
    {
        if (_dialogues.Count == 0)
        {
            return;
        }
        _loading = true;
        ClearRows();
        var selectors = new[]
        {
            (PlaceableConversationTrigger.Interaction, "examiner"),
            (PlaceableConversationTrigger.State, "brisé"),
            (PlaceableConversationTrigger.User, "tag:initié"),
            (PlaceableConversationTrigger.Item, string.Empty),
            (PlaceableConversationTrigger.Event, "OnDisturbed"),
        };
        for (var index = 0; index < selectors.Length; index++)
        {
            var dialogue = _dialogues[index % _dialogues.Count];
            var (trigger, selector) = selectors[index];
            if (trigger == PlaceableConversationTrigger.Item && _items.Count == 0)
            {
                continue;
            }
            AddBindingRow(new(
                Guid.NewGuid(),
                dialogue.Id,
                trigger,
                selector,
                trigger == PlaceableConversationTrigger.Item ? _items[0].Id : null,
                dialogue.EntryPointKeys.FirstOrDefault() ?? "default",
                50 - index * 10,
                index == 0));
        }
        _runtimeRequired.ButtonPressed = true;
        _previewAlreadyUsed.ButtonPressed = previewUsed;
        RebuildPreviewChoices();
        _previewBinding.Select(0);
        _loading = false;
        UpdateEmptyState();
        ClearPreview();
        Changed?.Invoke();
    }

    internal void SetConversationPreview(StudioPlaceableConversationPreview preview)
    {
        _previewResult.Text = preview.Summary;
        _previewResult.AddThemeColorOverride(
            "font_color", preview.Succeeded ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void AddDefaultBinding()
    {
        if (_dialogues.Count == 0)
        {
            _state.Text = "Aucun dialogue disponible · créez d’abord un dialogue dans la rubrique Histoire.";
            _state.AddThemeColorOverride("font_color", StudioStyle.Warning);
            return;
        }
        var dialogue = _dialogues[0];
        AddBindingRow(new(
            Guid.NewGuid(),
            dialogue.Id,
            PlaceableConversationTrigger.Interaction,
            "utiliser",
            null,
            dialogue.EntryPointKeys.FirstOrDefault() ?? "default",
            _rows.Count == 0 ? 10 : _rows.Min(row => (int)row.Priority.Value) - 10,
            false));
        RebuildPreviewChoices();
        UpdateEmptyState();
        Changed?.Invoke();
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            _bindingsHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _rows.Clear();
        _previewBindingIds.Clear();
        _previewBinding.Clear();
        UpdateEmptyState();
    }

    private void AddBindingRow(StudioPlaceableConversationBindingDraft source)
    {
        var card = StudioStyle.Panel("PlaceableConversationBinding", StudioStyle.SurfaceRaised, 6);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _bindingsHost.AddChild(card);
        var margin = StudioStyle.Margin(9, 7, 9, 7);
        card.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        column.AddChild(header);
        var title = StudioStyle.Label("LIAISON DE CONVERSATION", 10, StudioStyle.Accent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var remove = StudioStyle.Button("Retirer", compact: true);
        header.AddChild(remove);

        var firstGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        firstGrid.AddThemeConstantOverride("h_separation", 7);
        column.AddChild(firstGrid);
        var dialogue = AddOption(firstGrid, "Dialogue");
        var trigger = AddOption(firstGrid, "Déclencheur");
        PopulateDialogues(dialogue, source.DialogueId);
        PopulateTriggers(trigger, source.Trigger);

        var secondGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        secondGrid.AddThemeConstantOverride("h_separation", 7);
        column.AddChild(secondGrid);
        var entryPoint = AddOption(secondGrid, "Point d’entrée");
        var priority = AddSpin(secondGrid, "Priorité", -1_000, 1_000, 1);
        PopulateEntryPoints(entryPoint, DialogueValue(dialogue), source.EntryPointKey);
        priority.Value = source.Priority;

        var selector = AddLine(column, "Situation attendue", "examiner, actif, tag:initié ou OnUsed");
        selector.Text = source.Selector;
        var item = AddOption(column, "Objet présenté au placeable");
        PopulateItems(item, source.ItemBlueprintId);
        var once = AddCheck(column, "Une seule fois par utilisateur");
        once.ButtonPressed = source.OncePerUser;

        var row = new BindingRow(source, card, dialogue, trigger, entryPoint, selector, item, priority, once, remove);
        _rows.Add(row);
        UpdateTriggerFields(row);

        dialogue.ItemSelected += _ =>
        {
            PopulateEntryPoints(entryPoint, DialogueValue(dialogue), string.Empty);
            NotifyChanged();
        };
        trigger.ItemSelected += _ =>
        {
            ApplyDefaultSelector(row);
            UpdateTriggerFields(row);
            NotifyChanged();
        };
        entryPoint.ItemSelected += _ => NotifyChanged();
        priority.ValueChanged += _ => NotifyChanged();
        selector.TextChanged += _ => NotifyChanged();
        item.ItemSelected += _ => NotifyChanged();
        once.Toggled += _ => NotifyChanged();
        remove.Pressed += () =>
        {
            _rows.Remove(row);
            _bindingsHost.RemoveChild(card);
            card.QueueFree();
            RebuildPreviewChoices();
            UpdateEmptyState();
            Changed?.Invoke();
        };
        UpdateEmptyState();
    }

    private void PopulateDialogues(OptionButton option, Guid selected)
    {
        option.Clear();
        foreach (var dialogue in _dialogues)
        {
            option.AddItem(dialogue.DisplayName);
            option.SetItemTooltip(option.ItemCount - 1,
                $"{dialogue.EntryPointKeys.Count} point(s) d’entrée · {dialogue.Slug}");
        }
        var index = _dialogues.ToList().FindIndex(dialogue => dialogue.Id == selected);
        if (option.ItemCount > 0)
        {
            option.Select(index >= 0 ? index : 0);
        }
    }

    private static void PopulateTriggers(OptionButton option, PlaceableConversationTrigger selected)
    {
        option.Clear();
        foreach (var trigger in Enum.GetValues<PlaceableConversationTrigger>())
        {
            option.AddItem(HumanTrigger(trigger));
        }
        option.Select((int)selected);
    }

    private void PopulateEntryPoints(OptionButton option, Guid dialogueId, string selected)
    {
        option.Clear();
        var dialogue = _dialogues.FirstOrDefault(candidate => candidate.Id == dialogueId);
        foreach (var entry in dialogue?.EntryPointKeys ?? [])
        {
            option.AddItem(HumanEntryPoint(entry));
            option.SetItemTooltip(option.ItemCount - 1, entry);
        }
        var index = dialogue?.EntryPointKeys.ToList().FindIndex(entry =>
            string.Equals(entry, selected, StringComparison.Ordinal)) ?? -1;
        if (option.ItemCount > 0)
        {
            option.Select(index >= 0 ? index : 0);
        }
    }

    private void PopulateItems(OptionButton option, BlueprintId? selected)
    {
        option.Clear();
        foreach (var item in _items)
        {
            option.AddItem(item.DisplayName);
            option.SetItemTooltip(option.ItemCount - 1, item.Slug);
        }
        var index = _items.ToList().FindIndex(item => item.Id == selected);
        if (option.ItemCount > 0)
        {
            option.Select(index >= 0 ? index : 0);
        }
    }

    private void RebuildPreviewChoices()
    {
        var selected = PreviewBindingId;
        _previewBindingIds.Clear();
        _previewBinding.Clear();
        foreach (var row in _rows)
        {
            _previewBindingIds.Add(row.Source.Id);
            _previewBinding.AddItem(
                $"{HumanTrigger(TriggerValue(row.Trigger))} · {DialogueLabel(row.Dialogue)}");
        }
        var index = selected is { } selectedId ? _previewBindingIds.IndexOf(selectedId) : -1;
        if (_previewBinding.ItemCount > 0)
        {
            _previewBinding.Select(index >= 0 ? index : 0);
        }
    }

    private bool RowIsReady(BindingRow row)
    {
        if (DialogueValue(row.Dialogue) == Guid.Empty || string.IsNullOrWhiteSpace(EntryPointValue(row)))
        {
            return false;
        }
        return TriggerValue(row.Trigger) == PlaceableConversationTrigger.Item
            ? ItemValue(row.Item) is not null
            : !string.IsNullOrWhiteSpace(row.Selector.Text);
    }

    private void ApplyDefaultSelector(BindingRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.Selector.Text) &&
            TriggerValue(row.Trigger) != PlaceableConversationTrigger.Item)
        {
            return;
        }
        row.Selector.Text = TriggerValue(row.Trigger) switch
        {
            PlaceableConversationTrigger.Interaction => "utiliser",
            PlaceableConversationTrigger.State => "actif",
            PlaceableConversationTrigger.User => "tag:joueur",
            PlaceableConversationTrigger.Event => "OnUsed",
            _ => string.Empty,
        };
    }

    private static void UpdateTriggerFields(BindingRow row)
    {
        var item = TriggerValue(row.Trigger) == PlaceableConversationTrigger.Item;
        ((Control)row.Selector.GetParent()).Visible = !item;
        ((Control)row.Item.GetParent()).Visible = item;
    }

    private void UpdateEnabledState(bool enabled)
    {
        Visible = enabled;
        _state.Text = _existingProfile
            ? "Conversation canonique existante · les liaisons non affichées restent conservées."
            : _importedLegacy && enabled
                ? "Référence Aurora historique reprise dans un brouillon typé · la référence d’origine sera conservée."
                : !enabled
                    ? "État vide · activez Conversation pour préparer des liaisons sûres."
                    : _dialogues.Count == 0
                        ? "Aucun dialogue disponible · créez d’abord un dialogue dans la rubrique Histoire."
                        : "Nouveau profil prêt · les liaisons restent dans le brouillon jusqu’à l’application validée.";
        _state.AddThemeColorOverride(
            "font_color", _existingProfile || enabled && _dialogues.Count > 0
                ? StudioStyle.Success
                : StudioStyle.Warning);
        UpdateEmptyState();
    }

    private void UpdateEmptyState() => _empty.Visible = _rows.Count == 0;

    private void NotifyChanged()
    {
        if (_loading)
        {
            return;
        }
        RebuildPreviewChoices();
        ClearPreview();
        Changed?.Invoke();
    }

    private void ClearPreview()
    {
        if (_loading)
        {
            return;
        }
        _previewResult.Text = "Conversation à recalculer · le module n’a pas été modifié.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private Guid DialogueValue(OptionButton option) =>
        option.Selected >= 0 && option.Selected < _dialogues.Count
            ? _dialogues[option.Selected].Id
            : Guid.Empty;

    private string DialogueLabel(OptionButton option) =>
        option.Selected >= 0 && option.Selected < option.ItemCount
            ? option.GetItemText(option.Selected)
            : "Dialogue indisponible";

    private static PlaceableConversationTrigger TriggerValue(OptionButton option) =>
        option.Selected >= 0 && option.Selected < Enum.GetValues<PlaceableConversationTrigger>().Length
            ? (PlaceableConversationTrigger)option.Selected
            : PlaceableConversationTrigger.Interaction;

    private BlueprintId? ItemValue(OptionButton option) =>
        option.Selected >= 0 && option.Selected < _items.Count
            ? _items[option.Selected].Id
            : null;

    private string EntryPointValue(BindingRow row)
    {
        var dialogue = _dialogues.FirstOrDefault(candidate => candidate.Id == DialogueValue(row.Dialogue));
        return row.EntryPoint.Selected >= 0 && row.EntryPoint.Selected < (dialogue?.EntryPointKeys.Count ?? 0)
            ? dialogue!.EntryPointKeys[row.EntryPoint.Selected]
            : string.Empty;
    }

    private static string HumanTrigger(PlaceableConversationTrigger trigger) => trigger switch
    {
        PlaceableConversationTrigger.Interaction => "Interaction",
        PlaceableConversationTrigger.State => "État du placeable",
        PlaceableConversationTrigger.User => "Utilisateur",
        PlaceableConversationTrigger.Item => "Objet présenté",
        PlaceableConversationTrigger.Event => "Événement",
        _ => "Déclencheur",
    };

    private static string HumanEntryPoint(string key) => key switch
    {
        "default" => "Entrée principale",
        "warning" => "Avertissement",
        _ => key.Replace('-', ' ').Replace('_', ' '),
    };

    private static OptionButton AddOption(Container parent, string label)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        parent.AddChild(column);
        column.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        column.AddChild(option);
        return option;
    }

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        parent.AddChild(column);
        column.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        column.AddChild(line);
        return line;
    }

    private static SpinBox AddSpin(Container parent, string label, double minimum, double maximum, double step)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        parent.AddChild(column);
        column.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        column.AddChild(spin);
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
