#if TOOLS
using Forge.Application.Areas;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AreaWorkspaceWindow : Window
{
    private readonly List<AreaId> _visibleAreaIds = [];
    private readonly List<EntityId> _sourceEntityIds = [];
    private readonly List<AreaId> _destinationAreaIds = [];
    private readonly List<EntityId> _destinationEntityIds = [];
    private readonly List<BlueprintId> _playerBlueprintIds = [];
    private readonly List<EntityId> _playtestSpawnEntityIds = [];
    private readonly List<Guid> _playtestWaypointIds = [];
    private readonly List<Guid> _playtestTargetIds = [];
    private readonly AreaKind[] _areaKinds = Enum.GetValues<AreaKind>();
    private readonly AreaTemplateKind[] _templates = Enum.GetValues<AreaTemplateKind>();

    private Func<ProjectState?>? _getState;
    private Func<AreaId?>? _getActiveAreaId;
    private Func<AreaId, bool>? _activateArea;
    private Func<string, AreaKind, AreaDimensions, AreaTemplateKind, AreaCreationSettings, AreaId?>? _createArea;
    private Func<AreaId, bool>? _deleteArea;
    private Func<AreaId, PortalLink, bool>? _upsertPortal;
    private Func<AreaId, AreaPlaytestProfile, bool>? _setAreaPlaytest;
    private Action? _startPlaytest;
    private Func<AreaId, string, AreaCloneMode, ExternalPortalPolicy, AreaId?>? _duplicateArea;
    private Func<AreaId, AreaDimensions, AreaResizeAnchor, bool>? _resizeArea;
    private Action? _openNavigation;
    private Action? _openEnvironment;
    private Action? _openTerrain;
    private Action? _openRuntimePolicies;
    private Action? _openCinematics;
    private Action? _openGameplayControls;
    private Action? _openRules;
    private Action? _openExperience;
    private Action? _openAuthoring;
    private Action? _openFoundation;
    private Action<Window>? _brandWindow;

    private ItemList? _areaList;
    private Label? _areaSummary;
    private Label? _activeAreaSummary;
    private LineEdit? _areaName;
    private OptionButton? _areaKind;
    private SpinBox? _areaWidth;
    private SpinBox? _areaDepth;
    private OptionButton? _areaTemplate;
    private LineEdit? _areaKit;
    private LineEdit? _areaTerrain;
    private LineEdit? _areaLighting;
    private LineEdit? _areaNavigationProfile;
    private LineEdit? _areaStreaming;
    private LineEdit? _areaBiome;
    private SpinBox? _areaSeed;
    private SpinBox? _areaBudget;
    private Label? _areaCreationPreview;
    private OptionButton? _sourceEntity;
    private OptionButton? _destinationArea;
    private OptionButton? _destinationEntity;
    private LineEdit? _portalName;
    private CheckBox? _bidirectional;
    private CheckBox? _preload;
    private OptionButton? _portalOrientation;
    private OptionButton? _portalVelocity;
    private SpinBox? _portalYaw;
    private LineEdit? _portalBeforeHook;
    private LineEdit? _portalAfterHook;
    private CheckBox? _portalHooksRequired;
    private OptionButton? _playtestMode;
    private OptionButton? _playtestPlayerBlueprint;
    private OptionButton? _playtestStart;
    private SpinBox? _playtestX;
    private SpinBox? _playtestY;
    private SpinBox? _playtestZ;
    private LineEdit? _playtestProfile;
    private OptionButton? _playtestTargetKind;
    private OptionButton? _playtestTarget;
    private CheckBox? _playtestHeadless;
    private Label? _playtestPreview;
    private ScrollContainer? _rootScroll;
    private ItemList? _validationList;
    private Label? _validationSummary;
    private Label? _status;
    private LineEdit? _cloneName;
    private OptionButton? _cloneMode;
    private CheckBox? _preserveExternalPortals;
    private Button? _applyCloneButton;
    private AreaId? _selectedAreaId;
    private AreaId? _cloneSourceAreaId;
    private SpinBox? _resizeWidth;
    private SpinBox? _resizeDepth;
    private OptionButton? _resizeAnchor;

    public int AreaCount => _visibleAreaIds.Count;

    public int ValidationCount => _validationList?.ItemCount ?? 0;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<AreaId?> getActiveAreaId,
        Func<AreaId, bool> activateArea,
        Func<string, AreaKind, AreaDimensions, AreaTemplateKind, AreaCreationSettings, AreaId?> createArea,
        Func<AreaId, bool> deleteArea,
        Func<AreaId, PortalLink, bool> upsertPortal,
        Func<AreaId, AreaPlaytestProfile, bool> setAreaPlaytest,
        Func<AreaId, string, AreaCloneMode, ExternalPortalPolicy, AreaId?> duplicateArea,
        Func<AreaId, AreaDimensions, AreaResizeAnchor, bool> resizeArea,
        Action openNavigation,
        Action openEnvironment,
        Action openTerrain,
        Action openRuntimePolicies,
        Action openCinematics,
        Action openGameplayControls,
        Action openRules,
        Action openExperience,
        Action openAuthoring,
        Action openFoundation,
        Action startPlaytest,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _getActiveAreaId = getActiveAreaId ?? throw new ArgumentNullException(nameof(getActiveAreaId));
        _activateArea = activateArea ?? throw new ArgumentNullException(nameof(activateArea));
        _createArea = createArea ?? throw new ArgumentNullException(nameof(createArea));
        _deleteArea = deleteArea ?? throw new ArgumentNullException(nameof(deleteArea));
        _upsertPortal = upsertPortal ?? throw new ArgumentNullException(nameof(upsertPortal));
        _setAreaPlaytest = setAreaPlaytest ?? throw new ArgumentNullException(nameof(setAreaPlaytest));
        _duplicateArea = duplicateArea ?? throw new ArgumentNullException(nameof(duplicateArea));
        _resizeArea = resizeArea ?? throw new ArgumentNullException(nameof(resizeArea));
        _openNavigation = openNavigation ?? throw new ArgumentNullException(nameof(openNavigation));
        _openEnvironment = openEnvironment ?? throw new ArgumentNullException(nameof(openEnvironment));
        _openTerrain = openTerrain ?? throw new ArgumentNullException(nameof(openTerrain));
        _openRuntimePolicies = openRuntimePolicies ?? throw new ArgumentNullException(nameof(openRuntimePolicies));
        _openCinematics = openCinematics ?? throw new ArgumentNullException(nameof(openCinematics));
        _openGameplayControls = openGameplayControls ?? throw new ArgumentNullException(nameof(openGameplayControls));
        _openRules = openRules ?? throw new ArgumentNullException(nameof(openRules));
        _openExperience = openExperience ?? throw new ArgumentNullException(nameof(openExperience));
        _openAuthoring = openAuthoring ?? throw new ArgumentNullException(nameof(openAuthoring));
        _openFoundation = openFoundation ?? throw new ArgumentNullException(nameof(openFoundation));
        _startPlaytest = startPlaytest ?? throw new ArgumentNullException(nameof(startPlaytest));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));

        Title = "Zones · OpenRPG Forge";
        MinSize = new Vector2I(1280, 840);
        Size = new Vector2I(1280, 840);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1280, 840));
        _brandWindow?.Invoke(this);
    }

    public void Refresh(AreaId? preferredAreaId = null)
    {
        var state = _getState?.Invoke();
        if (state is null || _areaList is null)
        {
            return;
        }

        var activeAreaId = preferredAreaId ?? _getActiveAreaId?.Invoke();
        _visibleAreaIds.Clear();
        _areaList.Clear();
        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        {
            _visibleAreaIds.Add(area.Id);
            var active = area.Id == activeAreaId ? "◆ " : string.Empty;
            _areaList.AddItem(
                $"{active}{area.DisplayName}\n{FormatKind(area.Kind)} · {area.Dimensions.Width} × {area.Dimensions.Depth} · " +
                $"{area.Entities.Count} entité(s) · {area.Portals.Count} transition(s)");
        }

        _areaSummary!.Text = $"{state.Project.Areas.Count} zone(s) · documents indépendants au prochain enregistrement";
        if (activeAreaId is not null)
        {
            var performance = AreaPerformanceAnalyzer.Analyze(state.Project, activeAreaId);
            _areaSummary.Text += $" · budget {(performance.IsWithinBudget ? "OK" : "DÉPASSÉ")} · " +
                $"{performance.UniqueAssetCount} assets · {performance.OccupiedOcclusionSectors} secteurs · " +
                $"LOD {performance.NearLodCount}/{performance.MidLodCount}/{performance.FarLodCount}";
        }
        var selectedIndex = activeAreaId is null ? -1 : _visibleAreaIds.FindIndex(id => id == activeAreaId);
        if (selectedIndex < 0 && _visibleAreaIds.Count > 0)
        {
            selectedIndex = 0;
        }

        if (selectedIndex >= 0)
        {
            _areaList.Select(selectedIndex);
            _selectedAreaId = _visibleAreaIds[selectedIndex];
        }

        RefreshActiveArea(state);
        RefreshValidation(state);
    }

    public bool SelectAreaForSmokeTest(AreaId areaId)
    {
        Refresh(areaId);
        var index = _visibleAreaIds.FindIndex(id => id == areaId);
        if (index < 0)
        {
            return false;
        }

        SelectArea(index);
        return _getActiveAreaId?.Invoke() == areaId;
    }

    public bool PrepareRuntimeValidationForSmokeTest()
    {
        Refresh();
        if (_playtestMode is null || _playtestProfile is null || _playtestPlayerBlueprint is null ||
            _playerBlueprintIds.Count == 0) return false;
        _playtestMode.Selected = (int)PlayFromHereStartMode.Camera;
        _playtestPlayerBlueprint.Selected = 0;
        _playtestProfile.Text = "r029-camera-smoke";
        _playtestX!.Value = 6;
        _playtestY!.Value = 2;
        _playtestZ!.Value = -2;
        RefreshPlaytestStartOptions();
        ConfigurePlaytest(launch: false);
        var area = _getState?.Invoke()?.Project.Areas.FirstOrDefault(item => item.Id == _getActiveAreaId?.Invoke());
        return area?.Playtest is
        {
            Enabled: true,
            StartMode: PlayFromHereStartMode.Camera,
            TestProfile: "r029-camera-smoke",
        };
    }

    public void ShowRuntimeValidationForCapture()
    {
        if (_rootScroll is not null)
            _rootScroll.ScrollVertical = int.MaxValue;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("15111f", "62458a", 1, 0));
        AddChild(background);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        background.AddChild(margin);

        _rootScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        margin.AddChild(_rootScroll);
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 12);
        _rootScroll.AddChild(root);

        var heading = new Label { Text = "ZONES & TRANSITIONS", ThemeTypeVariation = "HeaderSmall" };
        heading.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(heading);
        root.AddChild(new Label
        {
            Text = "Composez le monde par documents indépendants, basculez la zone active et reliez ses points d’ancrage.",
        });

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        var catalog = AddCard(body, new Vector2(330, 0));
        catalog.AddChild(new Label { Text = "MONDE DU MODULE" });
        _areaSummary = MutedLabel("Aucune zone");
        catalog.AddChild(_areaSummary);
        _areaList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AllowReselect = true,
        };
        catalog.AddChild(_areaList);
        var deleteAreaButton = new Button { Text = "Supprimer la zone" };
        deleteAreaButton.TooltipText = "La zone d’entrée et la dernière zone du module sont protégées";
        deleteAreaButton.Pressed += DeleteSelectedArea;
        catalog.AddChild(deleteAreaButton);
        catalog.AddChild(MutedLabel("◆ indique la zone affichée et éditée dans le workspace Monde."));
        catalog.AddChild(new HSeparator());
        catalog.AddChild(new Label { Text = "COPIE DE ZONE" });
        _cloneName = new LineEdit { PlaceholderText = "Nom de la copie" };
        _cloneName.TextChanged += _ => ResetAreaClonePreview();
        catalog.AddChild(_cloneName);
        _cloneMode = new OptionButton();
        _cloneMode.AddItem("Clone complet");
        _cloneMode.AddItem("Gabarit vide");
        _cloneMode.ItemSelected += _ => ResetAreaClonePreview();
        catalog.AddChild(_cloneMode);
        _preserveExternalPortals = new CheckBox
        {
            Text = "Conserver les transitions externes",
            ButtonPressed = true,
        };
        _preserveExternalPortals.Toggled += _ => ResetAreaClonePreview();
        catalog.AddChild(_preserveExternalPortals);
        var previewCloneButton = new Button { Text = "Prévisualiser la copie" };
        previewCloneButton.Pressed += PreviewAreaClone;
        catalog.AddChild(previewCloneButton);
        _applyCloneButton = new Button
        {
            Text = "Créer la copie",
            Disabled = true,
        };
        _applyCloneButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        _applyCloneButton.Pressed += ApplyAreaClone;
        catalog.AddChild(_applyCloneButton);

        var create = AddCard(body, new Vector2(330, 0));
        create.AddChild(new Label { Text = "NOUVELLE ZONE" });
        create.AddChild(new Label { Text = "Nom affiché" });
        _areaName = new LineEdit { PlaceholderText = "Ex. Lisière de la forêt" };
        create.AddChild(_areaName);
        create.AddChild(new Label { Text = "Environnement" });
        _areaKind = new OptionButton();
        foreach (var kind in _areaKinds)
        {
            _areaKind.AddItem(FormatKind(kind));
        }
        create.AddChild(_areaKind);
        var dimensions = new HBoxContainer();
        dimensions.AddThemeConstantOverride("separation", 8);
        create.AddChild(dimensions);
        var widthColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        widthColumn.AddChild(new Label { Text = "Largeur" });
        _areaWidth = DimensionSpinBox();
        widthColumn.AddChild(_areaWidth);
        dimensions.AddChild(widthColumn);
        var depthColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        depthColumn.AddChild(new Label { Text = "Profondeur" });
        _areaDepth = DimensionSpinBox();
        depthColumn.AddChild(_areaDepth);
        dimensions.AddChild(depthColumn);
        create.AddChild(new Label { Text = "Gabarit" });
        _areaTemplate = new OptionButton();
        foreach (var template in _templates)
        {
            _areaTemplate.AddItem(FormatTemplate(template));
        }
        _areaTemplate.Selected = Array.IndexOf(_templates, AreaTemplateKind.Playable);
        create.AddChild(_areaTemplate);
        var presets = new GridContainer { Columns = 2 };
        create.AddChild(presets);
        _areaKit = CompactField(presets, "Kit", "forge-generic");
        _areaTerrain = CompactField(presets, "Terrain", "flat");
        _areaLighting = CompactField(presets, "Éclairage", "interior-neutral");
        _areaNavigationProfile = CompactField(presets, "Navigation", "walk-medium");
        _areaStreaming = CompactField(presets, "Streaming", "single-area");
        _areaBiome = CompactField(presets, "Biome", "interior");
        _areaSeed = CompactSpin(presets, "Graine", int.MinValue, int.MaxValue, 2026);
        _areaBudget = CompactSpin(presets, "Budget MiB", 32, 65536, 512);
        create.AddChild(MutedLabel(
            "Le gabarit jouable ajoute un point d’apparition si le module possède un blueprint PlayerSpawn."));
        _areaCreationPreview = MutedLabel("Aperçu non calculé · aucun document ne sera écrit.");
        _areaCreationPreview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        create.AddChild(_areaCreationPreview);
        var previewButton = new Button { Text = "Prévisualiser la fondation" };
        previewButton.Pressed += PreviewAreaCreation;
        create.AddChild(previewButton);
        var createButton = new Button { Text = "+ Créer la zone", CustomMinimumSize = new Vector2(0, 42) };
        createButton.AddThemeColorOverride("font_color", Colors.White);
        createButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        createButton.Pressed += CreateArea;
        create.AddChild(createButton);

        var transitions = AddCard(body, new Vector2(410, 0), expand: true);
        transitions.AddChild(new Label { Text = "TRANSITION DEPUIS LA ZONE ACTIVE" });
        _activeAreaSummary = new Label { Text = "Aucune zone active", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _activeAreaSummary.AddThemeColorOverride("font_color", new Color("ffb07c"));
        transitions.AddChild(_activeAreaSummary);
        var resizeRow = new HBoxContainer(); transitions.AddChild(resizeRow);
        _resizeWidth = DimensionSpinBox(); _resizeDepth = DimensionSpinBox();
        _resizeAnchor = new OptionButton(); foreach (var anchor in Enum.GetValues<AreaResizeAnchor>()) _resizeAnchor.AddItem(anchor.ToString());
        resizeRow.AddChild(_resizeWidth); resizeRow.AddChild(_resizeDepth); resizeRow.AddChild(_resizeAnchor);
        var resizeButton = new Button { Text = "Prévisualiser & redimensionner" }; resizeButton.Pressed += ResizeActiveArea; transitions.AddChild(resizeButton);
        transitions.AddChild(new Label { Text = "Point d’ancrage source" });
        _sourceEntity = new OptionButton();
        transitions.AddChild(_sourceEntity);
        transitions.AddChild(new Label { Text = "Zone de destination" });
        _destinationArea = new OptionButton();
        transitions.AddChild(_destinationArea);
        transitions.AddChild(new Label { Text = "Point d’arrivée" });
        _destinationEntity = new OptionButton();
        transitions.AddChild(_destinationEntity);
        transitions.AddChild(new Label { Text = "Nom de la transition" });
        _portalName = new LineEdit { PlaceholderText = "Ex. Vers les ruines" };
        transitions.AddChild(_portalName);
        _bidirectional = new CheckBox { Text = "Liaison bidirectionnelle", ButtonPressed = true };
        transitions.AddChild(_bidirectional);
        _preload = new CheckBox { Text = "Précharger la destination" };
        transitions.AddChild(_preload);
        var portalPolicies = new GridContainer { Columns = 3 };
        transitions.AddChild(portalPolicies);
        _portalOrientation = CompactOption(portalPolicies, "Orientation", Enum.GetNames<PortalOrientationPolicy>());
        _portalVelocity = CompactOption(portalPolicies, "Vitesse", Enum.GetNames<PortalVelocityPolicy>());
        _portalYaw = CompactSpin(portalPolicies, "Yaw fixe", -360, 360, 0);
        var portalHooks = new GridContainer { Columns = 2 };
        transitions.AddChild(portalHooks);
        _portalBeforeHook = CompactField(portalHooks, "Hook avant", "");
        _portalBeforeHook.PlaceholderText = "scripts/before.forge";
        _portalAfterHook = CompactField(portalHooks, "Hook après", "");
        _portalAfterHook.PlaceholderText = "scripts/after.forge";
        _portalHooksRequired = new CheckBox { Text = "Hooks requis au runtime" };
        transitions.AddChild(_portalHooksRequired);
        var portalButton = new Button { Text = "Relier les zones", CustomMinimumSize = new Vector2(0, 42) };
        portalButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        portalButton.Pressed += CreatePortal;
        transitions.AddChild(portalButton);

        var validation = new PanelContainer { CustomMinimumSize = new Vector2(0, 150) };
        validation.AddThemeStyleboxOverride("panel", Card("100d1b", "47365e", 1, 10));
        root.AddChild(validation);
        var validationMargin = new MarginContainer();
        validationMargin.AddThemeConstantOverride("margin_left", 14);
        validationMargin.AddThemeConstantOverride("margin_top", 10);
        validationMargin.AddThemeConstantOverride("margin_right", 14);
        validationMargin.AddThemeConstantOverride("margin_bottom", 10);
        validation.AddChild(validationMargin);
        var validationRow = new HBoxContainer();
        validationRow.AddThemeConstantOverride("separation", 12);
        validationMargin.AddChild(validationRow);
        var validationHeading = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        validationHeading.AddChild(new Label { Text = "VALIDATION DES ZONES" });
        _validationSummary = MutedLabel("Validation en attente");
        validationHeading.AddChild(_validationSummary);
        var navigationButton = new Button { Text = "Navigation & géométrie…", CustomMinimumSize = new Vector2(0, 38) };
        navigationButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        navigationButton.Pressed += () => _openNavigation?.Invoke();
        validationHeading.AddChild(navigationButton);
        var environmentButton = new Button { Text = "Environnement…" };
        environmentButton.Pressed += () => _openEnvironment?.Invoke();
        validationHeading.AddChild(environmentButton);
        var terrainButton = new Button { Text = "Terrain & volumes…" };
        terrainButton.Pressed += () => _openTerrain?.Invoke();
        validationHeading.AddChild(terrainButton);
        var runtimePoliciesButton = new Button { Text = "Rythme, repos & séquences…" };
        runtimePoliciesButton.Pressed += () => _openRuntimePolicies?.Invoke();
        validationHeading.AddChild(runtimePoliciesButton);
        var cinematicButton = new Button { Text = "Séquences & caméras…" };
        cinematicButton.Pressed += () => _openCinematics?.Invoke();
        validationHeading.AddChild(cinematicButton);
        var gameplayButton = new Button { Text = "Contrôles, actions & combat…" };
        gameplayButton.Pressed += () => _openGameplayControls?.Invoke();
        validationHeading.AddChild(gameplayButton);
        var rulesButton = new Button { Text = "Progression, difficulté & règles…" };
        rulesButton.Pressed += () => _openRules?.Invoke();
        validationHeading.AddChild(rulesButton);
        var experienceButton = new Button { Text = "Audio, patrouilles & chargement…" };
        experienceButton.Pressed += () => _openExperience?.Invoke();
        validationHeading.AddChild(experienceButton);
        var authoringButton = new Button { Text = "Carte, événements & calques…" };
        authoringButton.Pressed += () => _openAuthoring?.Invoke(); validationHeading.AddChild(authoringButton);
        var foundationButton = new Button { Text = "Fondation & kits modulaires…" };
        foundationButton.Pressed += () => _openFoundation?.Invoke(); validationHeading.AddChild(foundationButton);
        validationRow.AddChild(validationHeading);
        var playtest = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        playtest.AddChild(new Label { Text = "PLAY FROM HERE" });
        playtest.AddChild(new Label { Text = "Mode de départ" });
        _playtestMode = new OptionButton();
        foreach (var mode in Enum.GetNames<PlayFromHereStartMode>()) _playtestMode.AddItem(mode);
        playtest.AddChild(_playtestMode);
        playtest.AddChild(new Label { Text = "Personnage (blueprint PlayerSpawn requis)" });
        _playtestPlayerBlueprint = new OptionButton();
        playtest.AddChild(_playtestPlayerBlueprint);
        playtest.AddChild(new Label { Text = "Référence de départ" });
        _playtestStart = new OptionButton();
        playtest.AddChild(_playtestStart);
        var camera = new GridContainer { Columns = 3 };
        _playtestX = CompactSpin(camera, "X", -100000, 100000, 0);
        _playtestY = CompactSpin(camera, "Y", -100000, 100000, 0);
        _playtestZ = CompactSpin(camera, "Z", -100000, 100000, 0);
        playtest.AddChild(camera);
        playtest.AddChild(new Label { Text = "Profil de test" });
        _playtestProfile = new LineEdit { Text = "default", PlaceholderText = "Profil de test" };
        playtest.AddChild(_playtestProfile);
        playtest.AddChild(new Label { Text = "Cible du test" });
        _playtestTargetKind = new OptionButton();
        foreach (var kind in Enum.GetNames<PlaytestTargetKind>()) _playtestTargetKind.AddItem(kind);
        playtest.AddChild(_playtestTargetKind);
        _playtestTarget = new OptionButton();
        playtest.AddChild(_playtestTarget);
        _playtestHeadless = new CheckBox { Text = "Exécuter sans rendu (headless)" };
        playtest.AddChild(_playtestHeadless);
        _playtestPreview = MutedLabel("Choisissez un personnage et un point de départ.");
        _playtestPreview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        playtest.AddChild(_playtestPreview);
        var playtestButton = new Button { Text = "Configurer & jouer d'ici" };
        playtestButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        playtestButton.Pressed += ConfigureAndStartPlaytest;
        playtest.AddChild(playtestButton);
        validationRow.AddChild(playtest);
        _validationList = new ItemList { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        validationRow.AddChild(_validationList);

        _status = new Label { Text = "Prêt." };
        _status.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        root.AddChild(_status);
    }

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        _areaList!.ItemSelected += SelectArea;
        _destinationArea!.ItemSelected += _ => RefreshDestinationEntities();
        _playtestMode!.ItemSelected += _ => RefreshPlaytestStartOptions();
        _playtestTargetKind!.ItemSelected += _ => RefreshPlaytestTargets();
    }

    private void ResizeActiveArea()
    {
        var areaId = _getActiveAreaId?.Invoke();
        if (areaId is null) return;
        var dimensions = new AreaDimensions((int)_resizeWidth!.Value, (int)_resizeDepth!.Value);
        var anchor = (AreaResizeAnchor)_resizeAnchor!.Selected;
        _status!.Text = _resizeArea?.Invoke(areaId, dimensions, anchor) == true
            ? "✓ Zone redimensionnée par une transaction atomique" : "✕ Redimensionnement refusé";
        Refresh(areaId);
    }

    private void SelectArea(long index)
    {
        var state = _getState?.Invoke();
        if (state is null || index < 0 || index >= _visibleAreaIds.Count)
        {
            return;
        }

        var areaId = _visibleAreaIds[(int)index];
        _selectedAreaId = areaId;
        ResetAreaClonePreview();
        if (_activateArea?.Invoke(areaId) == true)
        {
            var area = state.Project.Areas.Single(candidate => candidate.Id == areaId);
            SetStatus($"Zone active : « {area.DisplayName} ».", false);
        }
        Refresh(areaId);
    }

    private void CreateArea()
    {
        if (_areaName is null || _areaKind is null || _areaWidth is null || _areaDepth is null ||
            _areaTemplate is null || _areaKind.Selected < 0 || _areaTemplate.Selected < 0 ||
            _areaKit is null || _areaTerrain is null || _areaLighting is null ||
            _areaNavigationProfile is null || _areaStreaming is null || _areaBiome is null ||
            _areaSeed is null || _areaBudget is null)
        {
            return;
        }

        var name = _areaName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Donnez un nom à la nouvelle zone.", true);
            return;
        }

        try
        {
            var dimensions = new AreaDimensions((int)_areaWidth.Value, (int)_areaDepth.Value);
            var areaId = _createArea?.Invoke(
                name,
                _areaKinds[_areaKind.Selected],
                dimensions,
                _templates[_areaTemplate.Selected],
                ReadCreationSettings());
            if (areaId is null)
            {
                SetStatus("La création de la zone a été refusée. Consultez les diagnostics du projet.", true);
                return;
            }

            _areaName.Text = string.Empty;
            SetStatus($"Zone « {name} » créée et activée.", false);
            Refresh(areaId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void DeleteSelectedArea()
    {
        var areaId = _selectedAreaId ?? _getActiveAreaId?.Invoke();
        if (areaId is null)
        {
            SetStatus("Sélectionnez d’abord une zone.", true);
            return;
        }

        if (_deleteArea?.Invoke(areaId) != true)
        {
            SetStatus("Suppression refusée : la zone est requise ou encore référencée.", true);
            return;
        }

        _selectedAreaId = null;
        Refresh();
        SetStatus("Zone supprimée. Utilisez Annuler pour la restaurer.", false);
    }

    private void PreviewAreaCreation()
    {
        if (_areaName is null || _areaKind is null || _areaWidth is null || _areaDepth is null ||
            _areaTemplate is null || _areaKind.Selected < 0 || _areaTemplate.Selected < 0) return;
        try
        {
            var settings = ReadCreationSettings();
            _ = new AreaDimensions((int)_areaWidth.Value, (int)_areaDepth.Value);
            _areaCreationPreview!.Text =
                $"Aperçu · {(_areaName.Text.Trim().Length == 0 ? "zone sans nom" : _areaName.Text.Trim())} · " +
                $"kit {settings.KitKey} · {settings.TerrainProfile}/{settings.LightingProfile} · " +
                $"nav {settings.NavigationProfile} · streaming {settings.StreamingProfile} · " +
                $"biome {settings.Biome} · seed {settings.Seed} · budget {settings.RuntimeBudgetMegabytes} MiB · 0 écriture";
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            _areaCreationPreview!.Text = $"Erreur · {exception.Message}";
        }
    }

    private AreaCreationSettings ReadCreationSettings() => new(
        _templates[_areaTemplate!.Selected] == AreaTemplateKind.Playable ? "playable" : "blank",
        _areaKit!.Text.Trim(), _areaTerrain!.Text.Trim(), _areaLighting!.Text.Trim(),
        _areaNavigationProfile!.Text.Trim(), _areaStreaming!.Text.Trim(), _areaBiome!.Text.Trim(),
        (int)_areaSeed!.Value, (int)_areaBudget!.Value);

    private static LineEdit CompactField(GridContainer grid, string label, string value)
    {
        var column = new VBoxContainer();
        column.AddChild(new Label { Text = label });
        var field = new LineEdit { Text = value, CustomMinimumSize = new Vector2(0, 30) };
        column.AddChild(field);
        grid.AddChild(column);
        return field;
    }

    private static SpinBox CompactSpin(GridContainer grid, string label, double min, double max, double value)
    {
        var column = new VBoxContainer();
        column.AddChild(new Label { Text = label });
        var field = new SpinBox { MinValue = min, MaxValue = max, Value = value, CustomMinimumSize = new Vector2(0, 30) };
        column.AddChild(field);
        grid.AddChild(column);
        return field;
    }

    private static OptionButton CompactOption(GridContainer grid, string label, IReadOnlyList<string> values)
    {
        var column = new VBoxContainer();
        column.AddChild(new Label { Text = label });
        var field = new OptionButton { CustomMinimumSize = new Vector2(0, 30) };
        foreach (var value in values) field.AddItem(value);
        column.AddChild(field);
        grid.AddChild(column);
        return field;
    }

    private void CreatePortal()
    {
        var state = _getState?.Invoke();
        var sourceAreaId = _getActiveAreaId?.Invoke();
        if (state is null || sourceAreaId is null || _sourceEntity is null || _destinationArea is null ||
            _destinationEntity is null || _portalName is null || _bidirectional is null || _preload is null ||
            _portalOrientation is null || _portalVelocity is null || _portalYaw is null ||
            _portalBeforeHook is null || _portalAfterHook is null || _portalHooksRequired is null ||
            _sourceEntity.Selected < 0 || _sourceEntity.Selected >= _sourceEntityIds.Count ||
            _destinationArea.Selected < 0 || _destinationArea.Selected >= _destinationAreaIds.Count ||
            _destinationEntity.Selected < 0 || _destinationEntity.Selected >= _destinationEntityIds.Count)
        {
            SetStatus("Choisissez une source et une destination valides.", true);
            return;
        }

        var destinationAreaId = _destinationAreaIds[_destinationArea.Selected];
        var displayName = _portalName.Text.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            var destination = state.Project.Areas.Single(area => area.Id == destinationAreaId);
            displayName = $"Vers {destination.DisplayName}";
        }

        var portal = new PortalLink(
            PortalId.From(Guid.NewGuid()),
            displayName,
            _sourceEntityIds[_sourceEntity.Selected],
            destinationAreaId,
            _destinationEntityIds[_destinationEntity.Selected],
            _bidirectional.ButtonPressed,
            _preload.ButtonPressed,
            provenance: "toolset:area-workspace-r029",
            orientation: (PortalOrientationPolicy)_portalOrientation.Selected,
            fixedYawDegrees: (float)_portalYaw.Value,
            velocity: (PortalVelocityPolicy)_portalVelocity.Selected,
            beforeTransitionScript: _portalBeforeHook.Text,
            afterTransitionScript: _portalAfterHook.Text,
            hooksRequiredAtRuntime: _portalHooksRequired.ButtonPressed);
        if (_upsertPortal?.Invoke(sourceAreaId, portal) != true)
        {
            SetStatus("La transition a été refusée. Vérifiez ses références.", true);
            return;
        }

        _portalName.Text = string.Empty;
        SetStatus($"Transition « {displayName} » créée.", false);
        Refresh(sourceAreaId);
    }

    private void PreviewAreaClone()
    {
        var state = _getState?.Invoke();
        if (state is null || _selectedAreaId is null || _cloneMode is null ||
            _preserveExternalPortals is null || _cloneName is null || _applyCloneButton is null)
        {
            SetStatus("Sélectionnez une zone à copier.", true);
            return;
        }

        var source = state.Project.Areas.Single(area => area.Id == _selectedAreaId);
        var mode = _cloneMode.Selected == 1 ? AreaCloneMode.Template : AreaCloneMode.Full;
        var externalPolicy = _preserveExternalPortals.ButtonPressed
            ? ExternalPortalPolicy.Preserve
            : ExternalPortalPolicy.Drop;
        var preview = AreaDuplicationPreview.Create(source, mode, externalPolicy);
        if (string.IsNullOrWhiteSpace(_cloneName.Text))
        {
            _cloneName.Text = $"{source.DisplayName} — Copie";
        }

        _cloneSourceAreaId = source.Id;
        _applyCloneButton.Disabled = false;
        SetStatus(
            $"Aperçu · {preview.EntityCount} entité(s), {preview.InternalPortalCount} transition(s) interne(s), " +
            $"{preview.ExternalPortalCount} externe(s) · nouveaux UUID à l’application.",
            false);
    }

    private void ApplyAreaClone()
    {
        if (_cloneSourceAreaId is null || _cloneName is null || _cloneMode is null ||
            _preserveExternalPortals is null)
        {
            SetStatus("Prévisualisez d’abord la copie de zone.", true);
            return;
        }

        var name = _cloneName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Donnez un nom à la copie de zone.", true);
            return;
        }

        var mode = _cloneMode.Selected == 1 ? AreaCloneMode.Template : AreaCloneMode.Full;
        var externalPolicy = _preserveExternalPortals.ButtonPressed
            ? ExternalPortalPolicy.Preserve
            : ExternalPortalPolicy.Drop;
        var cloneId = _duplicateArea?.Invoke(_cloneSourceAreaId, name, mode, externalPolicy);
        if (cloneId is null)
        {
            SetStatus("La copie a été refusée. Consultez les diagnostics du projet.", true);
            return;
        }

        SetStatus($"Zone « {name} » créée depuis l’aperçu.", false);
        ResetAreaClonePreview();
        Refresh(cloneId);
    }

    private void ResetAreaClonePreview()
    {
        _cloneSourceAreaId = null;
        if (_applyCloneButton is not null)
        {
            _applyCloneButton.Disabled = true;
        }
    }

    private void RefreshActiveArea(ProjectState state)
    {
        var activeId = _getActiveAreaId?.Invoke();
        var activeArea = state.Project.Areas.FirstOrDefault(area => area.Id == activeId) ?? state.Project.Areas.FirstOrDefault();
        _sourceEntityIds.Clear();
        _sourceEntity!.Clear();
        _destinationAreaIds.Clear();
        _destinationArea!.Clear();
        if (activeArea is null)
        {
            _activeAreaSummary!.Text = "Aucune zone active";
            _destinationEntityIds.Clear();
            _destinationEntity!.Clear();
            return;
        }

        _activeAreaSummary!.Text =
            $"{activeArea.DisplayName} · {FormatKind(activeArea.Kind)} · document v{activeArea.SchemaVersion}";
        _resizeWidth!.Value = activeArea.Dimensions.Width;
        _resizeDepth!.Value = activeArea.Dimensions.Depth;
        foreach (var entity in activeArea.Entities)
        {
            _sourceEntityIds.Add(entity.Id);
            _sourceEntity.AddItem(FormatEntity(state, entity));
        }

        _playerBlueprintIds.Clear();
        _playtestPlayerBlueprint!.Clear();
        foreach (var blueprint in state.Project.Blueprints.Where(item => item.Kind == BlueprintKind.PlayerSpawn))
        {
            _playerBlueprintIds.Add(blueprint.Id);
            _playtestPlayerBlueprint.AddItem(blueprint.DisplayName);
        }
        var playtest = activeArea.Playtest;
        _playtestMode!.Selected = (int)playtest.StartMode;
        _playtestProfile!.Text = playtest.TestProfile;
        _playtestTargetKind!.Selected = (int)playtest.TargetKind;
        _playtestHeadless!.ButtonPressed = playtest.Headless;
        _playtestX!.Value = playtest.CameraTransform.Position.X;
        _playtestY!.Value = playtest.CameraTransform.Position.Y;
        _playtestZ!.Value = playtest.CameraTransform.Position.Z;
        if (playtest.PlayerBlueprintId is not null)
            _playtestPlayerBlueprint.Selected = _playerBlueprintIds.FindIndex(item => item == playtest.PlayerBlueprintId);
        if (_playtestPlayerBlueprint.Selected < 0 && _playerBlueprintIds.Count > 0)
            _playtestPlayerBlueprint.Selected = 0;
        RefreshPlaytestStartOptions();
        RefreshPlaytestTargets();

        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        {
            _destinationAreaIds.Add(area.Id);
            _destinationArea.AddItem(area.DisplayName);
        }

        var preferredDestination = _destinationAreaIds.FindIndex(id => id != activeArea.Id);
        _destinationArea.Selected = preferredDestination >= 0 ? preferredDestination : 0;
        RefreshDestinationEntities();
    }

    private void RefreshPlaytestStartOptions()
    {
        var state = _getState?.Invoke();
        var area = state?.Project.Areas.FirstOrDefault(item => item.Id == _getActiveAreaId?.Invoke());
        if (area is null || _playtestMode is null || _playtestStart is null) return;
        _playtestStart.Clear();
        _playtestSpawnEntityIds.Clear();
        _playtestWaypointIds.Clear();
        var mode = (PlayFromHereStartMode)_playtestMode.Selected;
        if (mode == PlayFromHereStartMode.PlayerSpawn)
        {
            var players = state!.Project.Blueprints.Where(item => item.Kind == BlueprintKind.PlayerSpawn)
                .Select(item => item.Id).ToHashSet();
            foreach (var entity in area.Entities.Where(item => players.Contains(item.BlueprintId)))
            {
                _playtestSpawnEntityIds.Add(entity.Id);
                _playtestStart.AddItem($"Spawn {entity.Id.ToString()[..8]}");
            }
            var selected = area.Playtest.SpawnEntityId is null ? -1 :
                _playtestSpawnEntityIds.FindIndex(item => item == area.Playtest.SpawnEntityId);
            if (selected >= 0) _playtestStart.Selected = selected;
        }
        else if (mode == PlayFromHereStartMode.Waypoint)
        {
            foreach (var waypoint in area.Navigation.Anchors.Instances)
            {
                _playtestWaypointIds.Add(waypoint.Id);
                _playtestStart.AddItem($"Waypoint {waypoint.Id.ToString()[..8]}");
            }
            var selected = area.Playtest.WaypointInstanceId is null ? -1 :
                _playtestWaypointIds.FindIndex(item => item == area.Playtest.WaypointInstanceId);
            if (selected >= 0) _playtestStart.Selected = selected;
        }
        else
        {
            _playtestStart.AddItem("Position de caméra X/Y/Z");
            _playtestStart.Selected = 0;
        }
        _playtestPreview!.Text = $"Aperçu · mode {mode} · profil {_playtestProfile!.Text} · snapshot temporaire, 0 écriture source";
    }

    private void RefreshPlaytestTargets()
    {
        var state = _getState?.Invoke();
        if (state is null || _playtestTargetKind is null || _playtestTarget is null) return;
        _playtestTarget.Clear();
        _playtestTargetIds.Clear();
        var kind = (PlaytestTargetKind)_playtestTargetKind.Selected;
        if (kind == PlaytestTargetKind.World)
        {
            _playtestTarget.AddItem("Monde courant");
            _playtestTarget.Disabled = true;
            return;
        }
        _playtestTarget.Disabled = false;
        if (kind == PlaytestTargetKind.SelectedQuest)
            foreach (var quest in state.Project.Quests.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                _playtestTargetIds.Add(quest.Id.Value);
                _playtestTarget.AddItem(quest.DisplayName);
            }
        else
            foreach (var dialogue in state.Project.Dialogues.OrderBy(item => item.DisplayName, StringComparer.Ordinal))
            {
                _playtestTargetIds.Add(dialogue.Id.Value);
                _playtestTarget.AddItem(dialogue.DisplayName);
            }
        var current = state.Project.Areas.Single(item => item.Id == _getActiveAreaId!.Invoke()).Playtest.TargetId;
        var selected = current is null ? -1 : _playtestTargetIds.IndexOf(current.Value);
        if (selected >= 0) _playtestTarget.Selected = selected;
    }

    private void ConfigureAndStartPlaytest() => ConfigurePlaytest(launch: true);

    private void ConfigurePlaytest(bool launch)
    {
        var area = _getState?.Invoke()?.Project.Areas.FirstOrDefault(item => item.Id == _getActiveAreaId?.Invoke());
        if (area is null || _playtestMode is null || _playtestPlayerBlueprint is null ||
            _playtestPlayerBlueprint.Selected < 0 || _playtestPlayerBlueprint.Selected >= _playerBlueprintIds.Count)
        {
            SetStatus("Ajoutez puis choisissez un blueprint PlayerSpawn.", true);
            return;
        }
        var mode = (PlayFromHereStartMode)_playtestMode.Selected;
        EntityId? spawn = mode == PlayFromHereStartMode.PlayerSpawn && _playtestStart!.Selected >= 0 &&
            _playtestStart.Selected < _playtestSpawnEntityIds.Count
            ? _playtestSpawnEntityIds[_playtestStart.Selected] : null;
        Guid? waypoint = mode == PlayFromHereStartMode.Waypoint && _playtestStart!.Selected >= 0 &&
            _playtestStart.Selected < _playtestWaypointIds.Count
            ? _playtestWaypointIds[_playtestStart.Selected] : null;
        var targetKind = (PlaytestTargetKind)_playtestTargetKind!.Selected;
        Guid? targetId = targetKind == PlaytestTargetKind.World || _playtestTarget!.Selected < 0 ||
            _playtestTarget.Selected >= _playtestTargetIds.Count
            ? null
            : _playtestTargetIds[_playtestTarget.Selected];
        var injections = area.Playtest.QuestInjections.ToList();
        if (targetKind == PlaytestTargetKind.SelectedQuest && targetId is { } questTarget &&
            injections.All(item => item.QuestId.Value != questTarget))
        {
            var quest = _getState!.Invoke()!.Project.Quests.Single(item => item.Id.Value == questTarget);
            injections.Add(new PlaytestQuestInjection(Guid.NewGuid(), quest.Id, quest.InitialStateId,
                new Dictionary<string, string>()));
        }
        var profile = new AreaPlaytestProfile(area.Playtest.Id, AreaPlaytestProfile.CurrentSchemaVersion,
            "toolset:area-workspace-r029", true, area.Playtest.RequiredAtRuntime, mode, spawn,
            new TransformValue(new Vector3Value((float)_playtestX!.Value, (float)_playtestY!.Value,
                (float)_playtestZ!.Value), QuaternionValue.Identity, Vector3Value.One), waypoint,
            _playerBlueprintIds[_playtestPlayerBlueprint.Selected], _playtestProfile!.Text,
            injections, targetKind, targetId, _playtestHeadless!.ButtonPressed);
        if (_setAreaPlaytest?.Invoke(area.Id, profile) != true)
        {
            SetStatus("Configuration Play From Here refusée : vérifiez le point de départ.", true);
            return;
        }
        SetStatus($"Play From Here configuré depuis {mode}.", false);
        Refresh(area.Id);
        if (launch) _startPlaytest?.Invoke();
    }

    private void RefreshDestinationEntities()
    {
        var state = _getState?.Invoke();
        _destinationEntityIds.Clear();
        _destinationEntity!.Clear();
        if (state is null || _destinationArea!.Selected < 0 ||
            _destinationArea.Selected >= _destinationAreaIds.Count)
        {
            return;
        }

        var destination = state.Project.Areas.Single(area => area.Id == _destinationAreaIds[_destinationArea.Selected]);
        foreach (var entity in destination.Entities)
        {
            _destinationEntityIds.Add(entity.Id);
            _destinationEntity.AddItem(FormatEntity(state, entity));
        }
    }

    private void RefreshValidation(ProjectState state)
    {
        var diagnostics = ProjectValidator.Validate(state.Project)
            .Where(item => item.Path.StartsWith("areas/", StringComparison.Ordinal) ||
                item.Code.StartsWith("area.", StringComparison.Ordinal))
            .ToList();
        foreach (var area in state.Project.Areas)
        {
            diagnostics.AddRange(AreaGeometryValidator.Validate(state.Project, area));
        }

        var activeArea = state.Project.Areas.FirstOrDefault(area => area.Id == _getActiveAreaId?.Invoke());
        if (activeArea is not null)
        {
            var readiness = AreaReadinessService.Run(state.Project, activeArea.Id);
            diagnostics.AddRange(readiness.Diagnostics.Select(item =>
                new DomainDiagnostic(item.Code, $"[{item.Category}] {item.Message}", item.Path)));
            try
            {
                diagnostics.AddRange(AreaNavigationValidator.AnalyzeAll(state.Project, activeArea)
                    .SelectMany(analysis => analysis.Diagnostics));
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Add(new DomainDiagnostic(
                    "area.navigation.bake_too_dense",
                    exception.Message,
                    $"areas/{activeArea.Id}/navigation/cellSize"));
            }
        }

        var distinctDiagnostics = diagnostics.Distinct().ToArray();
        _validationList!.Clear();
        if (distinctDiagnostics.Length == 0)
        {
            _validationSummary!.Text = "✓ 9/9 contrôles de zone valides";
            _validationSummary.AddThemeColorOverride("font_color", new Color("83d6ae"));
            _validationList.AddItem("Aucune erreur de zone pour la révision courante.");
            _validationList.SetItemSelectable(0, false);
            return;
        }

        _validationSummary!.Text = $"{distinctDiagnostics.Length} problème(s) à corriger";
        _validationSummary.AddThemeColorOverride("font_color", new Color("ff8c8c"));
        foreach (var diagnostic in distinctDiagnostics)
        {
            _validationList.AddItem($"{diagnostic.Code} · {diagnostic.Message}");
            _validationList.SetItemTooltip(_validationList.ItemCount - 1, diagnostic.Path);
        }
    }

    private static string FormatEntity(ProjectState state, EntityInstance entity)
    {
        var blueprint = state.Project.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
        return $"{blueprint?.DisplayName ?? "Blueprint manquant"} · {entity.Id.ToString()[..8]}";
    }

    private static SpinBox DimensionSpinBox() => new()
    {
        MinValue = AreaDimensions.MinimumSize,
        MaxValue = AreaDimensions.MaximumSize,
        Step = 4,
        Value = 64,
        CustomMinimumSize = new Vector2(0, 38),
    };

    private static Label MutedLabel(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color("a899b9"));
        return label;
    }

    private static VBoxContainer AddCard(HBoxContainer parent, Vector2 minimumSize, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimumSize,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("211a2d", "47365e", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    private static StyleBoxFlat Card(string background, string border, int borderWidth, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };

    private void SetStatus(string text, bool error)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", error ? new Color("ff8c8c") : new Color("9ee6b3"));
    }

    private static string FormatKind(AreaKind kind) => kind switch
    {
        AreaKind.Interior => "Intérieur",
        AreaKind.Exterior => "Extérieur",
        _ => kind.ToString(),
    };

    private static string FormatTemplate(AreaTemplateKind template) => template switch
    {
        AreaTemplateKind.Empty => "Vide",
        AreaTemplateKind.Playable => "Jouable · point d’apparition",
        _ => template.ToString(),
    };
}
#endif
