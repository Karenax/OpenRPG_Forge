using System.Globalization;
using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTerrainBrushCanvasUx2C : Control
{
    private float _width = 40;
    private float _depth = 40;
    private float _worldX;
    private float _worldZ;
    private float _radius = 4;

    internal event Action<float, float>? TargetChanged;
    internal bool DirectInputExercised { get; private set; }

    internal StudioTerrainBrushCanvasUx2C()
    {
        Name = "TerrainBrushCanvasUx2C";
        CustomMinimumSize = new Vector2(420, 300);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Cross;
        FocusMode = FocusModeEnum.All;
    }

    internal void Configure(float width, float depth, float worldX, float worldZ, float radius)
    {
        _width = MathF.Max(1, width);
        _depth = MathF.Max(1, depth);
        _worldX = worldX;
        _worldZ = worldZ;
        _radius = MathF.Max(.01f, radius);
        QueueRedraw();
    }

    internal bool RunDirectInputEvidence()
    {
        var size = Size.X > 20 && Size.Y > 20 ? Size : CustomMinimumSize;
        _GuiInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = new Vector2(size.X * .63f, size.Y * .42f),
        });
        return DirectInputExercised;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), StudioStyle.Background, filled: true);
        for (var index = 0; index <= 10; index++)
        {
            var x = Size.X * index / 10f;
            var z = Size.Y * index / 10f;
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), StudioStyle.Border, 1);
            DrawLine(new Vector2(0, z), new Vector2(Size.X, z), StudioStyle.Border, 1);
        }

        if (Size.X <= 0 || Size.Y <= 0) return;
        var point = new Vector2(
            (_worldX / _width + .5f) * Size.X,
            (_worldZ / _depth + .5f) * Size.Y);
        var radius = MathF.Max(5, MathF.Min(Size.X * _radius / _width, Size.Y * _radius / _depth));
        DrawCircle(point, radius, new Color(StudioStyle.Accent, .18f));
        DrawArc(point, radius, 0, MathF.Tau, 48, StudioStyle.Accent, 2);
        DrawLine(point - new Vector2(8, 0), point + new Vector2(8, 0), StudioStyle.Success, 2);
        DrawLine(point - new Vector2(0, 8), point + new Vector2(0, 8), StudioStyle.Success, 2);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse &&
            @event is not InputEventMouseMotion { ButtonMask: MouseButtonMask.Left }) return;
        var position = @event is InputEventMouseButton button ? button.Position : ((InputEventMouseMotion)@event).Position;
        var size = Size.X > 20 && Size.Y > 20 ? Size : CustomMinimumSize;
        _worldX = Math.Clamp((position.X / size.X - .5f) * _width, -_width / 2, _width / 2);
        _worldZ = Math.Clamp((position.Y / size.Y - .5f) * _depth, -_depth / 2, _depth / 2);
        DirectInputExercised = true;
        TargetChanged?.Invoke(_worldX, _worldZ);
        QueueRedraw();
        AcceptEvent();
    }
}

internal sealed partial class StudioTerrainSurfaceWorkspaceUx2C : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private readonly OptionButton _area;
    private readonly TabContainer _tabs;
    private readonly StudioTerrainBrushCanvasUx2C _brushCanvas;
    private readonly OptionButton _brushOperation;
    private readonly SpinBox _worldX;
    private readonly SpinBox _worldZ;
    private readonly SpinBox _radius;
    private readonly SpinBox _strength;
    private readonly SpinBox _targetHeight;
    private readonly OptionButton _material;
    private readonly OptionButton _feature;
    private readonly Button _newFeature;
    private readonly LineEdit _featureName;
    private readonly OptionButton _featureKind;
    private readonly TextEdit _featurePoints;
    private readonly SpinBox _featureWidth;
    private readonly SpinBox _featureHeight;
    private readonly SpinBox _featureBank;
    private readonly OptionButton _featureCrossing;
    private readonly LineEdit _featureTags;
    private readonly LineEdit _featureMaterial;
    private readonly OptionButton _placement;
    private readonly LineEdit _semanticState;
    private readonly LineEdit _animation;
    private readonly CheckBox _loopAnimation;
    private readonly TextEdit _materialOverrides;
    private readonly CheckBox _lightEnabled;
    private readonly LineEdit _lightColor;
    private readonly SpinBox _lightIntensity;
    private readonly SpinBox _lightRange;
    private readonly CheckBox _collision;
    private readonly CheckBox _navigation;
    private readonly LineEdit _sockets;
    private readonly TextEdit _propertyOverrides;
    private readonly Label _summary;
    private readonly Label _diagnostics;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2CAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2CNamedChoice> _materialChoices = [];
    private IReadOnlyList<StudioUx2CNamedChoice> _featureChoices = [];
    private IReadOnlyList<StudioUx2CPlacementChoice> _placementChoices = [];
    private StudioUx2CTerrainDraft? _draft;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? TerrainChanged;
    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady { get; private set; }
    internal bool DirectBrushInputExercised => _brushCanvas.DirectInputExercised;
    internal bool ShowsAllModes => _tabs.GetTabCount() == 3 &&
                                   _tabs.GetTabTitle(0).Contains("Sculpter", StringComparison.Ordinal) &&
                                   _tabs.GetTabTitle(1).Contains("Features", StringComparison.Ordinal) &&
                                   _tabs.GetTabTitle(2).Contains("Propriétés", StringComparison.Ordinal);
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', _summary.Text, _diagnostics.Text, _status.Text,
                _area.GetItemText(Math.Max(0, _area.Selected)), _tabs.GetTabTitle(_tabs.CurrentTab));
            return text.Contains("Aperçu", StringComparison.CurrentCultureIgnoreCase) &&
                   (text.Contains("terrain", StringComparison.CurrentCultureIgnoreCase) ||
                    text.Contains("relief", StringComparison.CurrentCultureIgnoreCase)) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioTerrainSurfaceWorkspaceUx2C(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioTerrainSurfaceWorkspaceUx2C";
        Visible = false;
        ZIndex = 475;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0, 0, 0, .8f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var panel = StudioStyle.Panel("TerrainSurfaceDialogUx2C", StudioStyle.Chrome, 12, 2);
        panel.AnchorLeft = .025f;
        panel.AnchorTop = .035f;
        panel.AnchorRight = .975f;
        panel.AnchorBottom = .965f;
        AddChild(panel);
        var margin = StudioStyle.Margin(16, 12, 16, 12);
        panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer();
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddChild(StudioStyle.Label("TERRAIN & TILESET · UX2", 10, StudioStyle.Accent));
        title.AddChild(StudioStyle.Label("Relief, features et propriétés de tuile", 20));
        title.AddChild(StudioStyle.Label(
            "Édition directe · aperçu sec canonique · collision/navigation authorées sans simulation moteur",
            10, StudioStyle.Muted));
        header.AddChild(title);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selector = new HBoxContainer();
        selector.AddChild(StudioStyle.Label("Zone", 10, StudioStyle.Muted));
        _area = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_area);
        _area.ItemSelected += index => SelectArea((int)index);
        selector.AddChild(_area);
        root.AddChild(selector);

        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _tabs.CustomMinimumSize = new Vector2(0, 430);
        _tabs.TabChanged += index => ChangeMode((int)index);
        root.AddChild(_tabs);

        var sculpt = Tab("Sculpter le terrain");
        var sculptBody = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        sculptBody.AddThemeConstantOverride("separation", 9);
        sculpt.AddChild(sculptBody);
        var canvasPanel = StudioStyle.Panel("TerrainBrushCanvasPanelUx2C", StudioStyle.Background, 7);
        canvasPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        canvasPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        sculptBody.AddChild(canvasPanel);
        _brushCanvas = new StudioTerrainBrushCanvasUx2C();
        canvasPanel.AddChild(_brushCanvas);
        var brushForm = Form(sculptBody, 340);
        _brushOperation = Field(brushForm, "Brosse", new OptionButton());
        StudioStyle.ApplyOptionButton(_brushOperation);
        foreach (var label in new[] { "Élever", "Abaisser", "Aplanir", "Lisser", "Peindre", "Ouvrir un trou", "Refermer un trou" })
            _brushOperation.AddItem(label);
        _worldX = Field(brushForm, "Position X", Number(-100000, 100000, .25, " m"));
        _worldZ = Field(brushForm, "Position Z", Number(-100000, 100000, .25, " m"));
        _radius = Field(brushForm, "Rayon", Number(.001, 100000, .25, " m"));
        _strength = Field(brushForm, "Intensité", Number(-100000, 100000, .05));
        _targetHeight = Field(brushForm, "Hauteur cible", Number(-100000, 100000, .25, " m"));
        _material = Field(brushForm, "Matériau", new OptionButton());
        StudioStyle.ApplyOptionButton(_material);
        _brushCanvas.TargetChanged += (x, z) =>
        {
            _updating = true;
            var roundedX = Math.Round(x, 3, MidpointRounding.AwayFromZero);
            var roundedZ = Math.Round(z, 3, MidpointRounding.AwayFromZero);
            _worldX.Value = roundedX;
            _worldZ.Value = roundedZ;
            _worldX.GetLineEdit().Text = roundedX.ToString("0.###", CultureInfo.InvariantCulture);
            _worldZ.GetLineEdit().Text = roundedZ.ToString("0.###", CultureInfo.InvariantCulture);
            _updating = false;
            InvalidatePreview();
        };
        brushForm.AddChild(StudioStyle.Label(
            "Cliquez ou glissez sur la carte pour placer la brosse. L’aperçu ne reconstruit pas le terrain Godot.",
            10, StudioStyle.Success, wrap: true));

        var featureTab = Tab("Features du tileset");
        var featureBody = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        featureBody.AddThemeConstantOverride("separation", 9);
        featureTab.AddChild(featureBody);
        var featureList = Form(featureBody, 300);
        _feature = Field(featureList, "Feature existante", new OptionButton());
        StudioStyle.ApplyOptionButton(_feature);
        _newFeature = StudioStyle.Button("Nouvelle feature", primary: true);
        featureList.AddChild(_newFeature);
        featureList.AddChild(StudioStyle.Label(
            "Routes, rivières, murs, ponts, falaises et bâtiments restent une collection complète.",
            10, StudioStyle.Muted, wrap: true));
        var featureScroll = ScrollForm(featureBody);
        _featureName = Field(featureScroll, "Nom", Line("Ex. Chemin de ronde"));
        _featureKind = Field(featureScroll, "Type", new OptionButton());
        StudioStyle.ApplyOptionButton(_featureKind);
        foreach (var label in new[] { "Route", "Rivière", "Mur", "Pont", "Falaise", "Bâtiment", "Détail" })
            _featureKind.AddItem(label);
        _featurePoints = Field(featureScroll, "Points X,Y,Z · un par ligne", Multiline("-5,0,0\n0,0,3\n5,0,1", 92));
        _featureWidth = Field(featureScroll, "Largeur", Number(0, 100000, .25, " m"));
        _featureHeight = Field(featureScroll, "Hauteur", Number(-100000, 100000, .1, " m"));
        _featureBank = Field(featureScroll, "Dévers", Number(-3600, 3600, 1, "°"));
        _featureCrossing = Field(featureScroll, "Croisement", new OptionButton());
        StudioStyle.ApplyOptionButton(_featureCrossing);
        foreach (var label in new[] { "Autorisé", "Pont requis", "Interdit" }) _featureCrossing.AddItem(label);
        _featureTags = Field(featureScroll, "Biomes compatibles", Line("forêt, prairie"));
        _featureMaterial = Field(featureScroll, "Ressource matériau", Line("Chemin optionnel"));

        var propertyTab = Tab("Propriétés de tuile");
        var propertyBody = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        propertyBody.AddThemeConstantOverride("separation", 9);
        propertyTab.AddChild(propertyBody);
        var propertyList = Form(propertyBody, 310);
        _placement = Field(propertyList, "Tuile placée", new OptionButton());
        StudioStyle.ApplyOptionButton(_placement);
        propertyList.AddChild(StudioStyle.Label(
            "Chaque fiche conserve animation, matériaux, lumière, collision, navigation, sockets et attributs avancés.",
            10, StudioStyle.Muted, wrap: true));
        var propertyScroll = ScrollForm(propertyBody);
        _semanticState = Field(propertyScroll, "État sémantique", Line("normal"));
        _animation = Field(propertyScroll, "Animation", Line("Optionnelle"));
        _loopAnimation = new CheckBox { Text = "Animation en boucle" };
        propertyScroll.AddChild(_loopAnimation);
        _materialOverrides = Field(propertyScroll, "Matériaux · clé=valeur", Multiline("floor=moss", 70));
        _lightEnabled = new CheckBox { Text = "Lumière activée" };
        propertyScroll.AddChild(_lightEnabled);
        _lightColor = Field(propertyScroll, "Couleur lumière", Line("#ffffff"));
        _lightIntensity = Field(propertyScroll, "Intensité lumière", Number(0, 100000, .1));
        _lightRange = Field(propertyScroll, "Portée lumière", Number(0, 100000, .25, " m"));
        _collision = new CheckBox { Text = "Collision activée", ButtonPressed = true };
        _navigation = new CheckBox { Text = "Navigation activée", ButtonPressed = true };
        propertyScroll.AddChild(_collision);
        propertyScroll.AddChild(_navigation);
        _sockets = Field(propertyScroll, "Sockets", Line("passage, mur-nord"));
        _propertyOverrides = Field(propertyScroll, "Attributs avancés · clé=valeur", Multiline("audio.step=stone", 70));

        _tabs.AddChild(sculpt);
        _tabs.SetTabTitle(0, "Sculpter");
        _tabs.AddChild(featureTab);
        _tabs.SetTabTitle(1, "Features");
        _tabs.AddChild(propertyTab);
        _tabs.SetTabTitle(2, "Propriétés de tuile");

        var review = StudioStyle.Panel("TerrainSurfaceReviewUx2C", StudioStyle.Surface, 8);
        review.CustomMinimumSize = new Vector2(0, 106);
        root.AddChild(review);
        var reviewMargin = StudioStyle.Margin(10, 8, 10, 8);
        review.AddChild(reviewMargin);
        var reviewBody = new VBoxContainer();
        reviewMargin.AddChild(reviewBody);
        _summary = StudioStyle.Label("Aucun aperçu. Choisissez explicitement une zone.", 11, StudioStyle.Text, wrap: true);
        _diagnostics = StudioStyle.Label("Les collections cachées restent intactes.", 10, StudioStyle.Muted, wrap: true);
        reviewBody.AddChild(_summary);
        reviewBody.AddChild(_diagnostics);
        _status = StudioStyle.Label("Sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Aperçu sec : aucune écriture. Appliquer : une commande canonique annulable. Aucune simulation collision/navigation.",
            10, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _preview = Action(actions, "Prévisualiser", Preview, true);
        _apply = Action(actions, "Appliquer", Apply);
        _undo = Action(actions, "Annuler", Undo);
        _redo = Action(actions, "Rétablir", Redo);
        _save = Action(actions, "Enregistrer", Save);
        root.AddChild(actions);

        WireInvalidation();
        SetProcessUnhandledKeyInput(true);
        UpdateActions();
    }

    internal void Open()
    {
        CloseEvidenceSession();
        _session = _sourceSession;
        Visible = true;
        LoadAreas();
        SetStatus("Choisissez explicitement une zone ; rien n’est ciblé automatiquement.");
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null) return;
        Visible = false;
        _session.CancelTerrainSurfaceApprovalUx2C();
        _draft = null;
        CanonicalPreviewReady = false;
        CloseEvidenceSession();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidenceSession();
        var sourceProject = _sourceSession.Project;
        var evidenceProject = _sourceSession.CreateTerrainSurfaceEvidenceProjectUx2C();
        if (sourceProject is null || evidenceProject is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux2c-ui-" + Guid.NewGuid().ToString("N"));
        if (!ProjectSession.Create(root, evidenceProject).Succeeded) return DeleteFailedEvidence(root);
        var controller = new StudioSessionController();
        if (!controller.Open(root).Succeeded)
        {
            controller.Close();
            return DeleteFailedEvidence(root);
        }
        _evidenceRoot = root;
        _evidenceSession = controller;
        _session = controller;
        Visible = true;
        LoadAreas();
        var target = FindIndex(_areaChoices, item =>
            controller.Project?.Areas.Single(area => area.Id == item.AreaId).Slug == "atelier-surface");
        if (target < 0) return FailEvidence();
        _area.Select(target + 1);
        SelectArea(target + 1);
        if (_draft is null || _featureChoices.Count < 2 || _placementChoices.Count < 1) return FailEvidence();
        _tabs.CurrentTab = 0;
        ChangeMode(0);
        _brushOperation.Select((int)TerrainBrushOperation.Raise);
        _radius.Value = 5.125;
        _strength.Value = .1375;
        _brushCanvas.RunDirectInputEvidence();
        Preview();
        return CanonicalPreviewReady && UsesIsolatedEvidence && DirectBrushInputExercised && ShowsAllModes;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceProject = _sourceSession.Project;
        var sourceHistory = _sourceSession.History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = _sourceSession.IsDirty;
        var sourceApproval = _sourceSession.CaptureTerrainSurfaceApprovalUx2C();
        try
        {
            if (!PrepareEvidence("ux2-c-terrain-surface") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var revision = _session.History?.State.Revision;
            Apply();
            var applied = _session.History?.State.Revision != revision && _session.CanUndo;
            Undo();
            var undone = _session.CanRedo;
            Redo();
            var redone = _session.CanUndo;
            Undo();
            return applied && undone && redone && DirectBrushInputExercised;
        }
        finally
        {
            Close();
            _sourceSession.RestoreTerrainSurfaceApprovalUx2C(sourceApproval);
            var history = _sourceSession.History;
            if (!ReferenceEquals(sourceProject, _sourceSession.Project) ||
                !ReferenceEquals(sourceHistory, history) || history?.State.Revision != sourceRevision ||
                !(history?.Journal ?? []).SequenceEqual(sourceJournal) || _sourceSession.IsDirty != sourceDirty)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2C_SOURCE_CHANGED");
        }
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = true;
        LoadAreas();
    }

    private void LoadAreas()
    {
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        _areaChoices = _session.TerrainSurfaceAreaChoicesUx2C;
        foreach (var choice in _areaChoices) _area.AddItem(choice.Label);
        _area.Select(0);
        ResetArea();
        _updating = false;
    }

    private void SelectArea(int index)
    {
        if (_updating) return;
        _session.CancelTerrainSurfaceApprovalUx2C();
        if (index <= 0 || index > _areaChoices.Count)
        {
            ResetArea();
            return;
        }
        _updating = true;
        var choice = _areaChoices[index - 1];
        _draft = _session.CreateTerrainSurfaceDraftUx2C(choice.AreaId);
        _materialChoices = _session.TerrainMaterialChoicesUx2C(choice.AreaId);
        _featureChoices = _session.TerrainFeatureChoicesUx2C(choice.AreaId);
        _placementChoices = _session.TerrainPropertyPlacementChoicesUx2C(choice.AreaId);
        _material.Clear();
        foreach (var item in _materialChoices) _material.AddItem(item.Label);
        if (_materialChoices.Count > 0) _material.Select(0);
        _feature.Clear();
        _feature.AddItem("Choisir une feature…");
        foreach (var item in _featureChoices) _feature.AddItem(item.Label);
        _feature.Select(0);
        _placement.Clear();
        _placement.AddItem("Choisir une tuile…");
        foreach (var item in _placementChoices) _placement.AddItem(item.Label);
        _placement.Select(0);
        if (_draft is not null)
        {
            var area = _session.Project!.Areas.Single(item => item.Id == choice.AreaId);
            PopulateBrush(_draft, area.Dimensions);
        }
        _summary.Text = choice.HasEditableTerrain
            ? "Zone chargée · sculptez directement ou choisissez une collection à éditer."
            : "Terrain plat ou désactivé · sculpture indisponible, features et propriétés restent éditables.";
        _diagnostics.Text = "Aucune commande préparée · toutes les collections voisines restent intactes.";
        _status.Text = "Zone sélectionnée · aperçu sec obligatoire avant application.";
        CanonicalPreviewReady = false;
        _updating = false;
        UpdateActions();
    }

    private void ChangeMode(int index)
    {
        if (_updating || _draft is null) return;
        _draft = _draft with { Operation = (StudioUx2CTerrainOperation)index };
        InvalidatePreview();
    }

    private void SelectFeature(int index)
    {
        if (_updating || _draft is null) return;
        if (index <= 0 || index > _featureChoices.Count) return;
        var selected = _session.SelectTerrainFeatureUx2C(_draft, _featureChoices[index - 1].Id, false);
        if (selected is null) return;
        _draft = selected;
        PopulateFeature(selected);
        InvalidatePreview();
    }

    private void NewFeature()
    {
        if (_draft is null) return;
        var selected = _session.SelectTerrainFeatureUx2C(_draft, null, true);
        if (selected is null) return;
        _draft = selected;
        _feature.Select(0);
        PopulateFeature(selected);
        InvalidatePreview();
    }

    private void SelectProperty(int index)
    {
        if (_updating || _draft is null || index <= 0 || index > _placementChoices.Count) return;
        var selected = _session.SelectTerrainPropertyUx2C(_draft, _placementChoices[index - 1].Id);
        if (selected is null) return;
        _draft = selected;
        PopulateProperty(selected);
        InvalidatePreview();
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Choisissez une zone et l’élément requis par cet onglet.", true);
            return;
        }
        var result = _session.PreviewTerrainSurfaceUx2C(draft);
        CanonicalPreviewReady = result.Succeeded && result.CanApply;
        _summary.Text = result.Succeeded
            ? result.Summary + "\n" + string.Join("\n", result.Changes.Select(item => "• " + item))
            : "Aperçu refusé · aucun changement préparé.";
        _diagnostics.Text = result.Diagnostics.Count > 0
            ? string.Join("\n", result.Diagnostics.Select(item => "• " + item))
            : "Validation canonique réussie · aucun diagnostic bloquant.";
        SetStatus(result.Message, !result.Succeeded);
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedTerrainSurfaceUx2C();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CanonicalPreviewReady = false;
            ReloadCurrentArea();
            TerrainChanged?.Invoke();
        }
        UpdateActions();
    }

    private void Undo() { var result = _session.Undo(); SetStatus(result.Message, !result.Succeeded); ReloadCurrentArea(); TerrainChanged?.Invoke(); }
    private void Redo() { var result = _session.Redo(); SetStatus(result.Message, !result.Succeeded); ReloadCurrentArea(); TerrainChanged?.Invoke(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); UpdateActions(); }

    private StudioUx2CTerrainDraft? ReadDraft()
    {
        if (_draft is null) return null;
        var operation = (StudioUx2CTerrainOperation)_tabs.CurrentTab;
        if (operation == StudioUx2CTerrainOperation.Feature && _draft.FeatureId == Guid.Empty) return null;
        if (operation == StudioUx2CTerrainOperation.TileProperty && _draft.PlacementId == Guid.Empty) return null;
        return _draft with
        {
            Operation = operation,
            BrushOperation = (TerrainBrushOperation)_brushOperation.Selected,
            WorldX = (float)_worldX.Value,
            WorldZ = (float)_worldZ.Value,
            Radius = (float)_radius.Value,
            Strength = (float)_strength.Value,
            TargetHeight = (float)_targetHeight.Value,
            MaterialLayerId = _materialChoices.Count == 0 ? Guid.Empty : _materialChoices[Math.Clamp(_material.Selected, 0, _materialChoices.Count - 1)].Id,
            FeatureName = _featureName.Text,
            FeatureKind = (TerrainFeatureKind)_featureKind.Selected,
            FeatureControlPoints = ParsePoints(_featurePoints.Text),
            FeatureWidth = (float)_featureWidth.Value,
            FeatureHeight = (float)_featureHeight.Value,
            FeatureBankDegrees = (float)_featureBank.Value,
            FeatureCrossingPolicy = (TerrainFeatureCrossingPolicy)_featureCrossing.Selected,
            FeatureBiomeTags = ParseList(_featureTags.Text),
            FeatureMaterialResource = _featureMaterial.Text.Trim(),
            SemanticState = _semanticState.Text,
            Animation = _animation.Text.Trim(),
            LoopAnimation = _loopAnimation.ButtonPressed,
            MaterialOverrides = ParseMap(_materialOverrides.Text),
            LightEnabled = _lightEnabled.ButtonPressed,
            LightColor = _lightColor.Text,
            LightIntensity = (float)_lightIntensity.Value,
            LightRangeMeters = (float)_lightRange.Value,
            CollisionEnabled = _collision.ButtonPressed,
            NavigationEnabled = _navigation.ButtonPressed,
            SocketOverrides = ParseList(_sockets.Text),
            PropertyOverrides = ParseMap(_propertyOverrides.Text),
        };
    }

    private void PopulateBrush(StudioUx2CTerrainDraft draft, AreaDimensions dimensions)
    {
        _brushOperation.Select((int)draft.BrushOperation);
        _worldX.Value = draft.WorldX;
        _worldZ.Value = draft.WorldZ;
        _radius.Value = draft.Radius;
        _strength.Value = draft.Strength;
        _targetHeight.Value = draft.TargetHeight;
        _brushCanvas.Configure(dimensions.Width, dimensions.Depth, draft.WorldX, draft.WorldZ, draft.Radius);
    }

    private void PopulateFeature(StudioUx2CTerrainDraft draft)
    {
        _updating = true;
        _featureName.Text = draft.FeatureName;
        _featureKind.Select((int)draft.FeatureKind);
        _featurePoints.Text = string.Join("\n", draft.FeatureControlPoints.Select(point =>
            string.Create(CultureInfo.InvariantCulture, $"{point.X:R},{point.Y:R},{point.Z:R}")));
        _featureWidth.Value = draft.FeatureWidth;
        _featureHeight.Value = draft.FeatureHeight;
        _featureBank.Value = draft.FeatureBankDegrees;
        _featureCrossing.Select((int)draft.FeatureCrossingPolicy);
        _featureTags.Text = string.Join(", ", draft.FeatureBiomeTags);
        _featureMaterial.Text = draft.FeatureMaterialResource;
        _updating = false;
    }

    private void PopulateProperty(StudioUx2CTerrainDraft draft)
    {
        _updating = true;
        _semanticState.Text = draft.SemanticState;
        _animation.Text = draft.Animation;
        _loopAnimation.ButtonPressed = draft.LoopAnimation;
        _materialOverrides.Text = FormatMap(draft.MaterialOverrides);
        _lightEnabled.ButtonPressed = draft.LightEnabled;
        _lightColor.Text = draft.LightColor;
        _lightIntensity.Value = draft.LightIntensity;
        _lightRange.Value = draft.LightRangeMeters;
        _collision.ButtonPressed = draft.CollisionEnabled;
        _navigation.ButtonPressed = draft.NavigationEnabled;
        _sockets.Text = string.Join(", ", draft.SocketOverrides);
        _propertyOverrides.Text = FormatMap(draft.PropertyOverrides);
        _updating = false;
    }

    private void ReloadCurrentArea()
    {
        if (_draft is null) return;
        var areaId = _draft.AreaId;
        var tab = _tabs.CurrentTab;
        var index = FindIndex(_areaChoices, item => item.AreaId == areaId);
        LoadAreas();
        if (index < 0) return;
        _area.Select(index + 1);
        SelectArea(index + 1);
        _tabs.CurrentTab = tab;
        ChangeMode(tab);
    }

    private void ResetArea()
    {
        _draft = null;
        _materialChoices = [];
        _featureChoices = [];
        _placementChoices = [];
        _material.Clear();
        _feature.Clear();
        _feature.AddItem("Choisir une feature…");
        _placement.Clear();
        _placement.AddItem("Choisir une tuile…");
        _summary.Text = "Aucun aperçu. Choisissez explicitement une zone.";
        _diagnostics.Text = "Aucune donnée n’est synthétisée dans le module source.";
        CanonicalPreviewReady = false;
        UpdateActions();
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelTerrainSurfaceApprovalUx2C();
        CanonicalPreviewReady = false;
        _summary.Text = "Paramètres modifiés · prévisualisez de nouveau avant d’appliquer.";
        if (_draft is not null)
        {
            var area = _session.Project?.Areas.SingleOrDefault(item => item.Id == _draft.AreaId);
            if (area is not null) _brushCanvas.Configure(area.Dimensions.Width, area.Dimensions.Depth,
                (float)_worldX.Value, (float)_worldZ.Value, (float)_radius.Value);
        }
        UpdateActions();
    }

    private void UpdateActions()
    {
        var ready = _draft is not null && (_tabs.CurrentTab switch
        {
            1 => _draft.FeatureId != Guid.Empty,
            2 => _draft.PlacementId != Guid.Empty,
            _ => true,
        });
        _preview.Disabled = !ready;
        _apply.Disabled = !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private bool FailEvidence() { Close(); return false; }
    private bool DeleteFailedEvidence(string root) => StudioSessionController.DeleteTerrainSurfaceTemporaryWorkspaceUx2C(root) && false;

    private void CloseEvidenceSession()
    {
        var controller = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        if (controller is not null) controller.Close();
        if (root is not null && !StudioSessionController.DeleteTerrainSurfaceTemporaryWorkspaceUx2C(root))
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2C_TEMP_CLEANUP_FAILED");
        _session = _sourceSession;
    }

    private void WireInvalidation()
    {
        _feature.ItemSelected += index => SelectFeature((int)index);
        _newFeature.Pressed += NewFeature;
        _placement.ItemSelected += index => SelectProperty((int)index);
        foreach (var control in new Control[] { _brushOperation, _material, _featureKind, _featureCrossing })
            ((OptionButton)control).ItemSelected += _ => InvalidatePreview();
        foreach (var spin in new[] { _worldX, _worldZ, _radius, _strength, _targetHeight, _featureWidth,
                     _featureHeight, _featureBank, _lightIntensity, _lightRange })
            spin.ValueChanged += _ => InvalidatePreview();
        foreach (var edit in new[] { _featureName, _featureTags, _featureMaterial, _semanticState, _animation,
                     _lightColor, _sockets })
            edit.TextChanged += _ => InvalidatePreview();
        foreach (var edit in new[] { _featurePoints, _materialOverrides, _propertyOverrides })
            edit.TextChanged += () => InvalidatePreview();
        foreach (var check in new[] { _loopAnimation, _lightEnabled, _collision, _navigation })
            check.Toggled += _ => InvalidatePreview();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape) { Close(); GetViewport().SetInputAsHandled(); }
    }

    private static VBoxContainer Tab(string name) => new() { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };

    private static VBoxContainer Form(Container parent, float width)
    {
        var panel = StudioStyle.Panel("TerrainSurfaceFormUx2C", StudioStyle.Surface, 8);
        panel.CustomMinimumSize = new Vector2(width, 0);
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 6);
        margin.AddChild(form);
        return form;
    }

    private static VBoxContainer ScrollForm(Container parent)
    {
        var panel = StudioStyle.Panel("TerrainSurfaceScrollFormUx2C", StudioStyle.Surface, 8);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        margin.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(form);
        return form;
    }

    private static T Field<T>(VBoxContainer parent, string label, T control) where T : Control
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        parent.AddChild(control);
        return control;
    }

    private static SpinBox Number(double min, double max, double arrow, string suffix = "")
    {
        var box = new SpinBox { MinValue = min, MaxValue = max, Step = 0, CustomArrowStep = arrow, Suffix = suffix };
        StudioStyle.ApplySpinBox(box);
        return box;
    }

    private static LineEdit Line(string placeholder)
    {
        var edit = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(edit);
        return edit;
    }

    private static TextEdit Multiline(string placeholder, float height)
    {
        var edit = new TextEdit { PlaceholderText = placeholder, CustomMinimumSize = new Vector2(0, height), WrapMode = TextEdit.LineWrappingMode.None };
        edit.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        edit.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 5, 2));
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("caret_color", StudioStyle.Accent);
        edit.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return edit;
    }

    private static Button Action(HBoxContainer parent, string label, Action handler, bool primary = false)
    {
        var button = StudioStyle.Button(label, primary: primary);
        button.CustomMinimumSize = new Vector2(116, 34);
        button.Pressed += handler;
        parent.AddChild(button);
        return button;
    }

    private static IReadOnlyList<Vector3Value> ParsePoints(string text)
    {
        var points = new List<Vector3Value>();
        foreach (var line in text.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var values = line.Split(',', StringSplitOptions.TrimEntries);
            if (values.Length != 3 || !float.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return [];
            points.Add(new(x, y, z));
        }
        return points;
    }

    private static IReadOnlyList<string> ParseList(string text) => text
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal).ToArray();

    private static IReadOnlyDictionary<string, string> ParseMap(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            map[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return map;
    }

    private static string FormatMap(IReadOnlyDictionary<string, string> values) =>
        string.Join("\n", values.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={item.Value}"));

    private static int FindIndex<T>(IReadOnlyList<T> values, Func<T, bool> predicate)
    {
        for (var index = 0; index < values.Count; index++) if (predicate(values[index])) return index;
        return -1;
    }
}
