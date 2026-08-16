using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableLootEditor : VBoxContainer
{
    private sealed class EntryRow(
        StudioPlaceableLootEntryDraft source,
        PanelContainer card,
        OptionButton item,
        LineEdit group,
        SpinBox weight,
        SpinBox minimumQuantity,
        SpinBox maximumQuantity,
        SpinBox minimumQuality,
        LineEdit condition,
        CheckBox unique,
        Button remove)
    {
        internal StudioPlaceableLootEntryDraft Source { get; } = source;
        internal PanelContainer Card { get; } = card;
        internal OptionButton Item { get; } = item;
        internal LineEdit Group { get; } = group;
        internal SpinBox Weight { get; } = weight;
        internal SpinBox MinimumQuantity { get; } = minimumQuantity;
        internal SpinBox MaximumQuantity { get; } = maximumQuantity;
        internal SpinBox MinimumQuality { get; } = minimumQuality;
        internal LineEdit Condition { get; } = condition;
        internal CheckBox Unique { get; } = unique;
        internal Button Remove { get; } = remove;
    }

    private readonly Label _state;
    private readonly OptionButton _timing;
    private readonly SpinBox _seed;
    private readonly CheckBox _persistGeneratedState;
    private readonly Label _generatedState;
    private readonly CheckBox _resetGeneratedState;
    private readonly VBoxContainer _entriesHost;
    private readonly Label _empty;
    private readonly Button _addEntry;
    private readonly OptionButton _previewTiming;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private readonly List<EntryRow> _rows = [];
    private IReadOnlyList<StudioPlaceableItemOption> _items = [];
    private bool _loading;
    private bool _existingProfile;
    private int _generation;
    private int _savedResultCount;

    internal event Action? Changed;

    internal event Action? PreviewRequested;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal StudioPlaceableLootPreviewMode PreviewMode =>
        _previewTiming.Selected >= 0 &&
        _previewTiming.Selected < Enum.GetValues<StudioPlaceableLootPreviewMode>().Length
            ? (StudioPlaceableLootPreviewMode)_previewTiming.Selected
            : StudioPlaceableLootPreviewMode.Open;

    internal bool IsReady => Visible && _rows.Count > 0 && _rows.All(RowIsReady);

    internal bool PreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        !_previewResult.Text.Contains("recalculer", StringComparison.OrdinalIgnoreCase);

    internal int EntryCount => _rows.Count;

    internal bool StableRowsReady =>
        _rows.All(row => row.Source.Id != Guid.Empty) &&
        _rows.Select(row => row.Source.Id).Distinct().Count() == _rows.Count;

    internal string HumanText => string.Join(' ',
        new[]
        {
            _state.Text,
            "Trésor initial Moment Chargement Ouverture Réinitialisation Graine État généré Table Objets Catégorie Poids Quantité Qualité Condition Unique Aperçu sans écriture Runtime",
            _generatedState.Text,
            _previewResult.Text,
        }.Concat(_rows.Select(row =>
            $"{ItemLabel(row.Item)} {row.Group.Text} poids {row.Weight.Value:0.##} quantité {row.MinimumQuantity.Value:0}-{row.MaximumQuantity.Value:0}")));

    internal StudioPlaceableLootEditor()
    {
        Name = "StudioPlaceableLootEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal",
            StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        AddChild(StudioStyle.Label("GÉNÉRATION DU TRÉSOR", 10, StudioStyle.Accent));
        var generationGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        generationGrid.AddThemeConstantOverride("h_separation", 7);
        AddChild(generationGrid);
        _timing = AddOption(generationGrid, "Moment du tirage");
        foreach (var timing in Enum.GetValues<PlaceableAuthoredLootSpawnTiming>())
        {
            _timing.AddItem(HumanTiming(timing));
        }
        _seed = AddSpin(generationGrid, "Graine déterministe", int.MinValue, int.MaxValue, 1);
        _persistGeneratedState = AddCheck(this, "Conserver le résultat généré dans la sauvegarde");
        _persistGeneratedState.TooltipText =
            "Le runtime restaure alors le même trésor au lieu de relancer la table.";

        _generatedState = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _generatedState.AddThemeStyleboxOverride("normal",
            StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_generatedState);
        _resetGeneratedState = AddCheck(this, "Réinitialiser explicitement l’état généré");
        _resetGeneratedState.TooltipText =
            "Obligatoire pour modifier une table déjà tirée ; cette action remet la génération et les résultats à zéro.";

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("OBJETS POSSIBLES", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Chaque catégorie produit au plus un résultat. Le poids départage les objets d’une même catégorie.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _empty = StudioStyle.Label(
            "État vide · ajoutez au moins un objet disponible dans ce projet.",
            10,
            StudioStyle.Warning,
            wrap: true);
        AddChild(_empty);
        _entriesHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _entriesHost.AddThemeConstantOverride("separation", 6);
        AddChild(_entriesHost);
        _addEntry = StudioStyle.Button("Ajouter un objet", compact: true);
        _addEntry.TooltipText = "Ajouter une ligne identifiée sans exposer son UUID";
        AddChild(_addEntry);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        _previewTiming = AddOption(this, "Moment à simuler");
        foreach (var mode in Enum.GetValues<StudioPlaceableLootPreviewMode>())
        {
            _previewTiming.AddItem(HumanPreviewMode(mode));
        }
        _preview = StudioStyle.Button("Prévisualiser le trésor", compact: true);
        _preview.TooltipText =
            "Utiliser le même tirage déterministe que le runtime sans modifier le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label(
            "Aucun trésor prévisualisé.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride("normal",
            StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger ce trésor initial au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, une table absente produit un diagnostic stable au lieu d’un repli optionnel.";

        _addEntry.Pressed += AddDefaultEntry;
        _timing.ItemSelected += _ => NotifyChanged();
        _seed.ValueChanged += _ => NotifyChanged();
        _persistGeneratedState.Toggled += _ => NotifyChanged();
        _resetGeneratedState.Toggled += _ => NotifyChanged();
        _previewTiming.ItemSelected += _ => ClearPreview();
        _preview.Pressed += () => PreviewRequested?.Invoke();
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
        StudioPlaceableLootDraft draft,
        IReadOnlyList<StudioPlaceableItemOption> items,
        bool enabled,
        bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        _generation = draft.Generation;
        _savedResultCount = draft.SavedResults.Count;
        _items = items;
        ClearRows();
        foreach (var entry in draft.Entries)
        {
            AddEntryRow(entry);
        }
        _timing.Select((int)draft.Timing);
        _seed.Value = draft.Seed;
        _persistGeneratedState.ButtonPressed = draft.PersistGeneratedState;
        _resetGeneratedState.ButtonPressed = draft.ResetGeneratedState;
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        _previewTiming.Select((int)StudioPlaceableLootPreviewMode.Open);
        _previewResult.Text = "Aucun trésor prévisualisé.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        UpdateEnabledState(enabled);
        UpdateGeneratedState();
        _loading = false;
    }

    internal StudioPlaceableLootDraft Capture(StudioPlaceableLootDraft source) => source with
    {
        Timing = TimingValue(),
        Seed = (int)_seed.Value,
        PersistGeneratedState = _persistGeneratedState.ButtonPressed,
        Entries = _rows.Select(row => new StudioPlaceableLootEntryDraft(
            row.Source.Id,
            ItemValue(row.Item),
            row.Group.Text,
            row.Weight.Value,
            (int)row.MinimumQuantity.Value,
            (int)row.MaximumQuantity.Value,
            (int)row.MinimumQuality.Value,
            row.Condition.Text,
            row.Unique.ButtonPressed)).ToArray(),
        ResetGeneratedState = _resetGeneratedState.ButtonPressed,
    };

    internal void SetEnabledState(bool enabled) => UpdateEnabledState(enabled);

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusTable() => _rows.FirstOrDefault()?.Item.GrabFocus();

    internal void FocusGeneration() => _timing.GrabFocus();

    internal void FocusGeneratedState() => _resetGeneratedState.GrabFocus();

    internal void FocusPreview() => _previewTiming.GrabFocus();

    internal void SetEvidenceValues(string mode)
    {
        if (_items.Count == 0)
        {
            return;
        }
        _loading = true;
        ClearRows();
        AddEntryRow(new(
            Guid.NewGuid(),
            _items[0].Id,
            "provisions",
            3,
            1,
            2,
            1,
            string.Empty,
            false));
        if (_items.Count > 1 || mode.Contains("table", StringComparison.OrdinalIgnoreCase))
        {
            AddEntryRow(new(
                Guid.NewGuid(),
                _items[Math.Min(1, _items.Count - 1)].Id,
                "rares",
                1,
                1,
                1,
                3,
                string.Empty,
                true));
        }
        var timing = mode.Contains("reset", StringComparison.OrdinalIgnoreCase)
            ? PlaceableAuthoredLootSpawnTiming.Reset
            : mode.Contains("initialize", StringComparison.OrdinalIgnoreCase)
                ? PlaceableAuthoredLootSpawnTiming.Initialize
                : PlaceableAuthoredLootSpawnTiming.Open;
        _timing.Select((int)timing);
        _previewTiming.Select((int)timing);
        _seed.Value = 4242;
        _persistGeneratedState.ButtonPressed = true;
        _resetGeneratedState.ButtonPressed = _resetGeneratedState.Visible;
        _runtimeRequired.ButtonPressed = true;
        _loading = false;
        UpdateEmptyState();
        ClearPreview();
        Changed?.Invoke();
    }

    internal void SetLootPreview(StudioPlaceableLootPreview preview)
    {
        var details = preview.Results.Count == 0
            ? string.Empty
            : " Résultat : " + string.Join(" · ", preview.Results.Select(result =>
                $"{ItemDisplayName(BlueprintId.From(result.ItemDefinitionId))} ×{result.Quantity} · qualité {result.Quality}"));
        _previewResult.Text = preview.Message + details;
        _previewResult.AddThemeColorOverride(
            "font_color", preview.Succeeded ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void AddDefaultEntry()
    {
        if (_items.Count == 0)
        {
            _state.Text = "Aucun objet disponible · créez d’abord un blueprint Objet.";
            _state.AddThemeColorOverride("font_color", StudioStyle.Warning);
            return;
        }
        AddEntryRow(new(
            Guid.NewGuid(),
            _items[0].Id,
            _rows.Count == 0 ? "principal" : $"catégorie-{_rows.Count + 1}",
            1,
            1,
            1,
            0,
            string.Empty,
            false));
        UpdateEmptyState();
        ClearPreview();
        Changed?.Invoke();
    }

    private void ClearRows()
    {
        foreach (var row in _rows)
        {
            _entriesHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _rows.Clear();
        UpdateEmptyState();
    }

    private void AddEntryRow(StudioPlaceableLootEntryDraft source)
    {
        var card = StudioStyle.Panel("PlaceableLootEntry", StudioStyle.SurfaceRaised, 6);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _entriesHost.AddChild(card);
        var margin = StudioStyle.Margin(9, 7, 9, 7);
        card.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        column.AddChild(header);
        var title = StudioStyle.Label("OBJET DU TRÉSOR", 10, StudioStyle.Accent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var remove = StudioStyle.Button("Retirer", compact: true);
        header.AddChild(remove);

        var identityGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identityGrid.AddThemeConstantOverride("h_separation", 7);
        column.AddChild(identityGrid);
        var item = AddOption(identityGrid, "Objet");
        PopulateItems(item, source.ItemBlueprintId);
        var group = AddLine(identityGrid, "Catégorie", "principal, rare, provisions…");
        group.Text = source.Group;

        var probabilityGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        probabilityGrid.AddThemeConstantOverride("h_separation", 7);
        column.AddChild(probabilityGrid);
        var weight = AddSpin(probabilityGrid, "Poids relatif", .001, 1_000_000, .1);
        var minimumQuantity = AddSpin(probabilityGrid, "Quantité minimale", 1, 1_000_000, 1);
        var maximumQuantity = AddSpin(probabilityGrid, "Quantité maximale", 1, 1_000_000, 1);
        var minimumQuality = AddSpin(probabilityGrid, "Qualité minimale", 0, 1_000_000, 1);
        weight.Value = source.Weight;
        minimumQuantity.Value = source.MinimumQuantity;
        maximumQuantity.Value = source.MaximumQuantity;
        minimumQuality.Value = source.MinimumQuality;
        var condition = AddLine(column, "Condition facultative", "laisser vide pour toujours disponible");
        condition.Text = source.Condition;
        var unique = AddCheck(column, "Objet unique dans ce tirage");
        unique.ButtonPressed = source.Unique;

        var row = new EntryRow(source, card, item, group, weight, minimumQuantity, maximumQuantity,
            minimumQuality, condition, unique, remove);
        _rows.Add(row);
        item.ItemSelected += _ => NotifyChanged();
        group.TextChanged += _ => NotifyChanged();
        weight.ValueChanged += _ => NotifyChanged();
        minimumQuantity.ValueChanged += _ => NotifyChanged();
        maximumQuantity.ValueChanged += _ => NotifyChanged();
        minimumQuality.ValueChanged += _ => NotifyChanged();
        condition.TextChanged += _ => NotifyChanged();
        unique.Toggled += _ => NotifyChanged();
        remove.Pressed += () =>
        {
            var selectedId = row.Source.Id;
            var selected = _rows.Single(candidate => candidate.Source.Id == selectedId);
            _rows.Remove(selected);
            _entriesHost.RemoveChild(selected.Card);
            selected.Card.QueueFree();
            UpdateEmptyState();
            ClearPreview();
            Changed?.Invoke();
        };
        UpdateEmptyState();
    }

    private void PopulateItems(OptionButton option, BlueprintId selected)
    {
        option.Clear();
        foreach (var item in _items)
        {
            option.AddItem(item.DisplayName);
            option.SetItemTooltip(option.ItemCount - 1, item.Slug);
        }
        var index = _items.ToList().FindIndex(item => item.Id == selected);
        option.Select(index >= 0 ? index : 0);
    }

    private void UpdateEnabledState(bool enabled)
    {
        Visible = enabled;
        _state.Text = _existingProfile
            ? "Trésor canonique existant · son état généré et les autres événements restent conservés par défaut."
            : !enabled
                ? "État vide · activez Trésor initial pour préparer une table."
                : _items.Count == 0
                    ? "Aucun objet disponible · créez d’abord un blueprint Objet."
                    : "Nouveau trésor prêt · la table reste dans le brouillon jusqu’à l’application validée.";
        _state.AddThemeColorOverride("font_color",
            _existingProfile || enabled && _items.Count > 0 ? StudioStyle.Success : StudioStyle.Warning);
        UpdateEmptyState();
    }

    private void UpdateGeneratedState()
    {
        var hasState = _generation > 0 || _savedResultCount > 0;
        _generatedState.Text = hasState
            ? $"État chargé · génération {_generation} · {_savedResultCount} résultat(s) conservé(s). Toute modification de table, moment ou graine exige la remise à zéro explicite."
            : "Aucun état généré enregistré · le premier tirage utilisera la graine configurée.";
        _generatedState.AddThemeColorOverride("font_color", hasState ? StudioStyle.Warning : StudioStyle.Muted);
        _resetGeneratedState.Visible = hasState;
    }

    private void UpdateEmptyState() => _empty.Visible = _rows.Count == 0;

    private void NotifyChanged()
    {
        if (_loading)
        {
            return;
        }
        ClearPreview();
        Changed?.Invoke();
    }

    private void ClearPreview()
    {
        if (_loading)
        {
            return;
        }
        _previewResult.Text = "Trésor à recalculer · le module n’a pas été modifié.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private bool RowIsReady(EntryRow row) =>
        ItemValue(row.Item).Value != Guid.Empty &&
        !string.IsNullOrWhiteSpace(row.Group.Text) &&
        double.IsFinite(row.Weight.Value) && row.Weight.Value > 0 &&
        row.MinimumQuantity.Value >= 1 &&
        row.MaximumQuantity.Value >= row.MinimumQuantity.Value &&
        row.MinimumQuality.Value >= 0;

    private PlaceableAuthoredLootSpawnTiming TimingValue() =>
        _timing.Selected >= 0 && _timing.Selected < Enum.GetValues<PlaceableAuthoredLootSpawnTiming>().Length
            ? (PlaceableAuthoredLootSpawnTiming)_timing.Selected
            : PlaceableAuthoredLootSpawnTiming.Open;

    private BlueprintId ItemValue(OptionButton option) =>
        option.Selected >= 0 && option.Selected < _items.Count
            ? _items[option.Selected].Id
            : BlueprintId.From(Guid.Empty);

    private string ItemLabel(OptionButton option) =>
        option.Selected >= 0 && option.Selected < option.ItemCount
            ? option.GetItemText(option.Selected)
            : "Objet indisponible";

    private string ItemDisplayName(BlueprintId id) =>
        _items.FirstOrDefault(item => item.Id == id)?.DisplayName ?? "Objet indisponible";

    private static string HumanTiming(PlaceableAuthoredLootSpawnTiming timing) => timing switch
    {
        PlaceableAuthoredLootSpawnTiming.Initialize => "Au chargement",
        PlaceableAuthoredLootSpawnTiming.Open => "À l’ouverture",
        PlaceableAuthoredLootSpawnTiming.Reset => "À la réinitialisation",
        _ => "Moment configuré",
    };

    private static string HumanPreviewMode(StudioPlaceableLootPreviewMode mode) => mode switch
    {
        StudioPlaceableLootPreviewMode.Initialize => "Chargement du placeable",
        StudioPlaceableLootPreviewMode.Open => "Ouverture du placeable",
        StudioPlaceableLootPreviewMode.Reset => "Réinitialisation du placeable",
        _ => "Moment configuré",
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
