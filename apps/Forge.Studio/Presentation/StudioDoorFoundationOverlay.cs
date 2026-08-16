using Forge.Domain.WorldFeatures;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDoorFoundationOverlay : Control
{
    private static readonly DoorWorldProfile.PlacementMode[] PlacementModes =
    [
        DoorWorldProfile.PlacementMode.FreePlacement,
        DoorWorldProfile.PlacementMode.ArchitectureSocket,
    ];

    private static readonly DoorWorldProfile.StateKind[] InitialStates =
    [
        DoorWorldProfile.StateKind.Closed,
        DoorWorldProfile.StateKind.Open,
        DoorWorldProfile.StateKind.Locked,
    ];

    private static readonly string[] Sides = ["both", "front", "back"];
    private static readonly string[] Authorities = ["server", "local"];
    private static readonly string[] KnownFailureFeedback = ["door.unavailable", "door.blocked", "door.busy"];

    private readonly StudioSessionController _session;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _door;
    private readonly Button _placementTab;
    private readonly Button _statesTab;
    private readonly Button _interactionTab;
    private readonly VBoxContainer _placementPanel;
    private readonly VBoxContainer _statesPanel;
    private readonly VBoxContainer _interactionPanel;
    private readonly OptionButton _placement;
    private readonly LineEdit _architectureAnchor;
    private readonly LineEdit _architectureKit;
    private readonly LineEdit _openingKind;
    private readonly SpinBox _widthAdjustment;
    private readonly SpinBox _heightAdjustment;
    private readonly SpinBox _depthAdjustment;
    private readonly Label _placementHint;
    private readonly OptionButton _initialState;
    private readonly Label _stateHint;
    private readonly SpinBox _range;
    private readonly OptionButton _side;
    private readonly SpinBox _clearanceWidth;
    private readonly SpinBox _clearanceHeight;
    private readonly SpinBox _clearanceDepth;
    private readonly OptionButton _authority;
    private readonly SpinBox _queue;
    private readonly OptionButton _failureFeedback;
    private readonly Label _previewSummary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Label _status;
    private IReadOnlyList<StudioDoorFoundationChoice> _choices = [];
    private readonly List<string> _feedbackKeys = [];
    private StudioDoorFoundationDraft? _draft;
    private StudioDoorFoundationDraft? _approvedDraft;
    private bool _updating;
    private bool _operationApplied;
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
                _placementTab.Text,
                _statesTab.Text,
                _interactionTab.Text,
                _placementHint.Text,
                _stateHint.Text,
                _previewSummary.Text,
                _status.Text);
            return SectionCount == 3 &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("portal", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("navigation", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioDoorFoundationOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioDoorFoundationOverlay";
        Visible = false;
        ZIndex = 447;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("DoorFoundationDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("FONDATION DE PORTE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Placement, états et interaction d’ouverture", 20));
        heading.AddChild(StudioStyle.Label(
            "Choisissez une porte · prévisualisez sans écriture · appliquez par commande annulable",
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
        _door.TooltipText = "Sélection explicite du blueprint de porte à modifier.";
        _door.ItemSelected += _ => LoadSelectedDoor();
        sourceRow.AddChild(_door);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _placementTab = TabButton("1 · Placement", "Choisir un placement libre ou un ancrage d’architecture.");
        _placementTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_placementTab);
        _statesTab = TabButton("2 · États", "Choisir l’état initial en conservant la machine complète.");
        _statesTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_statesTab);
        _interactionTab = TabButton("3 · Ouverture", "Configurer l’interaction d’ouverture et son retour humain.");
        _interactionTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_interactionTab);

        var body = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _placementPanel = PanelColumn();
        panels.AddChild(_placementPanel);
        _placementPanel.AddChild(StudioStyle.Label("Installer la porte", 17));
        _placementPanel.AddChild(StudioStyle.Label(
            "Le placement libre reste manipulable dans la vue 3D. L’ancrage aligne la porte sur une ouverture fournie par un kit d’architecture.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var placementGrid = Grid();
        _placementPanel.AddChild(placementGrid);
        _placement = AddField(placementGrid, "Mode", Option("Placement libre", "Ancrage d’architecture"));
        _architectureAnchor = AddField(
            placementGrid,
            "Emplacement d’architecture",
            Text("Ex. mur-porte-standard"));
        _architectureKit = AddField(
            placementGrid,
            "Kit d’architecture",
            Text("Ex. kit-architecture-standard"));
        _openingKind = AddField(
            placementGrid,
            "Type d’ouverture",
            Text("Ex. ouverture-standard"));
        _widthAdjustment = AddField(placementGrid, "Ajustement largeur", OffsetBox());
        _heightAdjustment = AddField(placementGrid, "Ajustement hauteur", OffsetBox());
        _depthAdjustment = AddField(placementGrid, "Ajustement profondeur", OffsetBox());
        _placementHint = StudioStyle.Label(string.Empty, 11, StudioStyle.Success, wrap: true);
        _placementPanel.AddChild(_placementHint);

        _statesPanel = PanelColumn();
        panels.AddChild(_statesPanel);
        _statesPanel.AddChild(StudioStyle.Label("Choisir l’état au démarrage", 17));
        _statesPanel.AddChild(StudioStyle.Label(
            "Ouverte, fermée ou verrouillée sont les choix courants. Les états intermédiaires et de repli restent conservés automatiquement.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var statesGrid = Grid();
        _statesPanel.AddChild(statesGrid);
        _initialState = AddField(statesGrid, "État initial", Option("Fermée", "Ouverte", "Verrouillée"));
        _stateHint = StudioStyle.Label(
            "Machine complète · ouverte · fermée · ouverture · fermeture · verrouillée · bloquée · brisée · désactivée · personnalisée",
            11,
            StudioStyle.Success,
            wrap: true);
        _statesPanel.AddChild(_stateHint);

        _interactionPanel = PanelColumn();
        panels.AddChild(_interactionPanel);
        _interactionPanel.AddChild(StudioStyle.Label("Régler l’ouverture par le joueur", 17));
        _interactionPanel.AddChild(StudioStyle.Label(
            "Portée, côté d’approche, passage disponible, file de demandes, autorité et message d’échec sont validés ensemble.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var interactionGrid = Grid();
        _interactionPanel.AddChild(interactionGrid);
        _range = AddField(interactionGrid, "Portée", Number(.1, 100, .05, " m"));
        _side = AddField(interactionGrid, "Côté autorisé", Option("Deux côtés", "Avant", "Arrière"));
        _clearanceWidth = AddField(interactionGrid, "Largeur du passage", Number(.1, 100, .05, " m"));
        _clearanceHeight = AddField(interactionGrid, "Hauteur du passage", Number(.1, 100, .05, " m"));
        _clearanceDepth = AddField(interactionGrid, "Profondeur du passage", Number(.1, 100, .05, " m"));
        _authority = AddField(
            interactionGrid,
            "Décision d’ouverture",
            Option("Serveur autoritaire", "Session locale"));
        _queue = AddField(interactionGrid, "Demandes en attente", Number(1, 128, 1, " demandes"));
        _failureFeedback = AddField(
            interactionGrid,
            "Message en cas d’échec",
            Option("Porte indisponible", "Passage bloqué", "Porte occupée"));

        var previewCard = StudioStyle.Panel("DoorFoundationPreview", StudioStyle.SurfaceRaised, 8);
        previewCard.CustomMinimumSize = new Vector2(0, 142);
        root.AddChild(previewCard);
        var previewMargin = StudioStyle.Margin(14, 11, 14, 11);
        previewCard.AddChild(previewMargin);
        var previewColumn = new VBoxContainer();
        previewColumn.AddThemeConstantOverride("separation", 7);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.SectionLabel("APERÇU DE LA PORTE"));
        _previewSummary = StudioStyle.Label(
            "Prévisualisez pour contrôler le placement, l’état initial et l’ouverture.",
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
        _session.CancelDoorFoundationPreviewUx1X();
        _choices = _session.DoorFoundationChoicesUx1X;
        _updating = true;
        _door.Clear();
        _door.AddItem("Choisir une porte…");
        foreach (var choice in _choices)
        {
            _door.AddItem($"{choice.Label} · {(choice.Configured ? "configurée" : "à configurer")}");
        }
        _door.Selected = 0;
        _updating = false;
        _operationApplied = false;
        _selectionRequiredAtOpen = true;
        _draft = null;
        _approvedDraft = null;
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
        _session.CancelDoorFoundationPreviewUx1X();
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

        _updating = true;
        _placement.Selected = Array.IndexOf(PlacementModes, DoorWorldProfile.PlacementMode.ArchitectureSocket);
        _architectureAnchor.Text = "mur-grande-porte";
        _architectureKit.Text = "kit-observatoire";
        _openingKind.Text = "double-battant";
        _widthAdjustment.Value = .15;
        _heightAdjustment.Value = .2;
        _depthAdjustment.Value = -.05;
        _initialState.Selected = Array.IndexOf(InitialStates, DoorWorldProfile.StateKind.Closed);
        _range.Value = 3.25;
        _side.Selected = Array.IndexOf(Sides, "front");
        _clearanceWidth.Value = 1.8;
        _clearanceHeight.Value = 2.8;
        _clearanceDepth.Value = 1.25;
        _authority.Selected = Array.IndexOf(Authorities, "server");
        _queue.Value = 3;
        SelectFeedback("door.blocked");
        _updating = false;
        UpdatePlacementFields();
        ShowSection(mode.EndsWith("placement", StringComparison.OrdinalIgnoreCase)
            ? 0
            : mode.EndsWith("states", StringComparison.OrdinalIgnoreCase)
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
        _updating = true;
        _placement.Selected = Array.IndexOf(PlacementModes, DoorWorldProfile.PlacementMode.ArchitectureSocket);
        _architectureAnchor.Text = "mur-preuve-porte";
        _architectureKit.Text = "kit-preuve";
        _openingKind.Text = "porte-standard";
        _initialState.Selected = Array.IndexOf(InitialStates, DoorWorldProfile.StateKind.Closed);
        _range.Value = 2.75;
        _side.Selected = Array.IndexOf(Sides, "both");
        _clearanceWidth.Value = 1.4;
        _clearanceHeight.Value = 2.5;
        _clearanceDepth.Value = 1.1;
        _authority.Selected = Array.IndexOf(Authorities, "server");
        _queue.Value = 4;
        SelectFeedback("door.unavailable");
        _updating = false;
        UpdatePlacementFields();
        Preview();
        var previewed = PreviewReady && ShowsHumanSurface;
        Apply();
        var appliedProfile = _session.Project?.Module.WorldFeatures.Doors.SingleOrDefault(profile =>
            profile.BlueprintId == blueprintId);
        var placementApplied = appliedProfile?.Foundation is
                      {
                          Placement: DoorWorldProfile.PlacementMode.ArchitectureSocket,
                          ArchitectureSocketId: "mur-preuve-porte",
                          Interaction:
                          {
                              Range: 2.75,
                              AllowedSide: "both",
                              Authority: "server",
                              QueueCapacity: 4,
                              FailureFeedbackKey: "door.unavailable",
                          },
                      };
        var statesApplied = appliedProfile?.Foundation is not null &&
                            Enum.GetValues<DoorWorldProfile.StateKind>().All(kind =>
                                appliedProfile.Foundation.States.Any(state => state.Kind == kind));
        var historyReady = !_undo.Disabled;
        var applied = placementApplied && statesApplied && historyReady;
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
                $"OPENRPG_FORGE_STUDIO_UX1X_UI_TRANSACTION_FAILED selection_required={selectionRequired.ToString().ToLowerInvariant()} " +
                $"explicit_selection={explicitSelection.ToString().ToLowerInvariant()} preview={previewed.ToString().ToLowerInvariant()} " +
                $"applied={applied.ToString().ToLowerInvariant()} placement={placementApplied.ToString().ToLowerInvariant()} " +
                $"states={statesApplied.ToString().ToLowerInvariant()} history_ready={historyReady.ToString().ToLowerInvariant()} " +
                $"undone={undone.ToString().ToLowerInvariant()} " +
                $"redone={redone.ToString().ToLowerInvariant()} restored={restored.ToString().ToLowerInvariant()}");
        }
        return valid;
    }

    private void LoadSelectedDoor()
    {
        if (_updating || _choices.Count == 0)
        {
            return;
        }

        if (_door.Selected <= 0)
        {
            _session.CancelDoorFoundationPreviewUx1X();
            _draft = null;
            _approvedDraft = null;
            SetEditingEnabled(false);
            _door.Disabled = false;
            _preview.Disabled = true;
            _apply.Disabled = true;
            _previewSummary.Text = "Choisissez explicitement une porte avant de préparer un aperçu.";
            _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
            SetStatus("Choisissez une porte · aucun brouillon n’est ouvert automatiquement.", false);
            return;
        }

        _session.CancelDoorFoundationPreviewUx1X();
        _approvedDraft = null;
        var index = Math.Clamp(_door.Selected - 1, 0, _choices.Count - 1);
        _draft = _session.CreateDoorFoundationDraftUx1X(_choices[index].BlueprintId);
        if (_draft is null)
        {
            SetStatus("Cette porte ne peut pas être ouverte dans l’éditeur tant que sa fiche est ambiguë.", true);
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
        _placement.Selected = Math.Max(0, Array.IndexOf(PlacementModes, draft.Placement));
        _architectureAnchor.Text = draft.ArchitectureAnchor;
        _architectureKit.Text = draft.ArchitectureKit;
        _openingKind.Text = draft.OpeningKind;
        ExpandLimits(_widthAdjustment, draft.WidthAdjustment);
        ExpandLimits(_heightAdjustment, draft.HeightAdjustment);
        ExpandLimits(_depthAdjustment, draft.DepthAdjustment);
        ExpandLimits(_range, draft.InteractionRange);
        ExpandLimits(_clearanceWidth, draft.ClearanceWidth);
        ExpandLimits(_clearanceHeight, draft.ClearanceHeight);
        ExpandLimits(_clearanceDepth, draft.ClearanceDepth);
        ExpandLimits(_queue, draft.QueueCapacity);
        _widthAdjustment.Value = draft.WidthAdjustment;
        _heightAdjustment.Value = draft.HeightAdjustment;
        _depthAdjustment.Value = draft.DepthAdjustment;
        _initialState.Selected = Math.Max(0, Array.IndexOf(InitialStates, draft.InitialState));
        _range.Value = draft.InteractionRange;
        _side.Selected = Math.Max(0, Array.IndexOf(Sides, draft.AllowedSide));
        _clearanceWidth.Value = draft.ClearanceWidth;
        _clearanceHeight.Value = draft.ClearanceHeight;
        _clearanceDepth.Value = draft.ClearanceDepth;
        _authority.Selected = Math.Max(0, Array.IndexOf(Authorities, draft.Authority));
        _queue.Value = draft.QueueCapacity;
        LoadFeedbackOptions(draft.FailureFeedbackKey);
        _updating = false;
        _operationApplied = false;
        _undo.Disabled = true;
        _redo.Disabled = true;
        UpdatePlacementFields();
        InvalidatePreview();
    }

    private StudioDoorFoundationDraft ReadDraft()
    {
        var placementIndex = Math.Clamp(_placement.Selected, 0, PlacementModes.Length - 1);
        var stateIndex = Math.Clamp(_initialState.Selected, 0, InitialStates.Length - 1);
        var sideIndex = Math.Clamp(_side.Selected, 0, Sides.Length - 1);
        var authorityIndex = Math.Clamp(_authority.Selected, 0, Authorities.Length - 1);
        var feedbackIndex = Math.Clamp(_failureFeedback.Selected, 0, _feedbackKeys.Count - 1);
        return _draft! with
        {
            Placement = PlacementModes[placementIndex],
            ArchitectureAnchor = _architectureAnchor.Text,
            ArchitectureKit = _architectureKit.Text,
            OpeningKind = _openingKind.Text,
            WidthAdjustment = _widthAdjustment.Value,
            HeightAdjustment = _heightAdjustment.Value,
            DepthAdjustment = _depthAdjustment.Value,
            InitialState = InitialStates[stateIndex],
            InteractionRange = _range.Value,
            AllowedSide = Sides[sideIndex],
            ClearanceWidth = _clearanceWidth.Value,
            ClearanceHeight = _clearanceHeight.Value,
            ClearanceDepth = _clearanceDepth.Value,
            Authority = Authorities[authorityIndex],
            QueueCapacity = (int)_queue.Value,
            FailureFeedbackKey = _feedbackKeys[feedbackIndex],
        };
    }

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Sélectionnez une porte avant de lancer l’aperçu.", true);
            return;
        }

        var candidate = ReadDraft();
        var result = _session.PreviewDoorFoundationUx1X(candidate);
        _previewSummary.Text = result.Succeeded
            ? result.Summary
            : "Aucun aperçu validé · corrigez les champs signalés puis recommencez.";
        _previewSummary.AddThemeColorOverride("font_color", result.Succeeded ? StudioStyle.Text : StudioStyle.Danger);
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

        var result = _session.ApplyApprovedDoorFoundationUx1X();
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

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _placementPanel.Visible = _section == 0;
        _statesPanel.Visible = _section == 1;
        _interactionPanel.Visible = _section == 2;
        ApplyTabStyle(_placementTab, _section == 0);
        ApplyTabStyle(_statesTab, _section == 1);
        ApplyTabStyle(_interactionTab, _section == 2);
    }

    private void UpdatePlacementFields()
    {
        var socket = _placement.Selected == Array.IndexOf(
            PlacementModes,
            DoorWorldProfile.PlacementMode.ArchitectureSocket);
        _architectureAnchor.Editable = socket && !_operationApplied;
        _architectureKit.Editable = socket && !_operationApplied;
        _openingKind.Editable = socket && !_operationApplied;
        _widthAdjustment.Editable = socket && !_operationApplied;
        _heightAdjustment.Editable = socket && !_operationApplied;
        _depthAdjustment.Editable = socket && !_operationApplied;
        _placementHint.Text = socket
            ? "Ancrage actif · l’emplacement, le kit, le type d’ouverture et les trois ajustements seront validés."
            : "Placement libre · positionnez ensuite la porte directement dans la vue 3D.";
    }

    private void ConnectInvalidation()
    {
        _placement.ItemSelected += _ =>
        {
            UpdatePlacementFields();
            InvalidatePreview();
        };
        foreach (var option in new[] { _initialState, _side, _authority, _failureFeedback })
        {
            option.ItemSelected += _ => InvalidatePreview();
        }
        foreach (var edit in new[] { _architectureAnchor, _architectureKit, _openingKind })
        {
            edit.TextChanged += _ => InvalidatePreview();
        }
        foreach (var spin in new[]
                 {
                     _widthAdjustment,
                     _heightAdjustment,
                     _depthAdjustment,
                     _range,
                     _clearanceWidth,
                     _clearanceHeight,
                     _clearanceDepth,
                     _queue,
                 })
        {
            spin.ValueChanged += _ => InvalidatePreview();
        }
    }

    private void InvalidatePreview()
    {
        if (_updating || _operationApplied)
        {
            return;
        }

        _approvedDraft = null;
        _session.CancelDoorFoundationPreviewUx1X();
        _apply.Disabled = true;
        _preview.Disabled = false;
        _previewSummary.Text = "Aperçu à recalculer · aucun changement appliqué.";
        _previewSummary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _door.Disabled = !enabled;
        _placement.Disabled = !enabled;
        _initialState.Disabled = !enabled;
        _side.Disabled = !enabled;
        _authority.Disabled = !enabled;
        _failureFeedback.Disabled = !enabled;
        foreach (var edit in new[] { _architectureAnchor, _architectureKit, _openingKind })
        {
            edit.Editable = enabled;
        }
        foreach (var spin in new[]
                 {
                     _widthAdjustment,
                     _heightAdjustment,
                     _depthAdjustment,
                     _range,
                     _clearanceWidth,
                     _clearanceHeight,
                     _clearanceDepth,
                     _queue,
                 })
        {
            spin.Editable = enabled;
        }
        _placementTab.Disabled = !enabled;
        _statesTab.Disabled = !enabled;
        _interactionTab.Disabled = !enabled;
        if (enabled)
        {
            UpdatePlacementFields();
        }
    }

    private void LoadFeedbackOptions(string current)
    {
        _feedbackKeys.Clear();
        _feedbackKeys.AddRange(KnownFailureFeedback);
        if (!_feedbackKeys.Contains(current, StringComparer.Ordinal))
        {
            _feedbackKeys.Add(current);
        }

        _failureFeedback.Clear();
        foreach (var key in _feedbackKeys)
        {
            _failureFeedback.AddItem(FeedbackLabel(key));
        }
        SelectFeedback(current);
    }

    private void SelectFeedback(string key)
    {
        var index = _feedbackKeys.IndexOf(key);
        _failureFeedback.Selected = index >= 0 ? index : 0;
    }

    private static string FeedbackLabel(string key) => key switch
    {
        "door.blocked" => "Passage bloqué",
        "door.busy" => "Porte occupée",
        "door.unavailable" => "Porte indisponible",
        _ => "Message existant · conservé",
    };

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.72f : Size.X >= 2300 ? 1.24f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 850, 1120);
        var height = Math.Clamp((Size.Y - 72) / scale, 670, 840);
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

    private static OptionButton Option(params string[] labels)
    {
        var option = new OptionButton();
        foreach (var label in labels)
        {
            option.AddItem(label);
        }
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

    private static SpinBox Number(double minimum, double maximum, double step, string suffix)
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

    private static SpinBox OffsetBox() => Number(-10, 10, .05, " m");

    private static void ExpandLimits(SpinBox spin, double value)
    {
        if (value < spin.MinValue)
        {
            spin.MinValue = value;
        }
        if (value > spin.MaxValue)
        {
            spin.MaxValue = value;
        }
    }
}
