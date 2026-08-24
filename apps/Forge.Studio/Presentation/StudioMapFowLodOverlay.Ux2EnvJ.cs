using Forge.Domain.Areas;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioMapPreviewCanvasUx2EnvJ : Control
{
    private IReadOnlyList<StudioUx2EnvJPinDraft> _pins = [];
    private int _floor;
    private bool _fog;

    internal bool HasCanonicalPlan { get; private set; }

    internal StudioMapPreviewCanvasUx2EnvJ()
    {
        CustomMinimumSize = new Vector2(0, 190);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    internal void SetDraft(
        IReadOnlyList<StudioUx2EnvJPinDraft> pins,
        int floor,
        bool fog,
        bool canonical)
    {
        _pins = pins;
        _floor = Math.Max(0, floor);
        _fog = fog;
        HasCanonicalPlan = canonical;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), StudioStyle.Background, filled: true);
        var bounds = new Rect2(new Vector2(12, 12), new Vector2(
            Math.Max(1, Size.X - 24), Math.Max(1, Size.Y - 24)));
        const int divisions = 8;
        for (var index = 1; index < divisions; index++)
        {
            var x = bounds.Position.X + bounds.Size.X * index / divisions;
            var z = bounds.Position.Y + bounds.Size.Y * index / divisions;
            DrawLine(new Vector2(x, bounds.Position.Y),
                new Vector2(x, bounds.End.Y), StudioStyle.Border, 1);
            DrawLine(new Vector2(bounds.Position.X, z),
                new Vector2(bounds.End.X, z), StudioStyle.Border, 1);
        }
        if (_fog) DrawRect(bounds, new Color(.08f, .12f, .18f, .38f), filled: true);
        var visible = _pins.Where(pin => int.TryParse(pin.Floor, out var floor) && floor == _floor)
            .Select(pin => (Pin: pin, X: ParseCoordinate(pin.X), Z: ParseCoordinate(pin.Z)))
            .Where(item => item.X is not null && item.Z is not null)
            .ToArray();
        var extent = Math.Max(10f, visible.SelectMany(item => new[]
            {
                MathF.Abs(item.X!.Value), MathF.Abs(item.Z!.Value),
            }).DefaultIfEmpty(10).Max() * 1.15f);
        foreach (var item in visible)
        {
            var point = new Vector2(
                bounds.GetCenter().X + item.X!.Value / extent * bounds.Size.X * .45f,
                bounds.GetCenter().Y + item.Z!.Value / extent * bounds.Size.Y * .45f);
            var color = item.Pin.InitiallyVisible ? StudioStyle.Success : StudioStyle.Warning;
            DrawCircle(point, 7, new Color(color, .28f));
            DrawCircle(point, 4, color);
        }
        DrawRect(bounds, HasCanonicalPlan ? StudioStyle.Accent : StudioStyle.Muted,
            filled: false, width: HasCanonicalPlan ? 2 : 1);
    }

    private static float? ParseCoordinate(string? text)
    {
        var value = (text ?? string.Empty).Trim();
        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var invariant) &&
            float.IsFinite(invariant)) return invariant;
        return float.TryParse(value, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.CurrentCulture, out var localized) &&
               float.IsFinite(localized)
            ? localized
            : null;
    }
}

internal sealed partial class StudioMapFowLodOverlayUx2EnvJ : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2EnvJEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _evidenceUsesRealModuleData;
    private bool _evidenceUsesCanonicalFallback;
    private bool _evidenceReleaseSucceeded = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly TabContainer _tabs;
    private readonly CheckBox _mapEnabled;
    private readonly CheckBox _fogOfWar;
    private readonly LineEdit _projection;
    private readonly LineEdit _floorCount;
    private readonly CheckBox _generate2D;
    private readonly CheckBox _generate3D;
    private readonly OptionButton _explorationScope;
    private readonly LineEdit _explorationCellSize;
    private readonly LineEdit _revealApi;
    private readonly ItemList _pins;
    private readonly LineEdit _pinLabel;
    private readonly LineEdit _pinX;
    private readonly LineEdit _pinZ;
    private readonly LineEdit _pinFloor;
    private readonly CheckBox _pinInitiallyVisible;
    private readonly Button _addPin;
    private readonly Button _removePin;
    private readonly StudioMapPreviewCanvasUx2EnvJ _mapCanvas;
    private readonly OptionButton _mapPreviewFloor;
    private readonly OptionButton _lodMode;
    private readonly LineEdit _distanceThresholds;
    private readonly LineEdit _maximumTriangles;
    private readonly LineEdit _maximumClusters;
    private readonly LineEdit _clusterSize;
    private readonly LineEdit _poppingTolerance;
    private readonly CheckBox _crossFade;
    private readonly CheckBox _measurePopping;
    private readonly ItemList _overrides;
    private readonly LineEdit _overrideAsset;
    private readonly OptionButton _overrideMode;
    private readonly LineEdit _overrideRatios;
    private readonly LineEdit _overrideBudgets;
    private readonly LineEdit _overrideCrossFade;
    private readonly LineEdit _overrideProvenance;
    private readonly Button _addOverride;
    private readonly Button _removeOverride;
    private readonly VBoxContainer _mapPlan;
    private readonly VBoxContainer _hlodPlan;
    private readonly Label _performancePlan;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2EnvJAreaChoice> _choices = [];
    private StudioUx2EnvJDraft? _draft;
    private List<StudioUx2EnvJPinDraft> _pinDrafts = [];
    private List<StudioUx2EnvJLodOverrideDraft> _overrideDrafts = [];
    private StudioUx2EnvJPreview? _lastPreview;
    private int _selectedPin = -1;
    private int _selectedOverride = -1;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? MapLodChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
    {
        Succeeded: true,
        CanonicalDryRun: true,
        RenderPlanBuilt: true,
        RevealPlanBuilt: true,
        HlodPlanBuilt: true,
        PerformancePlanBuilt: true,
        GenerationSkipped: true,
        PlayerStateMutationSkipped: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsMapPlan => _lastPreview is { Floors.Count: > 0, Reveal: not null } &&
                                  _mapPlan.GetChildCount() >= _lastPreview.Floors.Count + 1;
    internal bool ShowsHlodPlan => _lastPreview?.HlodPlanBuilt == true && _hlodPlan.GetChildCount() > 0;
    internal bool ShowsPerformancePlan => _lastPreview?.PerformancePlanBuilt == true &&
                                          _performancePlan.Text.Contains("entité", StringComparison.Ordinal) &&
                                          _performancePlan.Text.Contains("popping", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsCompleteCollections => _draft is not null &&
                                               _pins.ItemCount == _pinDrafts.Count &&
                                               _overrides.ItemCount == _overrideDrafts.Count;
    internal bool ShowsStructuredDiagnostics => _lastPreview?.Groups.Count == 4;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview is
    {
        GenerationSkipped: true,
        PlayerStateMutationSkipped: true,
        RuntimeClaimsBounded: true,
    } && _lastPreview.Summary.Contains("aucun GLB", StringComparison.Ordinal) &&
         _lastPreview.Summary.Contains("aucune carte n’est rendue", StringComparison.Ordinal) &&
         _lastPreview.Summary.Contains("aucun état", StringComparison.Ordinal);
    internal bool ShowsHumanSurface
    {
        get
        {
            var titles = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = string.Join(' ', DescendantVisibleTextUx2EnvJ(this).Concat(titles));
            return text.Contains("Carte & brouillard", StringComparison.Ordinal) &&
                   text.Contains("LOD & HLOD", StringComparison.Ordinal) &&
                   text.Contains("Aperçu & validation", StringComparison.Ordinal) &&
                   text.Contains("Repères par étage", StringComparison.Ordinal) &&
                   text.Contains("Prévisualiser", StringComparison.Ordinal) &&
                   !ContainsUuidUx2EnvJ(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleSource => UsesIsolatedEvidence && _evidenceUsesRealModuleData;
    internal bool UsesCanonicalFallbackEvidence => UsesIsolatedEvidence && _evidenceUsesCanonicalFallback;
    internal bool EvidenceSourceUntouched => SourceSnapshotMatchesUx2EnvJ();
    internal bool EvidenceResourcesReleased => _evidenceReleaseSucceeded &&
                                                _evidenceSession is null && _evidenceTempRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);

    internal StudioMapFowLodOverlayUx2EnvJ(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.014f, .019f, .026f, .97f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux2EnvJMapFowLodDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Carte, brouillard & LOD", 18));
        headingText.AddChild(StudioStyle.Label(
            "Carte 2D/3D, exploration, repères et budgets LOD/HLOD · aperçu sec sans génération ni état joueur.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _area = StudioUx2EnvBControls.Options();
        _area.ItemSelected += SelectAreaUx2EnvJ;
        root.AddChild(StudioUx2EnvBControls.Field("Zone à modifier · sélection explicite", _area, 540));
        _selection = StudioStyle.Label("Aucune zone sélectionnée.", 10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 360),
            CustomMaximumSize = new Vector2(-1, 650),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var mapTab = ScrollTabUx2EnvJ("Carte & brouillard");
        _mapEnabled = StudioUx2EnvBControls.Check("Carte active");
        _fogOfWar = StudioUx2EnvBControls.Check("Brouillard de guerre actif");
        _projection = TextUx2EnvJ("projection, par exemple orthographic");
        _floorCount = TextUx2EnvJ("nombre entier d’étages");
        _generate2D = StudioUx2EnvBControls.Check("Générer le plan 2D");
        _generate3D = StudioUx2EnvBControls.Check("Générer le plan 3D");
        _explorationScope = StudioUx2EnvBControls.Options("Par joueur", "Par groupe", "Partagée au monde");
        _explorationCellSize = TextUx2EnvJ("taille de cellule en mètres");
        _revealApi = TextUx2EnvJ("nom de l’API de révélation");
        var mapGrid = GridUx2EnvJ(3);
        mapGrid.AddChild(_mapEnabled);
        mapGrid.AddChild(_fogOfWar);
        mapGrid.AddChild(StudioUx2EnvBControls.Field("Projection", _projection));
        mapGrid.AddChild(StudioUx2EnvBControls.Field("Nombre d’étages", _floorCount));
        mapGrid.AddChild(_generate2D);
        mapGrid.AddChild(_generate3D);
        mapGrid.AddChild(StudioUx2EnvBControls.Field("Exploration", _explorationScope));
        mapGrid.AddChild(StudioUx2EnvBControls.Field("Cellule d’exploration (m)", _explorationCellSize));
        mapGrid.AddChild(StudioUx2EnvBControls.Field("API de révélation", _revealApi));
        mapTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJMap", "Carte par étage",
            "Les nombres restent bruts jusqu’au dry-run; aucune valeur valide n’est arrondie ou plafonnée.",
            mapGrid));

        _pins = new ItemList
        {
            CustomMinimumSize = new Vector2(360, 176),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _pins.ItemSelected += SelectPinUx2EnvJ;
        _pinLabel = TextUx2EnvJ("nom lisible du repère");
        _pinX = TextUx2EnvJ("coordonnée X");
        _pinZ = TextUx2EnvJ("coordonnée Z");
        _pinFloor = TextUx2EnvJ("étage à partir de 0");
        _pinInitiallyVisible = StudioUx2EnvBControls.Check("Visible avant exploration");
        var pinEditor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var pinGrid = GridUx2EnvJ(2);
        pinGrid.AddChild(StudioUx2EnvBControls.Field("Nom", _pinLabel));
        pinGrid.AddChild(StudioUx2EnvBControls.Field("Étage", _pinFloor));
        pinGrid.AddChild(StudioUx2EnvBControls.Field("Position X", _pinX));
        pinGrid.AddChild(StudioUx2EnvBControls.Field("Position Z", _pinZ));
        pinGrid.AddChild(_pinInitiallyVisible);
        pinEditor.AddChild(pinGrid);
        var pinActions = new HBoxContainer();
        _addPin = StudioStyle.Button("Ajouter un repère", compact: true);
        _removePin = StudioStyle.Button("Retirer le repère", compact: true);
        pinActions.AddChild(_addPin);
        pinActions.AddChild(_removePin);
        pinEditor.AddChild(pinActions);
        var pinBody = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pinBody.AddThemeConstantOverride("separation", 12);
        pinBody.AddChild(_pins);
        pinBody.AddChild(pinEditor);
        mapTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJPins", "Repères par étage",
            "La liste complète est conservée; les identités stables restent cachées et chaque repère garde sa visibilité initiale.",
            pinBody));
        _mapCanvas = new StudioMapPreviewCanvasUx2EnvJ();
        _mapPreviewFloor = StudioUx2EnvBControls.Options("Étage 0");
        _mapPreviewFloor.ItemSelected += _ => UpdateMapCanvasUx2EnvJ(canonical: false);
        var canvasBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        canvasBody.AddChild(StudioUx2EnvBControls.Field("Étage aperçu", _mapPreviewFloor));
        canvasBody.AddChild(_mapCanvas);
        canvasBody.AddChild(StudioStyle.Label(
            "Empreinte 2D auto-ajustée pour placer les repères; ce cadre n’est ni la carte rendue ni le viewport runtime.",
            9, StudioStyle.Muted, wrap: true));
        mapTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJMapCanvas", "Aperçu spatial des repères",
            "La grille rend immédiatement les coordonnées de l’étage choisi; le contour accentué apparaît après dry-run.",
            canvasBody));

        var lodTab = ScrollTabUx2EnvJ("LOD & HLOD");
        _lodMode = StudioUx2EnvBControls.Options("Importé", "Généré", "Hybride");
        _distanceThresholds = TextUx2EnvJ("distances séparées par ;");
        _maximumTriangles = TextUx2EnvJ("budget de triangles visibles");
        _maximumClusters = TextUx2EnvJ("nombre maximal de clusters");
        _clusterSize = TextUx2EnvJ("taille de cellule HLOD");
        _poppingTolerance = TextUx2EnvJ("tolérance entre 0 et 1");
        _crossFade = StudioUx2EnvBControls.Check("Fondu entre niveaux LOD");
        _measurePopping = StudioUx2EnvBControls.Check("Mesurer le popping");
        var lodGrid = GridUx2EnvJ(3);
        lodGrid.AddChild(StudioUx2EnvBControls.Field("Source par défaut", _lodMode));
        lodGrid.AddChild(StudioUx2EnvBControls.Field("Seuils de distance (m)", _distanceThresholds));
        lodGrid.AddChild(StudioUx2EnvBControls.Field("Triangles visibles maximum", _maximumTriangles));
        lodGrid.AddChild(StudioUx2EnvBControls.Field("Clusters HLOD maximum", _maximumClusters));
        lodGrid.AddChild(StudioUx2EnvBControls.Field("Taille de cluster (m)", _clusterSize));
        lodGrid.AddChild(StudioUx2EnvBControls.Field("Tolérance de popping", _poppingTolerance));
        lodGrid.AddChild(_crossFade);
        lodGrid.AddChild(_measurePopping);
        lodTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJLod", "Politique mesurable",
            "Les seuils, budgets, fondus et mesures alimentent les plans canoniques; Studio ne fabrique aucun mesh.",
            lodGrid));

        _overrides = new ItemList
        {
            CustomMinimumSize = new Vector2(360, 176),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _overrides.ItemSelected += SelectOverrideUx2EnvJ;
        _overrideAsset = TextUx2EnvJ("clé humaine de l’asset");
        _overrideMode = StudioUx2EnvBControls.Options("Importé", "Généré", "Hybride");
        _overrideRatios = TextUx2EnvJ("ratios séparés par ;");
        _overrideBudgets = TextUx2EnvJ("budgets séparés par ;");
        _overrideCrossFade = TextUx2EnvJ("durée de fondu en secondes");
        _overrideProvenance = TextUx2EnvJ("provenance de l’override");
        var overrideEditor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var overrideGrid = GridUx2EnvJ(2);
        overrideGrid.AddChild(StudioUx2EnvBControls.Field("Asset", _overrideAsset));
        overrideGrid.AddChild(StudioUx2EnvBControls.Field("Source", _overrideMode));
        overrideGrid.AddChild(StudioUx2EnvBControls.Field("Ratios écran", _overrideRatios));
        overrideGrid.AddChild(StudioUx2EnvBControls.Field("Budgets triangles", _overrideBudgets));
        overrideGrid.AddChild(StudioUx2EnvBControls.Field("Fondu (s)", _overrideCrossFade));
        overrideGrid.AddChild(StudioUx2EnvBControls.Field("Provenance", _overrideProvenance));
        overrideEditor.AddChild(overrideGrid);
        var overrideActions = new HBoxContainer();
        _addOverride = StudioStyle.Button("Ajouter un override", compact: true);
        _removeOverride = StudioStyle.Button("Retirer l’override", compact: true);
        overrideActions.AddChild(_addOverride);
        overrideActions.AddChild(_removeOverride);
        overrideEditor.AddChild(overrideActions);
        var overrideBody = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        overrideBody.AddThemeConstantOverride("separation", 12);
        overrideBody.AddChild(_overrides);
        overrideBody.AddChild(overrideEditor);
        lodTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJOverrides", "Overrides LOD par asset",
            "La collection complète reste éditable; ratios et budgets doivent garder exactement la même longueur.",
            overrideBody));

        var reviewTab = ScrollTabUx2EnvJ("Aperçu & validation");
        _mapPlan = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _hlodPlan = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _performancePlan = StudioStyle.Label("Rapport de performance en attente.", 9,
            StudioStyle.Muted, wrap: true);
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJMapPlan", "Plan carte & révélation",
            "Le plan 2D/3D et l’état d’exploration sont des copies de prévisualisation.", _mapPlan));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJHlodPlan", "Plan HLOD confiné",
            "Les hooks sont calculés et vérifiés, mais aucun fichier GLB n’est écrit.", _hlodPlan));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJPerformance", "Budget & popping",
            "Le rapport canonique estime les unités de dessin et les paliers LOD.", _performancePlan));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJDiff", "Modifications proposées",
            "Le dry-run canonique ne change ni projet, ni historique.", _diff));
        reviewTab.AddChild(StudioUx2EnvBControls.Card("Ux2EnvJDiagnostics", "Validation structurée",
            "Carte, LOD, performance et commande sont présentés séparément.", _diagnostics));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 58);
        root.AddChild(_summary);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(126, 34);
            actions.AddChild(button);
        }
        var actionNote = StudioStyle.Label(
            "Aperçu sec obligatoire · une commande set_area_authoring · aucune génération de fichier.",
            9, StudioStyle.Muted, wrap: true);
        actionNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actionNote.CustomMinimumSize = new Vector2(300, 34);
        actions.AddChild(actionNote);
        root.AddChild(actions);
        _preview.Pressed += PreviewUx2EnvJ;
        _apply.Pressed += ApplyUx2EnvJ;
        _undo.Pressed += UndoUx2EnvJ;
        _redo.Pressed += RedoUx2EnvJ;
        _save.Pressed += SaveUx2EnvJ;
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        _addPin.Pressed += AddPinUx2EnvJ;
        _removePin.Pressed += RemovePinUx2EnvJ;
        _addOverride.Pressed += AddOverrideUx2EnvJ;
        _removeOverride.Pressed += RemoveOverrideUx2EnvJ;
        WireDraftChangesUx2EnvJ();
        SetEditingEnabledUx2EnvJ(false);
        ResetReviewUx2EnvJ();
        UpdateActionStateUx2EnvJ();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
            ReleaseIsolatedEvidenceSessionUx2EnvJ();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvJ();
        RefreshChoicesUx2EnvJ();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession))
            _session.CancelMapLodApprovalUx2EnvJ();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        ReleaseIsolatedEvidenceSessionUx2EnvJ();
        _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureMapLodApprovalUx2EnvJ();
        _session = _sourceSession;
        Visible = true;
        UpdateDialogBoundsUx2EnvJ();
        RefreshChoicesUx2EnvJ();
        _sourceSession.RestoreMapLodApprovalUx2EnvJ(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureMapLodApprovalUx2EnvJ();
        Close();
        _sourceSession.RestoreMapLodApprovalUx2EnvJ(sourceApproval);
        _evidenceReleaseSucceeded = true;
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || sourceProject.Areas.Count == 0) return false;
        _evidenceSourceSnapshot = CaptureSourceSnapshotUx2EnvJ();
        var tempRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux2-env-j-{Guid.NewGuid():N}");
        var evidenceSession = new StudioSessionController();
        try
        {
            var realArea = sourceProject.Areas
                .Where(area => area.Authoring.Map.Enabled && area.Authoring.Map.Generate2D &&
                               area.Authoring.Map.Generate3D &&
                               area.Authoring.Experience.Lod.DefaultSourceMode != AreaLodSourceMode.Imported &&
                               AreaExperienceAuthoringValidator.Validate(area).Count == 0 &&
                               Forge.Application.Areas.AreaHlodGenerationPlanner.Plan(area).CanGenerate)
                .OrderBy(area => area.Entities.Count)
                .FirstOrDefault();
            _evidenceUsesRealModuleData = realArea is not null;
            _evidenceUsesCanonicalFallback = realArea is null;
            var evidenceProject = realArea is null
                ? StudioSessionController.CreateUx2EnvJContractProject(sourceProject)
                : sourceProject;
            var evidenceAreaId = realArea?.Id ?? evidenceProject.Areas[0].Id;
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded || !evidenceSession.Open(tempRoot, evidenceAreaId.ToString()).Succeeded)
            {
                evidenceSession.Close();
                var released = StudioSessionController.DeleteUx2EnvJTemporaryWorkspace(tempRoot);
                ResetEvidenceStateUx2EnvJ();
                _evidenceReleaseSucceeded = released;
                if (!released) _evidenceTempRoot = tempRoot;
                return false;
            }
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            Visible = true;
            UpdateDialogBoundsUx2EnvJ();
            RefreshChoicesUx2EnvJ();
            var choiceIndex = _choices.ToList().FindIndex(choice => choice.AreaId == evidenceAreaId);
            if (choiceIndex < 0)
            {
                Close();
                return false;
            }
            _area.Select(choiceIndex + 1);
            LoadChoiceUx2EnvJ(choiceIndex);
            if (!ConfigureEvidenceDraftUx2EnvJ())
            {
                Close();
                return false;
            }
            PreviewUx2EnvJ();
            _tabs.CurrentTab = 2;
            var ready = CanonicalPreviewReady && HasExplicitSelection && ShowsMapPlan && ShowsHlodPlan &&
                        ShowsPerformancePlan && ShowsCompleteCollections && ShowsStructuredDiagnostics &&
                        ShowsBoundedRuntimeClaim && ShowsHumanSurface && UsesIsolatedEvidence &&
                        (UsesRealModuleSource ^ UsesCanonicalFallbackEvidence) && EvidenceSourceUntouched;
            if (!ready)
            {
                Close();
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_J_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            var sessionReleased = true;
            try
            {
                evidenceSession.Close();
            }
            catch (Exception releaseException)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_J_EVIDENCE_RELEASE_FAILED " +
                            $"type={releaseException.GetType().Name} message={releaseException.Message}");
            }
            var released = StudioSessionController.DeleteUx2EnvJTemporaryWorkspace(tempRoot);
            ResetEvidenceStateUx2EnvJ();
            _evidenceReleaseSucceeded = sessionReleased && released;
            if (!released) _evidenceTempRoot = tempRoot;
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshotUx2EnvJ();
        if (!PrepareEvidence("ux2-env-j-map-fow-lod") ||
            !CanonicalPreviewReady || !UsesIsolatedEvidence)
        {
            Close();
            return false;
        }
        var isolated = _session;
        if (_lastPreview is not { Succeeded: true, CanApply: true })
        {
            Close();
            return false;
        }
        var isolatedProjectBefore = isolated.Project;
        ApplyUx2EnvJ();
        var applied = _applied && isolated.Project is not null;
        var appliedJson = applied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        UndoUx2EnvJ();
        var undone = !_applied && ReferenceEquals(isolatedProjectBefore, isolated.Project) &&
                     isolated.History?.CanRedo == true;
        RedoUx2EnvJ();
        var redone = _applied && isolated.Project is not null &&
                     string.Equals(appliedJson, ProjectJsonSerializer.Serialize(isolated.Project!),
                         StringComparison.Ordinal);
        SaveUx2EnvJ();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            var opened = _evidenceTempRoot is not null && reopen.Open(_evidenceTempRoot).Succeeded;
            roundTrip = opened && reopen.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopen.Project!),
                            StringComparison.Ordinal);
        }
        finally
        {
            reopen.Close();
        }
        Close();
        return applied && undone && redone && roundTrip && SourceSnapshotMatchesUx2EnvJ(sourceSnapshot) &&
               EvidenceResourcesReleased && SourceSessionRestored;
    }

    private VBoxContainer ScrollTabUx2EnvJ(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1080, 0),
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private void RefreshChoicesUx2EnvJ()
    {
        _updating = true;
        _choices = _session.MapLodChoicesUx2EnvJ;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        foreach (var choice in _choices) _area.AddItem(choice.Label);
        _area.Select(0);
        _updating = false;
        ClearSelectionUx2EnvJ();
    }

    private void SelectAreaUx2EnvJ(long index)
    {
        if (_updating) return;
        if (index <= 0 || index > _choices.Count)
        {
            ClearSelectionUx2EnvJ();
            return;
        }
        LoadChoiceUx2EnvJ((int)index - 1);
    }

    private void ClearSelectionUx2EnvJ()
    {
        _draft = null;
        _pinDrafts = [];
        _overrideDrafts = [];
        _selectedPin = -1;
        _selectedOverride = -1;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _selection.Text = "Aucune zone sélectionnée · aucune donnée chargée.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditingEnabledUx2EnvJ(false);
        RefreshPinListUx2EnvJ();
        RefreshOverrideListUx2EnvJ();
        ResetReviewUx2EnvJ();
        _session.CancelMapLodApprovalUx2EnvJ();
        UpdateActionStateUx2EnvJ();
    }

    private void LoadChoiceUx2EnvJ(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            ClearSelectionUx2EnvJ();
            return;
        }
        _draft = _session.CreateMapLodDraftUx2EnvJ(_choices[choiceIndex].AreaId);
        if (_draft is null)
        {
            ClearSelectionUx2EnvJ();
            SetStatusUx2EnvJ("La zone choisie n’est plus disponible.", error: true);
            return;
        }
        PopulateUx2EnvJ(_draft);
        _selection.Text = $"Zone sélectionnée · {HumanLabelUx2EnvJ(_draft.AreaDisplayName)} · " +
                          $"{_choices[choiceIndex].FloorCount} étage(s) · " +
                          $"{_choices[choiceIndex].PinCount} repère(s) · " +
                          $"{_choices[choiceIndex].LodOverrideCount} override(s) LOD";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        SetEditingEnabledUx2EnvJ(true);
        ResetReviewUx2EnvJ();
        SetStatusUx2EnvJ("Fiche chargée · prévisualisation sèche obligatoire");
        UpdateActionStateUx2EnvJ();
    }

    private void PopulateUx2EnvJ(StudioUx2EnvJDraft draft)
    {
        _updating = true;
        _mapEnabled.ButtonPressed = draft.MapEnabled;
        _fogOfWar.ButtonPressed = draft.FogOfWar;
        _projection.Text = draft.Projection;
        _floorCount.Text = draft.FloorCount;
        _generate2D.ButtonPressed = draft.Generate2D;
        _generate3D.ButtonPressed = draft.Generate3D;
        _explorationScope.Select((int)draft.ExplorationScope);
        _explorationCellSize.Text = draft.ExplorationCellSize;
        _revealApi.Text = draft.RevealApi;
        _lodMode.Select((int)draft.DefaultSourceMode);
        _distanceThresholds.Text = draft.DistanceThresholds;
        _maximumTriangles.Text = draft.MaximumVisibleTriangles;
        _maximumClusters.Text = draft.MaximumHlodClusters;
        _clusterSize.Text = draft.HlodClusterSize;
        _poppingTolerance.Text = draft.PoppingTolerance;
        _crossFade.ButtonPressed = draft.CrossFade;
        _measurePopping.ButtonPressed = draft.MeasurePopping;
        _pinDrafts = draft.Pins.ToList();
        _overrideDrafts = draft.AssetOverrides.ToList();
        _selectedPin = -1;
        _selectedOverride = -1;
        RefreshPinListUx2EnvJ();
        RefreshOverrideListUx2EnvJ();
        ClearPinEditorUx2EnvJ();
        ClearOverrideEditorUx2EnvJ();
        RefreshMapPreviewFloorsUx2EnvJ();
        UpdateMapCanvasUx2EnvJ(canonical: false);
        _updating = false;
    }

    private StudioUx2EnvJDraft? CaptureDraftUx2EnvJ()
    {
        if (_draft is null) return null;
        return _draft with
        {
            MapEnabled = _mapEnabled.ButtonPressed,
            FogOfWar = _fogOfWar.ButtonPressed,
            Projection = _projection.Text,
            FloorCount = _floorCount.Text,
            Generate2D = _generate2D.ButtonPressed,
            Generate3D = _generate3D.ButtonPressed,
            ExplorationScope = (AreaMapExplorationScope)_explorationScope.Selected,
            ExplorationCellSize = _explorationCellSize.Text,
            RevealApi = _revealApi.Text,
            Pins = _pinDrafts.ToArray(),
            DefaultSourceMode = (AreaLodSourceMode)_lodMode.Selected,
            DistanceThresholds = _distanceThresholds.Text,
            MaximumVisibleTriangles = _maximumTriangles.Text,
            MaximumHlodClusters = _maximumClusters.Text,
            HlodClusterSize = _clusterSize.Text,
            PoppingTolerance = _poppingTolerance.Text,
            CrossFade = _crossFade.ButtonPressed,
            MeasurePopping = _measurePopping.ButtonPressed,
            AssetOverrides = _overrideDrafts.ToArray(),
        };
    }

    private void SelectPinUx2EnvJ(long index)
    {
        if (_updating || index < 0 || index >= _pinDrafts.Count) return;
        _selectedPin = (int)index;
        var pin = _pinDrafts[_selectedPin];
        _updating = true;
        _pinLabel.Text = pin.Label;
        _pinX.Text = pin.X;
        _pinZ.Text = pin.Z;
        _pinFloor.Text = pin.Floor;
        _pinInitiallyVisible.ButtonPressed = pin.InitiallyVisible;
        _updating = false;
        UpdateCollectionActionsUx2EnvJ();
    }

    private void AddPinUx2EnvJ()
    {
        if (_draft is null) return;
        var item = new StudioUx2EnvJPinDraft(Guid.NewGuid(), "Nouveau repère", "0", "0", "0", false);
        _pinDrafts.Add(item);
        RefreshPinListUx2EnvJ();
        UpdateMapCanvasUx2EnvJ(canonical: false);
        _pins.Select(_pinDrafts.Count - 1);
        SelectPinUx2EnvJ(_pinDrafts.Count - 1);
        InvalidatePreviewUx2EnvJ();
    }

    private void RemovePinUx2EnvJ()
    {
        if (_selectedPin < 0 || _selectedPin >= _pinDrafts.Count) return;
        _pinDrafts.RemoveAt(_selectedPin);
        _selectedPin = -1;
        ClearPinEditorUx2EnvJ();
        RefreshPinListUx2EnvJ();
        UpdateMapCanvasUx2EnvJ(canonical: false);
        InvalidatePreviewUx2EnvJ();
    }

    private void UpdateSelectedPinUx2EnvJ()
    {
        if (_updating || _selectedPin < 0 || _selectedPin >= _pinDrafts.Count) return;
        var source = _pinDrafts[_selectedPin];
        _pinDrafts[_selectedPin] = source with
        {
            Label = _pinLabel.Text,
            X = _pinX.Text,
            Z = _pinZ.Text,
            Floor = _pinFloor.Text,
            InitiallyVisible = _pinInitiallyVisible.ButtonPressed,
        };
        RefreshPinListUx2EnvJ(_selectedPin);
        UpdateMapCanvasUx2EnvJ(canonical: false);
        InvalidatePreviewUx2EnvJ();
    }

    private void RefreshPinListUx2EnvJ(int restoreSelection = -1)
    {
        var wasUpdating = _updating;
        _updating = true;
        _pins.Clear();
        foreach (var pin in _pinDrafts)
            _pins.AddItem($"{HumanLabelUx2EnvJ(pin.Label)} · étage {pin.Floor} · " +
                          (pin.InitiallyVisible ? "visible" : "à révéler"));
        if (restoreSelection >= 0 && restoreSelection < _pinDrafts.Count)
            _pins.Select(restoreSelection);
        _updating = wasUpdating;
        UpdateCollectionActionsUx2EnvJ();
    }

    private void ClearPinEditorUx2EnvJ()
    {
        var wasUpdating = _updating;
        _updating = true;
        _pinLabel.Text = string.Empty;
        _pinX.Text = string.Empty;
        _pinZ.Text = string.Empty;
        _pinFloor.Text = string.Empty;
        _pinInitiallyVisible.ButtonPressed = false;
        _updating = wasUpdating;
    }

    private void SelectOverrideUx2EnvJ(long index)
    {
        if (_updating || index < 0 || index >= _overrideDrafts.Count) return;
        _selectedOverride = (int)index;
        var item = _overrideDrafts[_selectedOverride];
        _updating = true;
        _overrideAsset.Text = item.AssetId;
        _overrideMode.Select((int)item.SourceMode);
        _overrideRatios.Text = item.ScreenRatios;
        _overrideBudgets.Text = item.TriangleBudgets;
        _overrideCrossFade.Text = item.CrossFadeSeconds;
        _overrideProvenance.Text = item.Provenance;
        _updating = false;
        UpdateCollectionActionsUx2EnvJ();
    }

    private void AddOverrideUx2EnvJ()
    {
        if (_draft is null) return;
        var suffix = _overrideDrafts.Count + 1;
        var item = new StudioUx2EnvJLodOverrideDraft(
            Guid.NewGuid(), $"asset-a-configurer-{suffix}", AreaLodSourceMode.Hybrid,
            "1; 0.5; 0.2", "12000; 6000; 2400", "0.25", "studio-author");
        _overrideDrafts.Add(item);
        RefreshOverrideListUx2EnvJ();
        _overrides.Select(_overrideDrafts.Count - 1);
        SelectOverrideUx2EnvJ(_overrideDrafts.Count - 1);
        InvalidatePreviewUx2EnvJ();
    }

    private void RemoveOverrideUx2EnvJ()
    {
        if (_selectedOverride < 0 || _selectedOverride >= _overrideDrafts.Count) return;
        _overrideDrafts.RemoveAt(_selectedOverride);
        _selectedOverride = -1;
        ClearOverrideEditorUx2EnvJ();
        RefreshOverrideListUx2EnvJ();
        InvalidatePreviewUx2EnvJ();
    }

    private void UpdateSelectedOverrideUx2EnvJ()
    {
        if (_updating || _selectedOverride < 0 || _selectedOverride >= _overrideDrafts.Count) return;
        var source = _overrideDrafts[_selectedOverride];
        _overrideDrafts[_selectedOverride] = source with
        {
            AssetId = _overrideAsset.Text,
            SourceMode = (AreaLodSourceMode)_overrideMode.Selected,
            ScreenRatios = _overrideRatios.Text,
            TriangleBudgets = _overrideBudgets.Text,
            CrossFadeSeconds = _overrideCrossFade.Text,
            Provenance = _overrideProvenance.Text,
        };
        RefreshOverrideListUx2EnvJ(_selectedOverride);
        InvalidatePreviewUx2EnvJ();
    }

    private void RefreshOverrideListUx2EnvJ(int restoreSelection = -1)
    {
        var wasUpdating = _updating;
        _updating = true;
        _overrides.Clear();
        foreach (var item in _overrideDrafts)
            _overrides.AddItem($"{HumanLabelUx2EnvJ(item.AssetId)} · {HumanLodModeUx2EnvJ(item.SourceMode)}");
        if (restoreSelection >= 0 && restoreSelection < _overrideDrafts.Count)
            _overrides.Select(restoreSelection);
        _updating = wasUpdating;
        UpdateCollectionActionsUx2EnvJ();
    }

    private void ClearOverrideEditorUx2EnvJ()
    {
        var wasUpdating = _updating;
        _updating = true;
        _overrideAsset.Text = string.Empty;
        _overrideMode.Select(0);
        _overrideRatios.Text = string.Empty;
        _overrideBudgets.Text = string.Empty;
        _overrideCrossFade.Text = string.Empty;
        _overrideProvenance.Text = string.Empty;
        _updating = wasUpdating;
    }

    private void UpdateCollectionActionsUx2EnvJ()
    {
        var enabled = _draft is not null;
        _addPin.Disabled = !enabled;
        _removePin.Disabled = !enabled || _selectedPin < 0 || _selectedPin >= _pinDrafts.Count;
        _addOverride.Disabled = !enabled;
        _removeOverride.Disabled = !enabled || _selectedOverride < 0 ||
                                   _selectedOverride >= _overrideDrafts.Count;
    }

    private void InvalidatePreviewUx2EnvJ()
    {
        if (_updating) return;
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _applied = false;
        _session.CancelMapLodApprovalUx2EnvJ();
        ResetReviewUx2EnvJ();
        SetStatusUx2EnvJ("Réglages modifiés · relancez la prévisualisation");
        UpdateActionStateUx2EnvJ();
    }

    private void PreviewUx2EnvJ()
    {
        var draft = CaptureDraftUx2EnvJ();
        if (draft is null)
        {
            SetStatusUx2EnvJ("Sélectionnez explicitement une zone.", error: true);
            return;
        }
        _draft = draft;
        _lastPreview = _session.PreviewMapLodUx2EnvJ(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded && _lastPreview.CanonicalDryRun;
        _applied = false;
        RenderPreviewUx2EnvJ(_lastPreview);
        SetStatusUx2EnvJ(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionStateUx2EnvJ();
    }

    private void ApplyUx2EnvJ()
    {
        if (!_canonicalPreviewReady)
        {
            SetStatusUx2EnvJ("Relancez l’aperçu sec avant d’appliquer.", error: true);
            return;
        }
        var result = _session.ApplyApprovedMapLodUx2EnvJ();
        _canonicalPreviewReady = false;
        _applied = result.Succeeded;
        if (result.Succeeded) MapLodChanged?.Invoke();
        SetStatusUx2EnvJ(result.Message, !result.Succeeded);
        UpdateActionStateUx2EnvJ();
    }

    private void UndoUx2EnvJ()
    {
        var result = _session.Undo();
        if (result.Succeeded) _applied = false;
        ReloadAfterHistoryUx2EnvJ();
        SetStatusUx2EnvJ(result.Message, !result.Succeeded);
    }

    private void RedoUx2EnvJ()
    {
        var result = _session.Redo();
        if (result.Succeeded) _applied = true;
        ReloadAfterHistoryUx2EnvJ();
        SetStatusUx2EnvJ(result.Message, !result.Succeeded);
    }

    private void SaveUx2EnvJ()
    {
        var result = _session.Save();
        UpdateActionStateUx2EnvJ();
        SetStatusUx2EnvJ(result.Message, !result.Succeeded);
    }

    private void ReloadAfterHistoryUx2EnvJ()
    {
        if (_draft is not null)
        {
            _draft = _session.CreateMapLodDraftUx2EnvJ(_draft.AreaId);
            if (_draft is not null) PopulateUx2EnvJ(_draft);
        }
        _canonicalPreviewReady = false;
        _lastPreview = null;
        ResetReviewUx2EnvJ();
        UpdateActionStateUx2EnvJ();
    }

    private void RenderPreviewUx2EnvJ(StudioUx2EnvJPreview preview)
    {
        ClearChildrenUx2EnvJ(_mapPlan);
        ClearChildrenUx2EnvJ(_hlodPlan);
        ClearChildrenUx2EnvJ(_diff);
        ClearChildrenUx2EnvJ(_diagnostics);
        foreach (var floor in preview.Floors)
            _mapPlan.AddChild(StudioStyle.Label(
                $"Étage {floor.Floor} · {floor.PinCount} repère(s) · {floor.ExplorationCellCount} cellule(s) · " +
                $"{(floor.Generate2D ? "plan 2D" : "sans 2D")} · " +
                $"{(floor.Generate3D ? "plan 3D" : "sans 3D")} · " +
                $"{(floor.FogOfWar ? "brouillard" : "révélé")}",
                9, StudioStyle.Text, wrap: true));
        if (preview.Reveal is { } reveal)
            _mapPlan.AddChild(StudioStyle.Label(
                $"Exploration {reveal.Scope} · {reveal.Summary}",
                9, StudioStyle.Accent, wrap: true));
        if (preview.Floors.Count == 0)
            _mapPlan.AddChild(StudioStyle.Label("Aucun plan carte disponible.", 9,
                StudioStyle.Muted, wrap: true));
        foreach (var hook in preview.HlodHooks)
            _hlodPlan.AddChild(StudioStyle.Label(
                $"Cellule {hook.CellX}, {hook.CellZ} · {hook.SourceEntityCount} source(s) · " +
                $"budget {hook.MaximumTriangles:N0} triangles · génération planifiée",
                9, StudioStyle.Text, wrap: true));
        if (preview.HlodHooks.Count == 0)
            _hlodPlan.AddChild(StudioStyle.Label(
                "Aucun cluster à générer pour ce mode ou cette distribution; le plan reste valide.",
                9, StudioStyle.Muted, wrap: true));
        _performancePlan.Text = preview.Performance is { } performance
            ? $"{performance.EntityCount} entité(s) · {performance.UniqueAssetCount} asset(s) · " +
              $"{performance.EstimatedDrawUnits} unité(s) de dessin · {performance.OccupiedSectors} secteur(s) · " +
              $"LOD proche/moyen/lointain {performance.NearLodCount}/{performance.MidLodCount}/{performance.FarLodCount} · " +
              $"popping {performance.EstimatedPopping} · " +
              (performance.WithinBudget ? "budget respecté" : "avertissements affichés")
            : "Rapport de performance indisponible.";
        foreach (var entry in preview.Diff.Entries)
            _diff.AddChild(StudioStyle.Label($"• {entry}", 9, StudioStyle.Text, wrap: true));
        if (preview.Diff.Entries.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucune différence.", 9, StudioStyle.Muted, wrap: true));
        foreach (var group in preview.Groups)
        {
            var color = group.Diagnostics.Any(item => item.Blocking)
                ? StudioStyle.Warning
                : group.Diagnostics.Count > 0 ? StudioStyle.Muted : StudioStyle.Accent;
            _diagnostics.AddChild(StudioStyle.Label(
                $"{group.Title} · {group.State}", 10, color, wrap: true));
            foreach (var diagnostic in group.Diagnostics)
                _diagnostics.AddChild(StudioStyle.Label(
                    $"  • {diagnostic.Message}", 9,
                    diagnostic.Blocking ? StudioStyle.Warning : StudioStyle.Muted, wrap: true));
        }
        _summary.Text = string.IsNullOrWhiteSpace(preview.Summary) ? preview.Message : preview.Summary;
        UpdateMapCanvasUx2EnvJ(canonical: preview.Succeeded && preview.RenderPlanBuilt);
    }

    private void ResetReviewUx2EnvJ()
    {
        ClearChildrenUx2EnvJ(_mapPlan);
        ClearChildrenUx2EnvJ(_hlodPlan);
        ClearChildrenUx2EnvJ(_diff);
        ClearChildrenUx2EnvJ(_diagnostics);
        _mapPlan.AddChild(StudioStyle.Label("Plan carte en attente.", 9, StudioStyle.Muted, wrap: true));
        _hlodPlan.AddChild(StudioStyle.Label("Plan HLOD en attente.", 9, StudioStyle.Muted, wrap: true));
        _performancePlan.Text = "Rapport de performance en attente.";
        _diff.AddChild(StudioStyle.Label("Aucun aperçu calculé.", 9, StudioStyle.Muted, wrap: true));
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 9, StudioStyle.Muted, wrap: true));
        _summary.Text = "Sélectionnez une zone puis prévisualisez · aucun changement appliqué.";
        UpdateMapCanvasUx2EnvJ(canonical: false);
    }

    private void UpdateActionStateUx2EnvJ()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !_canonicalPreviewReady || _lastPreview?.CanApply != true;
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
        UpdateCollectionActionsUx2EnvJ();
    }

    private void SetEditingEnabledUx2EnvJ(bool enabled)
    {
        foreach (var edit in MainLineEditsUx2EnvJ()) edit.Editable = enabled;
        foreach (var edit in PinLineEditsUx2EnvJ()) edit.Editable = enabled;
        foreach (var edit in OverrideLineEditsUx2EnvJ()) edit.Editable = enabled;
        foreach (var option in new[] { _explorationScope, _lodMode, _overrideMode }) option.Disabled = !enabled;
        _mapPreviewFloor.Disabled = !enabled;
        foreach (var check in new[]
                 {
                     _mapEnabled, _fogOfWar, _generate2D, _generate3D, _pinInitiallyVisible,
                     _crossFade, _measurePopping,
                 }) check.Disabled = !enabled;
        _pins.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _overrides.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        UpdateCollectionActionsUx2EnvJ();
    }

    private bool ConfigureEvidenceDraftUx2EnvJ()
    {
        var current = CaptureDraftUx2EnvJ();
        if (current is null) return false;
        var floorCount = 2;
        if (int.TryParse(current.FloorCount, out var sourceFloors)) floorCount = Math.Max(2, sourceFloors + 1);
        var pin = new StudioUx2EnvJPinDraft(Guid.NewGuid(), "Belvédère révélé", "12.5", "-4.25",
            (floorCount - 1).ToString(), true);
        var overrideAsset = "architecture/studio-preview";
        if (current.AssetOverrides.Any(item => item.AssetId.Equals(overrideAsset, StringComparison.Ordinal)))
            overrideAsset = "architecture/studio-preview-alt";
        var item = new StudioUx2EnvJLodOverrideDraft(
            Guid.NewGuid(), overrideAsset, AreaLodSourceMode.Generated,
            "1; 0.5; 0.2", "12000; 6000; 2400", "0.25", "studio-evidence");
        var draft = current with
        {
            MapEnabled = true,
            FogOfWar = true,
            Projection = string.IsNullOrWhiteSpace(current.Projection) ? "orthographic" : current.Projection,
            FloorCount = floorCount.ToString(),
            Generate2D = true,
            Generate3D = true,
            ExplorationScope = AreaMapExplorationScope.World,
            ExplorationCellSize = "6.5",
            RevealApi = string.IsNullOrWhiteSpace(current.RevealApi) ? "area.map.reveal" : current.RevealApi,
            Pins = [.. current.Pins, pin],
            DefaultSourceMode = AreaLodSourceMode.Hybrid,
            DistanceThresholds = "18; 42; 96",
            MaximumVisibleTriangles = "1500000",
            MaximumHlodClusters = "256",
            HlodClusterSize = "32",
            PoppingTolerance = "0.15",
            CrossFade = true,
            MeasurePopping = true,
            AssetOverrides = [.. current.AssetOverrides, item],
        };
        _draft = draft;
        PopulateUx2EnvJ(draft);
        return true;
    }

    private void RefreshMapPreviewFloorsUx2EnvJ()
    {
        var selected = Math.Max(0, _mapPreviewFloor.Selected);
        var floors = 1;
        if (!int.TryParse(_floorCount.Text, out floors) || floors < 1) floors = 1;
        floors = Math.Min(floors, 256);
        var wasUpdating = _updating;
        _updating = true;
        _mapPreviewFloor.Clear();
        for (var floor = 0; floor < floors; floor++) _mapPreviewFloor.AddItem($"Étage {floor}");
        _mapPreviewFloor.Select(Math.Min(selected, floors - 1));
        _updating = wasUpdating;
    }

    private void UpdateMapCanvasUx2EnvJ(bool canonical)
    {
        if (_mapCanvas is null) return;
        _mapCanvas.SetDraft(_pinDrafts, Math.Max(0, _mapPreviewFloor.Selected),
            _fogOfWar.ButtonPressed, canonical);
    }

    private StudioUx2EnvJEvidenceSourceSnapshot CaptureSourceSnapshotUx2EnvJ()
    {
        var history = _sourceSession.History ??
                      throw new InvalidOperationException("The source history is unavailable.");
        var project = _sourceSession.Project ??
                      throw new InvalidOperationException("The source project is unavailable.");
        return new StudioUx2EnvJEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureMapLodApprovalUx2EnvJ());
    }

    private bool SourceSnapshotMatchesUx2EnvJ(StudioUx2EnvJEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _evidenceSourceSnapshot;
        var history = _sourceSession.History;
        if (snapshot is null || history is null) return false;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureMapLodApprovalUx2EnvJ() == snapshot.Approval;
    }

    private bool ReleaseIsolatedEvidenceSessionUx2EnvJ()
    {
        var evidence = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        var sessionReleased = true;
        if (evidence is not null)
        {
            try
            {
                evidence.Close();
            }
            catch (Exception exception)
            {
                sessionReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_ENV_J_RELEASE_FAILED " +
                            $"type={exception.GetType().Name} message={exception.Message}");
            }
        }
        var rootReleased = StudioSessionController.DeleteUx2EnvJTemporaryWorkspace(tempRoot);
        ResetEvidenceStateUx2EnvJ();
        _session = _sourceSession;
        _evidenceReleaseSucceeded = _evidenceReleaseSucceeded && sessionReleased && rootReleased;
        if (!rootReleased) _evidenceTempRoot = tempRoot;
        return sessionReleased && rootReleased;
    }

    private void ResetEvidenceStateUx2EnvJ()
    {
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceUsesRealModuleData = false;
        _evidenceUsesCanonicalFallback = false;
        _evidenceSourceSnapshot = null;
    }

    private void WireDraftChangesUx2EnvJ()
    {
        foreach (var edit in MainLineEditsUx2EnvJ()) edit.TextChanged += _ =>
        {
            if (ReferenceEquals(edit, _floorCount)) RefreshMapPreviewFloorsUx2EnvJ();
            InvalidatePreviewUx2EnvJ();
        };
        foreach (var option in new[] { _explorationScope, _lodMode })
            option.ItemSelected += _ => InvalidatePreviewUx2EnvJ();
        foreach (var check in new[]
                 {
                     _mapEnabled, _fogOfWar, _generate2D, _generate3D, _crossFade, _measurePopping,
                 }) check.Toggled += _ =>
        {
            if (ReferenceEquals(check, _fogOfWar)) UpdateMapCanvasUx2EnvJ(canonical: false);
            InvalidatePreviewUx2EnvJ();
        };
        foreach (var edit in PinLineEditsUx2EnvJ()) edit.TextChanged += _ => UpdateSelectedPinUx2EnvJ();
        _pinInitiallyVisible.Toggled += _ => UpdateSelectedPinUx2EnvJ();
        foreach (var edit in OverrideLineEditsUx2EnvJ())
            edit.TextChanged += _ => UpdateSelectedOverrideUx2EnvJ();
        _overrideMode.ItemSelected += _ => UpdateSelectedOverrideUx2EnvJ();
    }

    private IReadOnlyList<LineEdit> MainLineEditsUx2EnvJ() =>
        [_projection, _floorCount, _explorationCellSize, _revealApi, _distanceThresholds,
            _maximumTriangles, _maximumClusters, _clusterSize, _poppingTolerance];

    private IReadOnlyList<LineEdit> PinLineEditsUx2EnvJ() =>
        [_pinLabel, _pinX, _pinZ, _pinFloor];

    private IReadOnlyList<LineEdit> OverrideLineEditsUx2EnvJ() =>
        [_overrideAsset, _overrideRatios, _overrideBudgets, _overrideCrossFade, _overrideProvenance];

    private void SetStatusUx2EnvJ(string text, bool error = false)
    {
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private void UpdateDialogBoundsUx2EnvJ()
    {
        var viewport = GetViewportRect().Size;
        var margin = viewport.X >= 2400 ? 70f : 28f;
        _dialog.Position = new Vector2(margin, margin);
        _dialog.Size = new Vector2(Math.Max(1180, viewport.X - margin * 2),
            Math.Max(720, viewport.Y - margin * 2));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateDialogBoundsUx2EnvJ();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true } key) return;
        if (key.Keycode == Key.Escape) Close();
        if (key.CtrlPressed && key.Keycode == Key.Enter) PreviewUx2EnvJ();
    }

    public override void _ExitTree()
    {
        ReleaseIsolatedEvidenceSessionUx2EnvJ();
    }

    private static LineEdit TextUx2EnvJ(string placeholder) => StudioUx2EnvBControls.Text(placeholder);

    private static GridContainer GridUx2EnvJ(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static string HumanLabelUx2EnvJ(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) || ContainsUuidUx2EnvJ(text) ? "Valeur à nommer" : text;
    }

    private static string HumanLodModeUx2EnvJ(AreaLodSourceMode mode) => mode switch
    {
        AreaLodSourceMode.Imported => "importé",
        AreaLodSourceMode.Generated => "généré",
        AreaLodSourceMode.Hybrid => "hybride",
        _ => "inconnu",
    };

    private static void ClearChildrenUx2EnvJ(Node node)
    {
        foreach (var child in node.GetChildren()) child.Free();
    }

    private static IEnumerable<string> DescendantVisibleTextUx2EnvJ(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when !string.IsNullOrWhiteSpace(label.Text):
                    yield return label.Text;
                    break;
                case Button button when !string.IsNullOrWhiteSpace(button.Text):
                    yield return button.Text;
                    break;
                case LineEdit edit when !string.IsNullOrWhiteSpace(edit.Text):
                    yield return edit.Text;
                    break;
                case OptionButton option:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case ItemList list:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
            }
            foreach (var descendant in DescendantVisibleTextUx2EnvJ(child)) yield return descendant;
        }
    }

    private static bool ContainsUuidUx2EnvJ(string text) => System.Text.RegularExpressions.Regex.IsMatch(
        text, "(?i)(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})");
}
