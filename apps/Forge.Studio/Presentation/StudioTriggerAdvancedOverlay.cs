using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTriggerAdvancedVolumeEditor : PanelContainer
{
    private readonly TriggerGameplayVolumeKind _kind;
    private readonly CheckBox _enabled;
    private readonly SpinBox _value;
    private readonly LineEdit _enterBinding;
    private readonly LineEdit _exitBinding;
    private bool _updating;
    private bool _valueEdited;

    internal event Action? Changed;

    internal TriggerGameplayVolumeKind Kind => _kind;

    internal StudioTriggerAdvancedVolumeEditor(TriggerGameplayVolumeKind kind)
    {
        _kind = kind;
        AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Border, 7));
        var margin = StudioStyle.Margin(9, 7, 9, 7);
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);

        var heading = new HBoxContainer();
        heading.AddChild(StudioStyle.Label(HumanizeVolume(kind), 12));
        heading.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _enabled = Check("Actif");
        heading.AddChild(_enabled);
        column.AddChild(heading);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(grid);
        grid.AddChild(StudioStyle.Label(PrimaryLabel(kind), 10, StudioStyle.Muted));
        _value = new SpinBox
        {
            MinValue = -1_000_000,
            MaxValue = 1_000_000,
            Step = .05,
            AllowGreater = true,
            AllowLesser = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(_value);
        grid.AddChild(_value);
        grid.AddChild(StudioStyle.Label("Événement d’entrée", 10, StudioStyle.Muted));
        _enterBinding = Text();
        grid.AddChild(_enterBinding);
        grid.AddChild(StudioStyle.Label("Événement de sortie", 10, StudioStyle.Muted));
        _exitBinding = Text();
        grid.AddChild(_exitBinding);

        _enabled.Toggled += _ => Changed?.Invoke();
        _value.ValueChanged += _ =>
        {
            if (!_updating)
            {
                _valueEdited = true;
            }
            Changed?.Invoke();
        };
        _enterBinding.TextChanged += _ => Changed?.Invoke();
        _exitBinding.TextChanged += _ => Changed?.Invoke();
    }

    internal void Populate(TriggerGameplayVolumeProfile value)
    {
        _updating = true;
        var primary = StudioSessionController.PrimaryTriggerVolumeParameterUx1AC(_kind);
        _enabled.ButtonPressed = value.Enabled;
        _value.Value = value.NumericParameters.GetValueOrDefault(primary.Key, primary.Value);
        _enterBinding.Text = value.EnterBinding;
        _exitBinding.Text = value.ExitBinding;
        _valueEdited = false;
        _updating = false;
    }

    internal TriggerGameplayVolumeProfile Read(TriggerGameplayVolumeProfile original)
    {
        var primary = StudioSessionController.PrimaryTriggerVolumeParameterUx1AC(_kind);
        var numeric = original.NumericParameters.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        if (_valueEdited)
        {
            numeric[primary.Key] = _value.Value;
        }
        return original with
        {
            NumericParameters = numeric,
            TextParameters = original.TextParameters.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            EnterBinding = _enterBinding.Text,
            ExitBinding = _exitBinding.Text,
            Enabled = _enabled.ButtonPressed,
        };
    }

    internal void SetEditingEnabled(bool enabled)
    {
        _enabled.Disabled = !enabled;
        _value.Editable = enabled;
        _enterBinding.Editable = enabled;
        _exitBinding.Editable = enabled;
    }

    internal bool HasTypedBindings =>
        _enterBinding.Text == $"{_kind}VolumeEntered" &&
        _exitBinding.Text == $"{_kind}VolumeExited";

    private static LineEdit Text()
    {
        var edit = new LineEdit();
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static CheckBox Check(string text)
    {
        var value = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        value.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        return value;
    }

    private static string HumanizeVolume(TriggerGameplayVolumeKind kind) => kind switch
    {
        TriggerGameplayVolumeKind.Damage => "Dégâts",
        TriggerGameplayVolumeKind.Heal => "Soin",
        TriggerGameplayVolumeKind.Gravity => "Gravité",
        TriggerGameplayVolumeKind.Water => "Eau",
        TriggerGameplayVolumeKind.Climb => "Escalade",
        TriggerGameplayVolumeKind.NoBuild => "Construction interdite",
        TriggerGameplayVolumeKind.Camera => "Caméra",
        TriggerGameplayVolumeKind.Audio => "Audio",
        TriggerGameplayVolumeKind.Weather => "Météo",
        TriggerGameplayVolumeKind.Checkpoint => "Point de contrôle",
        _ => "Volume",
    };

    private static string PrimaryLabel(TriggerGameplayVolumeKind kind) => kind switch
    {
        TriggerGameplayVolumeKind.Damage => "Variation de santé",
        TriggerGameplayVolumeKind.Heal => "Soin de santé",
        TriggerGameplayVolumeKind.Gravity => "Facteur de gravité",
        TriggerGameplayVolumeKind.Water => "Facteur de mouvement",
        TriggerGameplayVolumeKind.Climb => "Facteur d’escalade",
        TriggerGameplayVolumeKind.NoBuild => "Autorisation de bâtir",
        TriggerGameplayVolumeKind.Camera => "Intensité caméra",
        TriggerGameplayVolumeKind.Audio => "Intensité audio",
        TriggerGameplayVolumeKind.Weather => "Intensité météo",
        TriggerGameplayVolumeKind.Checkpoint => "Activation du point",
        _ => "Valeur",
    };
}

internal sealed partial class StudioTriggerAdvancedOverlay : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx1ACEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _trigger;
    private readonly Label _selection;
    private readonly Button _trapTab;
    private readonly Button _filterTab;
    private readonly Button _volumesTab;
    private readonly VBoxContainer _trapPanel;
    private readonly VBoxContainer _filterPanel;
    private readonly VBoxContainer _volumesPanel;
    private readonly LineEdit _api;
    private readonly CheckBox _requireRuntime;
    private readonly LineEdit _triggerBinding;
    private readonly LineEdit _payload;
    private readonly SpinBox _detection;
    private readonly SpinBox _disarm;
    private readonly SpinBox _reset;
    private readonly LineEdit _owner;
    private readonly LineEdit _visualEffect;
    private readonly LineEdit _friendlyPolicy;
    private readonly CheckBox _armed;
    private readonly CheckBox _oneShot;
    private readonly CheckBox _gmVisualization;
    private readonly LineEdit _visualizationColor;
    private readonly LineEdit _detectedBinding;
    private readonly LineEdit _disarmedBinding;
    private readonly LineEdit _triggeredBinding;
    private readonly LineEdit _resetBinding;
    private readonly CheckBox _playersOnly;
    private readonly StudioTriggerStringListEditor _requiredTags;
    private readonly StudioTriggerStringListEditor _excludedTags;
    private readonly StudioTriggerStringListEditor _requiredComponents;
    private readonly ItemList _factions;
    private readonly Label _factionHint;
    private readonly LineEdit _queryOwner;
    private readonly LineEdit _team;
    private readonly LineEdit _predicate;
    private readonly IReadOnlyList<StudioTriggerAdvancedVolumeEditor> _volumeEditors;
    private readonly CheckBox _debugEnabled;
    private readonly CheckBox _showOccupants;
    private readonly CheckBox _showLastFires;
    private readonly CheckBox _showCooldowns;
    private readonly CheckBox _showFilters;
    private readonly CheckBox _showEventTrace;
    private readonly SpinBox _maximumTrace;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private IReadOnlyList<StudioTriggerAdvancedChoice> _choices = [];
    private IReadOnlyList<StudioTriggerFactionChoice> _factionChoices = [];
    private StudioTriggerAdvancedDraft? _draft;
    private bool _updating;
    private bool _applied;
    private bool _detectionEdited;
    private bool _disarmEdited;
    private bool _resetEdited;
    private bool _maximumTraceEdited;
    private int _section;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? TriggerChanged;

    internal bool IsOpen => Visible;

    internal int SectionCount => 3;

    internal int ChoiceCount => _choices.Count;

    internal int VolumeCount => _volumeEditors.Count;

    internal bool HasExplicitSelection => _draft is not null && _trigger.Selected > 0;

    internal bool HasTypedBindings => _volumeEditors.All(item => item.HasTypedBindings);

    internal bool UsesIsolatedEvidence =>
        _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);

    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;

    internal bool EvidenceResourcesReleased =>
        _evidenceSession is null && _evidenceTempRoot is null && _lastEvidenceResourcesReleased;

    internal bool SourceSessionRestored =>
        ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ',
                _selection.Text,
                _summary.Text,
                _status.Text,
                _trapTab.Text,
                _filterTab.Text,
                _volumesTab.Text);
            return HasExplicitSelection && SectionCount == 3 && VolumeCount == 10 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioTriggerAdvancedOverlay(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioTriggerAdvancedOverlay";
        Visible = false;
        ZIndex = 449;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0, 0, 0, .8f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("TriggerAdvancedDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("TRIGGER AVANCÉ", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Piège, filtre, volumes et occupation", 20));
        heading.AddChild(StudioStyle.Label(
            "ORF-TRG-004 · ORF-TRG-011 · ORF-TRG-017 · ORF-TRG-019 · aperçu obligatoire avant application",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer();
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Déclencheur à configurer", 10, StudioStyle.Muted));
        _trigger = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_trigger);
        _trigger.ItemSelected += SelectTrigger;
        selector.AddChild(_trigger);
        _selection = StudioStyle.Label(
            "Choisissez explicitement un déclencheur doté d’un volume de base.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _trapTab = Tab("Trigger de piège");
        _trapTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_trapTab);
        _filterTab = Tab("Filtre de cible");
        _filterTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_filterTab);
        _volumesTab = Tab("Volumes & occupation");
        _volumesTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_volumesTab);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 560),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var body = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _trapPanel = PanelColumn();
        panels.AddChild(_trapPanel);
        _trapPanel.AddChild(StudioStyle.Label("Trigger de piège", 17));
        _trapPanel.AddChild(StudioStyle.Label(
            "Réglez détection, désamorçage, charge, réarmement, propriétaire et repère maître de jeu sans modifier le volume de base.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var trapGrid = Grid();
        _trapPanel.AddChild(trapGrid);
        _api = AddField(trapGrid, "API ForgeScript", Text("1.0.0"));
        _requireRuntime = AddField(trapGrid, "Runtime", Check("Exiger le profil avancé"));
        _triggerBinding = AddField(trapGrid, "Déclenchement", Text("enter"));
        _payload = AddField(trapGrid, "Charge", Text("damage:trigger"));
        _detection = AddField(trapGrid, "Difficulté de détection", Number(0, 100_000, .5, allowGreater: true));
        _disarm = AddField(trapGrid, "Difficulté de désamorçage", Number(0, 100_000, .5, allowGreater: true));
        _reset = AddField(trapGrid, "Réarmement", Number(0, 86_400, .5, " s", allowGreater: true));
        _owner = AddField(trapGrid, "Propriétaire", Text("module"));
        _visualEffect = AddField(trapGrid, "Effet visuel", Text("vfx:trap"));
        _friendlyPolicy = AddField(trapGrid, "Politique alliée", Text("owner-safe"));
        _armed = AddField(trapGrid, "État", Check("Piège armé"));
        _oneShot = AddField(trapGrid, "Usage", Check("Une seule activation"));
        _gmVisualization = AddField(trapGrid, "Maître de jeu", Check("Afficher le repère"));
        _visualizationColor = AddField(trapGrid, "Couleur du repère", Text("#ff7a45"));
        _trapPanel.AddChild(StudioStyle.SectionLabel("ÉVÉNEMENTS TYPÉS DU PIÈGE"));
        var bindingGrid = Grid();
        _trapPanel.AddChild(bindingGrid);
        _detectedBinding = AddField(bindingGrid, "Détecté", Text("TrapDetected"));
        _disarmedBinding = AddField(bindingGrid, "Désarmé", Text("TrapDisarmed"));
        _triggeredBinding = AddField(bindingGrid, "Déclenché", Text("TrapTriggered"));
        _resetBinding = AddField(bindingGrid, "Réarmé", Text("TrapReset"));

        _filterPanel = PanelColumn();
        panels.AddChild(_filterPanel);
        _filterPanel.AddChild(StudioStyle.Label("Filtre de cible", 17));
        _filterPanel.AddChild(StudioStyle.Label(
            "Les tags, factions et composants forment une requête typée. Les factions sont affichées par leur nom humain.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _playersOnly = Check("Joueurs uniquement");
        _filterPanel.AddChild(_playersOnly);
        var filterColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        filterColumns.AddThemeConstantOverride("separation", 10);
        _filterPanel.AddChild(filterColumns);
        var named = PanelColumn();
        named.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        filterColumns.AddChild(named);
        named.AddChild(StudioStyle.SectionLabel("TAGS REQUIS"));
        _requiredTags = new StudioTriggerStringListEditor("Aucun tag requis.", "player");
        named.AddChild(_requiredTags);
        named.AddChild(StudioStyle.SectionLabel("TAGS EXCLUS"));
        _excludedTags = new StudioTriggerStringListEditor("Aucun tag exclu.", "undead");
        named.AddChild(_excludedTags);
        named.AddChild(StudioStyle.SectionLabel("COMPOSANTS REQUIS"));
        _requiredComponents = new StudioTriggerStringListEditor("Aucun composant requis.", "health");
        named.AddChild(_requiredComponents);
        var relations = PanelColumn();
        relations.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        filterColumns.AddChild(relations);
        relations.AddChild(StudioStyle.SectionLabel("FACTIONS AUTORISÉES"));
        _factions = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 135),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Multi,
        };
        _factions.AddThemeStyleboxOverride("panel", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 6));
        relations.AddChild(_factions);
        _factionHint = StudioStyle.Label(
            "Aucune faction sélectionnée : toutes les factions sont acceptées.",
            10,
            StudioStyle.Muted,
            wrap: true);
        relations.AddChild(_factionHint);
        var relationGrid = Grid();
        relations.AddChild(relationGrid);
        _queryOwner = AddField(relationGrid, "Propriétaire", Text("Facultatif"));
        _team = AddField(relationGrid, "Équipe", Text("Facultatif"));
        _predicate = AddField(relationGrid, "État sandbox", Text("quest==active"));

        _volumesPanel = PanelColumn();
        panels.AddChild(_volumesPanel);
        _volumesPanel.AddChild(StudioStyle.Label("Volumes de gameplay", 17));
        _volumesPanel.AddChild(StudioStyle.Label(
            "La bibliothèque canonique contient exactement dix volumes. La valeur principale est éditable ; les paramètres d’auteur supplémentaires restent préservés.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var volumeGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _volumesPanel.AddChild(volumeGrid);
        _volumeEditors = Enum.GetValues<TriggerGameplayVolumeKind>()
            .Select(kind => new StudioTriggerAdvancedVolumeEditor(kind))
            .ToArray();
        foreach (var editor in _volumeEditors)
        {
            editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            editor.Changed += InvalidatePreview;
            volumeGrid.AddChild(editor);
        }
        _volumesPanel.AddChild(StudioStyle.SectionLabel("VISUALISATION D’OCCUPATION"));
        _volumesPanel.AddChild(StudioStyle.Label(
            "Ce profil gouverne l’overlay de diagnostic runtime : il ne remplace pas la manipulation du volume dans la vue 3D.",
            10,
            StudioStyle.Warning,
            wrap: true));
        var debugGrid = Grid();
        _volumesPanel.AddChild(debugGrid);
        _debugEnabled = AddField(debugGrid, "Diagnostic", Check("Activer la visualisation"));
        _showOccupants = AddField(debugGrid, "Occupation", Check("Afficher les occupants"));
        _showLastFires = AddField(debugGrid, "Déclenchements", Check("Afficher les derniers tirs"));
        _showCooldowns = AddField(debugGrid, "Délais", Check("Afficher les délais restants"));
        _showFilters = AddField(debugGrid, "Filtres", Check("Afficher le résumé de filtre"));
        _showEventTrace = AddField(debugGrid, "Trace", Check("Afficher les événements"));
        _maximumTrace = AddField(debugGrid, "Taille maximale", Number(1, 1000, 1, " entrées"));

        _summary = StudioStyle.Label(
            "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.",
            10,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
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
        _undo = StudioStyle.Button("Annuler le profil avancé", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession()
    {
        _session.CancelTriggerAdvancedUx1AC();
        _choices = _session.TriggerAdvancedChoicesUx1AC;
        _factionChoices = _session.TriggerFactionChoicesUx1AC;
        _trigger.Clear();
        _trigger.AddItem("Choisissez explicitement un déclencheur…");
        foreach (var choice in _choices)
        {
            _trigger.AddItem(choice.Label);
        }
        _trigger.Select(0);
        _draft = null;
        _applied = false;
        _selection.Text = _choices.Count == 0
            ? "Aucun profil de déclencheur n’est disponible dans la zone active."
            : "Choisissez explicitement un déclencheur doté d’un volume de base.";
        _selection.AddThemeColorOverride("font_color", _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez un déclencheur puis prévisualisez · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = "Prêt · sélection explicite requise";
        SetEditingEnabled(false);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        MoveToFront();
        ShowSection(0);
        UpdateDialogBounds();
        _trigger.GrabFocus();
    }

    internal void Close()
    {
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelTriggerAdvancedUx1AC();
        Visible = false;
        _draft = null;
        _applied = false;
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceResourcesReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        var index = _choices.ToList().FindIndex(item => item.HasFoundation);
        if (index < 0)
        {
            if (!TryOpenIsolatedEvidenceSession())
            {
                return false;
            }
            index = _choices.ToList().FindIndex(item => item.HasFoundation);
            if (index < 0)
            {
                Close();
                return false;
            }
        }
        _trigger.Select(index + 1);
        LoadChoice(index);
        if (_draft is null)
        {
            return false;
        }
        ShowSection(mode.Contains("filter", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("volume", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("occupation", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
        Preview();
        if (UsesIsolatedEvidence)
        {
            _selection.Text += " · session de preuve isolée";
            _summary.Text =
                "PREUVE ISOLÉE · le projet source ne contient encore aucune Foundation de trigger et reste strictement inchangé.\n" +
                _summary.Text;
            SetStatus("Preuve isolée prête · fermeture = suppression de la session temporaire", error: false);
        }
        return !_apply.Disabled && HasExplicitSelection && HasTypedBindings && ShowsHumanSurface &&
               EvidenceSourceUntouched;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ac-ui-" + Guid.NewGuid().ToString("N"));
        var isolatedSession = new StudioSessionController();
        StudioTriggerAdvancedOverlay? isolatedOverlay = null;
        try
        {
            var created = ProjectSession.Create(tempRoot, sourceSnapshot.Project);
            if (!created.Succeeded ||
                !isolatedSession.Open(tempRoot, sourceSnapshot.ActiveAreaId?.ToString()).Succeeded)
            {
                return false;
            }
            isolatedOverlay = new StudioTriggerAdvancedOverlay(isolatedSession);
            AddChild(isolatedOverlay);
            var isolatedEvidence = isolatedOverlay.RunBoundUiTransactionEvidence();
            return isolatedEvidence && SourceMatchesSnapshot(sourceSnapshot);
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AC_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            if (isolatedOverlay is not null && GodotObject.IsInstanceValid(isolatedOverlay))
            {
                isolatedOverlay.Close();
                isolatedOverlay.GetParent()?.RemoveChild(isolatedOverlay);
                isolatedOverlay.Free();
            }
            isolatedSession.Close();
            DeleteEvidenceRoot(tempRoot);
        }
    }

    private bool RunBoundUiTransactionEvidence()
    {
        var boundSourceSnapshot = CaptureSourceSnapshot();
        if (boundSourceSnapshot is null)
        {
            return false;
        }
        var ready = PrepareEvidence("ux1-ac-trigger-advanced");
        var usedFallback = UsesIsolatedEvidence;
        var transactionSession = _session;
        var originalProject = transactionSession.Project;
        var originalDirty = transactionSession.IsDirty;
        var dryRun = ready && originalProject is not null &&
                     ReferenceEquals(originalProject, transactionSession.Project) &&
                     transactionSession.IsDirty == originalDirty &&
                     (!usedFallback || SourceMatchesSnapshot(boundSourceSnapshot));
        Apply();
        var appliedProject = transactionSession.Project;
        var applied = dryRun && _applied && !ReferenceEquals(originalProject, appliedProject);
        if (applied)
        {
            Undo();
        }
        var undone = applied && ReferenceEquals(originalProject, transactionSession.Project) &&
                     transactionSession.IsDirty == originalDirty;
        if (undone)
        {
            Redo();
        }
        var redone = undone && ReferenceEquals(appliedProject, transactionSession.Project);
        if (redone)
        {
            Undo();
        }
        var restored = redone && ReferenceEquals(originalProject, transactionSession.Project) &&
                       transactionSession.IsDirty == originalDirty;
        var boundSourceUntouched = !usedFallback || SourceMatchesSnapshot(boundSourceSnapshot);
        Close();
        return dryRun && applied && undone && redone && restored && boundSourceUntouched &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx1ACEvidenceProject();
        if (snapshot is null || evidenceProject is null)
        {
            return false;
        }

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ac-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded ||
                !evidenceSession.Open(tempRoot, snapshot.ActiveAreaId?.ToString()).Succeeded)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased = DeleteEvidenceRoot(tempRoot);
                _evidenceTempRoot = _lastEvidenceResourcesReleased ? null : tempRoot;
                _session = _sourceSession;
                _lastSourceRestored = true;
                return false;
            }

            _evidenceSourceSnapshot = snapshot;
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            _lastEvidenceResourcesReleased = false;
            _lastSourceRestored = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.HasFoundation) && SourceMatchesSnapshot(snapshot);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AC_EVIDENCE_FALLBACK_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = DeleteEvidenceRoot(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = released;
            _lastSourceRestored = true;
            return false;
        }
    }

    private StudioUx1ACEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx1ACEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId);
    }

    private bool SourceMatchesSnapshot(StudioUx1ACEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId;
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceSourceSnapshot = null;
        _session = _sourceSession;
        var released = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AC_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = DeleteEvidenceRoot(tempRoot);
        if (!rootReleased)
        {
            _evidenceTempRoot = tempRoot;
        }
        return released && rootReleased;
    }

    private static bool DeleteEvidenceRoot(string? tempRoot)
    {
        if (tempRoot is null)
        {
            return true;
        }
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            return !Directory.Exists(tempRoot);
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AC_EVIDENCE_DELETE_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
    }

    private void SelectTrigger(long index)
    {
        if (_updating)
        {
            return;
        }
        if (index <= 0)
        {
            _draft = null;
            SetEditingEnabled(false);
            return;
        }
        LoadChoice((int)index - 1);
    }

    private void LoadChoice(int index)
    {
        if (index < 0 || index >= _choices.Count)
        {
            return;
        }
        _session.CancelTriggerAdvancedUx1AC();
        _applied = false;
        var choice = _choices[index];
        _draft = _session.CreateTriggerAdvancedDraftUx1AC(choice.ProfileId);
        if (!choice.HasFoundation)
        {
            _selection.Text =
                $"{choice.Label} · configurez d’abord son volume avec l’outil Déclencheur.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            _summary.Text =
                "Le profil avancé exige une fondation canonique. Aucun portail ni autre page n’est créé automatiquement.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
            SetEditingEnabled(false);
            SetStatus("Volume de base requis · ouvrez Déclencheur", error: false);
            return;
        }
        if (_draft is null)
        {
            _selection.Text = "Ce déclencheur n’existe plus dans la zone active. Rechargez le module.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false);
            return;
        }
        Populate(_draft);
        _selection.Text = choice.HasAdvanced
            ? $"{choice.Label} · profil avancé existant chargé"
            : $"{choice.Label} · nouveau profil avancé préparé";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetEditingEnabled(true);
        SetStatus("Profil chargé · prévisualisez avant d’appliquer", error: false);
    }

    private void Populate(StudioTriggerAdvancedDraft draft)
    {
        _updating = true;
        _api.Text = draft.ForgeScriptApiVersion;
        _requireRuntime.ButtonPressed = draft.RequireRuntime;
        var trap = draft.Trap;
        _triggerBinding.Text = trap.TriggerId;
        _payload.Text = trap.PayloadId;
        _detection.Value = trap.DetectionDifficulty;
        _disarm.Value = trap.DisarmDifficulty;
        _reset.Value = trap.ResetDelaySeconds;
        _owner.Text = trap.OwnerId;
        _visualEffect.Text = trap.VisualEffectId;
        _friendlyPolicy.Text = trap.FriendlyPolicy;
        _armed.ButtonPressed = trap.Armed;
        _oneShot.ButtonPressed = trap.OneShot;
        _gmVisualization.ButtonPressed = trap.GameMasterVisualization;
        _visualizationColor.Text = trap.VisualizationColor;
        _detectedBinding.Text = trap.DetectedBinding;
        _disarmedBinding.Text = trap.DisarmedBinding;
        _triggeredBinding.Text = trap.TriggeredBinding;
        _resetBinding.Text = trap.ResetBinding;

        var query = draft.TargetFilter;
        _playersOnly.ButtonPressed = query.PlayersOnly;
        _requiredTags.SetValues(query.RequiredTags);
        _excludedTags.SetValues(query.ExcludedTags);
        _requiredComponents.SetValues(query.RequiredComponents);
        _factions.Clear();
        foreach (var choice in _factionChoices)
        {
            _factions.AddItem(choice.Label);
        }
        for (var index = 0; index < _factionChoices.Count; index++)
        {
            if (query.FactionIds.Contains(_factionChoices[index].FactionId))
            {
                _factions.Select(index, single: false);
            }
        }
        var hiddenFactionCount = query.FactionIds.Count(id =>
            _factionChoices.All(choice => choice.FactionId != id));
        _factionHint.Text = hiddenFactionCount > 0
            ? $"{hiddenFactionCount} faction(s) archivée(s) ou externe(s) seront préservées sans être exposées."
            : "Aucune faction sélectionnée : toutes les factions sont acceptées.";
        _queryOwner.Text = query.OwnerId ?? string.Empty;
        _team.Text = query.TeamId ?? string.Empty;
        _predicate.Text = query.SandboxPredicate ?? string.Empty;

        foreach (var editor in _volumeEditors)
        {
            editor.Populate(draft.GameplayVolumes.Single(item => item.Kind == editor.Kind));
        }
        var debug = draft.OccupancyDebug;
        _debugEnabled.ButtonPressed = debug.Enabled;
        _showOccupants.ButtonPressed = debug.ShowOccupants;
        _showLastFires.ButtonPressed = debug.ShowLastFires;
        _showCooldowns.ButtonPressed = debug.ShowCooldowns;
        _showFilters.ButtonPressed = debug.ShowFilters;
        _showEventTrace.ButtonPressed = debug.ShowEventTrace;
        _maximumTrace.Value = debug.MaximumTraceEntries;
        _detectionEdited = false;
        _disarmEdited = false;
        _resetEdited = false;
        _maximumTraceEdited = false;
        _summary.Text =
            "Profil chargé · fondation, portail, validation et déclencheurs voisins préservés · aperçu à calculer.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _updating = false;
        InvalidatePreview();
    }

    private StudioTriggerAdvancedDraft ReadDraft()
    {
        var draft = _draft ?? throw new InvalidOperationException("Aucun déclencheur n’est sélectionné.");
        var knownFactionIds = new List<Guid>();
        for (var index = 0; index < _factionChoices.Count; index++)
        {
            if (_factions.IsSelected(index))
            {
                knownFactionIds.Add(_factionChoices[index].FactionId);
            }
        }
        var selectedKnown = knownFactionIds.ToHashSet();
        var preservedFactionIds = draft.TargetFilter.FactionIds
            .Where(id => _factionChoices.All(choice => choice.FactionId != id) || selectedKnown.Contains(id))
            .ToList();
        preservedFactionIds.AddRange(knownFactionIds.Where(id => !preservedFactionIds.Contains(id)));
        var factionIds = preservedFactionIds.ToArray();
        var editedVolumes = _volumeEditors.ToDictionary(
            editor => editor.Kind,
            editor => editor.Read(draft.GameplayVolumes.Single(item => item.Kind == editor.Kind)));
        var volumes = draft.GameplayVolumes.Select(item => editedVolumes[item.Kind]).ToArray();
        return draft with
        {
            ForgeScriptApiVersion = _api.Text,
            RequireRuntime = _requireRuntime.ButtonPressed,
            Trap = draft.Trap with
            {
                TriggerId = _triggerBinding.Text,
                PayloadId = _payload.Text,
                DetectionDifficulty = _detectionEdited ? _detection.Value : draft.Trap.DetectionDifficulty,
                DisarmDifficulty = _disarmEdited ? _disarm.Value : draft.Trap.DisarmDifficulty,
                ResetDelaySeconds = _resetEdited ? _reset.Value : draft.Trap.ResetDelaySeconds,
                OwnerId = _owner.Text,
                VisualEffectId = _visualEffect.Text,
                FriendlyPolicy = _friendlyPolicy.Text,
                Armed = _armed.ButtonPressed,
                OneShot = _oneShot.ButtonPressed,
                GameMasterVisualization = _gmVisualization.ButtonPressed,
                VisualizationColor = _visualizationColor.Text,
                DetectedBinding = _detectedBinding.Text,
                DisarmedBinding = _disarmedBinding.Text,
                TriggeredBinding = _triggeredBinding.Text,
                ResetBinding = _resetBinding.Text,
            },
            TargetFilter = draft.TargetFilter with
            {
                PlayersOnly = _playersOnly.ButtonPressed,
                RequiredTags = _requiredTags.Values,
                ExcludedTags = _excludedTags.Values,
                FactionIds = factionIds,
                RequiredComponents = _requiredComponents.Values,
                OwnerId = OptionalText(_queryOwner.Text, draft.TargetFilter.OwnerId),
                TeamId = OptionalText(_team.Text, draft.TargetFilter.TeamId),
                SandboxPredicate = OptionalText(_predicate.Text, draft.TargetFilter.SandboxPredicate),
            },
            GameplayVolumes = volumes,
            OccupancyDebug = draft.OccupancyDebug with
            {
                Enabled = _debugEnabled.ButtonPressed,
                ShowOccupants = _showOccupants.ButtonPressed,
                ShowLastFires = _showLastFires.ButtonPressed,
                ShowCooldowns = _showCooldowns.ButtonPressed,
                ShowFilters = _showFilters.ButtonPressed,
                ShowEventTrace = _showEventTrace.ButtonPressed,
                MaximumTraceEntries = _maximumTraceEdited
                    ? (int)_maximumTrace.Value
                    : draft.OccupancyDebug.MaximumTraceEntries,
            },
        };
    }

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Choisissez d’abord un déclencheur doté d’un volume de base.", error: true);
            return;
        }
        var result = _session.PreviewTriggerAdvancedUx1AC(ReadDraft());
        _apply.Disabled = !result.CanApply;
        _summary.Text = result.Succeeded ? result.Summary : result.Message;
        _summary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedTriggerAdvancedUx1AC();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        _applied = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        SetEditingEnabled(false);
        TriggerChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = true;
            _redo.Disabled = false;
            TriggerChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _undo.Disabled = false;
            _redo.Disabled = true;
            TriggerChanged?.Invoke();
        }
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _trapPanel.Visible = _section == 0;
        _filterPanel.Visible = _section == 1;
        _volumesPanel.Visible = _section == 2;
        ApplyTabStyle(_trapTab, _section == 0);
        ApplyTabStyle(_filterTab, _section == 1);
        ApplyTabStyle(_volumesTab, _section == 2);
    }

    private void ConnectInvalidation()
    {
        foreach (var edit in new[]
                 {
                     _api, _triggerBinding, _payload, _owner, _visualEffect, _friendlyPolicy,
                     _visualizationColor, _detectedBinding, _disarmedBinding, _triggeredBinding,
                     _resetBinding, _queryOwner, _team, _predicate,
                 })
        {
            edit.TextChanged += _ => InvalidatePreview();
        }
        _detection.ValueChanged += _ =>
        {
            if (!_updating) _detectionEdited = true;
            InvalidatePreview();
        };
        _disarm.ValueChanged += _ =>
        {
            if (!_updating) _disarmEdited = true;
            InvalidatePreview();
        };
        _reset.ValueChanged += _ =>
        {
            if (!_updating) _resetEdited = true;
            InvalidatePreview();
        };
        _maximumTrace.ValueChanged += _ =>
        {
            if (!_updating) _maximumTraceEdited = true;
            InvalidatePreview();
        };
        foreach (var check in new[]
                 {
                     _requireRuntime, _armed, _oneShot, _gmVisualization, _playersOnly,
                     _debugEnabled, _showOccupants, _showLastFires, _showCooldowns,
                     _showFilters, _showEventTrace,
                 })
        {
            check.Toggled += _ => InvalidatePreview();
        }
        _requiredTags.Changed += InvalidatePreview;
        _excludedTags.Changed += InvalidatePreview;
        _requiredComponents.Changed += InvalidatePreview;
        _factions.MultiSelected += (_, _) => InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null || _applied)
        {
            return;
        }
        _session.CancelTriggerAdvancedUx1AC();
        _apply.Disabled = true;
        _preview.Disabled = false;
        _summary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetEditingEnabled(bool enabled)
    {
        foreach (var edit in new[]
                 {
                     _api, _triggerBinding, _payload, _owner, _visualEffect, _friendlyPolicy,
                     _visualizationColor, _detectedBinding, _disarmedBinding, _triggeredBinding,
                     _resetBinding, _queryOwner, _team, _predicate,
                 })
        {
            edit.Editable = enabled;
        }
        foreach (var spin in new[] { _detection, _disarm, _reset, _maximumTrace })
        {
            spin.Editable = enabled;
        }
        foreach (var check in new[]
                 {
                     _requireRuntime, _armed, _oneShot, _gmVisualization, _playersOnly,
                     _debugEnabled, _showOccupants, _showLastFires, _showCooldowns,
                     _showFilters, _showEventTrace,
                 })
        {
            check.Disabled = !enabled;
        }
        _requiredTags.SetEditingEnabled(enabled);
        _excludedTags.SetEditingEnabled(enabled);
        _requiredComponents.SetEditingEnabled(enabled);
        _factions.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        foreach (var editor in _volumeEditors)
        {
            editor.SetEditingEnabled(enabled);
        }
        _trapTab.Disabled = !enabled;
        _filterTab.Disabled = !enabled;
        _volumesTab.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled)
        {
            _apply.Disabled = true;
        }
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
        var width = Math.Clamp((Size.X - 56) / scale, 940, 1220);
        var height = Math.Clamp((Size.Y - 72) / scale, 700, 880);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 8);
        return panel;
    }

    private static Button Tab(string text)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
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
        title.CustomMinimumSize = new Vector2(180, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static LineEdit Text(string placeholder)
    {
        var value = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(value);
        return value;
    }

    private static SpinBox Number(
        double min,
        double max,
        double step,
        string suffix = "",
        bool allowGreater = false)
    {
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Suffix = suffix,
            AllowGreater = allowGreater,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(value);
        return value;
    }

    private static CheckBox Check(string text)
    {
        var value = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        value.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        return value;
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? OptionalText(string value, string? original) =>
        string.Equals(value, original, StringComparison.Ordinal)
            ? original
            : EmptyToNull(value);
}
