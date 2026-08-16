using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDoorDamageLoadingOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _door;
    private readonly Button _damageTab;
    private readonly Button _loadingTab;
    private readonly Button _eventsTab;
    private readonly VBoxContainer _damagePanel;
    private readonly VBoxContainer _loadingPanel;
    private readonly VBoxContainer _eventsPanel;
    private readonly CheckBox _damageEnabled;
    private readonly SpinBox _maximumHealth;
    private readonly SpinBox _hardness;
    private readonly CheckBox _plotProtected;
    private readonly LineEdit _damagedVisual;
    private readonly LineEdit _breachedVisual;
    private readonly CheckBox _updateNavigation;
    private readonly LineEdit _debrisAsset;
    private readonly LineEdit _repairService;
    private readonly CheckBox _loadingEnabled;
    private readonly LineEdit _loadingImage;
    private readonly LineEdit _loadingTitle;
    private readonly LineEdit _loadingTip;
    private readonly CheckBox _destinationFallback;
    private readonly SpinBox _minimumDisplay;
    private readonly CheckBox _damageEventEnabled;
    private readonly LineEdit _damageScript;
    private readonly CheckBox _destructionEventEnabled;
    private readonly LineEdit _destructionScript;
    private readonly CheckBox _attackTargetedEnabled;
    private readonly LineEdit _attackTargetedScript;
    private readonly CheckBox _attackResolvedEnabled;
    private readonly LineEdit _attackResolvedScript;
    private readonly Label _previewSummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;

    private IReadOnlyList<StudioDoorDamageLoadingChoice> _choices = [];
    private StudioDoorDamageLoadingDraft? _draft;
    private StudioDoorDamageLoadingDraft? _approvedDraft;
    private bool _updating;
    private bool _operationApplied;
    private bool _editingEnabled;
    private bool _selectionRequiredAtOpen;
    private int _section;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? DoorChanged;

    internal bool IsOpen => Visible;
    internal int SectionCount => 3;
    internal bool PreviewReady => Visible && _approvedDraft is not null && !_apply.Disabled;
    internal bool ExplicitSelectionProven =>
        _selectionRequiredAtOpen && _door.Selected > 0 && _draft is not null;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(
                ' ',
                _damageTab.Text,
                _loadingTab.Text,
                _eventsTab.Text,
                _previewSummary.Text,
                _status.Text);
            return SectionCount == 3 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioDoorDamageLoadingOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioDoorDamageLoadingOverlay";
        Visible = false;
        ZIndex = 451;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("DoorDamageLoadingDialog", StudioStyle.Chrome, 12, 2);
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("PORTE · RÉSISTANCE ET RÉACTIONS", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Dégâts, chargement et événements serveur", 20));
        heading.AddChild(StudioStyle.Label(
            "Choisissez une porte · simulez sans écriture · appliquez par commande annulable",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le changement en attente · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var sourceRow = new HBoxContainer();
        sourceRow.AddThemeConstantOverride("separation", 10);
        root.AddChild(sourceRow);
        sourceRow.AddChild(StudioStyle.Label("Porte à configurer", 10, StudioStyle.Muted));
        _door = Option();
        _door.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _door.TooltipText = "Sélection explicite de la porte à modifier.";
        _door.ItemSelected += _ => LoadSelectedDoor();
        sourceRow.AddChild(_door);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _damageTab = TabButton("1 · Solidité", "Configurer dégâts, brèche, débris et réparation.");
        _damageTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_damageTab);
        _loadingTab = TabButton("2 · Chargement", "Configurer le visuel et le repli de destination.");
        _loadingTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_loadingTab);
        _eventsTab = TabButton("3 · Événements", "Configurer les réactions serveur aux dégâts et attaques.");
        _eventsTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_eventsTab);

        var body = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _damagePanel = PanelColumn();
        panels.AddChild(_damagePanel);
        _damagePanel.AddChild(StudioStyle.Label("Rendre la porte destructible", 17));
        _damagePanel.AddChild(StudioStyle.Label(
            "La brèche choisit une apparence, produit des débris, peut libérer le passage et reste réparable.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _damageEnabled = AddCheck(_damagePanel, "Activer la destructibilité");
        var damageGrid = Grid();
        _damagePanel.AddChild(damageGrid);
        _maximumHealth = AddField(damageGrid, "Points de structure", Number(1, 1_000_000, 1));
        _hardness = AddField(damageGrid, "Dureté", Number(0, 100_000, .5));
        _plotProtected = AddField(damageGrid, "Protection narrative", Check("Empêcher les dégâts"));
        _damagedVisual = AddField(damageGrid, "Apparence endommagée", Text("Ex. door.damaged"));
        _breachedVisual = AddField(damageGrid, "Apparence brisée", Text("Ex. door.breached"));
        _updateNavigation = AddField(damageGrid, "Passage après brèche", Check("Recalculer le passage"));
        _debrisAsset = AddField(damageGrid, "Asset de débris", Text("Ex. assets/debris/door.glb"));
        _repairService = AddField(damageGrid, "Service de réparation", Text("Ex. service.repair-door"));

        _loadingPanel = PanelColumn();
        panels.AddChild(_loadingPanel);
        _loadingPanel.AddChild(StudioStyle.Label("Présenter la transition", 17));
        _loadingPanel.AddChild(StudioStyle.Label(
            "Le visuel spécifique peut céder la place à celui de la destination lorsqu’il est absent.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _loadingEnabled = AddCheck(_loadingPanel, "Activer une présentation de chargement spécifique");
        var loadingGrid = Grid();
        _loadingPanel.AddChild(loadingGrid);
        _loadingImage = AddField(loadingGrid, "Image gouvernée", Text("Ex. assets/loading/door.png"));
        _loadingTitle = AddField(loadingGrid, "Titre localisé", Text("Ex. loading.door.title"));
        _loadingTip = AddField(loadingGrid, "Conseil localisé", Text("Ex. loading.door.tip"));
        _destinationFallback = AddField(
            loadingGrid,
            "Visuel de repli",
            Check("Utiliser celui de la destination"));
        _minimumDisplay = AddField(loadingGrid, "Durée minimale", Number(0, 120, .05, " s"));

        _eventsPanel = PanelColumn();
        panels.AddChild(_eventsPanel);
        _eventsPanel.AddChild(StudioStyle.Label("Réagir côté serveur", 17));
        _eventsPanel.AddChild(StudioStyle.Label(
            "Chaque événement activé appelle un script explicite. Les événements de capacité, rythme et personnalisés restent intacts.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var eventsGrid = Grid();
        _eventsPanel.AddChild(eventsGrid);
        _damageEventEnabled = AddField(eventsGrid, "OnDamaged", Check("Après application des dégâts"));
        _damageScript = AddField(eventsGrid, "Script OnDamaged", Text("Ex. scripts/door_damaged"));
        _destructionEventEnabled = AddField(eventsGrid, "OnDeath", Check("Quand la porte est détruite"));
        _destructionScript = AddField(eventsGrid, "Script OnDeath", Text("Ex. scripts/door_destroyed"));
        _attackTargetedEnabled = AddField(eventsGrid, "Attaque ciblée", Check("Quand la porte devient la cible"));
        _attackTargetedScript = AddField(
            eventsGrid,
            "Script au ciblage",
            Text("Ex. scripts/door_attack_targeted"));
        _attackResolvedEnabled = AddField(eventsGrid, "Attaque résolue", Check("Après résolution de l’attaque"));
        _attackResolvedScript = AddField(
            eventsGrid,
            "Script après attaque",
            Text("Ex. scripts/door_attack_resolved"));
        _eventsPanel.AddChild(StudioStyle.Label(
            "Autorité · serveur pour les quatre réactions",
            11,
            StudioStyle.Success,
            wrap: true));

        var previewCard = StudioStyle.Panel("DoorDamageLoadingPreview", StudioStyle.SurfaceRaised, 8);
        previewCard.CustomMinimumSize = new Vector2(0, 142);
        root.AddChild(previewCard);
        var previewMargin = StudioStyle.Margin(14, 11, 14, 11);
        previewCard.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 7);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("APERÇU SANS ÉCRITURE"));
        _previewSummary = StudioStyle.Label(
            "Choisissez une porte puis prévisualisez les dégâts, le chargement et les réactions.",
            11,
            StudioStyle.Muted,
            wrap: true);
        previewColumn.AddChild(_previewSummary);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _status = StudioStyle.Label("Prêt · aucun changement appliqué", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
    }

    internal void Open()
    {
        _session.CancelDoorDamageLoadingPreviewUx1AB();
        _choices = _session.DoorDamageLoadingChoicesUx1AB;
        _updating = true;
        _door.Clear();
        _door.AddItem("Choisir une porte…");
        foreach (var choice in _choices)
        {
            _door.AddItem($"{choice.Label} · {(choice.Configured ? "configurée" : "à configurer")}");
        }
        _door.Selected = 0;
        _updating = false;
        _draft = null;
        _approvedDraft = null;
        _operationApplied = false;
        _selectionRequiredAtOpen = true;
        Visible = true;
        SetEditingEnabled(false);
        _door.Disabled = _choices.Count == 0;
        _preview.Disabled = true;
        _apply.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
        ShowSection(0);
        _previewSummary.Text = "Choisissez explicitement une porte avant de préparer un aperçu.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus(
            _choices.Count == 0
                ? "Ajoutez d’abord un blueprint de porte au module."
                : "Choisissez une porte · aucun brouillon n’est ouvert automatiquement.",
            _choices.Count == 0);
    }

    internal void Close()
    {
        _session.CancelDoorDamageLoadingPreviewUx1AB();
        _draft = null;
        _approvedDraft = null;
        _selectionRequiredAtOpen = false;
        Visible = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        if (!Visible || !SelectDoorExplicitlyForEvidence() || _draft is null)
        {
            return false;
        }
        SetEvidenceValues("preuve-ab");
        ShowSection(mode.EndsWith("damage", StringComparison.OrdinalIgnoreCase)
            ? 0
            : mode.EndsWith("loading", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 2);
        Preview();
        return PreviewReady && ShowsHumanSurface && ExplicitSelectionProven;
    }

    internal bool RunUiTransactionEvidence()
    {
        var originalProject = _session.Project;
        var originalDirty = _session.IsDirty;
        Open();
        var selectionRequired = Visible && originalProject is not null &&
                                _door.Selected == 0 && _draft is null &&
                                _preview.Disabled && _apply.Disabled &&
                                ReferenceEquals(originalProject, _session.Project);
        if (!selectionRequired || !SelectDoorExplicitlyForEvidence() || _draft is null ||
            originalProject is null)
        {
            Close();
            return false;
        }

        var blueprintId = _draft.BlueprintId;
        var explicitSelection = ExplicitSelectionProven;
        SetEvidenceValues("transaction-ab");
        Preview();
        var previewed = PreviewReady && ShowsHumanSurface;
        Apply();
        var appliedProfile = _session.Project?.Module.WorldFeatures.Doors.SingleOrDefault(profile =>
            profile.BlueprintId == blueprintId);
        var damageApplied = appliedProfile?.Damage is
        {
            MaximumHealth: 135,
            Hardness: 7,
            PlotProtected: false,
            DamagedVisual: "door.transaction-ab.damaged",
            BreachedVisual: "door.transaction-ab.breached",
            UpdateNavigation: true,
            DebrisAssetId: "assets/debris/transaction-ab.glb",
            RepairServiceId: "service.repair-transaction-ab",
        };
        var loadingApplied = appliedProfile?.Loading is
        {
            ImageAssetId: "assets/loading/transaction-ab.png",
            TitleKey: "loading.transaction-ab.title",
            TipKey: "loading.transaction-ab.tip",
            UseDestinationFallback: true,
            MinimumDisplaySeconds: .75,
        };
        var expectedEvents = new Dictionary<string, (string Script, bool Before)>(StringComparer.Ordinal)
        {
            ["damage-applied"] = ("scripts/transaction-ab_damaged", false),
            ["entity-destroyed"] = ("scripts/transaction-ab_destroyed", false),
            ["physical-attack-targeted"] = ("scripts/transaction-ab_attack_targeted", true),
            ["physical-attack-resolved"] = ("scripts/transaction-ab_attack_resolved", false),
        };
        var eventsApplied = appliedProfile is { } eventProfile &&
                            expectedEvents.All(expected => eventProfile.Events.Any(binding =>
                                binding.Event == expected.Key &&
                                binding.ScriptId == expected.Value.Script &&
                                binding.Before == expected.Value.Before &&
                                binding.Authoritative));
        var historyReady = !_undo.Disabled;
        var applied = damageApplied && loadingApplied && eventsApplied && historyReady;
        Undo();
        var undone = ReferenceEquals(originalProject, _session.Project) &&
                     _session.IsDirty == originalDirty && !_redo.Disabled;
        Redo();
        var redone = !ReferenceEquals(originalProject, _session.Project) && !_undo.Disabled;
        Undo();
        var restored = ReferenceEquals(originalProject, _session.Project) &&
                       _session.IsDirty == originalDirty;
        Close();
        var valid = selectionRequired && explicitSelection && previewed && applied && undone && redone && restored;
        if (!valid)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AB_UI_TRANSACTION_FAILED selection_required={selectionRequired.ToString().ToLowerInvariant()} " +
                $"explicit_selection={explicitSelection.ToString().ToLowerInvariant()} preview={previewed.ToString().ToLowerInvariant()} " +
                $"damage={damageApplied.ToString().ToLowerInvariant()} loading={loadingApplied.ToString().ToLowerInvariant()} " +
                $"events={eventsApplied.ToString().ToLowerInvariant()} history={historyReady.ToString().ToLowerInvariant()} " +
                $"undone={undone.ToString().ToLowerInvariant()} redone={redone.ToString().ToLowerInvariant()} " +
                $"restored={restored.ToString().ToLowerInvariant()}");
        }
        return valid;
    }

    private void LoadSelectedDoor()
    {
        if (_updating || _choices.Count == 0)
        {
            return;
        }
        _session.CancelDoorDamageLoadingPreviewUx1AB();
        _approvedDraft = null;
        if (_door.Selected <= 0)
        {
            _draft = null;
            SetEditingEnabled(false);
            _door.Disabled = false;
            _preview.Disabled = true;
            _apply.Disabled = true;
            SetStatus("Choisissez une porte · aucun brouillon n’est ouvert automatiquement.", false);
            return;
        }

        var index = Math.Clamp(_door.Selected - 1, 0, _choices.Count - 1);
        _draft = _session.CreateDoorDamageLoadingDraftUx1AB(_choices[index].BlueprintId);
        if (_draft is null)
        {
            SetStatus("Cette porte reste indisponible tant que sa fiche est ambiguë.", true);
            SetEditingEnabled(false);
            _door.Disabled = false;
            _preview.Disabled = true;
            _apply.Disabled = true;
            return;
        }
        LoadDraft();
        SetEditingEnabled(true);
        _preview.Disabled = false;
        SetStatus("Porte sélectionnée · prévisualisation obligatoire avant application.", false);
    }

    private bool SelectDoorExplicitlyForEvidence()
    {
        if (_choices.Count == 0 || _door.Selected != 0 || _draft is not null)
        {
            return false;
        }
        var configuredIndex = _choices.ToList().FindIndex(choice => choice.Configured);
        _door.Selected = (configuredIndex >= 0 ? configuredIndex : 0) + 1;
        LoadSelectedDoor();
        return ExplicitSelectionProven;
    }

    private void LoadDraft()
    {
        var draft = _draft!;
        _updating = true;
        ExpandLimits(_maximumHealth, draft.MaximumHealth);
        ExpandLimits(_hardness, draft.Hardness);
        ExpandLimits(_minimumDisplay, draft.MinimumDisplaySeconds);
        _damageEnabled.ButtonPressed = draft.DamageEnabled;
        _maximumHealth.Value = draft.MaximumHealth;
        _hardness.Value = draft.Hardness;
        _plotProtected.ButtonPressed = draft.PlotProtected;
        _damagedVisual.Text = draft.DamagedVisual;
        _breachedVisual.Text = draft.BreachedVisual;
        _updateNavigation.ButtonPressed = draft.UpdateNavigation;
        _debrisAsset.Text = draft.DebrisAssetId;
        _repairService.Text = draft.RepairServiceId;
        _loadingEnabled.ButtonPressed = draft.LoadingEnabled;
        _loadingImage.Text = draft.LoadingImageAssetId;
        _loadingTitle.Text = draft.LoadingTitleKey;
        _loadingTip.Text = draft.LoadingTipKey;
        _destinationFallback.ButtonPressed = draft.UseDestinationFallback;
        _minimumDisplay.Value = draft.MinimumDisplaySeconds;
        _damageEventEnabled.ButtonPressed = draft.DamageEventEnabled;
        _damageScript.Text = draft.DamageScriptId;
        _destructionEventEnabled.ButtonPressed = draft.DestructionEventEnabled;
        _destructionScript.Text = draft.DestructionScriptId;
        _attackTargetedEnabled.ButtonPressed = draft.AttackTargetedEventEnabled;
        _attackTargetedScript.Text = draft.AttackTargetedScriptId;
        _attackResolvedEnabled.ButtonPressed = draft.AttackResolvedEventEnabled;
        _attackResolvedScript.Text = draft.AttackResolvedScriptId;
        _updating = false;
        _operationApplied = false;
        _undo.Disabled = true;
        _redo.Disabled = true;
        InvalidatePreview();
    }

    private StudioDoorDamageLoadingDraft ReadDraft() => _draft! with
    {
        DamageEnabled = _damageEnabled.ButtonPressed,
        MaximumHealth = _maximumHealth.Value,
        Hardness = _hardness.Value,
        PlotProtected = _plotProtected.ButtonPressed,
        DamagedVisual = _damagedVisual.Text,
        BreachedVisual = _breachedVisual.Text,
        UpdateNavigation = _updateNavigation.ButtonPressed,
        DebrisAssetId = _debrisAsset.Text,
        RepairServiceId = _repairService.Text,
        LoadingEnabled = _loadingEnabled.ButtonPressed,
        LoadingImageAssetId = _loadingImage.Text,
        LoadingTitleKey = _loadingTitle.Text,
        LoadingTipKey = _loadingTip.Text,
        UseDestinationFallback = _destinationFallback.ButtonPressed,
        MinimumDisplaySeconds = _minimumDisplay.Value,
        DamageEventEnabled = _damageEventEnabled.ButtonPressed,
        DamageScriptId = _damageScript.Text,
        DestructionEventEnabled = _destructionEventEnabled.ButtonPressed,
        DestructionScriptId = _destructionScript.Text,
        AttackTargetedEventEnabled = _attackTargetedEnabled.ButtonPressed,
        AttackTargetedScriptId = _attackTargetedScript.Text,
        AttackResolvedEventEnabled = _attackResolvedEnabled.ButtonPressed,
        AttackResolvedScriptId = _attackResolvedScript.Text,
    };

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Sélectionnez une porte avant de lancer l’aperçu.", true);
            return;
        }
        var candidate = ReadDraft();
        var result = _session.PreviewDoorDamageLoadingUx1AB(candidate);
        _previewSummary.Text = result.Succeeded
            ? result.Summary
            : "Aucun aperçu validé · corrigez les champs signalés puis recommencez.";
        _previewSummary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
        _approvedDraft = result.CanApply ? candidate : null;
        _apply.Disabled = _approvedDraft is null;
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Apply()
    {
        if (_approvedDraft is null)
        {
            SetStatus("Relancez l’aperçu avant d’appliquer.", true);
            return;
        }
        var result = _session.ApplyApprovedDoorDamageLoadingUx1AB();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _approvedDraft = null;
            _apply.Disabled = true;
            return;
        }
        _operationApplied = true;
        SetEditingEnabled(false);
        _preview.Disabled = true;
        _apply.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        DoorChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(
            result.Succeeded ? "Modification annulée · la porte précédente est restaurée." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            DoorChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(
            result.Succeeded ? "Modification rétablie · enregistrez pour écrire le module." : result.Message,
            !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            DoorChanged?.Invoke();
        }
    }

    private void SetEvidenceValues(string stem)
    {
        _updating = true;
        _damageEnabled.ButtonPressed = true;
        _maximumHealth.Value = 135;
        _hardness.Value = 7;
        _plotProtected.ButtonPressed = false;
        _damagedVisual.Text = $"door.{stem}.damaged";
        _breachedVisual.Text = $"door.{stem}.breached";
        _updateNavigation.ButtonPressed = true;
        _debrisAsset.Text = $"assets/debris/{stem}.glb";
        _repairService.Text = $"service.repair-{stem}";
        _loadingEnabled.ButtonPressed = true;
        _loadingImage.Text = $"assets/loading/{stem}.png";
        _loadingTitle.Text = $"loading.{stem}.title";
        _loadingTip.Text = $"loading.{stem}.tip";
        _destinationFallback.ButtonPressed = true;
        _minimumDisplay.Value = .75;
        _damageEventEnabled.ButtonPressed = true;
        _damageScript.Text = $"scripts/{stem}_damaged";
        _destructionEventEnabled.ButtonPressed = true;
        _destructionScript.Text = $"scripts/{stem}_destroyed";
        _attackTargetedEnabled.ButtonPressed = true;
        _attackTargetedScript.Text = $"scripts/{stem}_attack_targeted";
        _attackResolvedEnabled.ButtonPressed = true;
        _attackResolvedScript.Text = $"scripts/{stem}_attack_resolved";
        _updating = false;
        UpdateFieldStates();
        InvalidatePreview();
    }

    private void ConnectInvalidation()
    {
        foreach (var check in new[]
                 {
                     _damageEnabled,
                     _plotProtected,
                     _updateNavigation,
                     _loadingEnabled,
                     _destinationFallback,
                     _damageEventEnabled,
                     _destructionEventEnabled,
                     _attackTargetedEnabled,
                     _attackResolvedEnabled,
                 })
        {
            check.Toggled += _ =>
            {
                if (_updating) return;
                UpdateFieldStates();
                InvalidatePreview();
            };
        }
        foreach (var edit in new[]
                 {
                     _damagedVisual,
                     _breachedVisual,
                     _debrisAsset,
                     _repairService,
                     _loadingImage,
                     _loadingTitle,
                     _loadingTip,
                     _damageScript,
                     _destructionScript,
                     _attackTargetedScript,
                     _attackResolvedScript,
                 })
        {
            edit.TextChanged += _ => InvalidatePreview();
        }
        foreach (var spin in new[] { _maximumHealth, _hardness, _minimumDisplay })
        {
            spin.ValueChanged += _ => InvalidatePreview();
        }
    }

    private void InvalidatePreview()
    {
        if (_updating || _operationApplied || _draft is null)
        {
            return;
        }
        _approvedDraft = null;
        _session.CancelDoorDamageLoadingPreviewUx1AB();
        _apply.Disabled = true;
        _preview.Disabled = false;
        _previewSummary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _editingEnabled = enabled;
        _door.Disabled = !enabled;
        _damageTab.Disabled = !enabled;
        _loadingTab.Disabled = !enabled;
        _eventsTab.Disabled = !enabled;
        UpdateFieldStates();
    }

    private void UpdateFieldStates()
    {
        _damageEnabled.Disabled = !_editingEnabled;
        _loadingEnabled.Disabled = !_editingEnabled;
        var damage = _editingEnabled && _damageEnabled.ButtonPressed;
        foreach (var spin in new[] { _maximumHealth, _hardness }) spin.Editable = damage;
        foreach (var check in new[] { _plotProtected, _updateNavigation }) check.Disabled = !damage;
        foreach (var edit in new[] { _damagedVisual, _breachedVisual, _debrisAsset, _repairService })
            edit.Editable = damage;

        var loading = _editingEnabled && _loadingEnabled.ButtonPressed;
        foreach (var edit in new[] { _loadingImage, _loadingTitle, _loadingTip }) edit.Editable = loading;
        _destinationFallback.Disabled = !loading;
        _minimumDisplay.Editable = loading;

        foreach (var check in new[]
                 {
                     _damageEventEnabled,
                     _destructionEventEnabled,
                     _attackTargetedEnabled,
                     _attackResolvedEnabled,
                 })
            check.Disabled = !_editingEnabled;
        _damageScript.Editable = _editingEnabled && _damageEventEnabled.ButtonPressed;
        _destructionScript.Editable = _editingEnabled && _destructionEventEnabled.ButtonPressed;
        _attackTargetedScript.Editable = _editingEnabled && _attackTargetedEnabled.ButtonPressed;
        _attackResolvedScript.Editable = _editingEnabled && _attackResolvedEnabled.ButtonPressed;
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _damagePanel.Visible = _section == 0;
        _loadingPanel.Visible = _section == 1;
        _eventsPanel.Visible = _section == 2;
        ApplyTabStyle(_damageTab, _section == 0);
        ApplyTabStyle(_loadingTab, _section == 1);
        ApplyTabStyle(_eventsTab, _section == 2);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.72f : Size.X >= 2300 ? 1.24f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 900, 1160);
        var height = Math.Clamp((Size.Y - 72) / scale, 700, 870);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 9);
        return panel;
    }

    private static Button TabButton(string text, string tooltip)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.TooltipText = tooltip;
        return button;
    }

    private static void ApplyTabStyle(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(
                selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                selected ? StudioStyle.Success : StudioStyle.Border,
                6,
                selected ? 2 : 1));
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(245, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = Check(text);
        parent.AddChild(check);
        return check;
    }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        return check;
    }

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        foreach (var label in labels) option.AddItem(label);
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static LineEdit Text(string placeholder)
    {
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static SpinBox Number(double minimum, double maximum, double step, string suffix = "")
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Suffix = suffix,
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static void ExpandLimits(SpinBox spin, double value)
    {
        if (value < spin.MinValue) spin.MinValue = value;
        if (value > spin.MaxValue) spin.MaxValue = value;
    }
}
