#if TOOLS
using System.Globalization;
using Forge.Application.Navigation;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Godot;

namespace Forge.Toolset.Editor;

public partial class NavigationConfigurationWindow : Window
{
    private readonly List<AreaId> _areaIds = [];
    private readonly List<NavigationAgentProfile> _agents = [];
    private readonly List<OffMeshNavigationLink> _links = [];
    private readonly List<AnchorDefinition> _anchorDefinitions = [];
    private readonly List<AnchorInstance> _anchorInstances = [];
    private readonly List<WaypointProfile> _waypoints = [];
    private readonly List<PatrolRoute> _patrolRoutes = [];
    private readonly List<GenericLocator> _locators = [];
    private readonly List<AnchorCollection> _locatorCollections = [];
    private readonly NavigationLinkKind[] _linkKinds = Enum.GetValues<NavigationLinkKind>();
    private readonly AnchorKind[] _anchorKinds = Enum.GetValues<AnchorKind>();
    private readonly AnchorScope[] _anchorScopes = Enum.GetValues<AnchorScope>();
    private readonly PortalOrientationPolicy[] _orientationPolicies = Enum.GetValues<PortalOrientationPolicy>();
    private readonly PortalVelocityPolicy[] _velocityPolicies = Enum.GetValues<PortalVelocityPolicy>();
    private readonly MarkerVisibilityPolicy[] _markerPolicies = Enum.GetValues<MarkerVisibilityPolicy>();
    private readonly MarkerVisibilityScope[] _markerScopes = Enum.GetValues<MarkerVisibilityScope>();
    private readonly PatrolTraversalPolicy[] _traversalPolicies = Enum.GetValues<PatrolTraversalPolicy>();
    private readonly ArrivalFacingMode[] _arrivalFacingModes = Enum.GetValues<ArrivalFacingMode>();
    private readonly LocatorPurpose[] _locatorPurposes = Enum.GetValues<LocatorPurpose>();
    private readonly LocatorSelectionMode[] _locatorSelectionModes = Enum.GetValues<LocatorSelectionMode>();

    private Func<ProjectState?>? _getState;
    private Func<AreaId?>? _getActiveAreaId;
    private Func<AreaId, AreaNavigationDefinition, bool>? _apply;
    private Action<NavigationSurface?>? _showOverlay;
    private Action<Window>? _brandWindow;
    private AreaNavigationDefinitionId? _definitionId;
    private Guid _catalogId;
    private string _catalogProvenance = "toolset:r009";
    private bool _catalogRequiredAtRuntime;
    private Guid _waypointProfileId;
    private string _waypointProvenance = "toolset:r064";
    private bool _waypointsRequiredAtRuntime;
    private RouteVisualizationProfile? _routeVisualization;
    private TabContainer? _tabs;
    private OptionButton? _areaPicker;
    private SpinBox? _cellSize;
    private ItemList? _agentList;
    private LineEdit? _agentName;
    private SpinBox? _agentRadius;
    private SpinBox? _agentHeight;
    private CheckBox? _agentDoors;
    private CheckBox? _agentLinks;
    private CheckBox? _agentFlying;
    private ItemList? _linkList;
    private LineEdit? _linkName;
    private OptionButton? _linkKind;
    private SpinBox? _startX;
    private SpinBox? _startY;
    private SpinBox? _startZ;
    private SpinBox? _endX;
    private SpinBox? _endY;
    private SpinBox? _endZ;
    private SpinBox? _linkRadius;
    private CheckBox? _linkBidirectional;
    private ItemList? _diagnostics;
    private Label? _summary;
    private Label? _status;
    private ItemList? _anchorList;
    private LineEdit? _anchorSlug;
    private LineEdit? _anchorLabelKey;
    private LineEdit? _anchorLabel;
    private LineEdit? _anchorTags;
    private OptionButton? _anchorKind;
    private OptionButton? _anchorScope;
    private LineEdit? _anchorBlueprint;
    private LineEdit? _anchorAsset;
    private CheckBox? _anchorRequired;
    private SpinBox? _anchorX;
    private SpinBox? _anchorY;
    private SpinBox? _anchorZ;
    private SpinBox? _anchorYaw;
    private LineEdit? _anchorOverrides;
    private LineEdit? _spawnProfile;
    private LineEdit? _spawnTeam;
    private SpinBox? _spawnPriority;
    private SpinBox? _spawnClearanceX;
    private SpinBox? _spawnClearanceY;
    private SpinBox? _spawnClearanceZ;
    private SpinBox? _spawnOrientation;
    private LineEdit? _spawnFallback;
    private CheckBox? _spawnRequireNavigation;
    private OptionButton? _portalTargetArea;
    private LineEdit? _portalTargetAnchor;
    private OptionButton? _portalOrientation;
    private SpinBox? _portalYaw;
    private OptionButton? _portalVelocity;
    private CheckBox? _portalPreload;
    private RichTextLabel? _anchorPreview;
    private ItemList? _waypointList;
    private LineEdit? _waypointName;
    private LineEdit? _waypointIcon;
    private LineEdit? _waypointColor;
    private LineEdit? _waypointLayer;
    private SpinBox? _waypointScreenSize;
    private SpinBox? _waypointX;
    private SpinBox? _waypointY;
    private SpinBox? _waypointZ;
    private CheckBox? _waypointVisible;
    private CheckBox? _waypointRequired;
    private OptionButton? _waypointArrivalFacing;
    private SpinBox? _waypointArrivalDirection;
    private LineEdit? _waypointArrivalTarget;
    private LineEdit? _waypointArrivalAnimation;
    private SpinBox? _waypointArrivalWait;
    private LineEdit? _waypointArrivalInteraction;
    private LineEdit? _waypointArrivalCallback;
    private LineEdit? _markerText;
    private LineEdit? _markerIcon;
    private SpinBox? _markerFloor;
    private LineEdit? _markerCategory;
    private SpinBox? _markerPrecision;
    private OptionButton? _markerPolicy;
    private OptionButton? _markerScope;
    private CheckBox? _markerTracked;
    private CheckBox? _markerRevealed;
    private LineEdit? _markerRevealEvent;
    private LineEdit? _markerRevealAnimation;
    private CheckBox? _markerPersistent;
    private ItemList? _routeList;
    private OptionButton? _routeTraversal;
    private TextEdit? _routePoints;
    private LineEdit? _routeScript;
    private CheckBox? _routeRequired;
    private WaypointRoutePreview? _waypointPreview;
    private RichTextLabel? _waypointSummary;
    private ItemList? _locatorList;
    private LineEdit? _locatorName;
    private OptionButton? _locatorPurpose;
    private LineEdit? _locatorTags;
    private SpinBox? _locatorX;
    private SpinBox? _locatorY;
    private SpinBox? _locatorZ;
    private OptionButton? _arrivalFacing;
    private SpinBox? _arrivalDirection;
    private LineEdit? _arrivalTarget;
    private LineEdit? _arrivalAnimation;
    private SpinBox? _arrivalWait;
    private LineEdit? _arrivalInteraction;
    private LineEdit? _arrivalCallback;
    private LineEdit? _locatorAsset;
    private CheckBox? _locatorRequired;
    private ItemList? _collectionList;
    private LineEdit? _collectionName;
    private LineEdit? _collectionQuery;
    private LineEdit? _collectionTags;
    private OptionButton? _collectionPurpose;
    private OptionButton? _collectionSelection;
    private SpinBox? _collectionSeed;
    private CheckBox? _collectionRequired;
    private CheckBox? _visualSegments;
    private CheckBox? _visualDirection;
    private CheckBox? _visualCosts;
    private CheckBox? _visualActions;
    private CheckBox? _visualBroken;
    private CheckBox? _visualNavProjection;
    private CheckBox? _visualTiming;
    private SpinBox? _visualCostPerMeter;
    private CheckBox? _visualRequired;
    private WaypointRoutePreview? _routeDiagnosticsPreview;
    private RichTextLabel? _locatorSummary;
    private int _selectedAgent = -1;
    private int _selectedLink = -1;
    private int _selectedAnchor = -1;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;
    private bool _r064Capture;
    private bool _r065Capture;
    private int _selectedWaypoint = -1;
    private int _selectedRoute = -1;
    private int _selectedLocator = -1;
    private int _selectedCollection = -1;

    public int AgentCount => _agents.Count;

    public int LinkCount => _links.Count;

    public int AnchorCount => _anchorInstances.Count;

    public int WaypointCount => _waypoints.Count;

    public int PatrolRouteCount => _patrolRoutes.Count;

    public int LocatorCount => _locators.Count;

    public int LocatorCollectionCount => _locatorCollections.Count;

    public int DiagnosticCount => _diagnostics?.ItemCount ?? 0;

    public bool LastPathSucceeded { get; private set; }

    public void Initialize(
        Func<ProjectState?> getState,
        Func<AreaId?> getActiveAreaId,
        Func<AreaId, AreaNavigationDefinition, bool> apply,
        Action<NavigationSurface?> showOverlay,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _getActiveAreaId = getActiveAreaId ?? throw new ArgumentNullException(nameof(getActiveAreaId));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _showOverlay = showOverlay ?? throw new ArgumentNullException(nameof(showOverlay));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Navigation & géométrie · OpenRPG Forge";
        MinSize = new Vector2I(1440, 900);
        Size = MinSize;
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        CloseRequested += Close;
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--waypoint-anchor-ui-capture=", StringComparison.Ordinal));
        var r064Capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r064-waypoint-patrol-ui-capture=", StringComparison.Ordinal));
        var r065Capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r065-waypoint-locator-ui-capture=", StringComparison.Ordinal));
        if (r065Capture is not null)
        {
            _capturePath = r065Capture["--r065-waypoint-locator-ui-capture=".Length..];
            _r065Capture = true;
            SetProcess(true);
            return;
        }
        if (r064Capture is not null)
        {
            _capturePath = r064Capture["--r064-waypoint-patrol-ui-capture=".Length..];
            _r064Capture = true;
            SetProcess(true);
            return;
        }
        if (capture is not null)
        {
            _capturePath = capture["--waypoint-anchor-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_capturePrepared && _captureDelay >= 1)
        {
            Open();
            if (_r065Capture)
            {
                ConfigureForR065SmokeTest();
                _tabs!.CurrentTab = 3;
            }
            else if (_r064Capture)
            {
                ConfigureForR064SmokeTest();
                _tabs!.CurrentTab = 2;
            }
            else
            {
                ConfigureForR009SmokeTest();
                _tabs!.CurrentTab = 1;
            }
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        var marker = _r065Capture ? "OPENRPG_FORGE_R065_WAYPOINT_LOCATOR_UI_CAPTURE" :
            _r064Capture ? "OPENRPG_FORGE_R064_WAYPOINT_PATROL_UI_CAPTURE" :
                "OPENRPG_FORGE_WAYPOINT_ANCHOR_UI_CAPTURE";
        GD.Print($"{marker}_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        RefreshAreas();
        Exclusive = true;
        PopupCentered(MinSize);
        _brandWindow?.Invoke(this);
    }

    public bool ConfigureForSmokeTest(AreaId areaId, AreaNavigationDefinition navigation)
    {
        RefreshAreas(areaId);
        if (!_areaIds.Contains(areaId))
        {
            return false;
        }

        LoadDraft(navigation);
        return true;
    }

    public bool PreviewForSmokeTest()
    {
        Preview();
        return DiagnosticCount >= 0;
    }

    public bool ApplyForSmokeTest() => Apply();

    public bool ConfigureForR009SmokeTest()
    {
        var state = _getState?.Invoke();
        var area = SelectedArea(state);
        if (state is null || area is null) return false;
        var spawnBlueprint = state.Project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.PlayerSpawn);
        if (spawnBlueprint is null) return false;
        const string smokeProvenance = "toolset:r009";
        _anchorDefinitions.RemoveAll(item => string.Equals(
            item.Provenance, smokeProvenance, StringComparison.Ordinal));
        _anchorInstances.RemoveAll(item => string.Equals(
            item.Provenance, smokeProvenance, StringComparison.Ordinal));
        var spawnDefinition = CreateSmokeDefinition("r009-player-start", "anchor.r009.player_start", "Départ de preuve",
            AnchorKind.Spawn, ["player", "spawn", "r009-smoke"], spawnBlueprint.Id);
        var destinationDefinition = CreateSmokeDefinition("r009-exit", "anchor.r009.exit", "Sortie de preuve",
            AnchorKind.TransitionDestination, ["exit", "portal", "r009-smoke"], null);
        var waypointDefinition = CreateSmokeDefinition("r009-marker", "anchor.r009.marker", "Repère de preuve",
            AnchorKind.Waypoint, ["ember", "quest", "r009-smoke"], null);
        var spawnId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var waypointId = Guid.NewGuid();
        _anchorDefinitions.AddRange([spawnDefinition, destinationDefinition, waypointDefinition]);
        var displayOrder = _anchorInstances.Count == 0 ? 1 : _anchorInstances.Max(item => item.DisplayOrder) + 1;
        _anchorInstances.AddRange([
            new AnchorInstance(spawnId, 1, smokeProvenance, spawnDefinition.Id,
                At(0, 0, 9, 180), displayOrder, EntityPropertyOverrides.Empty,
                new SpawnAnchorProfile("r009-player", new Vector3Value(1.2f, 2f, 1.2f), 100, "r009-team", 180,
                    null, true), null),
            new AnchorInstance(destinationId, 1, smokeProvenance, destinationDefinition.Id,
                At(4, 0, 1, 90), displayOrder + 1, EntityPropertyOverrides.Empty, null,
                new PortalAnchorDestination(area.Id, waypointId, PortalOrientationPolicy.AnchorRotation, 0,
                    PortalVelocityPolicy.RedirectForward, true)),
            new AnchorInstance(waypointId, 1, smokeProvenance, waypointDefinition.Id,
                At(-3, 0, -2, 45), displayOrder + 2,
                new EntityPropertyOverrides(1, [new EntityPropertyOverride("quest.marker", EntityPropertyValueKind.String, "ember")]),
                null, null),
        ]);
        RefreshAnchorList();
        var selectedIndex = _anchorInstances.FindIndex(item => item.Id == spawnId);
        _anchorList!.Select(selectedIndex);
        SelectAnchor(selectedIndex);
        Preview();
        return DiagnosticCount == 0 &&
               _anchorInstances.Count(item => string.Equals(
                   item.Provenance, smokeProvenance, StringComparison.Ordinal)) == 3;
    }

    public bool ConfigureForR064SmokeTest()
    {
        var area = SelectedArea(_getState?.Invoke());
        if (area is null) return false;
        _waypointProfileId = Guid.Parse("a0645000-0000-4000-8000-000000000001");
        _waypointProvenance = "toolset:r064";
        _waypointsRequiredAtRuntime = true;
        _waypoints.Clear();
        _waypoints.AddRange([
            new(Guid.Parse("a0645000-0000-4000-8000-000000000011"), 1, "toolset:r064",
                "Entrée de la crypte", new(-6, 0, -3), new("flag", "#ff8844", "quête", true, 32),
                new("Entrée révélée", "quest", -1, "quête", 2,
                    MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party,
                    true, false, "quest.crypt.found", "pulse", true), RequiredAtRuntime: true),
            new(Guid.Parse("a0645000-0000-4000-8000-000000000012"), 1, "toolset:r064",
                "Brasero central", Vector3Value.Zero, new("diamond", "#83d6ae", "patrouille", true, 26),
                null, RequiredAtRuntime: true),
            new(Guid.Parse("a0645000-0000-4000-8000-000000000013"), 1, "toolset:r064",
                "Porte des cendres", new(6, 0, 4), new("arrow", "#a78bfa", "patrouille", true, 28),
                null, RequiredAtRuntime: true),
        ]);
        var points = _waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position,
            index == 1 ? 2 : 0, index * 90, index == 2 ? 3 : 2,
            index == 1 ? "inspect" : null, index == 0 ? "quest.crypt.active" : null,
            index + 1)).ToArray();
        _patrolRoutes.Clear();
        _patrolRoutes.Add(new(Guid.Parse("a0645000-0000-4000-8000-000000000020"), 1,
            "toolset:r064", PatrolTraversalPolicy.PingPong, points, requiredAtRuntime: true));
        _locators.Clear();
        _locatorCollections.Clear();
        _routeVisualization = RouteVisualizationProfile.CreateDefault(area.Id);
        _selectedWaypoint = 0;
        _selectedRoute = 0;
        RefreshWaypointLists();
        SelectWaypoint(0);
        SelectRoute(0);
        RefreshWaypointPreview();
        return WaypointCount == 3 && PatrolRouteCount == 1;
    }

    public bool ConfigureForR065SmokeTest()
    {
        var area = SelectedArea(_getState?.Invoke());
        if (area is null || !ConfigureForR064SmokeTest()) return false;
        _waypointProvenance = "toolset:r065";
        _waypoints[0] = _waypoints[0] with
        {
            Provenance = "toolset:r065",
            Arrival = new ArrivalBehavior(ArrivalFacingMode.Target, 0, "gate.ashes", "inspect",
                1.5, "open", "waypoint.arrived"),
        };
        _locators.AddRange([
            new GenericLocator(Guid.Parse("a0655000-0000-4000-8000-000000000011"), 1,
                "toolset:r065", "Brasero VFX", new(-3, 0, 2), LocatorPurpose.Vfx,
                ["crypt", "fire"],
                new ArrivalBehavior(ArrivalFacingMode.Direction, 90, null, "ignite", .5,
                    "inspect", "locator.vfx.arrived"), "vfx:ember-burst", RequiredAtRuntime: true),
            new GenericLocator(Guid.Parse("a0655000-0000-4000-8000-000000000012"), 1,
                "toolset:r065", "Cloche audio", new(1, 0, 3), LocatorPurpose.Audio,
                ["crypt", "ritual"], null, "audio:crypt-bell"),
            new GenericLocator(Guid.Parse("a0655000-0000-4000-8000-000000000013"), 1,
                "toolset:r065", "Objectif des cendres", new(5, 0, 1), LocatorPurpose.Objective,
                ["crypt", "quest"], null),
        ]);
        _locatorCollections.AddRange([
            new AnchorCollection(Guid.Parse("a0655000-0000-4000-8000-000000000021"), 1,
                "toolset:r065", "crypt", _locators.Select(item => item.Id).ToArray(),
                "Ancres de la crypte", RequiredTags: ["crypt"]),
            new AnchorCollection(Guid.Parse("a0655000-0000-4000-8000-000000000022"), 1,
                "toolset:r065", "crypt", _locators.Select(item => item.Id).ToArray(),
                "Ancre la plus proche", RequiredTags: ["crypt"],
                Selection: LocatorSelectionMode.Nearest, Origin: Vector3Value.Zero, Seed: 2026,
                RequiredAtRuntime: true),
        ]);
        _routeVisualization = new RouteVisualizationProfile(
            Guid.Parse("a0655000-0000-4000-8000-000000000030"), 1, "toolset:r065",
            true, true, true, true, true, true, true, 1.25, true);
        _selectedLocator = 0;
        _selectedCollection = 0;
        LoadVisualizationDraft();
        RefreshLocatorLists();
        SelectLocator(0);
        SelectCollection(0);
        RefreshWaypointPreview();
        return LocatorCount == 3 && LocatorCollectionCount == 2 &&
               _waypoints[0].Arrival?.Facing == ArrivalFacingMode.Target;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("120f1c", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", side is "left" or "right" ? 22 : 18);
        }
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 12);
        root.AddChild(heading);
        var title = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddChild(AccentLabel("NAVIGATION & CONTRÔLES SPATIAUX"));
        title.AddChild(MutedLabel(
            "Configurez les tailles d’agents, testez les portes dynamiques et reliez les ruptures de parcours."));
        heading.AddChild(title);
        _areaPicker = new OptionButton { CustomMinimumSize = new Vector2(280, 42) };
        _areaPicker.ItemSelected += _ => LoadSelectedArea();
        heading.AddChild(_areaPicker);
        _cellSize = Spin(0.25, 4, 0.25, 1);
        _cellSize.CustomMinimumSize = new Vector2(105, 42);
        _cellSize.TooltipText = "Taille de cellule du bake, en mètres";
        heading.AddChild(_cellSize);

        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(_tabs);
        var body = new HBoxContainer
        {
            Name = "Navigation",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 14);
        _tabs.AddChild(body);
        BuildAgentsCard(body);
        BuildLinksCard(body);
        BuildDiagnosticsCard(body);
        var anchors = new HBoxContainer
        {
            Name = "Ancres & portails",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        anchors.AddThemeConstantOverride("separation", 14);
        _tabs.AddChild(anchors);
        BuildAnchorsCard(anchors);
        BuildAnchorIdentityCard(anchors);
        BuildAnchorPolicyCard(anchors);
        var waypoints = new HBoxContainer
        {
            Name = "Waypoints & patrouilles",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        waypoints.AddThemeConstantOverride("separation", 14);
        _tabs.AddChild(waypoints);
        BuildWaypointCard(waypoints);
        BuildPatrolCard(waypoints);
        BuildWaypointPreviewCard(waypoints);
        var locators = new HBoxContainer
        {
            Name = "Locators & diagnostic",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        locators.AddThemeConstantOverride("separation", 14);
        _tabs.AddChild(locators);
        BuildLocatorCard(locators);
        BuildCollectionCard(locators);
        BuildRouteDiagnosticsCard(locators);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        _status = MutedLabel("Prêt pour l’analyse.");
        _status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(_status);
        var hideOverlay = new Button { Text = "Masquer l’overlay" };
        hideOverlay.Pressed += () => _showOverlay?.Invoke(null);
        footer.AddChild(hideOverlay);
        var preview = new Button { Text = "Analyser & afficher", CustomMinimumSize = new Vector2(175, 42) };
        preview.Pressed += Preview;
        footer.AddChild(preview);
        var apply = new Button { Text = "Appliquer", CustomMinimumSize = new Vector2(150, 42) };
        apply.AddThemeStyleboxOverride("normal", Card("d84f32", "ff9a73", 1, 9));
        apply.Pressed += () => Apply();
        footer.AddChild(apply);
    }

    private void BuildAgentsCard(HBoxContainer body)
    {
        var card = AddCard(body, 330);
        card.AddChild(AccentLabel("PROFILS D’AGENT"));
        _agentList = new ItemList { CustomMinimumSize = new Vector2(0, 150) };
        _agentList.ItemSelected += SelectAgent;
        card.AddChild(_agentList);
        _agentName = new LineEdit { PlaceholderText = "Nom du profil" };
        card.AddChild(_agentName);
        var measures = new HBoxContainer();
        _agentRadius = Spin(0.1, 8, 0.1, 0.5);
        _agentRadius.TooltipText = "Rayon en mètres";
        _agentHeight = Spin(0.2, 16, 0.1, 1.8);
        _agentHeight.TooltipText = "Hauteur en mètres";
        measures.AddChild(_agentRadius);
        measures.AddChild(_agentHeight);
        card.AddChild(measures);
        _agentDoors = new CheckBox { Text = "Peut ouvrir les portes", ButtonPressed = true };
        _agentLinks = new CheckBox { Text = "Utilise les liens hors-maillage", ButtonPressed = true };
        _agentFlying = new CheckBox { Text = "Agent volant" };
        card.AddChild(_agentDoors);
        card.AddChild(_agentLinks);
        card.AddChild(_agentFlying);
        var actions = new HBoxContainer();
        var save = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        save.Pressed += SaveAgent;
        actions.AddChild(save);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveAgent;
        actions.AddChild(remove);
        card.AddChild(actions);
    }

    private void BuildLinksCard(HBoxContainer body)
    {
        var card = AddCard(body, 390);
        card.AddChild(AccentLabel("LIENS HORS-MAILLAGE"));
        _linkList = new ItemList { CustomMinimumSize = new Vector2(0, 125) };
        _linkList.ItemSelected += SelectLink;
        card.AddChild(_linkList);
        var identity = new HBoxContainer();
        _linkName = new LineEdit { PlaceholderText = "Nom du lien", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _linkKind = new OptionButton();
        foreach (var kind in _linkKinds) _linkKind.AddItem(FormatKind(kind));
        identity.AddChild(_linkName);
        identity.AddChild(_linkKind);
        card.AddChild(identity);
        card.AddChild(MutedLabel("Départ X / Y / Z"));
        var start = new HBoxContainer();
        _startX = Coordinate(); _startY = Coordinate(); _startZ = Coordinate();
        start.AddChild(_startX); start.AddChild(_startY); start.AddChild(_startZ);
        card.AddChild(start);
        card.AddChild(MutedLabel("Arrivée X / Y / Z"));
        var end = new HBoxContainer();
        _endX = Coordinate(); _endY = Coordinate(); _endZ = Coordinate();
        end.AddChild(_endX); end.AddChild(_endY); end.AddChild(_endZ);
        card.AddChild(end);
        var options = new HBoxContainer();
        _linkBidirectional = new CheckBox { Text = "Bidirectionnel", ButtonPressed = true };
        _linkRadius = Spin(0.1, 8, 0.1, 1);
        _linkRadius.TooltipText = "Rayon maximal de l’agent";
        options.AddChild(_linkBidirectional);
        options.AddChild(_linkRadius);
        card.AddChild(options);
        var actions = new HBoxContainer();
        var save = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        save.Pressed += SaveLink;
        actions.AddChild(save);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveLink;
        actions.AddChild(remove);
        card.AddChild(actions);
    }

    private void BuildDiagnosticsCard(HBoxContainer body)
    {
        var card = AddCard(body, 430, expand: true);
        card.AddChild(AccentLabel("APERÇU & DIAGNOSTICS"));
        _summary = new Label
        {
            Text = "Lancez l’analyse pour calculer le bake.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _summary.AddThemeColorOverride("font_color", new Color("83d6ae"));
        card.AddChild(_summary);
        _diagnostics = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        card.AddChild(_diagnostics);
        card.AddChild(MutedLabel(
            "États testés : portes courantes, toutes ouvertes et toutes fermées. L’overlay turquoise indique les cellules praticables ; le violet les liaisons."));
    }

    private void BuildWaypointCard(HBoxContainer body)
    {
        var card = AddCard(body, 360);
        card.AddChild(AccentLabel("APPARENCE & MAP NOTE"));
        _waypointList = new ItemList { CustomMinimumSize = new Vector2(0, 105) };
        _waypointList.ItemSelected += SelectWaypoint;
        card.AddChild(_waypointList);
        var identity = new HBoxContainer();
        _waypointName = new LineEdit { PlaceholderText = "Nom du waypoint", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _waypointIcon = new LineEdit { PlaceholderText = "Icône" };
        identity.AddChild(_waypointName); identity.AddChild(_waypointIcon);
        card.AddChild(identity);
        var appearance = new HBoxContainer();
        _waypointColor = new LineEdit { PlaceholderText = "#ff8844", Text = "#ff8844" };
        _waypointLayer = new LineEdit { PlaceholderText = "Couche", Text = "gameplay" };
        _waypointScreenSize = Spin(8, 96, 1, 28);
        _waypointScreenSize.TooltipText = "Taille écran constante en pixels";
        appearance.AddChild(_waypointColor); appearance.AddChild(_waypointLayer);
        appearance.AddChild(_waypointScreenSize);
        card.AddChild(appearance);
        var position = new HBoxContainer();
        _waypointX = Coordinate(); _waypointY = Coordinate(); _waypointZ = Coordinate();
        position.AddChild(_waypointX); position.AddChild(_waypointY); position.AddChild(_waypointZ);
        card.AddChild(position);
        var flags = new HBoxContainer();
        _waypointVisible = new CheckBox { Text = "Visible", ButtonPressed = true };
        _waypointRequired = new CheckBox { Text = "Requis runtime" };
        flags.AddChild(_waypointVisible); flags.AddChild(_waypointRequired);
        card.AddChild(flags);
        var arrivalFacing = new HBoxContainer();
        _waypointArrivalFacing = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var mode in _arrivalFacingModes) _waypointArrivalFacing.AddItem(mode.ToString());
        _waypointArrivalDirection = Spin(-360, 360, 5, 0);
        _waypointArrivalTarget = new LineEdit { PlaceholderText = "Cible à l’arrivée" };
        arrivalFacing.AddChild(_waypointArrivalFacing); arrivalFacing.AddChild(_waypointArrivalDirection);
        arrivalFacing.AddChild(_waypointArrivalTarget);
        card.AddChild(arrivalFacing);
        var arrivalAction = new HBoxContainer();
        _waypointArrivalAnimation = new LineEdit { PlaceholderText = "Animation d’arrivée" };
        _waypointArrivalWait = Spin(0, 3600, .25, 0);
        _waypointArrivalInteraction = new LineEdit { PlaceholderText = "Interaction" };
        arrivalAction.AddChild(_waypointArrivalAnimation); arrivalAction.AddChild(_waypointArrivalWait);
        arrivalAction.AddChild(_waypointArrivalInteraction);
        card.AddChild(arrivalAction);
        _waypointArrivalCallback = new LineEdit { PlaceholderText = "Callback ForgeScript d’arrivée" };
        card.AddChild(_waypointArrivalCallback);
        _markerText = new LineEdit { PlaceholderText = "Texte de map note (vide = aucune)" };
        card.AddChild(_markerText);
        var markerIdentity = new HBoxContainer();
        _markerIcon = new LineEdit { PlaceholderText = "Icône carte" };
        _markerCategory = new LineEdit { PlaceholderText = "Catégorie" };
        _markerFloor = Spin(-64, 64, 1, 0);
        markerIdentity.AddChild(_markerIcon); markerIdentity.AddChild(_markerCategory);
        markerIdentity.AddChild(_markerFloor);
        card.AddChild(markerIdentity);
        var markerPolicy = new HBoxContainer();
        _markerPolicy = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var policy in _markerPolicies) _markerPolicy.AddItem(policy.ToString());
        _markerScope = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var scope in _markerScopes) _markerScope.AddItem(scope.ToString());
        _markerPrecision = Spin(0, 100, .5, 2);
        markerPolicy.AddChild(_markerPolicy); markerPolicy.AddChild(_markerScope);
        markerPolicy.AddChild(_markerPrecision);
        card.AddChild(markerPolicy);
        var markerFlags = new HBoxContainer();
        _markerTracked = new CheckBox { Text = "Suivi", ButtonPressed = true };
        _markerRevealed = new CheckBox { Text = "Révélé" };
        _markerPersistent = new CheckBox { Text = "Persistant", ButtonPressed = true };
        markerFlags.AddChild(_markerTracked); markerFlags.AddChild(_markerRevealed);
        markerFlags.AddChild(_markerPersistent);
        card.AddChild(markerFlags);
        _markerRevealEvent = new LineEdit { PlaceholderText = "Événement de révélation" };
        _markerRevealAnimation = new LineEdit { PlaceholderText = "Animation (ex. pulse)" };
        card.AddChild(_markerRevealEvent); card.AddChild(_markerRevealAnimation);
        var actions = new HBoxContainer();
        var save = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        save.Pressed += SaveWaypoint;
        actions.AddChild(save);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveWaypoint;
        actions.AddChild(remove);
        card.AddChild(actions);
    }

    private void BuildPatrolCard(HBoxContainer body)
    {
        var card = AddCard(body, 390);
        card.AddChild(AccentLabel("ROUTE DE PATROUILLE"));
        _routeList = new ItemList { CustomMinimumSize = new Vector2(0, 110) };
        _routeList.ItemSelected += SelectRoute;
        card.AddChild(_routeList);
        _routeTraversal = new OptionButton();
        foreach (var policy in _traversalPolicies) _routeTraversal.AddItem(FormatTraversal(policy));
        card.AddChild(_routeTraversal);
        card.AddChild(MutedLabel("Un point par ligne : X | Y | Z | attente | facing | vitesse | action | condition | poids"));
        _routePoints = new TextEdit
        {
            PlaceholderText = "-6|0|-3|0|0|2|||1\n0|0|0|2|90|2|inspect||2\n6|0|4|0|180|3|||3",
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        card.AddChild(_routePoints);
        _routeScript = new LineEdit { PlaceholderText = "Handler ForgeScript si mode Scripted" };
        card.AddChild(_routeScript);
        _routeRequired = new CheckBox { Text = "Route requise au runtime" };
        card.AddChild(_routeRequired);
        var actions = new HBoxContainer();
        var save = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        save.Pressed += SaveRoute;
        actions.AddChild(save);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveRoute;
        actions.AddChild(remove);
        card.AddChild(actions);
    }

    private void BuildWaypointPreviewCard(HBoxContainer body)
    {
        var card = AddCard(body, 430, expand: true);
        card.AddChild(AccentLabel("APERÇU VIEWPORT & RUNTIME"));
        _waypointPreview = new WaypointRoutePreview { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        card.AddChild(_waypointPreview);
        _waypointSummary = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(0, 105),
        };
        card.AddChild(_waypointSummary);
        card.AddChild(MutedLabel("Les icônes gardent une taille écran constante ; les arêtes violettes suivent l’ordre canonique."));
    }

    private void BuildLocatorCard(HBoxContainer body)
    {
        var card = AddCard(body, 430);
        card.AddChild(AccentLabel("LOCATOR & ARRIVÉE"));
        _locatorList = new ItemList { CustomMinimumSize = new Vector2(0, 105) };
        _locatorList.ItemSelected += SelectLocator;
        card.AddChild(_locatorList);
        var identity = new HBoxContainer();
        _locatorName = new LineEdit { PlaceholderText = "Nom du locator", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _locatorPurpose = new OptionButton();
        foreach (var purpose in _locatorPurposes) _locatorPurpose.AddItem(purpose.ToString());
        identity.AddChild(_locatorName); identity.AddChild(_locatorPurpose);
        card.AddChild(identity);
        _locatorTags = new LineEdit { PlaceholderText = "Tags séparés par virgules" };
        card.AddChild(_locatorTags);
        var position = new HBoxContainer();
        _locatorX = Coordinate(); _locatorY = Coordinate(); _locatorZ = Coordinate();
        position.AddChild(_locatorX); position.AddChild(_locatorY); position.AddChild(_locatorZ);
        card.AddChild(position);
        var facing = new HBoxContainer();
        _arrivalFacing = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var mode in _arrivalFacingModes) _arrivalFacing.AddItem(mode.ToString());
        _arrivalDirection = Spin(-360, 360, 5, 0);
        _arrivalTarget = new LineEdit { PlaceholderText = "Cible typée" };
        facing.AddChild(_arrivalFacing); facing.AddChild(_arrivalDirection); facing.AddChild(_arrivalTarget);
        card.AddChild(facing);
        var arrival = new HBoxContainer();
        _arrivalAnimation = new LineEdit { PlaceholderText = "Animation" };
        _arrivalWait = Spin(0, 3600, .25, 0);
        _arrivalInteraction = new LineEdit { PlaceholderText = "Interaction" };
        arrival.AddChild(_arrivalAnimation); arrival.AddChild(_arrivalWait); arrival.AddChild(_arrivalInteraction);
        card.AddChild(arrival);
        _arrivalCallback = new LineEdit { PlaceholderText = "Callback ForgeScript" };
        _locatorAsset = new LineEdit { PlaceholderText = "AssetId audio/VFX (optionnel)" };
        card.AddChild(_arrivalCallback); card.AddChild(_locatorAsset);
        _locatorRequired = new CheckBox { Text = "Locator requis au runtime" };
        card.AddChild(_locatorRequired);
        var actions = new HBoxContainer();
        var save = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        save.Pressed += SaveLocator;
        actions.AddChild(save);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveLocator;
        actions.AddChild(remove);
        card.AddChild(actions);
    }

    private void BuildCollectionCard(HBoxContainer body)
    {
        var card = AddCard(body, 400);
        card.AddChild(AccentLabel("GROUPES & REQUÊTES"));
        _collectionList = new ItemList { CustomMinimumSize = new Vector2(0, 120) };
        _collectionList.ItemSelected += SelectCollection;
        card.AddChild(_collectionList);
        _collectionName = new LineEdit { PlaceholderText = "Nom de collection" };
        _collectionQuery = new LineEdit { PlaceholderText = "Recherche texte/tag" };
        _collectionTags = new LineEdit { PlaceholderText = "Tags requis" };
        card.AddChild(_collectionName); card.AddChild(_collectionQuery); card.AddChild(_collectionTags);
        var policy = new HBoxContainer();
        _collectionPurpose = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _collectionPurpose.AddItem("Tous les buts");
        foreach (var purpose in _locatorPurposes) _collectionPurpose.AddItem(purpose.ToString());
        _collectionSelection = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var selection in _locatorSelectionModes) _collectionSelection.AddItem(FormatSelection(selection));
        _collectionSeed = Spin(int.MinValue, int.MaxValue, 1, 2026);
        policy.AddChild(_collectionPurpose); policy.AddChild(_collectionSelection); policy.AddChild(_collectionSeed);
        card.AddChild(policy);
        _collectionRequired = new CheckBox { Text = "Collection requise au runtime" };
        card.AddChild(_collectionRequired);
        card.AddChild(MutedLabel("Ordre stable, plus proche ou pseudo-aléatoire déterministe. Les membres sont prévisualisés avant application."));
        var actions = new HBoxContainer();
        var save = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        save.Pressed += SaveCollection;
        actions.AddChild(save);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveCollection;
        actions.AddChild(remove);
        card.AddChild(actions);
        card.AddChild(AccentLabel("COUCHES DU DIAGNOSTIC"));
        var row1 = new HBoxContainer();
        _visualSegments = Check("Segments"); _visualDirection = Check("Sens");
        _visualCosts = Check("Coûts"); _visualActions = Check("Actions");
        row1.AddChild(_visualSegments); row1.AddChild(_visualDirection);
        row1.AddChild(_visualCosts); row1.AddChild(_visualActions);
        card.AddChild(row1);
        var row2 = new HBoxContainer();
        _visualBroken = Check("Liens cassés"); _visualNavProjection = Check("Projection nav");
        _visualTiming = Check("Timing");
        row2.AddChild(_visualBroken); row2.AddChild(_visualNavProjection); row2.AddChild(_visualTiming);
        card.AddChild(row2);
        var visualPolicy = new HBoxContainer();
        _visualCostPerMeter = Spin(.01, 10_000, .01, 1);
        _visualCostPerMeter.TooltipText = "Coût de parcours par mètre";
        _visualRequired = new CheckBox { Text = "Diagnostic requis runtime" };
        visualPolicy.AddChild(_visualCostPerMeter); visualPolicy.AddChild(_visualRequired);
        card.AddChild(visualPolicy);
        foreach (var control in new BaseButton[] { _visualSegments, _visualDirection, _visualCosts,
                     _visualActions, _visualBroken, _visualNavProjection, _visualTiming, _visualRequired })
            control.Toggled += _ => SaveVisualizationDraft();
        _visualCostPerMeter.ValueChanged += _ => SaveVisualizationDraft();
    }

    private void BuildRouteDiagnosticsCard(HBoxContainer body)
    {
        var card = AddCard(body, 400, expand: true);
        card.AddChild(AccentLabel("APERÇU ROUTE & LOCATORS"));
        _routeDiagnosticsPreview = new WaypointRoutePreview { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        card.AddChild(_routeDiagnosticsPreview);
        _locatorSummary = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            CustomMinimumSize = new Vector2(0, 115),
        };
        card.AddChild(_locatorSummary);
        card.AddChild(MutedLabel("Diamants bleus/orange/verts/jaunes : audio, VFX, spawn et objectif. Les flèches, coûts, actions, projections et temps proviennent du document canonique."));
    }

    private void BuildAnchorsCard(HBoxContainer body)
    {
        var card = AddCard(body, 300);
        card.AddChild(AccentLabel("ANCRES DE LA ZONE"));
        card.AddChild(MutedLabel("Waypoints nommés, départs jouables et destinations de transition."));
        _anchorList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _anchorList.ItemSelected += SelectAnchor;
        card.AddChild(_anchorList);
        var actions = new HBoxContainer();
        var add = new Button { Text = "+ Nouvelle", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        add.Pressed += NewAnchor;
        actions.AddChild(add);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveAnchor;
        actions.AddChild(remove);
        card.AddChild(actions);
        card.AddChild(MutedLabel("Chaque entrée conserve UUID, schéma, provenance et ordre déterministe."));
    }

    private void BuildAnchorIdentityCard(HBoxContainer body)
    {
        var card = AddCard(body, 410);
        card.AddChild(AccentLabel("IDENTITÉ & TRANSFORM"));
        _anchorSlug = new LineEdit { PlaceholderText = "Slug stable (ex. player-start)" };
        _anchorLabelKey = new LineEdit { PlaceholderText = "Clé loc. (ex. anchor.player_start)" };
        _anchorLabel = new LineEdit { PlaceholderText = "Libellé de secours" };
        card.AddChild(_anchorSlug); card.AddChild(_anchorLabelKey); card.AddChild(_anchorLabel);
        var classification = new HBoxContainer();
        _anchorKind = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var kind in _anchorKinds) _anchorKind.AddItem(FormatAnchorKind(kind));
        _anchorScope = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var scope in _anchorScopes) _anchorScope.AddItem(scope.ToString());
        classification.AddChild(_anchorKind); classification.AddChild(_anchorScope);
        card.AddChild(classification);
        _anchorTags = new LineEdit { PlaceholderText = "Tags séparés par des virgules" };
        _anchorBlueprint = new LineEdit { PlaceholderText = "UUID blueprint optionnel / PlayerSpawn requis au départ" };
        _anchorAsset = new LineEdit { PlaceholderText = "Asset visuel optionnel" };
        card.AddChild(_anchorTags); card.AddChild(_anchorBlueprint); card.AddChild(_anchorAsset);
        _anchorRequired = new CheckBox { Text = "Requis au runtime" };
        card.AddChild(_anchorRequired);
        card.AddChild(MutedLabel("Position X / Y / Z · rotation Y"));
        var transform = new HBoxContainer();
        _anchorX = Coordinate(); _anchorY = Coordinate(); _anchorZ = Coordinate();
        _anchorYaw = Spin(-360, 360, 1, 0);
        transform.AddChild(_anchorX); transform.AddChild(_anchorY); transform.AddChild(_anchorZ); transform.AddChild(_anchorYaw);
        card.AddChild(transform);
        _anchorOverrides = new LineEdit { PlaceholderText = "Overrides instance : clé=valeur;clé=valeur" };
        card.AddChild(_anchorOverrides);
        var save = new Button { Text = "Enregistrer l’ancre dans le brouillon", CustomMinimumSize = new Vector2(0, 40) };
        save.Pressed += SaveAnchor;
        card.AddChild(save);
    }

    private void BuildAnchorPolicyCard(HBoxContainer body)
    {
        var card = AddCard(body, 470, expand: true);
        card.AddChild(AccentLabel("DÉPART & DESTINATION"));
        card.AddChild(MutedLabel("Départ : profil · équipe · priorité · dégagement X/Y/Z · orientation"));
        var spawnIdentity = new HBoxContainer();
        _spawnProfile = new LineEdit { PlaceholderText = "Profil", Text = "player", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _spawnTeam = new LineEdit { PlaceholderText = "Équipe", Text = "heroes", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _spawnPriority = Spin(1, 100, 1, 50);
        spawnIdentity.AddChild(_spawnProfile); spawnIdentity.AddChild(_spawnTeam); spawnIdentity.AddChild(_spawnPriority);
        card.AddChild(spawnIdentity);
        var clearance = new HBoxContainer();
        _spawnClearanceX = Spin(0.1, 16, 0.1, 1.2);
        _spawnClearanceY = Spin(0.1, 16, 0.1, 2);
        _spawnClearanceZ = Spin(0.1, 16, 0.1, 1.2);
        _spawnOrientation = Spin(-360, 360, 1, 0);
        clearance.AddChild(_spawnClearanceX); clearance.AddChild(_spawnClearanceY);
        clearance.AddChild(_spawnClearanceZ); clearance.AddChild(_spawnOrientation);
        card.AddChild(clearance);
        _spawnFallback = new LineEdit { PlaceholderText = "UUID du départ de repli (optionnel)" };
        _spawnRequireNavigation = new CheckBox { Text = "Exiger une surface navigable", ButtonPressed = true };
        card.AddChild(_spawnFallback); card.AddChild(_spawnRequireNavigation);
        card.AddChild(new HSeparator());
        card.AddChild(MutedLabel("Portail : zone et ancre cibles · orientation · vélocité · préchargement"));
        _portalTargetArea = new OptionButton();
        card.AddChild(_portalTargetArea);
        _portalTargetAnchor = new LineEdit { PlaceholderText = "UUID de l’ancre cible" };
        card.AddChild(_portalTargetAnchor);
        var portalPolicies = new HBoxContainer();
        _portalOrientation = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var policy in _orientationPolicies) _portalOrientation.AddItem(policy.ToString());
        _portalYaw = Spin(-360, 360, 1, 0);
        _portalVelocity = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var policy in _velocityPolicies) _portalVelocity.AddItem(policy.ToString());
        portalPolicies.AddChild(_portalOrientation); portalPolicies.AddChild(_portalYaw); portalPolicies.AddChild(_portalVelocity);
        card.AddChild(portalPolicies);
        _portalPreload = new CheckBox { Text = "Précharger la zone cible", ButtonPressed = true };
        card.AddChild(_portalPreload);
        _anchorPreview = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 120),
        };
        card.AddChild(_anchorPreview);
    }

    private void RefreshAreas(AreaId? preferred = null)
    {
        var state = _getState?.Invoke();
        if (state is null || _areaPicker is null) return;
        var selected = preferred ?? _getActiveAreaId?.Invoke();
        _areaIds.Clear();
        _areaPicker.Clear();
        _portalTargetArea?.Clear();
        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        {
            _areaIds.Add(area.Id);
            _portalTargetArea?.AddItem(area.DisplayName);
            _areaPicker.AddItem($"{area.DisplayName} · {area.Dimensions.Width} × {area.Dimensions.Depth}");
        }

        var index = selected is null ? 0 : Math.Max(0, _areaIds.FindIndex(id => id == selected));
        if (_areaIds.Count > 0)
        {
            _areaPicker.Selected = index;
            LoadSelectedArea();
        }
    }

    private void LoadSelectedArea()
    {
        var state = _getState?.Invoke();
        if (state is null || _areaPicker is null || _areaPicker.Selected < 0 ||
            _areaPicker.Selected >= _areaIds.Count) return;
        var area = state.Project.Areas.Single(candidate => candidate.Id == _areaIds[_areaPicker.Selected]);
        LoadDraft(area.Navigation);
    }

    private void LoadDraft(AreaNavigationDefinition navigation)
    {
        _definitionId = navigation.Id;
        _cellSize!.Value = navigation.CellSize;
        _agents.Clear();
        _agents.AddRange(navigation.AgentProfiles);
        _links.Clear();
        _links.AddRange(navigation.OffMeshLinks);
        _catalogId = navigation.Anchors.Id;
        _catalogProvenance = navigation.Anchors.Provenance;
        _catalogRequiredAtRuntime = navigation.Anchors.RequiredAtRuntime;
        _anchorDefinitions.Clear();
        _anchorDefinitions.AddRange(navigation.Anchors.Definitions);
        _anchorInstances.Clear();
        _anchorInstances.AddRange(navigation.Anchors.Instances);
        _waypointProfileId = navigation.Waypoints.Id;
        _waypointProvenance = navigation.Waypoints.Provenance;
        _waypointsRequiredAtRuntime = navigation.Waypoints.RequiredAtRuntime;
        _waypoints.Clear();
        _waypoints.AddRange(navigation.Waypoints.Waypoints);
        _patrolRoutes.Clear();
        _patrolRoutes.AddRange(navigation.Waypoints.PatrolRoutes);
        _locators.Clear();
        _locators.AddRange(navigation.Waypoints.Locators);
        _locatorCollections.Clear();
        _locatorCollections.AddRange(navigation.Waypoints.Collections);
        _routeVisualization = navigation.Waypoints.RouteVisualization;
        _selectedAgent = -1;
        _selectedLink = -1;
        _selectedAnchor = -1;
        _selectedWaypoint = -1;
        _selectedRoute = -1;
        _selectedLocator = -1;
        _selectedCollection = -1;
        RefreshAgentList();
        RefreshLinkList();
        RefreshAnchorList();
        RefreshWaypointLists();
        RefreshLocatorLists();
        LoadVisualizationDraft();
        RefreshWaypointPreview();
        Preview();
    }

    private void RefreshAgentList()
    {
        _agentList!.Clear();
        foreach (var agent in _agents)
        {
            _agentList.AddItem(
                $"{agent.Name} · r {agent.Radius:0.0} m · h {agent.Height:0.0} m" +
                (agent.CanFly ? " · VOL" : string.Empty));
        }
    }

    private void RefreshLinkList()
    {
        _linkList!.Clear();
        foreach (var link in _links)
        {
            _linkList.AddItem($"{link.Name} · {FormatKind(link.Kind)} · r ≤ {link.MaximumAgentRadius:0.0} m");
        }
    }

    private void RefreshAnchorList()
    {
        if (_anchorList is null) return;
        _anchorList.Clear();
        var definitions = _anchorDefinitions.ToDictionary(item => item.Id);
        foreach (var instance in _anchorInstances.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id))
        {
            if (!definitions.TryGetValue(instance.DefinitionId, out var definition))
            {
                _anchorList.AddItem($"Ancre orpheline · {instance.Id:D}");
                continue;
            }
            _anchorList.AddItem($"{instance.DisplayOrder:00} · {FormatAnchorKind(definition.Kind)} · {definition.Label.Fallback}");
        }
        RefreshAnchorPreview();
    }

    private void SelectAnchor(long index)
    {
        var ordered = _anchorInstances.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).ToArray();
        if (index < 0 || index >= ordered.Length) return;
        var instance = ordered[index];
        _selectedAnchor = _anchorInstances.FindIndex(item => item.Id == instance.Id);
        var definition = _anchorDefinitions.Single(item => item.Id == instance.DefinitionId);
        _anchorSlug!.Text = definition.Slug;
        _anchorLabelKey!.Text = definition.Label.Key;
        _anchorLabel!.Text = definition.Label.Fallback;
        _anchorTags!.Text = string.Join(", ", definition.Tags.Order(StringComparer.Ordinal));
        Select(_anchorKind!, definition.Kind);
        Select(_anchorScope!, definition.Scope);
        _anchorBlueprint!.Text = definition.BlueprintId?.ToString() ?? string.Empty;
        _anchorAsset!.Text = definition.AssetId ?? string.Empty;
        _anchorRequired!.ButtonPressed = definition.RequiredAtRuntime;
        _anchorX!.Value = instance.Transform.Position.X;
        _anchorY!.Value = instance.Transform.Position.Y;
        _anchorZ!.Value = instance.Transform.Position.Z;
        _anchorYaw!.Value = QuaternionYaw(instance.Transform.Rotation);
        _anchorOverrides!.Text = string.Join(';', instance.Overrides.Properties.Select(item => $"{item.Key}={item.Value}"));
        var spawn = instance.Spawn;
        _spawnProfile!.Text = spawn?.Profile ?? "player";
        _spawnTeam!.Text = spawn?.Team ?? "heroes";
        _spawnPriority!.Value = spawn?.Priority ?? 50;
        _spawnClearanceX!.Value = spawn?.Clearance.X ?? 1.2;
        _spawnClearanceY!.Value = spawn?.Clearance.Y ?? 2;
        _spawnClearanceZ!.Value = spawn?.Clearance.Z ?? 1.2;
        _spawnOrientation!.Value = spawn?.OrientationDegrees ?? 0;
        _spawnFallback!.Text = spawn?.FallbackInstanceId?.ToString("D") ?? string.Empty;
        _spawnRequireNavigation!.ButtonPressed = spawn?.RequireNavigation ?? true;
        var portal = instance.Portal;
        if (portal is not null)
        {
            _portalTargetArea!.Selected = Math.Max(0, _areaIds.FindIndex(item => item == portal.TargetAreaId));
            _portalTargetAnchor!.Text = portal.TargetAnchorInstanceId.ToString("D");
            Select(_portalOrientation!, portal.Orientation);
            _portalYaw!.Value = portal.FixedYawDegrees;
            Select(_portalVelocity!, portal.Velocity);
            _portalPreload!.ButtonPressed = portal.Preload;
        }
        RefreshAnchorPreview();
    }

    private void NewAnchor()
    {
        _selectedAnchor = -1;
        _anchorSlug!.Text = string.Empty;
        _anchorLabelKey!.Text = string.Empty;
        _anchorLabel!.Text = string.Empty;
        _anchorTags!.Text = string.Empty;
        _anchorBlueprint!.Text = string.Empty;
        _anchorAsset!.Text = string.Empty;
        _anchorRequired!.ButtonPressed = false;
        _anchorX!.Value = 0; _anchorY!.Value = 0; _anchorZ!.Value = 0; _anchorYaw!.Value = 0;
        _anchorOverrides!.Text = string.Empty;
        Select(_anchorKind!, AnchorKind.Waypoint);
        SetStatus("Nouvelle ancre prête à être décrite.", false);
    }

    private void SaveAnchor()
    {
        try
        {
            var existing = _selectedAnchor >= 0 ? _anchorInstances[_selectedAnchor] : null;
            var existingDefinition = existing is null ? null :
                _anchorDefinitions.Single(item => item.Id == existing.DefinitionId);
            var definitionId = existingDefinition?.Id ?? Guid.NewGuid();
            BlueprintId? blueprintId = null;
            if (!string.IsNullOrWhiteSpace(_anchorBlueprint!.Text))
                blueprintId = BlueprintId.From(Guid.Parse(_anchorBlueprint.Text.Trim()));
            var definition = new AnchorDefinition(definitionId, 1, "toolset:r009",
                Required(_anchorSlug!.Text, "slug"),
                new LocalizedText(Required(_anchorLabelKey!.Text, "clé de localisation"),
                    Required(_anchorLabel!.Text, "libellé"), provenance: "toolset:r009"),
                _anchorKinds[_anchorKind!.Selected], _anchorScopes[_anchorScope!.Selected],
                _anchorTags!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), blueprintId,
                NullIfBlank(_anchorAsset!.Text), _anchorRequired!.ButtonPressed);
            var kind = definition.Kind;
            SpawnAnchorProfile? spawn = kind == AnchorKind.Spawn
                ? new SpawnAnchorProfile(Required(_spawnProfile!.Text, "profil de départ"),
                    new Vector3Value((float)_spawnClearanceX!.Value, (float)_spawnClearanceY!.Value,
                        (float)_spawnClearanceZ!.Value), (int)_spawnPriority!.Value,
                    Required(_spawnTeam!.Text, "équipe"), (float)_spawnOrientation!.Value,
                    ParseOptionalGuid(_spawnFallback!.Text), _spawnRequireNavigation!.ButtonPressed)
                : null;
            PortalAnchorDestination? portal = kind == AnchorKind.TransitionDestination
                ? new PortalAnchorDestination(_areaIds[_portalTargetArea!.Selected],
                    Guid.Parse(Required(_portalTargetAnchor!.Text, "ancre cible")),
                    _orientationPolicies[_portalOrientation!.Selected], (float)_portalYaw!.Value,
                    _velocityPolicies[_portalVelocity!.Selected], _portalPreload!.ButtonPressed)
                : null;
            var instance = new AnchorInstance(existing?.Id ?? Guid.NewGuid(), 1, "toolset:r009", definitionId,
                At((float)_anchorX!.Value, (float)_anchorY!.Value, (float)_anchorZ!.Value,
                    (float)_anchorYaw!.Value), existing?.DisplayOrder ?? _anchorInstances.Count + 1,
                ParseOverrides(_anchorOverrides!.Text), spawn, portal);
            if (existing is null)
            {
                _anchorDefinitions.Add(definition);
                _anchorInstances.Add(instance);
                _selectedAnchor = _anchorInstances.Count - 1;
            }
            else
            {
                _anchorDefinitions[_anchorDefinitions.FindIndex(item => item.Id == definitionId)] = definition;
                _anchorInstances[_selectedAnchor] = instance;
            }
            RefreshAnchorList();
            SetStatus("Ancre enregistrée dans le brouillon déterministe.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveAnchor()
    {
        if (_selectedAnchor < 0 || _selectedAnchor >= _anchorInstances.Count) return;
        var removed = _anchorInstances[_selectedAnchor];
        _anchorInstances.RemoveAt(_selectedAnchor);
        if (_anchorInstances.All(item => item.DefinitionId != removed.DefinitionId))
            _anchorDefinitions.RemoveAll(item => item.Id == removed.DefinitionId);
        _selectedAnchor = -1;
        RefreshAnchorList();
    }

    private void RefreshAnchorPreview()
    {
        if (_anchorPreview is null) return;
        var definitions = _anchorDefinitions.ToDictionary(item => item.Id);
        var spawnCount = _anchorInstances.Count(item => item.Spawn is not null);
        var portalCount = _anchorInstances.Count(item => item.Portal is not null);
        var requiredCount = _anchorInstances.Count(item => definitions.TryGetValue(item.DefinitionId, out var definition) &&
            definition.RequiredAtRuntime);
        _anchorPreview.Text = $"[color=#ff9858]CONTRAT R009[/color]\n" +
            $"{_anchorInstances.Count} instance(s) · {spawnCount} départ(s) · {portalCount} portail(s)\n" +
            $"{requiredCount} requise(s) au runtime · UUID, provenance, localisation et overrides persistés\n\n" +
            "[color=#83d6ae]Appliquer passe par dry-run, validation projet et historique undo/redo.[/color]";
    }

    private void RefreshWaypointLists()
    {
        if (_waypointList is not null)
        {
            _waypointList.Clear();
            foreach (var waypoint in _waypoints)
                _waypointList.AddItem($"{waypoint.Name} · {waypoint.Appearance.Icon} · " +
                    $"{waypoint.Appearance.ScreenSize:0}px" +
                    (waypoint.Marker is null ? string.Empty : " · MAP"));
        }
        if (_routeList is not null)
        {
            _routeList.Clear();
            foreach (var route in _patrolRoutes)
                _routeList.AddItem($"{FormatTraversal(route.Traversal)} · {route.Points.Count} points · " +
                    $"{route.Edges.Count} arêtes");
        }
    }

    private void SelectWaypoint(long index)
    {
        if (index < 0 || index >= _waypoints.Count) return;
        _selectedWaypoint = (int)index;
        var waypoint = _waypoints[_selectedWaypoint];
        _waypointName!.Text = waypoint.Name;
        _waypointIcon!.Text = waypoint.Appearance.Icon;
        _waypointColor!.Text = waypoint.Appearance.Color;
        _waypointLayer!.Text = waypoint.Appearance.Layer;
        _waypointScreenSize!.Value = waypoint.Appearance.ScreenSize;
        _waypointX!.Value = waypoint.Position.X;
        _waypointY!.Value = waypoint.Position.Y;
        _waypointZ!.Value = waypoint.Position.Z;
        _waypointVisible!.ButtonPressed = waypoint.Appearance.Visible;
        _waypointRequired!.ButtonPressed = waypoint.RequiredAtRuntime;
        var arrival = waypoint.Arrival;
        Select(_waypointArrivalFacing!, arrival?.Facing ?? ArrivalFacingMode.Preserve);
        _waypointArrivalDirection!.Value = arrival?.DirectionDegrees ?? 0;
        _waypointArrivalTarget!.Text = arrival?.TargetId ?? string.Empty;
        _waypointArrivalAnimation!.Text = arrival?.Animation ?? string.Empty;
        _waypointArrivalWait!.Value = arrival?.WaitSeconds ?? 0;
        _waypointArrivalInteraction!.Text = arrival?.Interaction ?? string.Empty;
        _waypointArrivalCallback!.Text = arrival?.Callback ?? string.Empty;
        var marker = waypoint.Marker;
        _markerText!.Text = marker?.Text ?? string.Empty;
        _markerIcon!.Text = marker?.Icon ?? string.Empty;
        _markerFloor!.Value = marker?.Floor ?? 0;
        _markerCategory!.Text = marker?.Category ?? string.Empty;
        _markerPrecision!.Value = marker?.Precision ?? 2;
        Select(_markerPolicy!, marker?.VisibilityPolicy ?? MarkerVisibilityPolicy.Always);
        Select(_markerScope!, marker?.VisibilityScope ?? MarkerVisibilityScope.Player);
        _markerTracked!.ButtonPressed = marker?.Tracked ?? false;
        _markerRevealed!.ButtonPressed = marker?.Revealed ?? false;
        _markerRevealEvent!.Text = marker?.RevealEvent ?? string.Empty;
        _markerRevealAnimation!.Text = marker?.RevealAnimation ?? string.Empty;
        _markerPersistent!.ButtonPressed = marker?.Persistent ?? true;
    }

    private void SaveWaypoint()
    {
        try
        {
            var existing = _selectedWaypoint >= 0 ? _waypoints[_selectedWaypoint] : null;
            MapMarker? marker = string.IsNullOrWhiteSpace(_markerText!.Text) ? null : new(
                _markerText.Text.Trim(), Required(_markerIcon!.Text, "icône de map note"),
                (int)_markerFloor!.Value, Required(_markerCategory!.Text, "catégorie de map note"),
                _markerPrecision!.Value, _markerPolicies[_markerPolicy!.Selected],
                _markerScopes[_markerScope!.Selected], _markerTracked!.ButtonPressed,
                _markerRevealed!.ButtonPressed, _markerRevealEvent!.Text.Trim(),
                _markerRevealAnimation!.Text.Trim(), _markerPersistent!.ButtonPressed);
            var waypoint = new WaypointProfile(existing?.Id ?? Guid.NewGuid(), 1, "toolset:r064",
                Required(_waypointName!.Text, "nom du waypoint"),
                new Vector3Value((float)_waypointX!.Value, (float)_waypointY!.Value,
                    (float)_waypointZ!.Value),
                new WaypointEditorAppearance(Required(_waypointIcon!.Text, "icône"),
                    Required(_waypointColor!.Text, "couleur"), Required(_waypointLayer!.Text, "couche"),
                    _waypointVisible!.ButtonPressed, _waypointScreenSize!.Value), marker,
                existing?.AnchorInstanceId, _waypointRequired!.ButtonPressed, "1.0",
                CreateArrival(_waypointArrivalFacing!, _waypointArrivalDirection!,
                    _waypointArrivalTarget!, _waypointArrivalAnimation!, _waypointArrivalWait!,
                    _waypointArrivalInteraction!, _waypointArrivalCallback!));
            if (existing is null)
            {
                _waypoints.Add(waypoint);
                _selectedWaypoint = _waypoints.Count - 1;
            }
            else _waypoints[_selectedWaypoint] = waypoint;
            RefreshWaypointLists();
            RefreshWaypointPreview();
            SetStatus("Waypoint et map note enregistrés dans le brouillon canonique.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveWaypoint()
    {
        if (_selectedWaypoint < 0 || _selectedWaypoint >= _waypoints.Count) return;
        _waypoints.RemoveAt(_selectedWaypoint);
        _selectedWaypoint = -1;
        RefreshWaypointLists();
        RefreshWaypointPreview();
    }

    private void SelectRoute(long index)
    {
        if (index < 0 || index >= _patrolRoutes.Count) return;
        _selectedRoute = (int)index;
        var route = _patrolRoutes[_selectedRoute];
        Select(_routeTraversal!, route.Traversal);
        _routeScript!.Text = route.ScriptHandler;
        _routeRequired!.ButtonPressed = route.RequiredAtRuntime;
        _routePoints!.Text = string.Join("\n", route.Points.Select(point => string.Join("|",
            point.Position.X.ToString("0.###", CultureInfo.InvariantCulture),
            point.Position.Y.ToString("0.###", CultureInfo.InvariantCulture),
            point.Position.Z.ToString("0.###", CultureInfo.InvariantCulture),
            point.WaitSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            point.FacingDegrees.ToString("0.###", CultureInfo.InvariantCulture),
            point.Speed.ToString("0.###", CultureInfo.InvariantCulture),
            point.Action ?? string.Empty, point.BranchCondition ?? string.Empty,
            point.Weight.ToString("0.###", CultureInfo.InvariantCulture))));
    }

    private void SaveRoute()
    {
        try
        {
            var existing = _selectedRoute >= 0 ? _patrolRoutes[_selectedRoute] : null;
            var lines = _routePoints!.Text.Split('\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var points = lines.Select((line, index) =>
            {
                var values = line.Split('|', StringSplitOptions.TrimEntries);
                if (values.Length != 9) throw new ArgumentException($"Point {index + 1} : 9 champs requis.");
                var position = new Vector3Value(ParseFloat(values[0]), ParseFloat(values[1]),
                    ParseFloat(values[2]));
                var matchingWaypoint = _waypoints.FirstOrDefault(item => item.Position == position);
                var id = existing is not null && index < existing.Points.Count
                    ? existing.Points[index].Id
                    : matchingWaypoint?.Id ?? Guid.NewGuid();
                return new PatrolPoint(id, position, ParseDouble(values[3]), ParseDouble(values[4]),
                    ParseDouble(values[5]), NullIfBlank(values[6]), NullIfBlank(values[7]),
                    ParseDouble(values[8]));
            }).ToArray();
            var route = new PatrolRoute(existing?.Id ?? Guid.NewGuid(), 1, "toolset:r064",
                _traversalPolicies[_routeTraversal!.Selected], points, scriptHandler: _routeScript!.Text,
                requiredAtRuntime: _routeRequired!.ButtonPressed);
            if (existing is null)
            {
                _patrolRoutes.Add(route);
                _selectedRoute = _patrolRoutes.Count - 1;
            }
            else _patrolRoutes[_selectedRoute] = route;
            RefreshWaypointLists();
            RefreshWaypointPreview();
            SetStatus("Route ordonnée enregistrée dans le brouillon canonique.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveRoute()
    {
        if (_selectedRoute < 0 || _selectedRoute >= _patrolRoutes.Count) return;
        _patrolRoutes.RemoveAt(_selectedRoute);
        _selectedRoute = -1;
        RefreshWaypointLists();
        RefreshWaypointPreview();
    }

    private void RefreshWaypointPreview(AreaDefinition? configuredArea = null)
    {
        var visualization = _routeVisualization ?? RouteVisualizationProfile.CreateDefault(
            SelectedArea(_getState?.Invoke())?.Id ?? _areaIds[0]);
        var projected = _waypoints.Take(2).Select(item => item.Id).ToHashSet();
        _waypointPreview?.SetData(_waypoints, _patrolRoutes, _locators, visualization, projected);
        _routeDiagnosticsPreview?.SetData(_waypoints, _patrolRoutes, _locators, visualization, projected);
        if (_waypointSummary is null) return;
        configuredArea ??= SelectedArea(_getState?.Invoke()) is { } area
            ? area.WithNavigation(CreateNavigation()) : null;
        IReadOnlyList<DomainDiagnostic> diagnostics = configuredArea is null
            ? [] : WaypointAuthoringValidator.Validate(configuredArea);
        _waypointSummary.Text = $"[color=#ff9858]R064 · PROFIL WAYPOINT[/color]\n" +
            $"{_waypoints.Count} waypoint(s) · {_patrolRoutes.Count} route(s) · " +
            $"{_patrolRoutes.Sum(item => item.Edges.Count)} arête(s) UUID\n" +
            $"Map notes {_waypoints.Count(item => item.Marker is not null)} · " +
            $"modes {string.Join(", ", _patrolRoutes.Select(item => FormatTraversal(item.Traversal)).Distinct())}\n" +
            (diagnostics.Count == 0
                ? "[color=#83d6ae]✓ Prêt · commande, historique, runtime et persistance[/color]"
                : $"[color=#ff8c8c]{diagnostics.Count} diagnostic(s) · {diagnostics[0].Code}[/color]");
        RefreshLocatorSummary();
    }

    private void RefreshLocatorLists()
    {
        if (_locatorList is not null)
        {
            _locatorList.Clear();
            foreach (var locator in _locators)
                _locatorList.AddItem($"{locator.Name} · {locator.Purpose} · " +
                    $"{string.Join(", ", locator.Tags.Order(StringComparer.Ordinal))}");
        }
        if (_collectionList is not null)
        {
            _collectionList.Clear();
            foreach (var collection in _locatorCollections)
            {
                var resolved = WaypointRouteService.ResolveCollection(collection, _locators);
                _collectionList.AddItem($"{collection.Name} · {FormatSelection(collection.Selection)} · " +
                    $"{resolved.Count} résultat(s)");
            }
        }
    }

    private void SelectLocator(long index)
    {
        if (index < 0 || index >= _locators.Count) return;
        _selectedLocator = (int)index;
        var locator = _locators[_selectedLocator];
        _locatorName!.Text = locator.Name;
        Select(_locatorPurpose!, locator.Purpose);
        _locatorTags!.Text = string.Join(", ", locator.Tags.Order(StringComparer.Ordinal));
        _locatorX!.Value = locator.Position.X; _locatorY!.Value = locator.Position.Y;
        _locatorZ!.Value = locator.Position.Z;
        var arrival = locator.Arrival;
        Select(_arrivalFacing!, arrival?.Facing ?? ArrivalFacingMode.Preserve);
        _arrivalDirection!.Value = arrival?.DirectionDegrees ?? 0;
        _arrivalTarget!.Text = arrival?.TargetId ?? string.Empty;
        _arrivalAnimation!.Text = arrival?.Animation ?? string.Empty;
        _arrivalWait!.Value = arrival?.WaitSeconds ?? 0;
        _arrivalInteraction!.Text = arrival?.Interaction ?? string.Empty;
        _arrivalCallback!.Text = arrival?.Callback ?? string.Empty;
        _locatorAsset!.Text = locator.AssetId ?? string.Empty;
        _locatorRequired!.ButtonPressed = locator.RequiredAtRuntime;
    }

    private void SaveLocator()
    {
        try
        {
            var existing = _selectedLocator >= 0 ? _locators[_selectedLocator] : null;
            var locator = new GenericLocator(existing?.Id ?? Guid.NewGuid(), 1, "toolset:r065",
                Required(_locatorName!.Text, "nom du locator"),
                new Vector3Value((float)_locatorX!.Value, (float)_locatorY!.Value,
                    (float)_locatorZ!.Value), _locatorPurposes[_locatorPurpose!.Selected],
                _locatorTags!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                    .ToArray(),
                CreateArrival(_arrivalFacing!, _arrivalDirection!, _arrivalTarget!,
                    _arrivalAnimation!, _arrivalWait!, _arrivalInteraction!, _arrivalCallback!),
                NullIfBlank(_locatorAsset!.Text), existing?.BlueprintId, existing?.ExplicitOverrides,
                _locatorRequired!.ButtonPressed);
            if (existing is null)
            {
                _locators.Add(locator);
                _selectedLocator = _locators.Count - 1;
            }
            else _locators[_selectedLocator] = locator;
            RefreshLocatorLists();
            RefreshWaypointPreview();
            SetStatus("Locator typé et comportement d’arrivée enregistrés.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveLocator()
    {
        if (_selectedLocator < 0 || _selectedLocator >= _locators.Count) return;
        var id = _locators[_selectedLocator].Id;
        _locators.RemoveAt(_selectedLocator);
        for (var index = 0; index < _locatorCollections.Count; index++)
            _locatorCollections[index] = _locatorCollections[index] with
            {
                LocatorIds = _locatorCollections[index].LocatorIds.Where(candidate => candidate != id).ToArray(),
            };
        _selectedLocator = -1;
        RefreshLocatorLists();
        RefreshWaypointPreview();
    }

    private void SelectCollection(long index)
    {
        if (index < 0 || index >= _locatorCollections.Count) return;
        _selectedCollection = (int)index;
        var collection = _locatorCollections[_selectedCollection];
        _collectionName!.Text = collection.Name;
        _collectionQuery!.Text = collection.Query;
        _collectionTags!.Text = string.Join(", ",
            (collection.RequiredTags ?? Array.Empty<string>()).Order(StringComparer.Ordinal));
        _collectionPurpose!.Selected = collection.Purpose is null
            ? 0 : Array.IndexOf(_locatorPurposes, collection.Purpose.Value) + 1;
        Select(_collectionSelection!, collection.Selection);
        _collectionSeed!.Value = collection.Seed;
        _collectionRequired!.ButtonPressed = collection.RequiredAtRuntime;
        RefreshLocatorSummary();
    }

    private void SaveCollection()
    {
        try
        {
            var existing = _selectedCollection >= 0 ? _locatorCollections[_selectedCollection] : null;
            var query = _collectionQuery!.Text.Trim();
            var requiredTags = _collectionTags!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
            LocatorPurpose? purpose = _collectionPurpose!.Selected == 0 ? null :
                _locatorPurposes[_collectionPurpose.Selected - 1];
            var memberIds = _locators.Where(locator =>
                    (purpose is null || locator.Purpose == purpose) &&
                    (string.IsNullOrWhiteSpace(query) ||
                     locator.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     locator.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))) &&
                    requiredTags.All(required => locator.Tags.Contains(required,
                        StringComparer.OrdinalIgnoreCase)))
                .OrderBy(locator => locator.Name, StringComparer.Ordinal).ThenBy(locator => locator.Id)
                .Select(locator => locator.Id).ToArray();
            var collection = new AnchorCollection(existing?.Id ?? Guid.NewGuid(), 1, "toolset:r065",
                query, memberIds, Required(_collectionName!.Text, "nom de collection"), purpose,
                requiredTags, _locatorSelectionModes[_collectionSelection!.Selected],
                Vector3Value.Zero, (int)_collectionSeed!.Value, _collectionRequired!.ButtonPressed);
            if (existing is null)
            {
                _locatorCollections.Add(collection);
                _selectedCollection = _locatorCollections.Count - 1;
            }
            else _locatorCollections[_selectedCollection] = collection;
            RefreshLocatorLists();
            RefreshWaypointPreview();
            SetStatus("Collection résolue sans écriture et enregistrée dans le brouillon.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveCollection()
    {
        if (_selectedCollection < 0 || _selectedCollection >= _locatorCollections.Count) return;
        _locatorCollections.RemoveAt(_selectedCollection);
        _selectedCollection = -1;
        RefreshLocatorLists();
        RefreshWaypointPreview();
    }

    private void LoadVisualizationDraft()
    {
        if (_visualSegments is null) return;
        var areaId = SelectedArea(_getState?.Invoke())?.Id ?? _areaIds.FirstOrDefault();
        _routeVisualization ??= RouteVisualizationProfile.CreateDefault(areaId!);
        var view = _routeVisualization;
        _visualSegments.ButtonPressed = view.ShowSegments;
        _visualDirection!.ButtonPressed = view.ShowDirection;
        _visualCosts!.ButtonPressed = view.ShowCosts;
        _visualActions!.ButtonPressed = view.ShowActions;
        _visualBroken!.ButtonPressed = view.ShowBrokenLinks;
        _visualNavProjection!.ButtonPressed = view.ShowNavigationProjection;
        _visualTiming!.ButtonPressed = view.ShowTiming;
        _visualCostPerMeter!.Value = view.CostPerMeter;
        _visualRequired!.ButtonPressed = view.RequiredAtRuntime;
    }

    private void SaveVisualizationDraft()
    {
        if (_visualSegments is null || _visualCostPerMeter is null) return;
        var areaId = SelectedArea(_getState?.Invoke())?.Id ?? _areaIds.FirstOrDefault();
        var current = _routeVisualization ?? RouteVisualizationProfile.CreateDefault(areaId!);
        _routeVisualization = new(current.Id, 1, "toolset:r065", _visualSegments.ButtonPressed,
            _visualDirection!.ButtonPressed, _visualCosts!.ButtonPressed, _visualActions!.ButtonPressed,
            _visualBroken!.ButtonPressed, _visualNavProjection!.ButtonPressed,
            _visualTiming!.ButtonPressed, _visualCostPerMeter.Value, _visualRequired!.ButtonPressed);
        RefreshWaypointPreview();
    }

    private void RefreshLocatorSummary()
    {
        if (_locatorSummary is null) return;
        var collection = _selectedCollection >= 0 && _selectedCollection < _locatorCollections.Count
            ? _locatorCollections[_selectedCollection] : _locatorCollections.FirstOrDefault();
        var resolved = collection is null ? _locators :
            WaypointRouteService.ResolveCollection(collection, _locators);
        var visualization = _routeVisualization;
        var known = _waypoints.Select(item => item.Id).Concat(_locators.Select(item => item.Id)).ToHashSet();
        var segments = _patrolRoutes.SelectMany(route => WaypointRouteService.AnalyzeRoute(route,
            _waypoints.Take(2).Select(item => item.Id).ToHashSet(), known,
            visualization?.CostPerMeter ?? 1)).ToArray();
        _locatorSummary.Text = "[color=#ff9858]R065 · LOCATORS & ROUTES[/color]\n" +
            $"{_locators.Count} locator(s) · {_locatorCollections.Count} collection(s) · " +
            $"{resolved.Count} résultat(s) prévisualisé(s)\n" +
            $"{segments.Length} segment(s) · {segments.Count(item => item.Direction < 0)} retour(s) · " +
            $"{segments.Count(item => item.NavProjected)} projection(s) nav\n" +
            "[color=#83d6ae]✓ Sens, coûts, actions, liens cassés et timing visibles[/color]";
    }

    private void SelectAgent(long index)
    {
        if (index < 0 || index >= _agents.Count) return;
        _selectedAgent = (int)index;
        var agent = _agents[_selectedAgent];
        _agentName!.Text = agent.Name;
        _agentRadius!.Value = agent.Radius;
        _agentHeight!.Value = agent.Height;
        _agentDoors!.ButtonPressed = agent.CanOpenDoors;
        _agentLinks!.ButtonPressed = agent.CanUseOffMeshLinks;
        _agentFlying!.ButtonPressed = agent.CanFly;
    }

    private void SaveAgent()
    {
        try
        {
            var id = _selectedAgent >= 0
                ? _agents[_selectedAgent].Id
                : NavigationAgentProfileId.From(Guid.NewGuid());
            var profile = new NavigationAgentProfile(
                id,
                _agentName!.Text,
                (float)_agentRadius!.Value,
                (float)_agentHeight!.Value,
                _agentDoors!.ButtonPressed,
                _agentLinks!.ButtonPressed,
                _agentFlying!.ButtonPressed,
                "aurora-navigation-editor");
            if (_selectedAgent >= 0) _agents[_selectedAgent] = profile;
            else _agents.Add(profile);
            _selectedAgent = -1;
            RefreshAgentList();
            SetStatus("Profil d’agent prêt à être appliqué.", false);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveAgent()
    {
        if (_selectedAgent < 0 || _agents.Count <= 1) return;
        _agents.RemoveAt(_selectedAgent);
        _selectedAgent = -1;
        RefreshAgentList();
    }

    private void SelectLink(long index)
    {
        if (index < 0 || index >= _links.Count) return;
        _selectedLink = (int)index;
        var link = _links[_selectedLink];
        _linkName!.Text = link.Name;
        _linkKind!.Selected = Array.IndexOf(_linkKinds, link.Kind);
        _startX!.Value = link.Start.X; _startY!.Value = link.Start.Y; _startZ!.Value = link.Start.Z;
        _endX!.Value = link.End.X; _endY!.Value = link.End.Y; _endZ!.Value = link.End.Z;
        _linkRadius!.Value = link.MaximumAgentRadius;
        _linkBidirectional!.ButtonPressed = link.Bidirectional;
    }

    private void SaveLink()
    {
        try
        {
            var id = _selectedLink >= 0 ? _links[_selectedLink].Id : OffMeshNavigationLinkId.From(Guid.NewGuid());
            var link = new OffMeshNavigationLink(
                id,
                _linkName!.Text,
                _linkKinds[_linkKind!.Selected],
                new Forge.Domain.Spatial.Vector3Value((float)_startX!.Value, (float)_startY!.Value, (float)_startZ!.Value),
                new Forge.Domain.Spatial.Vector3Value((float)_endX!.Value, (float)_endY!.Value, (float)_endZ!.Value),
                _linkBidirectional!.ButtonPressed,
                (float)_linkRadius!.Value,
                "aurora-navigation-editor");
            if (_selectedLink >= 0) _links[_selectedLink] = link;
            else _links.Add(link);
            _selectedLink = -1;
            RefreshLinkList();
            SetStatus("Lien hors-maillage prêt à être appliqué.", false);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void RemoveLink()
    {
        if (_selectedLink < 0) return;
        _links.RemoveAt(_selectedLink);
        _selectedLink = -1;
        RefreshLinkList();
    }

    private void Preview()
    {
        var state = _getState?.Invoke();
        var area = SelectedArea(state);
        if (state is null || area is null) return;
        try
        {
            var configuredArea = area.WithNavigation(CreateNavigation());
            var geometry = AreaGeometryValidator.Validate(state.Project, configuredArea);
            var analyses = new List<AreaNavigationAnalysis>();
            foreach (var doorState in Enum.GetValues<NavigationDoorState>())
            {
                analyses.AddRange(AreaNavigationValidator.AnalyzeAll(state.Project, configuredArea, doorState));
            }
            var project = new Forge.Domain.Projects.ProjectDefinition(state.Project.Id, state.Project.Title,
                state.Project.Module, state.Project.Blueprints,
                state.Project.Areas.Select(item => item.Id == configuredArea.Id ? configuredArea : item).ToArray(),
                state.Project.Dialogues, state.Project.Quests);
            var anchorDiagnostics = AnchorCatalogValidator.Validate(project, configuredArea);
            var waypointDiagnostics = WaypointAuthoringValidator.Validate(configuredArea);

            _diagnostics!.Clear();
            foreach (var diagnostic in geometry.Concat(analyses.SelectMany(item => item.Diagnostics))
                         .Concat(anchorDiagnostics).Concat(waypointDiagnostics))
            {
                _diagnostics.AddItem($"{diagnostic.Code} · {diagnostic.Message}");
                _diagnostics.SetItemTooltip(_diagnostics.ItemCount - 1, diagnostic.Path);
            }

            var selectedAgent = configuredArea.Navigation.AgentProfiles[0];
            var surface = analyses.First(item => item.AgentProfile.Id == selectedAgent.Id &&
                item.DoorState == NavigationDoorState.Current).Surface;
            var endpoints = configuredArea.Entities
                .Select(entity => surface.CellAt(entity.Transform.Position))
                .Distinct()
                .Take(2)
                .ToArray();
            var path = endpoints.Length == 2
                ? NavigationPathfinder.FindPath(surface, new NavigationPathQuery(endpoints[0], endpoints[1]))
                : null;
            LastPathSucceeded = path?.Succeeded == true;
            _showOverlay?.Invoke(surface);
            _summary!.Text =
                $"{surface.WalkableCells.Count:N0} cellules · {surface.OffMeshEdges.Count} arête(s) hors-maillage · " +
                $"{configuredArea.Navigation.AgentProfiles.Count} profil(s) · {_diagnostics.ItemCount} diagnostic(s) · " +
                (path is null
                    ? "route non applicable"
                    : path.Succeeded
                        ? $"route {path.Cells.Count} cellules / {path.VisitedCellCount} visitées"
                        : $"route {path.Failure}");
            _summary.AddThemeColorOverride("font_color",
                _diagnostics.ItemCount == 0 ? new Color("83d6ae") : new Color("ffb36d"));
            RefreshWaypointPreview(configuredArea);
            SetStatus("Bake déterministe actualisé dans le viewport.", false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            SetStatus(exception.Message, true);
        }
    }

    private bool Apply()
    {
        var state = _getState?.Invoke();
        var area = SelectedArea(state);
        if (state is null || area is null) return false;
        try
        {
            var result = _apply?.Invoke(area.Id, CreateNavigation()) == true;
            SetStatus(result ? "Configuration appliquée et ajoutée à l’historique." : "Application refusée.", !result);
            if (result) Preview();
            return result;
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
            return false;
        }
    }

    private AreaDefinition? SelectedArea(ProjectState? state) =>
        state is null || _areaPicker is null || _areaPicker.Selected < 0 || _areaPicker.Selected >= _areaIds.Count
            ? null
            : state.Project.Areas.FirstOrDefault(area => area.Id == _areaIds[_areaPicker.Selected]);

    private AreaNavigationDefinition CreateNavigation() => new(
        _definitionId ?? AreaNavigationDefinitionId.From(Guid.NewGuid()),
        (float)_cellSize!.Value,
        _agents,
        _links,
        "aurora-navigation-editor",
        anchors: new AnchorCatalog(_catalogId == Guid.Empty ? Guid.NewGuid() : _catalogId, 1,
            _catalogProvenance, SelectedArea(_getState?.Invoke())?.Id ?? _areaIds[0],
            _catalogRequiredAtRuntime, _anchorDefinitions, _anchorInstances),
        waypoints: new WaypointAuthoringProfile(
            _waypointProfileId == Guid.Empty ? Guid.NewGuid() : _waypointProfileId, 1,
            _waypointProvenance, SelectedArea(_getState?.Invoke())?.Id ?? _areaIds[0],
            _waypointsRequiredAtRuntime, _waypoints, _patrolRoutes, _locators, _locatorCollections,
            _routeVisualization));

    private void Close()
    {
        _showOverlay?.Invoke(null);
        Hide();
    }

    private void SetStatus(string message, bool error)
    {
        if (_status is null) return;
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? new Color("ff8c8c") : new Color("83d6ae"));
    }

    private static VBoxContainer AddCard(HBoxContainer parent, float width, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("211a2d", "47365e", 1, 12));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 14);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);
        return column;
    }

    private static SpinBox Coordinate() => Spin(-4096, 4096, 0.25, 0);

    private static SpinBox Spin(double min, double max, double step, double value) => new()
    {
        MinValue = min,
        MaxValue = max,
        Step = step,
        Value = value,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    private static CheckBox Check(string text) => new() { Text = text, ButtonPressed = true };

    private static Label AccentLabel(string text)
    {
        var label = new Label { Text = text, ThemeTypeVariation = "HeaderSmall" };
        label.AddThemeColorOverride("font_color", new Color("ff9858"));
        return label;
    }

    private static Label MutedLabel(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color("a899b9"));
        return label;
    }

    private static string FormatKind(NavigationLinkKind kind) => kind switch
    {
        NavigationLinkKind.Jump => "Saut",
        NavigationLinkKind.Door => "Porte",
        NavigationLinkKind.Flight => "Vol",
        _ => kind.ToString(),
    };

    private static string FormatAnchorKind(AnchorKind kind) => kind switch
    {
        AnchorKind.Waypoint => "Waypoint",
        AnchorKind.Spawn => "Départ",
        AnchorKind.TransitionDestination => "Destination",
        _ => kind.ToString(),
    };

    private static AnchorDefinition CreateSmokeDefinition(string slug, string key, string label,
        AnchorKind kind, IReadOnlyList<string> tags, BlueprintId? blueprintId) => new(
        Guid.NewGuid(), 1, "toolset:r009", slug,
        new LocalizedText(key, label, provenance: "toolset:r009"), kind, AnchorScope.Area,
        tags, blueprintId, null, true);

    private static TransformValue At(float x, float y, float z, float yawDegrees)
    {
        var half = yawDegrees * MathF.PI / 360;
        return new TransformValue(new Vector3Value(x, y, z),
            new QuaternionValue(0, MathF.Sin(half), 0, MathF.Cos(half)), Vector3Value.One);
    }

    private static float QuaternionYaw(QuaternionValue value)
    {
        var siny = 2 * (value.W * value.Y + value.X * value.Z);
        var cosy = 1 - 2 * (value.Y * value.Y + value.Z * value.Z);
        return MathF.Atan2(siny, cosy) * 180 / MathF.PI;
    }

    private static EntityPropertyOverrides ParseOverrides(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return EntityPropertyOverrides.Empty;
        var properties = text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => entry.Split('=', 2, StringSplitOptions.TrimEntries))
            .Select(parts => parts.Length == 2
                ? new EntityPropertyOverride(Required(parts[0], "clé override"), EntityPropertyValueKind.String, parts[1])
                : throw new ArgumentException($"Override invalide : {parts[0]}"))
            .ToArray();
        return new EntityPropertyOverrides(EntityPropertyOverrides.CurrentSchemaVersion, properties);
    }

    private static Guid? ParseOptionalGuid(string text) =>
        string.IsNullOrWhiteSpace(text) ? null : Guid.Parse(text.Trim());

    private static string Required(string text, string label) =>
        string.IsNullOrWhiteSpace(text) ? throw new ArgumentException($"{label} requis") : text.Trim();

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private ArrivalBehavior? CreateArrival(OptionButton facing, SpinBox direction, LineEdit target,
        LineEdit animation, SpinBox wait, LineEdit interaction, LineEdit callback)
    {
        var mode = _arrivalFacingModes[facing.Selected];
        if (mode == ArrivalFacingMode.Preserve && direction.Value == 0 &&
            string.IsNullOrWhiteSpace(target.Text) && string.IsNullOrWhiteSpace(animation.Text) &&
            wait.Value == 0 && string.IsNullOrWhiteSpace(interaction.Text) &&
            string.IsNullOrWhiteSpace(callback.Text)) return null;
        return new(mode, direction.Value, NullIfBlank(target.Text), NullIfBlank(animation.Text),
            wait.Value, NullIfBlank(interaction.Text), NullIfBlank(callback.Text));
    }

    private static float ParseFloat(string value) => float.Parse(value, NumberStyles.Float,
        CultureInfo.InvariantCulture);

    private static double ParseDouble(string value) => double.Parse(value, NumberStyles.Float,
        CultureInfo.InvariantCulture);

    private static void Select<T>(OptionButton button, T value) where T : struct, Enum =>
        button.Selected = Array.IndexOf(Enum.GetValues<T>(), value);

    private static string FormatTraversal(PatrolTraversalPolicy policy) => policy switch
    {
        PatrolTraversalPolicy.Loop => "Boucle",
        PatrolTraversalPolicy.PingPong => "Aller / retour",
        PatrolTraversalPolicy.Once => "Une fois",
        PatrolTraversalPolicy.Random => "Aléatoire déterministe",
        PatrolTraversalPolicy.Weighted => "Pondéré",
        PatrolTraversalPolicy.Scripted => "Scripté",
        _ => policy.ToString(),
    };

    private static string FormatSelection(LocatorSelectionMode selection) => selection switch
    {
        LocatorSelectionMode.Stable => "Ordre stable",
        LocatorSelectionMode.Nearest => "Plus proche",
        LocatorSelectionMode.RandomDeterministic => "Aléatoire déterministe",
        _ => selection.ToString(),
    };

    private static StyleBoxFlat Card(string background, string border, int width, int radius) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
    };
}
#endif
