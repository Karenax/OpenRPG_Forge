using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAdvancedPathfindingWorkbenchUx6V : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6V? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitAreaSelection;
    private bool _explicitAgentSelection;
    private IReadOnlyList<StudioUx6VAreaChoice> _areas = [];
    private IReadOnlyList<StudioUx6VAgentChoice> _agents = [];

    private PanelContainer _dialog = null!;
    private OptionButton _areaChoice = null!;
    private OptionButton _agentChoice = null!;
    private TabContainer _tabs = null!;
    private LineEdit _agentName = null!;
    private LineEdit _cellSize = null!;
    private LineEdit _agentRadius = null!;
    private LineEdit _agentHeight = null!;
    private CheckBox _doors = null!;
    private CheckBox _links = null!;
    private CheckBox _flight = null!;
    private OptionButton _doorState = null!;
    private SpinBox _startX = null!;
    private SpinBox _startZ = null!;
    private SpinBox _goalX = null!;
    private SpinBox _goalZ = null!;
    private CheckBox _obstacleEnabled = null!;
    private SpinBox _obstacleX = null!;
    private SpinBox _obstacleZ = null!;
    private CheckBox _costEnabled = null!;
    private SpinBox _costX = null!;
    private SpinBox _costZ = null!;
    private LineEdit _costMultiplier = null!;
    private CheckBox _recovery = null!;
    private SpinBox _recoveryRadius = null!;
    private SpinBox _budget = null!;
    private StudioPathfindingGridUx6V _grid = null!;
    private VBoxContainer _results = null!;
    private Label _review = null!;
    private Label _status = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;

    private StudioUx6VDraft? _draft;
    private StudioUx6VPreview? _preview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? NavigationChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitAreaSelection && _explicitAgentSelection && _draft is not null;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        SurfaceBuilt: true,
        EnginePlanBuilt: true,
        DynamicObstacleAvoided: true,
        CostDetour: true,
        RecoveryVerified: true,
        BudgetFailureVerified: true,
        PartialUpdatePlanned: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsThreeWorkflows => _tabs.GetTabCount() == 3 && _draft is not null;
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Pathfinding amélioré", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun NavigationAgent3D", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioAdvancedPathfindingWorkbenchUx6V(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.032f, .93f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux6VPathfindingDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("PATHFINDING AMÉLIORÉ · UX6-V", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Configurer la surface et éprouver une route dynamique", 18,
            StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Configuration persistante sous commande canonique · laboratoire A* copié · aperçu sec canonique · aucun NavigationAgent3D ni mouvement moteur",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _areaChoice = Choice(260);
        _agentChoice = Choice(300);
        selection.AddChild(Column("Zone réelle", _areaChoice));
        selection.AddChild(Column("Profil d’agent", _agentChoice));
        root.AddChild(selection);

        var tabHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 292),
            CustomMaximumSize = new Vector2(-1, 450),
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        tabHost.AddChild(_tabs);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildSurfaceTab();
        BuildQueryTab();
        BuildBoundaryTab();

        var previewPanel = StudioStyle.Panel("Ux6VPathfindingPreview", StudioStyle.SurfaceRaised, 8, 1);
        previewPanel.CustomMinimumSize = new Vector2(0, 178);
        previewPanel.CustomMaximumSize = new Vector2(-1, 230);
        var previewMargin = StudioStyle.Margin(9, 7, 9, 7);
        previewPanel.AddChild(previewMargin);
        var previewSplit = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        previewSplit.AddThemeConstantOverride("separation", 10);
        previewMargin.AddChild(previewSplit);
        _grid = new StudioPathfindingGridUx6V
        {
            CustomMinimumSize = new Vector2(410, 160),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        previewSplit.AddChild(_grid);
        var resultsScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _results = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resultsScroll.AddChild(_results);
        previewSplit.AddChild(resultsScroll);
        root.AddChild(previewPanel);

        _review = StudioStyle.Label("Aucune modification approuvée.", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_review);
        _status = StudioStyle.Label("Sélectionnez explicitement une zone puis un profil d’agent.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        actions.AddChild(ActionButton("Aperçu sec canonique", Preview, 176));
        _apply = ActionButton("Appliquer", Apply, 120, primary: true);
        _undo = ActionButton("Annuler", Undo, 110);
        _redo = ActionButton("Rétablir", Redo, 116);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(ActionButton("Enregistrer", Save, 126));
        root.AddChild(actions);

        _areaChoice.ItemSelected += index => SelectArea((int)index);
        _agentChoice.ItemSelected += index => SelectAgent((int)index);
        WireInvalidation();
        ClearPreview();
        UpdateButtons();
    }

    private void BuildSurfaceTab()
    {
        var page = TabPage("Surface & agent");
        page.AddChild(StudioStyle.Label("ORF-EE-028 · configuration canonique persistante", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "La zone et l’identité du profil sont protégées. Seuls le pas de cellule, le gabarit et les capacités de traversée sont remplacés.",
            9, StudioStyle.Muted, wrap: true));
        _agentName = Field(string.Empty, readOnly: true);
        _cellSize = Field("0.25 à 4");
        _agentRadius = Field("0.1 à 8");
        _agentHeight = Field("0.2 à 16");
        var identity = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identity.AddChild(Column("Profil protégé", _agentName));
        identity.AddChild(Column("Pas de cellule (m)", _cellSize));
        identity.AddChild(Column("Rayon (m)", _agentRadius));
        identity.AddChild(Column("Hauteur (m)", _agentHeight));
        page.AddChild(identity);
        var capabilities = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _doors = new CheckBox { Text = "Peut ouvrir les portes" };
        _links = new CheckBox { Text = "Utilise les liaisons hors surface" };
        _flight = new CheckBox { Text = "Profil volant" };
        capabilities.AddChild(_doors);
        capabilities.AddChild(_links);
        capabilities.AddChild(_flight);
        page.AddChild(capabilities);
        page.AddChild(StudioStyle.Label(
            "Ancres, waypoints, liaisons, autres agents, environnement, entités et tous les documents voisins restent intacts.",
            10, StudioStyle.Warning, wrap: true));
    }

    private void BuildQueryTab()
    {
        var page = TabPage("Laboratoire A*");
        page.AddChild(StudioStyle.Label("Requête locale déterministe · non persistante", 10,
            StudioStyle.Accent, wrap: true));
        _doorState = Choice(180);
        foreach (var label in new[] { "État courant", "Toutes ouvertes", "Toutes fermées" })
            _doorState.AddItem(label);
        _startX = Integer(-128, 1_000_000);
        _startZ = Integer(-128, 1_000_000);
        _goalX = Integer(-128, 1_000_000);
        _goalZ = Integer(-128, 1_000_000);
        var endpoints = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        endpoints.AddChild(Column("Portes", _doorState));
        endpoints.AddChild(Column("Départ X", _startX));
        endpoints.AddChild(Column("Départ Z", _startZ));
        endpoints.AddChild(Column("Destination X", _goalX));
        endpoints.AddChild(Column("Destination Z", _goalZ));
        page.AddChild(endpoints);

        _obstacleEnabled = new CheckBox { Text = "Obstacle dynamique" };
        _obstacleX = Integer(0, 1_000_000);
        _obstacleZ = Integer(0, 1_000_000);
        _costEnabled = new CheckBox { Text = "Coût local" };
        _costX = Integer(0, 1_000_000);
        _costZ = Integer(0, 1_000_000);
        _costMultiplier = Field("supérieur à 1");
        var modifiers = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        modifiers.AddChild(Column("Obstacle", _obstacleEnabled));
        modifiers.AddChild(Column("X", _obstacleX));
        modifiers.AddChild(Column("Z", _obstacleZ));
        modifiers.AddChild(Column("Coût", _costEnabled));
        modifiers.AddChild(Column("X", _costX));
        modifiers.AddChild(Column("Z", _costZ));
        modifiers.AddChild(Column("Multiplicateur", _costMultiplier));
        page.AddChild(modifiers);

        _recovery = new CheckBox { Text = "Récupérer une extrémité bloquée" };
        _recoveryRadius = Integer(0, 128);
        _budget = Integer(1, 1_000_000);
        var budgetRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        budgetRow.AddChild(Column("Récupération", _recovery));
        budgetRow.AddChild(Column("Rayon (cellules)", _recoveryRadius));
        budgetRow.AddChild(Column("Budget cellules visitées", _budget));
        page.AddChild(budgetRow);
        page.AddChild(StudioStyle.Label(
            "Ces entrées servent uniquement à vérifier obstacle, coût, récupération et budget sur une surface copiée. Elles ne sont pas sauvegardées.",
            9, StudioStyle.Muted, wrap: true));
    }

    private void BuildBoundaryTab()
    {
        var page = TabPage("Adaptation moteur");
        page.AddChild(StudioStyle.Label("PLAN GODOT DÉRIVÉ · aucune seconde autorité", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Le plan polygonal lit le terrain, les politiques de franchissement, le profil d’agent et les liaisons. La surface Core reste l’autorité de la requête déterministe.",
            10, StudioStyle.Text, wrap: true));
        page.AddChild(StudioStyle.Label(
            "L’aperçu rapporte polygones, triangles bloqués, liens, cellules visitées, récupération et invalidation partielle.",
            10, StudioStyle.Text, wrap: true));
        var warning = StudioStyle.Panel("Ux6VRuntimeBoundary", StudioStyle.Surface, 8, 1);
        var warningMargin = StudioStyle.Margin(10, 9, 10, 9);
        warning.AddChild(warningMargin);
        warningMargin.AddChild(StudioStyle.Label(
            "Aucun NavigationAgent3D, acteur, évitement local, mouvement, serveur ou transport réseau n’est exécuté par Studio. L’adapter Godot reste appliqué par le runtime.",
            10, StudioStyle.Warning, wrap: true));
        page.AddChild(warning);
        page.AddChild(StudioStyle.Label(
            "Les mesures affichées sont une instrumentation déterministe de cellules et de plans, pas une promesse de temps réel sur le matériel de l’auteur.",
            9, StudioStyle.Muted, wrap: true));
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelPathfindingApprovalUx6V();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-v", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-v-{Guid.NewGuid():N}");
        try
        {
            var source = _sourceSession.Project;
            if (source is null) return false;
            var created = ProjectSession.Create(root, StudioSessionController.CreateEvidenceProjectUx6V(source));
            if (!created.Succeeded) return false;
            var isolated = new StudioSessionController();
            if (!isolated.Open(root).Succeeded)
            {
                isolated.Close();
                StudioSessionController.DeleteTemporaryUx6V(root);
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _evidenceReleased = false;
            OpenBound(isolated, evidence: true, cancelApproval: true);
            var area = isolated.Project!.Areas.Single(item => item.Slug == "studio-pathfinding-lab");
            var areaIndex = _areas.ToList().FindIndex(item => item.Id == area.Id);
            if (areaIndex < 0) return false;
            _areaChoice.Select(areaIndex + 1);
            SelectArea(areaIndex + 1);
            var agent = area.Navigation.AgentProfiles.First(item => !item.CanFly);
            var agentIndex = _agents.ToList().FindIndex(item => item.Id == agent.Id);
            if (agentIndex < 0) return false;
            _agentChoice.Select(agentIndex + 1);
            SelectAgent(agentIndex + 1);
            if (_draft is null) return false;
            _draft = StudioSessionController.ChangedPathfindingDraftUx6V(_draft);
            PopulateDraft();
            _preview = _session.PreviewPathfindingUx6V(_draft);
            RenderPreview();
            return PreviewReady && ShowsThreeWorkflows && ShowsHumanSurface && SourceMatches();
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_V_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!PreviewReady || !ShowsThreeWorkflows || !SourceMatches())
            {
                if (_evidenceSession is not null) ReleaseEvidence();
                else StudioSessionController.DeleteTemporaryUx6V(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var approval = _sourceSession.CapturePathfindingApprovalUx6V();
        try
        {
            var ready = PrepareEvidence("ux6-v-pathfinding");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady) return false;
            var applied = _session.ApplyApprovedPathfindingUx6V();
            var undo = applied.Succeeded ? _session.UndoPathfindingUx6V() : new(false, "non exécuté");
            var redo = undo.Succeeded ? _session.RedoPathfindingUx6V() : new(false, "non exécuté");
            var saved = redo.Succeeded ? _session.SavePathfindingUx6V() : new(false, "non exécuté");
            return applied.Succeeded && undo.Succeeded && redo.Succeeded && saved.Succeeded && SourceMatches();
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestorePathfindingApprovalUx6V(approval);
        }
    }

    private void OpenBound(StudioSessionController target, bool evidence, bool cancelApproval)
    {
        _session = target;
        if (cancelApproval) _session.CancelPathfindingApprovalUx6V();
        Visible = true;
        _explicitAreaSelection = false;
        _explicitAgentSelection = false;
        _draft = null;
        _preview = null;
        PopulateAreas();
        PopulateAgents(null);
        ClearEditors();
        ClearPreview();
        _status.Text = evidence
            ? "Preuve isolée · choisissez le laboratoire puis son profil de surface."
            : "Sélectionnez explicitement une zone puis un profil d’agent.";
        UpdateBounds();
        UpdateButtons();
    }

    private void PopulateAreas()
    {
        _areas = _session.PathfindingAreaChoicesUx6V();
        _areaChoice.Clear();
        _areaChoice.AddItem(_areas.Count == 0 ? "Aucune zone disponible" : "Choisir une zone…");
        foreach (var area in _areas) _areaChoice.AddItem(area.Label);
        _areaChoice.Select(0);
        _areaChoice.Disabled = _areas.Count == 0;
    }

    private void PopulateAgents(AreaId? areaId)
    {
        _agents = _session.PathfindingAgentChoicesUx6V(areaId);
        _agentChoice.Clear();
        _agentChoice.AddItem(_agents.Count == 0 ? "Aucun profil d’agent" : "Choisir un profil…");
        foreach (var agent in _agents) _agentChoice.AddItem(agent.Label);
        _agentChoice.Select(0);
        _agentChoice.Disabled = _agents.Count == 0;
    }

    private void SelectArea(int index)
    {
        if (_updating) return;
        _session.CancelPathfindingApprovalUx6V();
        _preview = null;
        _draft = null;
        _explicitAgentSelection = false;
        if (index <= 0 || index > _areas.Count)
        {
            _explicitAreaSelection = false;
            PopulateAgents(null);
            ClearEditors();
            ClearPreview();
            SetStatus("Sélectionnez explicitement une zone.");
            return;
        }
        _explicitAreaSelection = true;
        PopulateAgents(_areas[index - 1].Id);
        ClearEditors();
        ClearPreview();
        SetStatus("Zone chargée · sélectionnez un profil d’agent.");
    }

    private void SelectAgent(int index)
    {
        if (_updating) return;
        _session.CancelPathfindingApprovalUx6V();
        _preview = null;
        if (!_explicitAreaSelection || index <= 0 || index > _agents.Count)
        {
            _explicitAgentSelection = false;
            _draft = null;
            ClearEditors();
            ClearPreview();
            SetStatus("Sélectionnez explicitement un profil d’agent.");
            return;
        }
        _explicitAgentSelection = true;
        var areaId = _areas[_areaChoice.Selected - 1].Id;
        _draft = _session.CreatePathfindingDraftUx6V(true, areaId, _agents[index - 1].Id);
        if (_draft is null)
        {
            SetStatus("Le profil n’est plus disponible · rechargez la surface.", error: true);
            return;
        }
        PopulateDraft();
        ClearPreview();
        SetStatus("Profil chargé · configurez la surface puis lancez l’aperçu sec canonique.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        _agentName.Text = _draft.AgentName;
        _cellSize.Text = Number(_draft.CellSize);
        _agentRadius.Text = Number(_draft.AgentRadius);
        _agentHeight.Text = Number(_draft.AgentHeight);
        _doors.ButtonPressed = _draft.CanOpenDoors;
        _links.ButtonPressed = _draft.CanUseOffMeshLinks;
        _flight.ButtonPressed = _draft.CanFly;
        _doorState.Select((int)_draft.DoorState);
        SetCell(_startX, _startZ, _draft.Start);
        SetCell(_goalX, _goalZ, _draft.Goal);
        _obstacleEnabled.ButtonPressed = _draft.DynamicObstacleEnabled;
        SetCell(_obstacleX, _obstacleZ, _draft.DynamicObstacle);
        _costEnabled.ButtonPressed = _draft.CostEnabled;
        SetCell(_costX, _costZ, _draft.CostCell);
        _costMultiplier.Text = Number(_draft.CostMultiplier);
        _recovery.ButtonPressed = _draft.RecoverBlockedEndpoints;
        _recoveryRadius.Value = _draft.RecoveryRadius;
        _budget.Value = _draft.MaximumVisitedCells;
        _updating = false;
        UpdateModifierAvailability();
        UpdateButtons();
    }

    private bool TryReadDraft(out StudioUx6VDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez explicitement une zone et un profil d’agent.";
            return false;
        }
        if (!TryFloat(_cellSize.Text, out var cellSize) || !TryFloat(_agentRadius.Text, out var radius) ||
            !TryFloat(_agentHeight.Text, out var height) || !TryFloat(_costMultiplier.Text, out var cost))
        {
            error = "Saisissez des nombres finis avec un point ou une virgule décimale.";
            return false;
        }
        draft = _draft with
        {
            CellSize = cellSize,
            AgentRadius = radius,
            AgentHeight = height,
            CanOpenDoors = _doors.ButtonPressed,
            CanUseOffMeshLinks = _links.ButtonPressed,
            CanFly = _flight.ButtonPressed,
            DoorState = (NavigationDoorState)_doorState.Selected,
            Start = ReadCell(_startX, _startZ),
            Goal = ReadCell(_goalX, _goalZ),
            DynamicObstacleEnabled = _obstacleEnabled.ButtonPressed,
            DynamicObstacle = ReadCell(_obstacleX, _obstacleZ),
            CostEnabled = _costEnabled.ButtonPressed,
            CostCell = ReadCell(_costX, _costZ),
            CostMultiplier = cost,
            RecoverBlockedEndpoints = _recovery.ButtonPressed,
            RecoveryRadius = (int)Math.Round(_recoveryRadius.Value),
            MaximumVisitedCells = (int)Math.Round(_budget.Value),
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft, out var error) || draft is null)
        {
            SetStatus(error, error: true);
            return;
        }
        _draft = draft;
        _preview = _session.PreviewPathfindingUx6V(draft);
        RenderPreview();
        SetStatus(_preview.Message, error: !_preview.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedPathfindingUx6V();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        NavigationChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoPathfindingUx6V();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        NavigationChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Redo()
    {
        var result = _session.RedoPathfindingUx6V();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        NavigationChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Save()
    {
        var result = _session.SavePathfindingUx6V();
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        var areaId = _draft?.AreaId;
        var agentId = _draft?.AgentId;
        PopulateAreas();
        var areaIndex = areaId is null ? -1 : _areas.ToList().FindIndex(item => item.Id == areaId);
        if (areaIndex < 0)
        {
            ResetControls();
            return;
        }
        _updating = true;
        _areaChoice.Select(areaIndex + 1);
        _updating = false;
        _explicitAreaSelection = true;
        PopulateAgents(areaId);
        var agentIndex = agentId is null ? -1 : _agents.ToList().FindIndex(item => item.Id == agentId);
        if (agentIndex < 0)
        {
            _draft = null;
            _explicitAgentSelection = false;
            ClearEditors();
        }
        else
        {
            _updating = true;
            _agentChoice.Select(agentIndex + 1);
            _updating = false;
            _explicitAgentSelection = true;
            _draft = _session.CreatePathfindingDraftUx6V(true, areaId, agentId);
            PopulateDraft();
        }
        _preview = null;
        ClearPreview();
        UpdateButtons();
    }

    private void RenderPreview()
    {
        ClearChildren(_results);
        _results.AddChild(StudioStyle.Label("RÉSULTATS · instrumentation déterministe", 9,
            StudioStyle.Accent));
        if (_preview is null) return;
        foreach (var line in _preview.Results.Take(8))
            _results.AddChild(StudioStyle.Label(line, 9,
                line.StartsWith("Aucun ", StringComparison.Ordinal) ? StudioStyle.Warning : StudioStyle.Text,
                wrap: true));
        foreach (var diagnostic in _preview.Diagnostics)
            _results.AddChild(StudioStyle.Label($"{diagnostic.Code} · {diagnostic.Message}", 9,
                StudioStyle.Danger, wrap: true));
        _grid.Snapshot = _preview.Grid;
        _review.Text = _preview.Diff.Count == 0
            ? "Aucune différence persistante · le laboratoire reste disponible."
            : "DIFF HUMAIN · " + string.Join(" · ", _preview.Diff);
        UpdateButtons();
    }

    private void ClearPreview()
    {
        if (_results is null) return;
        ClearChildren(_results);
        _results.AddChild(StudioStyle.Label("RÉSULTATS · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        _grid.Snapshot = null;
        if (_review is not null) _review.Text = "Aucune modification approuvée.";
    }

    private void WireInvalidation()
    {
        _cellSize.TextChanged += _ => DraftChanged();
        _agentRadius.TextChanged += _ => DraftChanged();
        _agentHeight.TextChanged += _ => DraftChanged();
        _doors.Toggled += _ => DraftChanged();
        _links.Toggled += _ => DraftChanged();
        _flight.Toggled += _ => DraftChanged();
        _doorState.ItemSelected += _ => DraftChanged();
        foreach (var spin in new[] { _startX, _startZ, _goalX, _goalZ, _obstacleX, _obstacleZ,
                     _costX, _costZ, _recoveryRadius, _budget })
            spin.ValueChanged += _ => DraftChanged();
        _obstacleEnabled.Toggled += _ => { UpdateModifierAvailability(); DraftChanged(); };
        _costEnabled.Toggled += _ => { UpdateModifierAvailability(); DraftChanged(); };
        _costMultiplier.TextChanged += _ => DraftChanged();
        _recovery.Toggled += _ => { UpdateModifierAvailability(); DraftChanged(); };
    }

    private void DraftChanged()
    {
        if (_updating) return;
        _session.CancelPathfindingApprovalUx6V();
        _preview = null;
        ClearPreview();
        SetStatus("Brouillon modifié · relancez l’aperçu sec canonique.");
        UpdateButtons();
    }

    private void UpdateModifierAvailability()
    {
        _obstacleX.Editable = _obstacleEnabled.ButtonPressed;
        _obstacleZ.Editable = _obstacleEnabled.ButtonPressed;
        _costX.Editable = _costEnabled.ButtonPressed;
        _costZ.Editable = _costEnabled.ButtonPressed;
        _costMultiplier.Editable = _costEnabled.ButtonPressed;
        _recoveryRadius.Editable = _recovery.ButtonPressed;
    }

    private void ResetControls(bool keepReleaseState = false)
    {
        _updating = true;
        _explicitAreaSelection = false;
        _explicitAgentSelection = false;
        _draft = null;
        _preview = null;
        _areas = [];
        _agents = [];
        if (_areaChoice is not null)
        {
            _areaChoice.Clear();
            _areaChoice.AddItem("Choisir une zone…");
        }
        if (_agentChoice is not null)
        {
            _agentChoice.Clear();
            _agentChoice.AddItem("Choisir un profil…");
        }
        ClearEditors();
        ClearPreview();
        if (_status is not null) _status.Text = "Sélectionnez explicitement une zone puis un profil d’agent.";
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        UpdateButtons();
    }

    private void ClearEditors()
    {
        foreach (var field in new[] { _agentName, _cellSize, _agentRadius, _agentHeight, _costMultiplier })
            if (field is not null) field.Text = string.Empty;
        foreach (var check in new[] { _doors, _links, _flight, _obstacleEnabled, _costEnabled, _recovery })
            if (check is not null) check.ButtonPressed = false;
        foreach (var spin in new[] { _startX, _startZ, _goalX, _goalZ, _obstacleX, _obstacleZ, _costX,
                     _costZ, _recoveryRadius, _budget })
            if (spin is not null) spin.Value = 0;
        if (_doorState is not null) _doorState.Select(0);
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (_apply is null) return;
        _apply.Disabled = !PreviewReady;
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
    }

    private SourceSnapshotUx6V CaptureSource()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CapturePathfindingApprovalUx6V());
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.AreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CapturePathfindingApprovalUx6V() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                       StudioSessionController.DeleteTemporaryUx6V(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_V_UI_CLEANUP_FAILED");
    }

    private VBoxContainer TabPage(string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var margin = StudioStyle.Margin(10, 9, 10, 9);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        margin.AddChild(page);
        _tabs.AddChild(scroll);
        return page;
    }

    private static LineEdit Field(string placeholder, bool readOnly = false) => new()
    {
        PlaceholderText = placeholder,
        Editable = !readOnly,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(120, 34),
    };

    private static OptionButton Choice(float width) => new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(width, 34),
    };

    private static SpinBox Integer(double minimum, double maximum) => new()
    {
        MinValue = minimum,
        MaxValue = maximum,
        Step = 1,
        Rounded = true,
        AllowGreater = false,
        AllowLesser = false,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(88, 34),
    };

    private static VBoxContainer Column(string label, Control control)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(StudioStyle.Label(label.ToUpperInvariant(), 8, StudioStyle.Muted));
        column.AddChild(control);
        return column;
    }

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 64, 1120, 1740);
        var height = Math.Clamp(viewport.Y - 42, 780, 1030);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static void SetCell(SpinBox x, SpinBox z, NavigationCell cell)
    {
        x.Value = cell.X;
        z.Value = cell.Z;
    }

    private static NavigationCell ReadCell(SpinBox x, SpinBox z) =>
        new((int)Math.Round(x.Value), (int)Math.Round(z.Value));

    private static bool TryFloat(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
        float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case CheckBox checkBox when checkBox.Visible: yield return checkBox.Text; break;
                case LineEdit lineEdit when lineEdit.Visible: yield return lineEdit.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private sealed record SourceSnapshotUx6V(
        ProjectDefinition Project,
        ProjectCommandHistory History,
        ProjectRevision Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        AreaId? AreaId,
        IReadOnlyList<EntityId> SelectedEntityIds,
        EntityId? SelectedEntityId,
        StudioUx6VApprovalSnapshot Approval);
}

internal sealed partial class StudioPathfindingGridUx6V : Control
{
    private StudioUx6VGridSnapshot? _snapshot;

    internal StudioUx6VGridSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(.025f, .035f, .055f, 1));
        var snapshot = _snapshot;
        if (snapshot is null || snapshot.Width <= 0 || snapshot.Depth <= 0) return;
        var cell = MathF.Max(1f, MathF.Min((Size.X - 12f) / snapshot.Width, (Size.Y - 12f) / snapshot.Depth));
        var origin = new Vector2((Size.X - snapshot.Width * cell) / 2f,
            (Size.Y - snapshot.Depth * cell) / 2f);
        var walkable = snapshot.Walkable.ToHashSet();
        var obstacles = snapshot.DynamicObstacles.ToHashSet();
        var costs = snapshot.CostCells.ToHashSet();
        var path = snapshot.Path.ToHashSet();
        foreach (var item in walkable)
        {
            var rect = CellRect(item, origin, cell);
            var color = path.Contains(item) ? new Color(.2f, .72f, .96f, .88f) :
                costs.Contains(item) ? new Color(.95f, .69f, .2f, .84f) :
                obstacles.Contains(item) ? new Color(.92f, .22f, .3f, .9f) :
                new Color(.18f, .25f, .34f, .74f);
            DrawRect(rect, color);
        }
        foreach (var obstacle in obstacles)
            DrawRect(CellRect(obstacle, origin, cell), new Color(.92f, .22f, .3f, .96f));
        DrawMarker(snapshot.RequestedStart, origin, cell, new Color(.3f, .95f, .52f));
        DrawMarker(snapshot.RequestedGoal, origin, cell, new Color(1f, .44f, .68f));
        if (snapshot.ResolvedStart is { } start && start != snapshot.RequestedStart)
            DrawMarker(start, origin, cell, new Color(.68f, 1f, .4f));
        if (snapshot.ResolvedGoal is { } goal && goal != snapshot.RequestedGoal)
            DrawMarker(goal, origin, cell, new Color(1f, .7f, .32f));
        DrawRect(new Rect2(origin, new Vector2(snapshot.Width * cell, snapshot.Depth * cell)),
            new Color(.42f, .58f, .75f, .8f), filled: false, width: 1f);
    }

    private static Rect2 CellRect(NavigationCell item, Vector2 origin, float size) =>
        new(origin.X + item.X * size + .5f, origin.Y + item.Z * size + .5f,
            MathF.Max(1f, size - 1f), MathF.Max(1f, size - 1f));

    private void DrawMarker(NavigationCell item, Vector2 origin, float size, Color color)
    {
        var rect = CellRect(item, origin, size);
        DrawCircle(rect.GetCenter(), MathF.Max(2f, MathF.Min(7f, size * .42f)), color);
    }
}
