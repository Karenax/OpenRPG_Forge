#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Validation;
using Godot;

namespace Forge.Toolset.Editor;

public partial class NavigationConfigurationWindow : Window
{
    private readonly List<AreaId> _areaIds = [];
    private readonly List<NavigationAgentProfile> _agents = [];
    private readonly List<OffMeshNavigationLink> _links = [];
    private readonly NavigationLinkKind[] _linkKinds = Enum.GetValues<NavigationLinkKind>();

    private Func<ProjectState?>? _getState;
    private Func<AreaId?>? _getActiveAreaId;
    private Func<AreaId, AreaNavigationDefinition, bool>? _apply;
    private Action<NavigationSurface?>? _showOverlay;
    private Action<Window>? _brandWindow;
    private AreaNavigationDefinitionId? _definitionId;
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
    private int _selectedAgent = -1;
    private int _selectedLink = -1;

    public int AgentCount => _agents.Count;

    public int LinkCount => _links.Count;

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

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);
        BuildAgentsCard(body);
        BuildLinksCard(body);
        BuildDiagnosticsCard(body);

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

    private void RefreshAreas(AreaId? preferred = null)
    {
        var state = _getState?.Invoke();
        if (state is null || _areaPicker is null) return;
        var selected = preferred ?? _getActiveAreaId?.Invoke();
        _areaIds.Clear();
        _areaPicker.Clear();
        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        {
            _areaIds.Add(area.Id);
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
        _selectedAgent = -1;
        _selectedLink = -1;
        RefreshAgentList();
        RefreshLinkList();
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

            _diagnostics!.Clear();
            foreach (var diagnostic in geometry.Concat(analyses.SelectMany(item => item.Diagnostics)))
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
        "aurora-navigation-editor");

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
