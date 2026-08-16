using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaceableDamageableEditor : VBoxContainer
{
    private sealed class ResistanceRow(
        StudioPlaceableDamageResistanceDraft source,
        PanelContainer card,
        LineEdit damageType,
        SpinBox ratio,
        Button remove)
    {
        internal StudioPlaceableDamageResistanceDraft Source { get; } = source;
        internal PanelContainer Card { get; } = card;
        internal LineEdit DamageType { get; } = damageType;
        internal SpinBox Ratio { get; } = ratio;
        internal Button Remove { get; } = remove;
    }

    private sealed class StageRow(
        double source,
        PanelContainer card,
        SpinBox threshold,
        Button remove)
    {
        internal double Source { get; } = source;
        internal PanelContainer Card { get; } = card;
        internal SpinBox Threshold { get; } = threshold;
        internal Button Remove { get; } = remove;
    }

    private readonly Label _state;
    private readonly SpinBox _maximumHealth;
    private readonly SpinBox _currentHealth;
    private readonly SpinBox _armor;
    private readonly VBoxContainer _resistancesHost;
    private readonly Label _resistancesEmpty;
    private readonly Button _addResistance;
    private readonly GridContainer _stagesHost;
    private readonly Label _stagesEmpty;
    private readonly Button _addStage;
    private readonly LineEdit _destructionEffect;
    private readonly LineEdit _debris;
    private readonly CheckBox _repairable;
    private readonly SpinBox _repairCost;
    private readonly SpinBox _previewDamage;
    private readonly LineEdit _previewDamageType;
    private readonly Button _preview;
    private readonly Label _previewResult;
    private readonly CheckBox _runtimeRequired;
    private readonly List<ResistanceRow> _resistances = [];
    private readonly List<StageRow> _stages = [];
    private bool _existingProfile;
    private bool _loading;

    internal event Action? Changed;

    internal event Action<bool>? RuntimeRequirementChanged;

    internal bool RequiredAtRuntime => _runtimeRequired.ButtonPressed;

    internal bool IsReady => Visible && _maximumHealth.Value > 0 &&
                             _currentHealth.Value >= 0 && _currentHealth.Value <= _maximumHealth.Value &&
                             _armor.Value >= 0 && _resistances.Count > 0 && _stages.Count > 0 &&
                             _resistances.All(row => !string.IsNullOrWhiteSpace(row.DamageType.Text) &&
                                                     row.Ratio.Value is >= 0 and <= 100) &&
                             _resistances.Select(row => row.DamageType.Text.Trim())
                                 .Distinct(StringComparer.Ordinal).Count() == _resistances.Count &&
                             _stages.All(row => row.Threshold.Value is >= 0 and <= 100) &&
                             _stages.Select(row => row.Threshold.Value).Distinct().Count() == _stages.Count &&
                             !string.IsNullOrWhiteSpace(_destructionEffect.Text) &&
                             !string.IsNullOrWhiteSpace(_debris.Text) && _repairCost.Value >= 0;

    internal bool DamagePreviewReady =>
        _previewResult.Text.Contains("Aperçu", StringComparison.OrdinalIgnoreCase) &&
        _previewResult.Text.Contains("PV", StringComparison.OrdinalIgnoreCase);

    internal string HumanText => string.Join(' ',
        new[]
        {
            _state.Text,
            "Santé actuelle Santé maximale Armure Résistances Type Réduction Étapes visuelles Destruction Débris Réparable Coût Runtime Aperçu dégâts réparation PV",
            _previewResult.Text,
        }.Concat(_resistances.Select(row => row.DamageType.Text)));

    internal StudioPlaceableDamageableEditor()
    {
        Name = "StudioPlaceableDamageableEditor";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 7);

        _state = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        _state.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_state);

        var health = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        health.AddThemeConstantOverride("h_separation", 7);
        AddChild(health);
        _maximumHealth = AddSpinCell(health, "Santé maximale", 1, 1_000_000_000, 1);
        _currentHealth = AddSpinCell(health, "Santé actuelle", 0, 1_000_000_000, 1);
        _armor = AddSpinCell(health, "Armure", 0, 1_000_000_000, 1);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("RÉSISTANCES AUX DÉGÂTS", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Chaque réduction est exprimée en pourcentage. Les types supplémentaires déjà écrits restent conservés.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _resistancesEmpty = StudioStyle.Label(
            "État vide · ajoutez au moins un type de dégâts.", 10, StudioStyle.Warning, wrap: true);
        AddChild(_resistancesEmpty);
        _resistancesHost = new VBoxContainer();
        _resistancesHost.AddThemeConstantOverride("separation", 5);
        AddChild(_resistancesHost);
        _addResistance = StudioStyle.Button("Ajouter une résistance", compact: true);
        _addResistance.TooltipText = "Ajouter un type de dégâts nommé et son pourcentage de réduction";
        AddChild(_addResistance);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("ÉTAPES VISUELLES", 10, StudioStyle.Accent));
        AddChild(StudioStyle.Label(
            "Le runtime choisit une apparence endommagée lorsque la santé franchit l’un de ces seuils.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _stagesEmpty = StudioStyle.Label(
            "État vide · ajoutez au moins un seuil visuel.", 10, StudioStyle.Warning, wrap: true);
        AddChild(_stagesEmpty);
        _stagesHost = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _stagesHost.AddThemeConstantOverride("h_separation", 6);
        _stagesHost.AddThemeConstantOverride("v_separation", 6);
        AddChild(_stagesHost);
        _addStage = StudioStyle.Button("Ajouter une étape visuelle", compact: true);
        _addStage.TooltipText = "Ajouter un seuil de santé en pourcentage";
        AddChild(_addStage);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("DESTRUCTION ET RÉPARATION", 10, StudioStyle.Accent));
        _destructionEffect = AddLine(this, "Effet à la destruction", "vfx:pilier-destruction");
        _debris = AddLine(this, "Débris produits", "debris:pilier");
        _repairable = AddCheck(this, "Autoriser la réparation");
        _repairCost = AddSpin(this, "Coût de réparation", 0, 1_000_000_000, 1);

        AddChild(new HSeparator());
        AddChild(StudioStyle.Label("APERÇU SANS ÉCRITURE", 10, StudioStyle.Accent));
        var previewInputs = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        previewInputs.AddThemeConstantOverride("h_separation", 7);
        AddChild(previewInputs);
        _previewDamage = AddSpinCell(previewInputs, "Dégâts reçus", 0, 1_000_000_000, 1);
        var typeColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        typeColumn.AddThemeConstantOverride("separation", 3);
        previewInputs.AddChild(typeColumn);
        typeColumn.AddChild(StudioStyle.Label("Type de dégâts", 10, StudioStyle.Muted));
        _previewDamageType = new LineEdit { PlaceholderText = "physical" };
        StudioStyle.ApplyLineEdit(_previewDamageType);
        typeColumn.AddChild(_previewDamageType);
        _preview = StudioStyle.Button("Prévisualiser les dégâts", compact: true);
        _preview.TooltipText = "Calculer santé, étape visuelle, destruction et réparation sans modifier le module";
        AddChild(_preview);
        _previewResult = StudioStyle.Label(
            "Aucun dégât prévisualisé.", 10, StudioStyle.Muted, wrap: true);
        _previewResult.AddThemeStyleboxOverride(
            "normal", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 6));
        AddChild(_previewResult);

        _runtimeRequired = AddCheck(this, "Exiger ce profil de destructibilité au runtime");
        _runtimeRequired.TooltipText =
            "Si activé, l’absence des opérations produit un diagnostic au lieu d’un repli optionnel.";

        foreach (var spin in new[] { _maximumHealth, _currentHealth, _armor, _repairCost, _previewDamage })
        {
            spin.ValueChanged += _ => NotifyChanged();
        }
        foreach (var line in new[] { _destructionEffect, _debris, _previewDamageType })
        {
            line.TextChanged += _ => NotifyChanged();
        }
        _repairable.Toggled += _ =>
        {
            UpdateRepairState();
            NotifyChanged();
        };
        _runtimeRequired.Toggled += value =>
        {
            if (!_loading)
            {
                RuntimeRequirementChanged?.Invoke(value);
                Changed?.Invoke();
            }
        };
        _addResistance.Pressed += () =>
        {
            var index = 1;
            var type = "nouveau-type";
            while (_resistances.Any(row => string.Equals(row.DamageType.Text, type, StringComparison.Ordinal)))
            {
                type = $"nouveau-type-{++index}";
            }
            AddResistanceRow(new(type, 0));
            UpdateEmptyStates();
            Changed?.Invoke();
        };
        _addStage.Pressed += () =>
        {
            var threshold = new[] { .75, .5, .25, .1, 0 }
                .FirstOrDefault(candidate => _stages.All(row => !row.Threshold.Value.Equals(candidate * 100)));
            AddStageRow(threshold);
            UpdateEmptyStates();
            Changed?.Invoke();
        };
        _preview.Pressed += PreviewDamage;
    }

    internal void Load(StudioPlaceableDamageableDraft draft, bool enabled, bool requiredAtRuntime)
    {
        _loading = true;
        _existingProfile = draft.ExistingProfile;
        UpdateEnabledState(enabled);
        _maximumHealth.Value = draft.MaximumHealth;
        _currentHealth.Value = draft.CurrentHealth;
        _armor.Value = draft.Armor;
        ClearRows();
        foreach (var resistance in draft.Resistances)
        {
            AddResistanceRow(resistance);
        }
        foreach (var threshold in draft.VisualStageThresholds)
        {
            AddStageRow(threshold);
        }
        _destructionEffect.Text = draft.DestructionEffectId;
        _debris.Text = draft.DebrisDefinitionId;
        _repairable.ButtonPressed = draft.Repairable;
        _repairCost.Value = draft.RepairCost;
        _previewDamage.Value = Math.Min(50, draft.MaximumHealth);
        _previewDamageType.Text = draft.Resistances.FirstOrDefault()?.DamageType ?? "physical";
        _previewResult.Text = "Aucun dégât prévisualisé.";
        _previewResult.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _runtimeRequired.ButtonPressed = requiredAtRuntime;
        UpdateRepairState();
        UpdateEmptyStates();
        _loading = false;
    }

    internal void SetEnabledState(bool enabled)
    {
        UpdateEnabledState(enabled);
    }

    internal StudioPlaceableDamageableDraft Capture(StudioPlaceableDamageableDraft source) => source with
    {
        MaximumHealth = _maximumHealth.Value,
        CurrentHealth = _currentHealth.Value,
        Armor = _armor.Value,
        Resistances = _resistances.Select(row => new StudioPlaceableDamageResistanceDraft(
            row.DamageType.Text,
            row.Ratio.Value / 100)).ToArray(),
        VisualStageThresholds = _stages.Select(row => row.Threshold.Value / 100).ToArray(),
        DestructionEffectId = _destructionEffect.Text,
        DebrisDefinitionId = _debris.Text,
        Repairable = _repairable.ButtonPressed,
        RepairCost = _repairCost.Value,
    };

    internal void SetRequiredAtRuntime(bool value)
    {
        _loading = true;
        _runtimeRequired.ButtonPressed = value;
        _loading = false;
    }

    internal void FocusHealth() => _maximumHealth.GetLineEdit().GrabFocus();

    internal void FocusFirstResistance() => _resistances.FirstOrDefault()?.DamageType.GrabFocus();

    internal void FocusDestruction() => _destructionEffect.GrabFocus();

    internal void SetEvidenceValues(bool destructionPreview)
    {
        _maximumHealth.Value = 180;
        _currentHealth.Value = 140;
        _armor.Value = 12;
        ClearRows();
        AddResistanceRow(new("physical", .15));
        AddResistanceRow(new("fire", .4));
        AddResistanceRow(new("force", .05));
        AddStageRow(.75);
        AddStageRow(.5);
        AddStageRow(.25);
        _destructionEffect.Text = "vfx:studio-balise-destruction";
        _debris.Text = "debris:studio-balise";
        _repairable.ButtonPressed = true;
        _repairCost.Value = 18;
        _previewDamageType.Text = "fire";
        _previewDamage.Value = destructionPreview ? 500 : 100;
        _runtimeRequired.ButtonPressed = true;
        UpdateRepairState();
        UpdateEmptyStates();
        PreviewDamage();
    }

    private void AddResistanceRow(StudioPlaceableDamageResistanceDraft source)
    {
        var card = StudioStyle.Panel("PlaceableDamageResistance", StudioStyle.SurfaceRaised, 6);
        _resistancesHost.AddChild(card);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        card.AddChild(margin);
        var rowHost = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rowHost.AddThemeConstantOverride("separation", 6);
        margin.AddChild(rowHost);
        var typeColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        typeColumn.AddThemeConstantOverride("separation", 3);
        rowHost.AddChild(typeColumn);
        typeColumn.AddChild(StudioStyle.Label("Type de dégâts", 10, StudioStyle.Muted));
        var damageType = new LineEdit { Text = source.DamageType, PlaceholderText = "fire" };
        StudioStyle.ApplyLineEdit(damageType);
        typeColumn.AddChild(damageType);
        var ratioColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ratioColumn.AddThemeConstantOverride("separation", 3);
        rowHost.AddChild(ratioColumn);
        ratioColumn.AddChild(StudioStyle.Label("Réduction (%)", 10, StudioStyle.Muted));
        var ratio = NewSpin(0, 100, 1);
        ratio.Value = source.Ratio * 100;
        ratioColumn.AddChild(ratio);
        var remove = StudioStyle.Button("Retirer", compact: true);
        remove.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        rowHost.AddChild(remove);
        var row = new ResistanceRow(source, card, damageType, ratio, remove);
        _resistances.Add(row);
        damageType.TextChanged += _ => NotifyChanged();
        ratio.ValueChanged += _ => NotifyChanged();
        remove.Pressed += () =>
        {
            _resistances.Remove(row);
            _resistancesHost.RemoveChild(card);
            card.QueueFree();
            UpdateEmptyStates();
            Changed?.Invoke();
        };
    }

    private void AddStageRow(double source)
    {
        var card = StudioStyle.Panel("PlaceableDamageStage", StudioStyle.SurfaceRaised, 6);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _stagesHost.AddChild(card);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        card.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label("Santé restante (%)", 10, StudioStyle.Muted));
        var rowHost = new HBoxContainer();
        rowHost.AddThemeConstantOverride("separation", 5);
        column.AddChild(rowHost);
        var threshold = NewSpin(0, 100, 1);
        threshold.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        threshold.Value = source * 100;
        rowHost.AddChild(threshold);
        var remove = StudioStyle.Button("Retirer", compact: true);
        rowHost.AddChild(remove);
        var row = new StageRow(source, card, threshold, remove);
        _stages.Add(row);
        threshold.ValueChanged += _ => NotifyChanged();
        remove.Pressed += () =>
        {
            _stages.Remove(row);
            _stagesHost.RemoveChild(card);
            card.QueueFree();
            UpdateEmptyStates();
            Changed?.Invoke();
        };
    }

    private void PreviewDamage()
    {
        if (_maximumHealth.Value <= 0 || _currentHealth.Value > _maximumHealth.Value)
        {
            _previewResult.Text = "Aperçu refusé · vérifiez la santé actuelle et maximale.";
            _previewResult.AddThemeColorOverride("font_color", StudioStyle.Danger);
            return;
        }
        var damageType = _previewDamageType.Text.Trim();
        var resistance = _resistances.FirstOrDefault(row =>
            string.Equals(row.DamageType.Text.Trim(), damageType, StringComparison.Ordinal))?.Ratio.Value / 100 ?? 0;
        var resisted = _previewDamage.Value * (1 - Math.Clamp(resistance, 0, 1));
        var applied = Math.Max(0, resisted - _armor.Value);
        var health = Math.Max(0, _currentHealth.Value - applied);
        var ratio = health / _maximumHealth.Value;
        var stage = _stages.Select(row => row.Threshold.Value / 100)
            .OrderByDescending(value => value)
            .FirstOrDefault(value => ratio <= value);
        var destroyed = health <= 0;
        var visual = destroyed ? "Brisé" : stage > 0 ? $"Endommagé à {stage:P0}" : "Intact";
        var end = destroyed
            ? $"détruit · effet {_destructionEffect.Text.Trim()} · débris {_debris.Text.Trim()}"
            : visual;
        var repair = _repairable.ButtonPressed
            ? $"Réparation disponible · coût {_repairCost.Value:0.##}"
            : "Réparation interdite";
        _previewResult.Text =
            $"Aperçu · {_previewDamage.Value:0.##} dégâts {damageType} · réduction {resistance:P0} · " +
            $"armure {_armor.Value:0.##} · {health:0.##}/{_maximumHealth.Value:0.##} PV · {end}. {repair}.";
        _previewResult.AddThemeColorOverride("font_color", destroyed ? StudioStyle.Warning : StudioStyle.Success);
    }

    private void ClearRows()
    {
        foreach (var row in _resistances)
        {
            _resistancesHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _resistances.Clear();
        foreach (var row in _stages)
        {
            _stagesHost.RemoveChild(row.Card);
            row.Card.QueueFree();
        }
        _stages.Clear();
    }

    private void UpdateEmptyStates()
    {
        _resistancesEmpty.Visible = _resistances.Count == 0;
        _stagesEmpty.Visible = _stages.Count == 0;
    }

    private void UpdateEnabledState(bool enabled)
    {
        _state.Text = _existingProfile
            ? "Profil canonique existant · les résistances et étapes supplémentaires seront conservées."
            : enabled
                ? "Nouveau profil prêt · les dégâts restent dans le brouillon jusqu’à l’application validée."
                : "État vide · activez Santé / dégâts pour préparer une destructibilité sûre avant validation.";
        _state.AddThemeColorOverride(
            "font_color", _existingProfile || enabled ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void UpdateRepairState()
    {
        _repairCost.Editable = _repairable.ButtonPressed;
    }

    private void NotifyChanged()
    {
        if (!_loading)
        {
            Changed?.Invoke();
        }
    }

    private static SpinBox AddSpinCell(
        GridContainer parent,
        string label,
        double minimum,
        double maximum,
        double step)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        parent.AddChild(column);
        column.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = NewSpin(minimum, maximum, step);
        spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.AddChild(spin);
        return spin;
    }

    private static SpinBox AddSpin(Container parent, string label, double minimum, double maximum, double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var spin = NewSpin(minimum, maximum, step);
        spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(spin);
        return spin;
    }

    private static SpinBox NewSpin(double minimum, double maximum, double step)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var line = new LineEdit { PlaceholderText = placeholder, ClearButtonEnabled = false };
        StudioStyle.ApplyLineEdit(line);
        parent.AddChild(line);
        return line;
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
