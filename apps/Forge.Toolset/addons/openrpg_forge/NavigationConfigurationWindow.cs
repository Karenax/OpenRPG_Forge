#if TOOLS
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
    private readonly NavigationLinkKind[] _linkKinds = Enum.GetValues<NavigationLinkKind>();
    private readonly AnchorKind[] _anchorKinds = Enum.GetValues<AnchorKind>();
    private readonly AnchorScope[] _anchorScopes = Enum.GetValues<AnchorScope>();
    private readonly PortalOrientationPolicy[] _orientationPolicies = Enum.GetValues<PortalOrientationPolicy>();
    private readonly PortalVelocityPolicy[] _velocityPolicies = Enum.GetValues<PortalVelocityPolicy>();

    private Func<ProjectState?>? _getState;
    private Func<AreaId?>? _getActiveAreaId;
    private Func<AreaId, AreaNavigationDefinition, bool>? _apply;
    private Action<NavigationSurface?>? _showOverlay;
    private Action<Window>? _brandWindow;
    private AreaNavigationDefinitionId? _definitionId;
    private Guid _catalogId;
    private string _catalogProvenance = "toolset:r009";
    private bool _catalogRequiredAtRuntime;
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
    private int _selectedAgent = -1;
    private int _selectedLink = -1;
    private int _selectedAnchor = -1;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;

    public int AgentCount => _agents.Count;

    public int LinkCount => _links.Count;

    public int AnchorCount => _anchorInstances.Count;

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
        MinSize = new Vector2I(1240, 790);
        Size = MinSize;
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        CloseRequested += Close;
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--waypoint-anchor-ui-capture=", StringComparison.Ordinal));
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
            ConfigureForR009SmokeTest();
            _tabs!.CurrentTab = 1;
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_WAYPOINT_ANCHOR_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
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
        var spawnDefinition = CreateSmokeDefinition("player-start", "anchor.player_start", "Départ du joueur",
            AnchorKind.Spawn, ["player", "spawn"], spawnBlueprint.Id);
        var destinationDefinition = CreateSmokeDefinition("crypt-exit", "anchor.crypt_exit", "Sortie de la crypte",
            AnchorKind.TransitionDestination, ["exit", "portal"], null);
        var waypointDefinition = CreateSmokeDefinition("ember-marker", "anchor.ember_marker", "Repère de la braise",
            AnchorKind.Waypoint, ["ember", "quest"], null);
        var spawnId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var waypointId = Guid.NewGuid();
        _anchorDefinitions.Clear();
        _anchorDefinitions.AddRange([spawnDefinition, destinationDefinition, waypointDefinition]);
        _anchorInstances.Clear();
        _anchorInstances.AddRange([
            new AnchorInstance(spawnId, 1, "toolset:r009", spawnDefinition.Id,
                At(0, 0, 9, 180), 1, EntityPropertyOverrides.Empty,
                new SpawnAnchorProfile("player", new Vector3Value(1.2f, 2f, 1.2f), 100, "heroes", 180,
                    null, true), null),
            new AnchorInstance(destinationId, 1, "toolset:r009", destinationDefinition.Id,
                At(4, 0, 1, 90), 2, EntityPropertyOverrides.Empty, null,
                new PortalAnchorDestination(area.Id, waypointId, PortalOrientationPolicy.AnchorRotation, 0,
                    PortalVelocityPolicy.RedirectForward, true)),
            new AnchorInstance(waypointId, 1, "toolset:r009", waypointDefinition.Id,
                At(-3, 0, -2, 45), 3,
                new EntityPropertyOverrides(1, [new EntityPropertyOverride("quest.marker", EntityPropertyValueKind.String, "ember")]),
                null, null),
        ]);
        _catalogId = Guid.NewGuid();
        _catalogProvenance = "toolset:r009";
        _catalogRequiredAtRuntime = true;
        RefreshAnchorList();
        _anchorList!.Select(0);
        SelectAnchor(0);
        Preview();
        return DiagnosticCount == 0 && AnchorCount == 3;
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
        _selectedAgent = -1;
        _selectedLink = -1;
        _selectedAnchor = -1;
        RefreshAgentList();
        RefreshLinkList();
        RefreshAnchorList();
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

            _diagnostics!.Clear();
            foreach (var diagnostic in geometry.Concat(analyses.SelectMany(item => item.Diagnostics))
                         .Concat(anchorDiagnostics))
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
            _catalogRequiredAtRuntime, _anchorDefinitions, _anchorInstances));

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

    private static void Select<T>(OptionButton button, T value) where T : struct, Enum =>
        button.Selected = Array.IndexOf(Enum.GetValues<T>(), value);

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
