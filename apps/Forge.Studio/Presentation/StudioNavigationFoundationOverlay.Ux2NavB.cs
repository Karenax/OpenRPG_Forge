using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal static class StudioUx2NavBControls
{
    internal static LineEdit Text(string placeholder = "")
    {
        var control = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(control);
        return control;
    }

    internal static SpinBox Number(double minimum, double maximum, double step = .1,
        bool allowOutside = false)
    {
        var control = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 0,
            CustomArrowStep = step,
            CustomArrowRound = false,
            AllowGreater = allowOutside,
            AllowLesser = allowOutside,
            UpdateOnTextChanged = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(control);
        return control;
    }

    internal static OptionButton Options(params string[] labels)
    {
        var control = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FitToLongestItem = false,
        };
        StudioStyle.ApplyOptionButton(control);
        foreach (var label in labels)
        {
            control.AddItem(label);
        }
        return control;
    }

    internal static CheckBox Check(string label)
    {
        var control = new CheckBox { Text = label };
        control.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        control.AddThemeColorOverride("font_color", StudioStyle.Text);
        control.AddThemeColorOverride("font_hover_color", StudioStyle.Text);
        control.AddThemeColorOverride("font_pressed_color", StudioStyle.Text);
        return control;
    }

    internal static VBoxContainer Field(string label, Control control, int minimumWidth = 105)
    {
        var field = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(minimumWidth, 0),
        };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        field.AddChild(control);
        return field;
    }

    internal static PanelContainer Card(string name, string title, string help, Control content)
    {
        var panel = StudioStyle.Panel(name, StudioStyle.Surface, 8, 1);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 5);
        root.AddChild(StudioStyle.Label(title, 12));
        root.AddChild(StudioStyle.Label(help, 9, StudioStyle.Muted, wrap: true));
        root.AddChild(content);
        margin.AddChild(root);
        return panel;
    }
}

internal sealed partial class StudioNavigationFoundationOverlayUx2NavB : Control
{
    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2NavBEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastEvidencePreviewDryRun;
    private bool _lastUiTransaction;

    private readonly PanelContainer _dialog;
    private readonly ScrollContainer _scroll;
    private readonly PanelContainer _anchorEvidenceTarget;
    private readonly OptionButton _area;
    private readonly Label _selection;
    private readonly SpinBox _cellSize;

    private readonly OptionButton _agent;
    private readonly LineEdit _agentName;
    private readonly SpinBox _agentRadius;
    private readonly SpinBox _agentHeight;
    private readonly CheckBox _agentDoors;
    private readonly CheckBox _agentLinks;
    private readonly CheckBox _agentFlight;
    private readonly Button _addAgent;
    private readonly Button _removeAgent;

    private readonly OptionButton _offMesh;
    private readonly LineEdit _offMeshName;
    private readonly OptionButton _offMeshKind;
    private readonly SpinBox _offMeshStartX;
    private readonly SpinBox _offMeshStartY;
    private readonly SpinBox _offMeshStartZ;
    private readonly SpinBox _offMeshEndX;
    private readonly SpinBox _offMeshEndY;
    private readonly SpinBox _offMeshEndZ;
    private readonly SpinBox _offMeshRadius;
    private readonly CheckBox _offMeshBidirectional;
    private readonly Button _addOffMesh;
    private readonly Button _removeOffMesh;

    private readonly OptionButton _definition;
    private readonly LineEdit _definitionLabel;
    private readonly LineEdit _definitionSlug;
    private readonly OptionButton _definitionKind;
    private readonly OptionButton _definitionScope;
    private readonly LineEdit _definitionTags;
    private readonly OptionButton _spawnBlueprint;
    private readonly CheckBox _definitionRequired;
    private readonly Label _definitionProtected;
    private readonly Button _addWaypoint;
    private readonly Button _addSpawn;
    private readonly Button _addTransition;
    private readonly Button _removeDefinition;

    private readonly OptionButton _instance;
    private readonly Button _addInstance;
    private readonly SpinBox _positionX;
    private readonly SpinBox _positionY;
    private readonly SpinBox _positionZ;
    private readonly SpinBox _displayOrder;
    private readonly VBoxContainer _spawnFields;
    private readonly LineEdit _spawnProfile;
    private readonly SpinBox _spawnClearanceX;
    private readonly SpinBox _spawnClearanceY;
    private readonly SpinBox _spawnClearanceZ;
    private readonly SpinBox _spawnPriority;
    private readonly LineEdit _spawnTeam;
    private readonly SpinBox _spawnOrientation;
    private readonly CheckBox _spawnNavigation;
    private readonly OptionButton _spawnFallback;
    private readonly VBoxContainer _portalFields;
    private readonly OptionButton _portalDestination;
    private readonly OptionButton _portalOrientation;
    private readonly SpinBox _portalYaw;
    private readonly OptionButton _portalVelocity;
    private readonly CheckBox _portalPreload;
    private readonly Label _instanceProtected;
    private readonly Button _removeInstance;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2NavBAreaChoice> _choices = [];
    private IReadOnlyList<StudioUx2NavBBlueprintChoice> _blueprintChoices = [];
    private IReadOnlyList<StudioUx2NavBDestinationChoice> _destinationChoices = [];
    private IReadOnlyList<Guid?> _spawnFallbackIds = [];
    private StudioUx2NavBDraft? _draft;
    private StudioUx2NavBPreview? _lastPreview;
    private bool _updating;
    private bool _applied;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? NavigationChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady;
    internal bool ShowsRealSurface => _lastPreview is { WalkableCellCount: > 0, DisplayedCellCount: > 0 } &&
                                      _worldViewport.NavigationOverlayPrimitiveCountUx2NavB > 0;
    internal bool ShowsHumanDestinations => _destinationChoices.Count > 0 &&
                                            _destinationChoices.All(item => !ContainsUuidUx2NavB(item.Label));
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;
    internal bool LastUiTransaction => _lastUiTransaction;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabelsUx2NavB(_dialog).Select(item => item.Text));
            return HasExplicitSelection &&
                   text.Contains("Surface", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Agent", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Liaison", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Ancre", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx2NavB(text);
        }
    }

    internal StudioNavigationFoundationOverlayUx2NavB(StudioSessionController session, StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioNavigationFoundationOverlayUx2NavB";
        Visible = false;
        ZIndex = 455;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("NavigationFoundationDialog", StudioStyle.Chrome, 12, 2);
        _dialog.MouseFilter = MouseFilterEnum.Stop;
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 7);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · SURFACE ET ANCRES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Chemins jouables et points d’arrivée", 19));
        heading.AddChild(StudioStyle.Label(
            "AREA-031 · WPT-001 / 005 / 006 / 017 · surface calculée, aperçu sec et commande atomique. " +
            "Aucun pathfinding moteur ou voyage n’est exécuté ici.", 9, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selector.AddThemeConstantOverride("separation", 7);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Zone", 10, StudioStyle.Muted));
        _area = StudioUx2NavBControls.Options();
        _area.ItemSelected += SelectArea;
        selector.AddChild(_area);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone ; aucune navigation n’est chargée automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 420),
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        _scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(_scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        _scroll.AddChild(content);

        var surface = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        surface.AddThemeConstantOverride("separation", 5);
        _cellSize = StudioUx2NavBControls.Number(.25, 4, .05);
        surface.AddChild(StudioUx2NavBControls.Field("Pas de surface (m)", _cellSize));
        content.AddChild(StudioUx2NavBControls.Card("NavigationSurface", "Surface de navigation",
            "La grille verte provient du builder canonique. L’aperçu indique reconstruction complète ou partielle.",
            surface));

        var agentContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        agentContent.AddThemeConstantOverride("separation", 5);
        var agentSelect = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _agent = StudioUx2NavBControls.Options();
        _agent.ItemSelected += SelectAgent;
        agentSelect.AddChild(_agent);
        _addAgent = StudioStyle.Button("Ajouter", compact: true);
        _addAgent.Pressed += AddAgent;
        agentSelect.AddChild(_addAgent);
        _removeAgent = StudioStyle.Button("Retirer", compact: true);
        _removeAgent.Pressed += RemoveAgent;
        agentSelect.AddChild(_removeAgent);
        agentContent.AddChild(agentSelect);
        var agentGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        agentGrid.AddThemeConstantOverride("h_separation", 6);
        agentContent.AddChild(agentGrid);
        _agentName = StudioUx2NavBControls.Text("Nom humain");
        _agentRadius = StudioUx2NavBControls.Number(.1, 8, .05);
        _agentHeight = StudioUx2NavBControls.Number(.2, 16, .05);
        agentGrid.AddChild(StudioUx2NavBControls.Field("Nom", _agentName, 170));
        agentGrid.AddChild(StudioUx2NavBControls.Field("Rayon (m)", _agentRadius));
        agentGrid.AddChild(StudioUx2NavBControls.Field("Hauteur (m)", _agentHeight));
        var agentFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _agentDoors = StudioUx2NavBControls.Check("Ouvre les portes");
        _agentLinks = StudioUx2NavBControls.Check("Utilise les liaisons");
        _agentFlight = StudioUx2NavBControls.Check("Volant");
        agentFlags.AddChild(_agentDoors);
        agentFlags.AddChild(_agentLinks);
        agentFlags.AddChild(_agentFlight);
        agentContent.AddChild(agentFlags);
        content.AddChild(StudioUx2NavBControls.Card("NavigationAgents", "Profils d’agent",
            "Chaque profil possède son propre gabarit et ses capacités ; aucun profil n’est sélectionné implicitement.",
            agentContent));

        var linkContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        linkContent.AddThemeConstantOverride("separation", 5);
        var linkSelect = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _offMesh = StudioUx2NavBControls.Options();
        _offMesh.ItemSelected += SelectOffMesh;
        linkSelect.AddChild(_offMesh);
        _addOffMesh = StudioStyle.Button("Ajouter", compact: true);
        _addOffMesh.Pressed += AddOffMesh;
        linkSelect.AddChild(_addOffMesh);
        _removeOffMesh = StudioStyle.Button("Retirer", compact: true);
        _removeOffMesh.Pressed += RemoveOffMesh;
        linkSelect.AddChild(_removeOffMesh);
        linkContent.AddChild(linkSelect);
        var linkIdentity = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _offMeshName = StudioUx2NavBControls.Text("Nom de liaison");
        _offMeshKind = StudioUx2NavBControls.Options("Saut", "Porte", "Vol");
        _offMeshRadius = StudioUx2NavBControls.Number(.1, 8, .05);
        linkIdentity.AddChild(StudioUx2NavBControls.Field("Nom", _offMeshName, 170));
        linkIdentity.AddChild(StudioUx2NavBControls.Field("Type", _offMeshKind));
        linkIdentity.AddChild(StudioUx2NavBControls.Field("Rayon agent max.", _offMeshRadius));
        linkContent.AddChild(linkIdentity);
        var linkPoints = new GridContainer { Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _offMeshStartX = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _offMeshStartY = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _offMeshStartZ = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _offMeshEndX = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _offMeshEndY = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _offMeshEndZ = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        linkPoints.AddChild(StudioUx2NavBControls.Field("Départ X", _offMeshStartX));
        linkPoints.AddChild(StudioUx2NavBControls.Field("Y", _offMeshStartY));
        linkPoints.AddChild(StudioUx2NavBControls.Field("Z", _offMeshStartZ));
        linkPoints.AddChild(StudioUx2NavBControls.Field("Arrivée X", _offMeshEndX));
        linkPoints.AddChild(StudioUx2NavBControls.Field("Y", _offMeshEndY));
        linkPoints.AddChild(StudioUx2NavBControls.Field("Z", _offMeshEndZ));
        linkContent.AddChild(linkPoints);
        _offMeshBidirectional = StudioUx2NavBControls.Check("Parcours dans les deux sens");
        linkContent.AddChild(_offMeshBidirectional);
        content.AddChild(StudioUx2NavBControls.Card("NavigationOffMesh", "Liaisons hors surface",
            "Sauts, escalades, téléportations ou vol sont dessinés en bleu ; le runtime n’est pas simulé.",
            linkContent));

        var definitionContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        definitionContent.AddThemeConstantOverride("separation", 5);
        var definitionSelect = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _definition = StudioUx2NavBControls.Options();
        _definition.ItemSelected += SelectDefinition;
        definitionSelect.AddChild(_definition);
        _addWaypoint = StudioStyle.Button("+ Waypoint", compact: true);
        _addWaypoint.Pressed += () => AddAnchor(AnchorKind.Waypoint);
        definitionSelect.AddChild(_addWaypoint);
        _addSpawn = StudioStyle.Button("+ Départ", compact: true);
        _addSpawn.Pressed += () => AddAnchor(AnchorKind.Spawn);
        definitionSelect.AddChild(_addSpawn);
        _addTransition = StudioStyle.Button("+ Transition", compact: true);
        _addTransition.Pressed += () => AddAnchor(AnchorKind.TransitionDestination);
        definitionSelect.AddChild(_addTransition);
        _removeDefinition = StudioStyle.Button("Retirer", compact: true);
        _removeDefinition.Pressed += RemoveDefinition;
        definitionSelect.AddChild(_removeDefinition);
        definitionContent.AddChild(definitionSelect);
        var definitionGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _definitionLabel = StudioUx2NavBControls.Text("Nom affiché");
        _definitionSlug = StudioUx2NavBControls.Text("slug-humain-global");
        _definitionKind = StudioUx2NavBControls.Options("Waypoint", "Point de départ", "Destination de transition");
        _definitionScope = StudioUx2NavBControls.Options("Zone", "Module", "Monde");
        _definitionTags = StudioUx2NavBControls.Text("navigation, village");
        _spawnBlueprint = StudioUx2NavBControls.Options();
        definitionGrid.AddChild(StudioUx2NavBControls.Field("Nom", _definitionLabel, 170));
        definitionGrid.AddChild(StudioUx2NavBControls.Field("Slug global", _definitionSlug, 170));
        definitionGrid.AddChild(StudioUx2NavBControls.Field("Rôle", _definitionKind));
        definitionGrid.AddChild(StudioUx2NavBControls.Field("Portée", _definitionScope));
        definitionGrid.AddChild(StudioUx2NavBControls.Field("Tags lisibles", _definitionTags, 170));
        definitionGrid.AddChild(StudioUx2NavBControls.Field("Blueprint d’apparition", _spawnBlueprint, 170));
        definitionContent.AddChild(definitionGrid);
        _definitionRequired = StudioUx2NavBControls.Check("Requise au runtime");
        definitionContent.AddChild(_definitionRequired);
        _definitionProtected = StudioStyle.Label(
            "Asset, localisation enrichie et provenance restent protégés tant qu’ils ne sont pas édités.",
            9, StudioStyle.Muted, wrap: true);
        definitionContent.AddChild(_definitionProtected);
        content.AddChild(StudioUx2NavBControls.Card("NavigationAnchorDefinitions", "Ancres nommées",
            "Nom localisé, slug global, rôle et portée. Le slug d’une ancre de départ est la clé d’arrivée canonique.",
            definitionContent));

        var instanceContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        instanceContent.AddThemeConstantOverride("separation", 5);
        var instanceSelect = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _instance = StudioUx2NavBControls.Options();
        _instance.ItemSelected += SelectInstance;
        instanceSelect.AddChild(_instance);
        _addInstance = StudioStyle.Button("+ Placement", compact: true);
        _addInstance.Pressed += AddInstance;
        instanceSelect.AddChild(_addInstance);
        _removeInstance = StudioStyle.Button("Retirer l’instance", compact: true);
        _removeInstance.Pressed += RemoveInstance;
        instanceSelect.AddChild(_removeInstance);
        instanceContent.AddChild(instanceSelect);
        var positionGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _positionX = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _positionY = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _positionZ = StudioUx2NavBControls.Number(-4096, 4096, .25, allowOutside: true);
        _displayOrder = StudioUx2NavBControls.Number(1, 100_000, 1, allowOutside: true);
        positionGrid.AddChild(StudioUx2NavBControls.Field("Position X", _positionX));
        positionGrid.AddChild(StudioUx2NavBControls.Field("Y", _positionY));
        positionGrid.AddChild(StudioUx2NavBControls.Field("Z", _positionZ));
        positionGrid.AddChild(StudioUx2NavBControls.Field("Ordre", _displayOrder));
        instanceContent.AddChild(positionGrid);

        _spawnFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _spawnFields.AddThemeConstantOverride("separation", 5);
        var spawnIdentity = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _spawnProfile = StudioUx2NavBControls.Text("Profil de départ");
        _spawnTeam = StudioUx2NavBControls.Text("Équipe");
        _spawnPriority = StudioUx2NavBControls.Number(1, 100, 1, allowOutside: true);
        _spawnOrientation = StudioUx2NavBControls.Number(-3600, 3600, 5, allowOutside: true);
        spawnIdentity.AddChild(StudioUx2NavBControls.Field("Profil", _spawnProfile));
        spawnIdentity.AddChild(StudioUx2NavBControls.Field("Équipe", _spawnTeam));
        spawnIdentity.AddChild(StudioUx2NavBControls.Field("Priorité", _spawnPriority));
        spawnIdentity.AddChild(StudioUx2NavBControls.Field("Orientation (°)", _spawnOrientation));
        _spawnFields.AddChild(spawnIdentity);
        var clearance = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _spawnClearanceX = StudioUx2NavBControls.Number(.01, 256, .05, allowOutside: true);
        _spawnClearanceY = StudioUx2NavBControls.Number(.01, 256, .05, allowOutside: true);
        _spawnClearanceZ = StudioUx2NavBControls.Number(.01, 256, .05, allowOutside: true);
        clearance.AddChild(StudioUx2NavBControls.Field("Dégagement largeur", _spawnClearanceX));
        clearance.AddChild(StudioUx2NavBControls.Field("Hauteur", _spawnClearanceY));
        clearance.AddChild(StudioUx2NavBControls.Field("Profondeur", _spawnClearanceZ));
        _spawnFields.AddChild(clearance);
        _spawnFallback = StudioUx2NavBControls.Options();
        _spawnFields.AddChild(StudioUx2NavBControls.Field(
            "Repli si ce départ est indisponible", _spawnFallback, 220));
        _spawnNavigation = StudioUx2NavBControls.Check("Exige une surface de navigation compatible");
        _spawnFields.AddChild(_spawnNavigation);
        instanceContent.AddChild(_spawnFields);

        _portalFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _portalFields.AddThemeConstantOverride("separation", 5);
        _portalDestination = StudioUx2NavBControls.Options();
        _portalFields.AddChild(StudioUx2NavBControls.Field("Destination humaine", _portalDestination, 260));
        var portalPolicy = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _portalOrientation = StudioUx2NavBControls.Options("Conserver", "Rotation de l’ancre", "Angle fixe");
        _portalYaw = StudioUx2NavBControls.Number(-3600, 3600, 5, allowOutside: true);
        _portalVelocity = StudioUx2NavBControls.Options("Conserver", "Réinitialiser", "Rediriger vers l’avant");
        portalPolicy.AddChild(StudioUx2NavBControls.Field("Orientation", _portalOrientation));
        portalPolicy.AddChild(StudioUx2NavBControls.Field("Angle fixe (°)", _portalYaw));
        portalPolicy.AddChild(StudioUx2NavBControls.Field("Vitesse", _portalVelocity));
        _portalFields.AddChild(portalPolicy);
        _portalPreload = StudioUx2NavBControls.Check("Précharger la zone cible");
        _portalFields.AddChild(_portalPreload);
        instanceContent.AddChild(_portalFields);
        _instanceProtected = StudioStyle.Label(
            "Rotation, échelle, overrides et repli éventuel restent strictement préservés hors de cette surface.",
            9, StudioStyle.Muted, wrap: true);
        instanceContent.AddChild(_instanceProtected);
        _anchorEvidenceTarget = StudioUx2NavBControls.Card("NavigationAnchorInstances", "Placement et arrivée",
            "Position, profil de départ ou destination inter-zone. Le déplacement reste borné et validé avant commande.",
            instanceContent);
        content.AddChild(_anchorEvidenceTarget);

        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diff.AddThemeConstantOverride("separation", 3);
        content.AddChild(StudioUx2NavBControls.Card("NavigationDiff", "Diff humain avant application",
            "Les compteurs décrivent les collections canoniques, sans identifiant technique.", _diff));
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics.AddThemeConstantOverride("separation", 3);
        content.AddChild(StudioUx2NavBControls.Card("NavigationDiagnostics", "Validation localisée",
            "Les erreurs de surface, d’ancre, de blueprint et de destination sont bloquantes et regroupées ici.",
            _diagnostics));

        _summary = StudioStyle.Label(
            "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.",
            10, StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 6);
        root.AddChild(footer);
        _preview = StudioStyle.Button("Aperçu sec", primary: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true);
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += Save;
        footer.AddChild(_save);
        footer.AddChild(StudioStyle.Label(
            "Une commande atomique ; contenu, waypoints avancés, monde vivant et autres zones préservés.",
            9, StudioStyle.Muted, wrap: true));

        ConnectInvalidation();
        ResetControls();
        SetEditingEnabled(false);
        Resized += UpdateDialogBounds;
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

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
        _session = _sourceSession;
        OpenBoundSession(cancelApproval: false);
    }

    private void OpenBoundSession(bool cancelApproval = true)
    {
        if (cancelApproval)
        {
            _session.CancelNavigationUx2NavB();
        }
        _choices = _session.NavigationAreaChoicesUx2NavB;
        _blueprintChoices = _session.SpawnBlueprintChoicesUx2NavB;
        _area.Clear();
        _area.AddItem("Choisissez explicitement une zone…");
        foreach (var choice in _choices)
        {
            _area.AddItem(choice.Label);
        }
        _area.Select(0);
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        ResetControls();
        _selection.Text = _choices.Count == 0
            ? "Aucune zone n’est disponible dans ce projet."
            : "Choisissez explicitement une zone ; aucune navigation n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetStatus("Prêt · sélection explicite requise", error: false);
        SetEditingEnabled(false);
        RenderDiff(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _scroll.ScrollVertical = 0;
        Visible = true;
        MoveToFront();
        UpdateDialogBounds();
        _area.GrabFocus();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null)
        {
            return;
        }
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        _session.CancelNavigationUx2NavB();
        Visible = false;
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        ResetControls();
        var released = ReleaseIsolatedEvidenceSession();
        RestoreSourceViewport();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceResourcesReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if ((_evidenceSession is not null || _evidenceTempRoot is not null) && !ReleaseIsolatedEvidenceSession())
        {
            return false;
        }
        if (!TryOpenIsolatedEvidenceSession())
        {
            return false;
        }
        var choiceIndex = _choices.ToList().FindIndex(item => item.IsEvidence);
        if (choiceIndex < 0)
        {
            Close();
            return false;
        }
        _area.Select(choiceIndex + 1);
        LoadChoice(choiceIndex);
        if (_draft is null || !ConfigureEvidenceThroughControls())
        {
            Close();
            return false;
        }
        var history = _session.History;
        var project = _session.Project;
        var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? [];
        var dirty = _session.IsDirty;
        var canUndo = history?.CanUndo;
        var canRedo = history?.CanRedo;
        Preview();
        _lastEvidencePreviewDryRun = project is not null && ReferenceEquals(project, _session.Project) &&
                                     ReferenceEquals(history, _session.History) &&
                                     history?.State.Revision == revision &&
                                     (history?.Journal ?? []).SequenceEqual(journal) &&
                                     _session.IsDirty == dirty && history?.CanUndo == canUndo &&
                                     history?.CanRedo == canRedo;
        _selection.Text += " · session de preuve isolée";
        _summary.Text = "PREUVE ISOLÉE · le projet source reste strictement inchangé.\n" + _summary.Text;
        SetStatus("Preuve isolée prête · la fermeture supprime le workspace temporaire", error: false);
        Callable.From(() => _scroll.EnsureControlVisible(_anchorEvidenceTarget)).CallDeferred();
        return _lastEvidencePreviewDryRun && HasExplicitSelection && CanonicalPreviewReady &&
               ShowsRealSurface && ShowsHumanDestinations && ShowsStructuredDiagnostics && ShowsHumanSurface &&
               UsesIsolatedEvidence && EvidenceSourceUntouched;
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        StudioSessionController? reopened = null;
        try
        {
            var ready = PrepareEvidence("ux2-nav-b");
            if (!ready || !UsesIsolatedEvidence || _draft is null)
            {
                return false;
            }
            var transactionSession = _session;
            var originalProject = transactionSession.Project;
            var targetId = _draft.AreaId;
            var originalArea = originalProject?.Areas.SingleOrDefault(item => item.Id == targetId);
            var dryRun = originalProject is not null && originalArea is not null && _lastEvidencePreviewDryRun &&
                         SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transactionSession.Project;
            var appliedArea = appliedProject?.Areas.SingleOrDefault(item => item.Id == targetId);
            var appliedJson = appliedArea is null ? null : AreaJsonSerializer.Serialize(appliedArea);
            var applied = dryRun && _applied && appliedArea is not null &&
                          !ReferenceEquals(originalProject, appliedProject) &&
                          ReferenceEquals(originalArea!.Navigation.Waypoints, appliedArea.Navigation.Waypoints) &&
                          ReferenceEquals(originalArea.Environment, appliedArea.Environment) &&
                          ReferenceEquals(originalArea.World, appliedArea.World);
            if (applied)
            {
                Undo();
            }
            var undone = applied && ReferenceEquals(originalProject, transactionSession.Project);
            if (undone)
            {
                Redo();
            }
            var redone = undone && ReferenceEquals(appliedProject, transactionSession.Project);
            var saved = redone && transactionSession.Save().Succeeded;
            var persisted = false;
            if (saved && transactionSession.Session is { } workspace && appliedJson is not null)
            {
                reopened = new StudioSessionController();
                if (reopened.Open(workspace.RootDirectory, targetId.ToString()).Succeeded)
                {
                    var restored = reopened.Project?.Areas.SingleOrDefault(item => item.Id == targetId);
                    persisted = restored is not null && string.Equals(appliedJson,
                        AreaJsonSerializer.Serialize(restored), StringComparison.Ordinal);
                }
            }
            var sourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            reopened?.Close();
            reopened = null;
            Close();
            _lastUiTransaction = dryRun && applied && undone && redone && saved && persisted && sourceUntouched &&
                                 EvidenceResourcesReleased && SourceSessionRestored;
            return _lastUiTransaction;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_B_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceTempRoot is not null)
            {
                Close();
            }
            _sourceSession.RestoreNavigationApprovalUx2NavB(sourceSnapshot.Approval);
        }
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx2NavBEvidenceProject();
        if (snapshot is null || evidenceProject is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-b-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            var opened = created.Succeeded && evidenceSession.Open(tempRoot).Succeeded;
            if (!opened)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavBTemporaryWorkspace(tempRoot);
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
            _lastEvidencePreviewDryRun = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.IsEvidence) && SourceMatchesSnapshot(snapshot);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_B_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = StudioSessionController.DeleteUx2NavBTemporaryWorkspace(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = released;
            _lastSourceRestored = true;
            return false;
        }
    }

    private StudioUx2NavBEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx2NavBEvidenceSourceSnapshot(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureNavigationApprovalUx2NavB());
    }

    private bool SourceMatchesSnapshot(StudioUx2NavBEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureNavigationApprovalUx2NavB() == snapshot.Approval;
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_B_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx2NavBTemporaryWorkspace(tempRoot);
        if (!rootReleased)
        {
            _evidenceTempRoot = tempRoot;
        }
        return released && rootReleased;
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

    private void SelectArea(long index)
    {
        if (_updating)
        {
            return;
        }
        var choiceIndex = (int)index - 1;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            ClearSelection();
            return;
        }
        LoadChoice(choiceIndex);
    }

    private void ClearSelection()
    {
        _draft = null;
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        _session.CancelNavigationUx2NavB();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        ResetControls();
        SetEditingEnabled(false);
        _selection.Text = "Choisissez explicitement une zone ; aucune navigation n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _summary.Text = "Sélectionnez une zone puis lancez l’aperçu sec · aucun changement appliqué.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        RenderDiff(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
        SetStatus("Prêt · sélection explicite requise", error: false);
    }

    private void LoadChoice(int choiceIndex)
    {
        var choice = _choices[choiceIndex];
        _session.CancelNavigationUx2NavB();
        _draft = _session.CreateNavigationDraftUx2NavB(choice.AreaId);
        _lastPreview = null;
        _applied = false;
        _canonicalPreviewReady = false;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        if (_draft is null)
        {
            ClearSelection();
            SetStatus("Zone indisponible · aucune mutation", error: true);
            return;
        }
        Populate(_draft);
        _selection.Text = choice.Label;
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        _summary.Text = "Navigation réelle chargée · choisissez explicitement un profil ou une ancre à éditer.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(true);
        RenderDiff(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons();
        SetStatus("Brouillon prêt · aperçu sec requis", error: false);
        BindViewportToArea(_session, choice.AreaId);
    }

    private void Populate(StudioUx2NavBDraft draft)
    {
        _updating = true;
        _cellSize.Value = draft.CellSize;
        PopulateAgentSelector(draft.SelectedAgentId);
        PopulateOffMeshSelector(draft.SelectedOffMeshId);
        PopulateDefinitionSelector(draft.SelectedDefinitionId);
        PopulateInstanceSelector(draft.SelectedInstanceId);
        _updating = false;
        PopulateSelectedAgent();
        PopulateSelectedOffMesh();
        PopulateSelectedDefinition();
        PopulateSelectedInstance();
    }

    private void PopulateAgentSelector(NavigationAgentProfileId? selected)
    {
        _agent.Clear();
        _agent.AddItem("Choisir un profil d’agent…");
        if (_draft is null)
        {
            _agent.Select(0);
            return;
        }
        foreach (var value in _draft.Agents)
        {
            _agent.AddItem(HumanTextUx2NavB(value.Name, "Agent protégé"));
        }
        var index = selected is null ? -1 : _draft.Agents.ToList().FindIndex(item => item.Id == selected);
        _agent.Select(index + 1);
    }

    private void PopulateOffMeshSelector(OffMeshNavigationLinkId? selected)
    {
        _offMesh.Clear();
        _offMesh.AddItem("Choisir une liaison…");
        if (_draft is null)
        {
            _offMesh.Select(0);
            return;
        }
        foreach (var value in _draft.OffMeshLinks)
        {
            _offMesh.AddItem(HumanTextUx2NavB(value.Name, "Liaison protégée"));
        }
        var index = selected is null ? -1 : _draft.OffMeshLinks.ToList().FindIndex(item => item.Id == selected);
        _offMesh.Select(index + 1);
    }

    private void PopulateDefinitionSelector(Guid? selected)
    {
        _definition.Clear();
        _definition.AddItem("Choisir une ancre nommée…");
        if (_draft is null)
        {
            _definition.Select(0);
            return;
        }
        foreach (var value in _draft.Definitions)
        {
            _definition.AddItem($"{HumanTextUx2NavB(value.Label, "Ancre protégée")} · {HumanTextUx2NavB(value.Slug, "slug protégé")}");
        }
        var index = selected is null ? -1 : _draft.Definitions.ToList().FindIndex(item => item.Id == selected);
        _definition.Select(index + 1);
    }

    private void PopulateInstanceSelector(Guid? selected)
    {
        _instance.Clear();
        _instance.AddItem("Choisir un placement d’ancre…");
        if (_draft is null)
        {
            _instance.Select(0);
            return;
        }
        var definitions = _draft.Definitions.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var value in _draft.Instances)
        {
            var label = definitions.TryGetValue(value.DefinitionId, out var definition)
                ? HumanTextUx2NavB(definition.Label, "Ancre protégée")
                : "Définition absente";
            _instance.AddItem($"{label} · placement {value.DisplayOrder}");
        }
        var index = selected is null ? -1 : _draft.Instances.ToList().FindIndex(item => item.Id == selected);
        _instance.Select(index + 1);
    }

    private void SelectAgent(long index)
    {
        if (_updating || _draft is null)
        {
            return;
        }
        var value = (int)index - 1;
        _draft = _draft with
        {
            SelectedAgentId = value >= 0 && value < _draft.Agents.Count ? _draft.Agents[value].Id : null,
        };
        PopulateSelectedAgent();
        InvalidatePreview();
    }

    private void SelectOffMesh(long index)
    {
        if (_updating || _draft is null)
        {
            return;
        }
        var value = (int)index - 1;
        _draft = _draft with
        {
            SelectedOffMeshId = value >= 0 && value < _draft.OffMeshLinks.Count
                ? _draft.OffMeshLinks[value].Id
                : null,
        };
        PopulateSelectedOffMesh();
        InvalidatePreview();
    }

    private void SelectDefinition(long index)
    {
        if (_updating || _draft is null)
        {
            return;
        }
        var value = (int)index - 1;
        _draft = _draft with
        {
            SelectedDefinitionId = value >= 0 && value < _draft.Definitions.Count
                ? _draft.Definitions[value].Id
                : null,
        };
        PopulateSelectedDefinition();
        InvalidatePreview();
    }

    private void SelectInstance(long index)
    {
        if (_updating || _draft is null)
        {
            return;
        }
        var value = (int)index - 1;
        _draft = _draft with
        {
            SelectedInstanceId = value >= 0 && value < _draft.Instances.Count
                ? _draft.Instances[value].Id
                : null,
        };
        PopulateSelectedInstance();
        InvalidatePreview();
    }

    private void PopulateSelectedAgent()
    {
        _updating = true;
        var draft = _draft;
        var value = draft is null
            ? null
            : draft.Agents.FirstOrDefault(item => item.Id == draft.SelectedAgentId);
        _agentName.Text = value?.Name ?? string.Empty;
        _agentRadius.Value = value?.Radius ?? .5;
        _agentHeight.Value = value?.Height ?? 1.8;
        _agentDoors.ButtonPressed = value?.CanOpenDoors ?? false;
        _agentLinks.ButtonPressed = value?.CanUseOffMeshLinks ?? false;
        _agentFlight.ButtonPressed = value?.CanFly ?? false;
        _updating = false;
        SetAgentFieldsEnabled(value is not null);
    }

    private void PopulateSelectedOffMesh()
    {
        _updating = true;
        var draft = _draft;
        var value = draft is null
            ? null
            : draft.OffMeshLinks.FirstOrDefault(item => item.Id == draft.SelectedOffMeshId);
        _offMeshName.Text = value?.Name ?? string.Empty;
        PopulateOffMeshKind(value?.Kind);
        _offMeshStartX.Value = value?.Start.X ?? 0;
        _offMeshStartY.Value = value?.Start.Y ?? 0;
        _offMeshStartZ.Value = value?.Start.Z ?? 0;
        _offMeshEndX.Value = value?.End.X ?? 1;
        _offMeshEndY.Value = value?.End.Y ?? 0;
        _offMeshEndZ.Value = value?.End.Z ?? 0;
        _offMeshRadius.Value = value?.MaximumAgentRadius ?? .5;
        _offMeshBidirectional.ButtonPressed = value?.Bidirectional ?? true;
        _updating = false;
        SetOffMeshFieldsEnabled(value is not null);
    }

    private void PopulateSelectedDefinition()
    {
        _updating = true;
        var draft = _draft;
        var value = draft is null
            ? null
            : draft.Definitions.FirstOrDefault(item => item.Id == draft.SelectedDefinitionId);
        _definitionLabel.Text = value?.Label ?? string.Empty;
        _definitionSlug.Text = value?.Slug ?? string.Empty;
        PopulateDefinitionKind(value?.Kind);
        PopulateDefinitionScope(value?.Scope);
        _definitionTags.Text = value is null ? string.Empty : string.Join(", ", value.Tags);
        _definitionRequired.ButtonPressed = value?.RequiredAtRuntime ?? false;
        PopulateBlueprintChoices(value);
        _updating = false;
        SetDefinitionFieldsEnabled(value is not null);
    }

    private void PopulateBlueprintChoices(StudioUx2NavBAnchorDefinitionDraft? definition)
    {
        _spawnBlueprint.Clear();
        _spawnBlueprint.AddItem("Choisir un blueprint de départ…");
        foreach (var choice in _blueprintChoices)
        {
            _spawnBlueprint.AddItem(choice.Label);
        }
        var selected = definition?.BlueprintId is not { } blueprintId
            ? -1
            : _blueprintChoices.ToList().FindIndex(item => item.BlueprintId == blueprintId);
        if (definition?.BlueprintId is not null && selected < 0)
        {
            _spawnBlueprint.AddItem("Blueprint hérité protégé · remplacez-le explicitement si nécessaire");
            selected = _blueprintChoices.Count;
        }
        _spawnBlueprint.Select(selected + 1);
    }

    private void PopulateSelectedInstance()
    {
        _updating = true;
        var draft = _draft;
        var value = draft is null
            ? null
            : draft.Instances.FirstOrDefault(item => item.Id == draft.SelectedInstanceId);
        var definition = value is null
            ? null
            : draft!.Definitions.FirstOrDefault(item => item.Id == value.DefinitionId);
        _positionX.Value = value?.Position.X ?? 0;
        _positionY.Value = value?.Position.Y ?? 0;
        _positionZ.Value = value?.Position.Z ?? 0;
        _displayOrder.Value = value?.DisplayOrder ?? 1;
        _spawnProfile.Text = value?.Spawn?.Profile ?? string.Empty;
        _spawnClearanceX.Value = value?.Spawn?.Clearance.X ?? 1.2;
        _spawnClearanceY.Value = value?.Spawn?.Clearance.Y ?? 2;
        _spawnClearanceZ.Value = value?.Spawn?.Clearance.Z ?? 1.2;
        _spawnPriority.Value = value?.Spawn?.Priority ?? 50;
        _spawnTeam.Text = value?.Spawn?.Team ?? "heroes";
        _spawnOrientation.Value = value?.Spawn?.OrientationDegrees ?? 0;
        _spawnNavigation.ButtonPressed = value?.Spawn?.RequireNavigation ?? true;
        PopulateSpawnFallbackChoices(value);
        if (value is null)
        {
            _destinationChoices = [];
            _portalDestination.Clear();
            _portalDestination.AddItem("Choisir explicitement une destination…");
            _portalDestination.Select(0);
        }
        else
        {
            PopulateDestinationChoices(value.Portal);
        }
        PopulatePortalOrientation(value?.Portal?.Orientation);
        _portalYaw.Value = value?.Portal?.FixedYawDegrees ?? 0;
        PopulatePortalVelocity(value?.Portal?.Velocity);
        _portalPreload.ButtonPressed = value?.Portal?.Preload ?? false;
        _spawnFields.Visible = definition?.Kind == AnchorKind.Spawn;
        _portalFields.Visible = definition?.Kind == AnchorKind.TransitionDestination;
        _updating = false;
        SetInstanceFieldsEnabled(value is not null);
    }

    private void PopulateDestinationChoices(StudioUx2NavBPortalDraft? portal)
    {
        _destinationChoices = _session.NavigationDestinationChoicesUx2NavB(portal);
        _portalDestination.Clear();
        _portalDestination.AddItem("Choisir explicitement une destination…");
        foreach (var choice in _destinationChoices)
        {
            _portalDestination.AddItem(choice.Label);
        }
        var selected = portal is null ? -1 : _destinationChoices.ToList().FindIndex(item =>
            item.Id == portal.TargetAnchorInstanceId && item.AreaId == portal.TargetAreaId);
        _portalDestination.Select(selected + 1);
    }

    private void PopulateOffMeshKind(NavigationLinkKind? value)
    {
        _offMeshKind.Clear();
        foreach (var label in new[] { "Saut", "Porte", "Vol" })
        {
            _offMeshKind.AddItem(label);
        }
        if (value is { } kind && Enum.IsDefined(kind))
        {
            _offMeshKind.Select((int)kind);
            return;
        }
        if (value is not null)
        {
            _offMeshKind.AddItem("Type hérité protégé · choisissez un remplacement explicite");
            _offMeshKind.Select(3);
            return;
        }
        _offMeshKind.Select(0);
    }

    private void PopulateDefinitionKind(AnchorKind? value)
    {
        _definitionKind.Clear();
        foreach (var label in new[] { "Waypoint", "Point de départ", "Destination de transition" })
        {
            _definitionKind.AddItem(label);
        }
        if (value is { } kind && Enum.IsDefined(kind))
        {
            _definitionKind.Select((int)kind);
            return;
        }
        if (value is not null)
        {
            _definitionKind.AddItem("Rôle hérité protégé · choisissez un remplacement explicite");
            _definitionKind.Select(3);
            return;
        }
        _definitionKind.Select(0);
    }

    private void PopulateDefinitionScope(AnchorScope? value)
    {
        _definitionScope.Clear();
        foreach (var label in new[] { "Zone", "Module", "Monde" })
        {
            _definitionScope.AddItem(label);
        }
        if (value is { } scope && Enum.IsDefined(scope))
        {
            _definitionScope.Select((int)scope);
            return;
        }
        if (value is not null)
        {
            _definitionScope.AddItem("Portée héritée protégée · choisissez un remplacement explicite");
            _definitionScope.Select(3);
            return;
        }
        _definitionScope.Select(0);
    }

    private void PopulatePortalOrientation(PortalOrientationPolicy? value)
    {
        _portalOrientation.Clear();
        foreach (var label in new[] { "Conserver", "Rotation de l’ancre", "Angle fixe" })
        {
            _portalOrientation.AddItem(label);
        }
        if (value is { } policy && Enum.IsDefined(policy))
        {
            _portalOrientation.Select((int)policy);
            return;
        }
        if (value is not null)
        {
            _portalOrientation.AddItem("Orientation héritée protégée · choisissez un remplacement explicite");
            _portalOrientation.Select(3);
            return;
        }
        _portalOrientation.Select(0);
    }

    private void PopulatePortalVelocity(PortalVelocityPolicy? value)
    {
        _portalVelocity.Clear();
        foreach (var label in new[] { "Conserver", "Réinitialiser", "Rediriger vers l’avant" })
        {
            _portalVelocity.AddItem(label);
        }
        if (value is { } policy && Enum.IsDefined(policy))
        {
            _portalVelocity.Select((int)policy);
            return;
        }
        if (value is not null)
        {
            _portalVelocity.AddItem("Vitesse héritée protégée · choisissez un remplacement explicite");
            _portalVelocity.Select(3);
            return;
        }
        _portalVelocity.Select(0);
    }

    private void PopulateSpawnFallbackChoices(StudioUx2NavBAnchorInstanceDraft? current)
    {
        _spawnFallback.Clear();
        _spawnFallback.AddItem("Aucun repli");
        var ids = new List<Guid?> { null };
        if (_draft is not { } draft)
        {
            _spawnFallbackIds = ids;
            _spawnFallback.Select(0);
            return;
        }
        var definitions = draft.Definitions.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var candidate in draft.Instances.Where(item => item.Id != current?.Id && item.Spawn is not null))
        {
            var label = definitions.TryGetValue(candidate.DefinitionId, out var definition)
                ? HumanTextUx2NavB(definition.Label, "Départ protégé")
                : "Départ protégé";
            _spawnFallback.AddItem($"{label} · placement {candidate.DisplayOrder}");
            ids.Add(candidate.Id);
        }
        var selected = current?.Spawn?.FallbackInstanceId is not { } fallback
            ? 0
            : ids.FindIndex(item => item == fallback);
        if (current?.Spawn?.FallbackInstanceId is { } protectedFallback && selected < 0)
        {
            _spawnFallback.AddItem("Repli hérité protégé · remplacez-le explicitement si nécessaire");
            ids.Add(protectedFallback);
            selected = ids.Count - 1;
        }
        _spawnFallbackIds = ids;
        _spawnFallback.Select(Math.Max(0, selected));
    }

    private void AddAgent()
    {
        if (_draft is null)
        {
            return;
        }
        var index = _draft.Agents.Count + 1;
        var added = new StudioUx2NavBAgentDraft(NavigationAgentProfileId.From(Guid.NewGuid()),
            NavigationAgentProfile.CurrentSchemaVersion, "studio:ux2-nav-b:navigation-foundation",
            $"Agent {index}", .5f, 1.8f, true, true, false);
        _draft = _draft with { Agents = [.. _draft.Agents, added], SelectedAgentId = added.Id };
        _updating = true;
        PopulateAgentSelector(added.Id);
        _updating = false;
        PopulateSelectedAgent();
        InvalidatePreview();
    }

    private void RemoveAgent()
    {
        var draft = _draft;
        if (draft?.SelectedAgentId is not { } id || draft.Agents.Count <= 1)
        {
            return;
        }
        _draft = draft with
        {
            Agents = draft.Agents.Where(item => item.Id != id).ToArray(),
            SelectedAgentId = null,
        };
        _updating = true;
        PopulateAgentSelector(null);
        _updating = false;
        PopulateSelectedAgent();
        InvalidatePreview();
    }

    private void AddOffMesh()
    {
        if (_draft is null)
        {
            return;
        }
        var index = _draft.OffMeshLinks.Count + 1;
        var added = new StudioUx2NavBOffMeshDraft(OffMeshNavigationLinkId.From(Guid.NewGuid()),
            OffMeshNavigationLink.CurrentSchemaVersion, "studio:ux2-nav-b:navigation-foundation",
            $"Liaison {index}", NavigationLinkKind.Jump, new(0, 0, 0), new(1, 0, 0), true, .5f);
        _draft = _draft with { OffMeshLinks = [.. _draft.OffMeshLinks, added], SelectedOffMeshId = added.Id };
        _updating = true;
        PopulateOffMeshSelector(added.Id);
        _updating = false;
        PopulateSelectedOffMesh();
        InvalidatePreview();
    }

    private void RemoveOffMesh()
    {
        var draft = _draft;
        if (draft?.SelectedOffMeshId is not { } id)
        {
            return;
        }
        _draft = draft with
        {
            OffMeshLinks = draft.OffMeshLinks.Where(item => item.Id != id).ToArray(),
            SelectedOffMeshId = null,
        };
        _updating = true;
        PopulateOffMeshSelector(null);
        _updating = false;
        PopulateSelectedOffMesh();
        InvalidatePreview();
    }

    private void AddAnchor(AnchorKind kind)
    {
        if (_draft is null)
        {
            return;
        }
        var ordinal = _draft.Definitions.Count + 1;
        var title = kind switch
        {
            AnchorKind.Spawn => "Nouveau point de départ",
            AnchorKind.TransitionDestination => "Nouvelle destination",
            _ => "Nouveau waypoint",
        };
        var slugBase = kind switch
        {
            AnchorKind.Spawn => "point-depart",
            AnchorKind.TransitionDestination => "destination-transition",
            _ => "waypoint",
        };
        var slug = UniqueSlugUx2NavB(_draft.Definitions.Select(item => item.Slug), $"{slugBase}-{ordinal}");
        var definitionId = Guid.NewGuid();
        var definition = new StudioUx2NavBAnchorDefinitionDraft(definitionId,
            AnchorDefinition.CurrentSchemaVersion, "studio:ux2-nav-b:navigation-foundation", slug,
            new LocalizedText($"navigation.{slug.Replace('-', '.')}", title,
                provenance: "studio:ux2-nav-b:navigation-foundation"), title, kind, AnchorScope.Area,
            ["navigation"], null, null, true);
        var instanceId = Guid.NewGuid();
        var order = _draft.Instances.Count == 0 ? 1 : _draft.Instances.Max(item => item.DisplayOrder) + 1;
        var spawn = kind == AnchorKind.Spawn
            ? new StudioUx2NavBSpawnDraft("depart", new(1.2f, 2, 1.2f), 50, "heroes", 0, null, true)
            : null;
        var instance = new StudioUx2NavBAnchorInstanceDraft(instanceId, AnchorInstance.CurrentSchemaVersion,
            "studio:ux2-nav-b:navigation-foundation", definitionId,
            new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One),
            Vector3Value.Zero, order, EntityPropertyOverrides.Empty, spawn, null);
        _draft = _draft with
        {
            Definitions = [.. _draft.Definitions, definition],
            Instances = [.. _draft.Instances, instance],
            SelectedDefinitionId = definitionId,
            SelectedInstanceId = instanceId,
        };
        _updating = true;
        PopulateDefinitionSelector(definitionId);
        PopulateInstanceSelector(instanceId);
        _updating = false;
        PopulateSelectedDefinition();
        PopulateSelectedInstance();
        InvalidatePreview();
    }

    private void RemoveDefinition()
    {
        var draft = _draft;
        if (draft?.SelectedDefinitionId is not { } id)
        {
            return;
        }
        var removedInstances = draft.Instances.Where(item => item.DefinitionId == id).Select(item => item.Id)
            .ToHashSet();
        var selectedInstanceId = draft.SelectedInstanceId is { } selected && removedInstances.Contains(selected)
            ? null
            : draft.SelectedInstanceId;
        _draft = draft with
        {
            Definitions = draft.Definitions.Where(item => item.Id != id).ToArray(),
            Instances = draft.Instances.Where(item => item.DefinitionId != id).ToArray(),
            SelectedDefinitionId = null,
            SelectedInstanceId = selectedInstanceId,
        };
        _updating = true;
        PopulateDefinitionSelector(null);
        PopulateInstanceSelector(selectedInstanceId);
        _updating = false;
        PopulateSelectedDefinition();
        PopulateSelectedInstance();
        InvalidatePreview();
    }

    private void AddInstance()
    {
        var draft = _draft;
        if (draft?.SelectedDefinitionId is not { } definitionId)
        {
            return;
        }
        var definition = draft.Definitions.FirstOrDefault(item => item.Id == definitionId);
        if (definition is null)
        {
            return;
        }
        var instanceId = Guid.NewGuid();
        var order = draft.Instances.Count == 0 ? 1 : draft.Instances.Max(item => item.DisplayOrder) + 1;
        var spawn = definition.Kind == AnchorKind.Spawn
            ? new StudioUx2NavBSpawnDraft("depart", new(1.2f, 2, 1.2f), 50, "heroes", 0, null, true)
            : null;
        var instance = new StudioUx2NavBAnchorInstanceDraft(instanceId, AnchorInstance.CurrentSchemaVersion,
            "studio:ux2-nav-b:navigation-foundation", definitionId,
            new TransformValue(Vector3Value.Zero, QuaternionValue.Identity, Vector3Value.One),
            Vector3Value.Zero, order, EntityPropertyOverrides.Empty, spawn, null);
        _draft = draft with
        {
            Instances = [.. draft.Instances, instance],
            SelectedInstanceId = instanceId,
        };
        _updating = true;
        PopulateInstanceSelector(instanceId);
        _updating = false;
        PopulateSelectedInstance();
        InvalidatePreview();
    }

    private void RemoveInstance()
    {
        var draft = _draft;
        if (draft?.SelectedInstanceId is not { } id)
        {
            return;
        }
        _draft = draft with
        {
            Instances = draft.Instances.Where(item => item.Id != id).ToArray(),
            SelectedInstanceId = null,
        };
        _updating = true;
        PopulateInstanceSelector(null);
        _updating = false;
        PopulateSelectedInstance();
        InvalidatePreview();
    }

    private void UpdateSurface()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _draft = _draft with { CellSize = (float)_cellSize.Value };
        InvalidatePreview();
    }

    private void UpdateSelectedAgent()
    {
        var draft = _draft;
        if (_updating || draft?.SelectedAgentId is not { } id)
        {
            return;
        }
        _draft = draft with
        {
            Agents = draft.Agents.Select(item => item.Id == id
                ? item with
                {
                    Name = _agentName.Text,
                    Radius = (float)_agentRadius.Value,
                    Height = (float)_agentHeight.Value,
                    CanOpenDoors = _agentDoors.ButtonPressed,
                    CanUseOffMeshLinks = _agentLinks.ButtonPressed,
                    CanFly = _agentFlight.ButtonPressed,
                }
                : item).ToArray(),
        };
        InvalidatePreview();
    }

    private void UpdateSelectedOffMesh()
    {
        var draft = _draft;
        if (_updating || draft?.SelectedOffMeshId is not { } id)
        {
            return;
        }
        _draft = draft with
        {
            OffMeshLinks = draft.OffMeshLinks.Select(item => item.Id == id
                ? item with
                {
                    Name = _offMeshName.Text,
                    Kind = _offMeshKind.Selected is >= 0 and <= 2
                        ? (NavigationLinkKind)_offMeshKind.Selected
                        : item.Kind,
                    Start = new((float)_offMeshStartX.Value, (float)_offMeshStartY.Value,
                        (float)_offMeshStartZ.Value),
                    End = new((float)_offMeshEndX.Value, (float)_offMeshEndY.Value,
                        (float)_offMeshEndZ.Value),
                    MaximumAgentRadius = (float)_offMeshRadius.Value,
                    Bidirectional = _offMeshBidirectional.ButtonPressed,
                }
                : item).ToArray(),
        };
        InvalidatePreview();
    }

    private void UpdateSelectedDefinition()
    {
        var draft = _draft;
        if (_updating || draft?.SelectedDefinitionId is not { } id)
        {
            return;
        }
        var previous = draft.Definitions.First(item => item.Id == id);
        BlueprintId? blueprint = null;
        if (_spawnBlueprint.Selected > 0)
        {
            var index = _spawnBlueprint.Selected - 1;
            blueprint = index < _blueprintChoices.Count
                ? _blueprintChoices[index].BlueprintId
                : previous.BlueprintId;
        }
        var kind = _definitionKind.Selected is >= 0 and <= 2
            ? (AnchorKind)_definitionKind.Selected
            : previous.Kind;
        var scope = _definitionScope.Selected is >= 0 and <= 2
            ? (AnchorScope)_definitionScope.Selected
            : previous.Scope;
        var renderedTags = string.Join(", ", previous.Tags);
        var tags = string.Equals(_definitionTags.Text, renderedTags, StringComparison.Ordinal)
            ? previous.Tags
            : ParseTagsUx2NavB(_definitionTags.Text);
        _draft = draft with
        {
            Definitions = draft.Definitions.Select(item => item.Id == id
                ? item with
                {
                    Label = _definitionLabel.Text,
                    Slug = _definitionSlug.Text,
                    Kind = kind,
                    Scope = scope,
                    Tags = tags,
                    BlueprintId = kind == AnchorKind.Spawn ? blueprint : item.BlueprintId,
                    RequiredAtRuntime = _definitionRequired.ButtonPressed,
                }
                : item).ToArray(),
            Instances = draft.Instances.Select(item => item.DefinitionId != id
                ? item
                : kind switch
                {
                    AnchorKind.Spawn => item with
                    {
                        Spawn = item.Spawn ?? new StudioUx2NavBSpawnDraft("depart", new(1.2f, 2, 1.2f), 50,
                            "heroes", 0, null, true),
                        Portal = null,
                    },
                    AnchorKind.TransitionDestination => item with { Spawn = null },
                    _ => item with { Spawn = null, Portal = null },
                }).ToArray(),
        };
        if (kind != previous.Kind)
        {
            _updating = true;
            PopulateInstanceSelector(draft.SelectedInstanceId);
            _updating = false;
            PopulateSelectedInstance();
        }
        SetDefinitionFieldsEnabled(true);
        InvalidatePreview();
    }

    private void UpdateSelectedInstance()
    {
        var draft = _draft;
        if (_updating || draft?.SelectedInstanceId is not { } id)
        {
            return;
        }
        var current = draft.Instances.First(item => item.Id == id);
        var definition = draft.Definitions.FirstOrDefault(item => item.Id == current.DefinitionId);
        var spawn = current.Spawn;
        var portal = current.Portal;
        if (definition?.Kind == AnchorKind.Spawn)
        {
            var fallbackIndex = _spawnFallback.Selected;
            var fallback = fallbackIndex >= 0 && fallbackIndex < _spawnFallbackIds.Count
                ? _spawnFallbackIds[fallbackIndex]
                : current.Spawn?.FallbackInstanceId;
            spawn = new StudioUx2NavBSpawnDraft(_spawnProfile.Text,
                new((float)_spawnClearanceX.Value, (float)_spawnClearanceY.Value,
                    (float)_spawnClearanceZ.Value),
                (int)_spawnPriority.Value, _spawnTeam.Text, (float)_spawnOrientation.Value,
                fallback, _spawnNavigation.ButtonPressed);
        }
        else if (definition?.Kind == AnchorKind.TransitionDestination)
        {
            var selected = _portalDestination.Selected - 1;
            if (selected >= 0 && selected < _destinationChoices.Count &&
                _destinationChoices[selected].AreaId is { } targetArea)
            {
                var destination = _destinationChoices[selected];
                portal = new StudioUx2NavBPortalDraft(targetArea, destination.Id,
                    _portalOrientation.Selected is >= 0 and <= 2
                        ? (PortalOrientationPolicy)_portalOrientation.Selected
                        : current.Portal?.Orientation ?? PortalOrientationPolicy.Preserve,
                    (float)_portalYaw.Value,
                    _portalVelocity.Selected is >= 0 and <= 2
                        ? (PortalVelocityPolicy)_portalVelocity.Selected
                        : current.Portal?.Velocity ?? PortalVelocityPolicy.Preserve,
                    _portalPreload.ButtonPressed);
            }
            else
            {
                portal = current.Portal;
            }
        }
        _draft = draft with
        {
            Instances = draft.Instances.Select(item => item.Id == id
                ? item with
                {
                    Position = new((float)_positionX.Value, (float)_positionY.Value,
                        (float)_positionZ.Value),
                    DisplayOrder = (int)_displayOrder.Value,
                    Spawn = spawn,
                    Portal = portal,
                }
                : item).ToArray(),
        };
        InvalidatePreview();
    }

    private bool ConfigureEvidenceThroughControls()
    {
        if (_draft is null || _draft.Agents.Count == 0 || _draft.OffMeshLinks.Count == 0)
        {
            return false;
        }
        _agent.Select(1);
        SelectAgent(1);
        _agentRadius.Value += .05;
        UpdateSelectedAgent();
        _offMesh.Select(1);
        SelectOffMesh(1);
        _offMeshRadius.Value = Math.Min(8, _offMeshRadius.Value + .05);
        UpdateSelectedOffMesh();
        var evidenceDraft = _draft;
        var spawnInstance = evidenceDraft.Instances.FirstOrDefault(instance =>
            evidenceDraft.Definitions.Any(definition => definition.Id == instance.DefinitionId &&
                                                        definition.Kind == AnchorKind.Spawn));
        if (spawnInstance is null)
        {
            return false;
        }
        var spawnDefinitionIndex = evidenceDraft.Definitions.ToList().FindIndex(item =>
            item.Id == spawnInstance.DefinitionId);
        var spawnInstanceIndex = evidenceDraft.Instances.ToList().FindIndex(item => item.Id == spawnInstance.Id);
        _definition.Select(spawnDefinitionIndex + 1);
        SelectDefinition(spawnDefinitionIndex + 1);
        _instance.Select(spawnInstanceIndex + 1);
        SelectInstance(spawnInstanceIndex + 1);
        _spawnPriority.Value = 95;
        UpdateSelectedInstance();
        evidenceDraft = _draft!;
        var transitionInstance = evidenceDraft.Instances.FirstOrDefault(instance =>
            evidenceDraft.Definitions.Any(definition => definition.Id == instance.DefinitionId &&
                                                        definition.Kind == AnchorKind.TransitionDestination));
        if (transitionInstance is null)
        {
            return false;
        }
        var transitionDefinitionIndex = evidenceDraft.Definitions.ToList().FindIndex(item =>
            item.Id == transitionInstance.DefinitionId);
        var transitionInstanceIndex = evidenceDraft.Instances.ToList().FindIndex(item => item.Id == transitionInstance.Id);
        _definition.Select(transitionDefinitionIndex + 1);
        SelectDefinition(transitionDefinitionIndex + 1);
        _instance.Select(transitionInstanceIndex + 1);
        SelectInstance(transitionInstanceIndex + 1);
        _portalPreload.ButtonPressed = false;
        UpdateSelectedInstance();
        AddAnchor(AnchorKind.Waypoint);
        _definitionLabel.Text = "Belvédère du ruisseau";
        _definitionSlug.Text = "belvedere-ruisseau-evidence";
        UpdateSelectedDefinition();
        _positionX.Value = 4;
        _positionZ.Value = -3;
        UpdateSelectedInstance();
        evidenceDraft = _draft!;
        transitionDefinitionIndex = evidenceDraft.Definitions.ToList().FindIndex(item =>
            item.Id == transitionInstance.DefinitionId);
        transitionInstanceIndex = evidenceDraft.Instances.ToList().FindIndex(item => item.Id == transitionInstance.Id);
        _definition.Select(transitionDefinitionIndex + 1);
        SelectDefinition(transitionDefinitionIndex + 1);
        _instance.Select(transitionInstanceIndex + 1);
        SelectInstance(transitionInstanceIndex + 1);
        var finalDraft = _draft!;
        return finalDraft.SelectedAgentId is not null && finalDraft.SelectedOffMeshId is not null &&
               finalDraft.SelectedDefinitionId is not null && finalDraft.SelectedInstanceId is not null;
    }

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Choisissez explicitement une zone avant l’aperçu.", error: true);
            return;
        }
        var preview = _session.PreviewNavigationUx2NavB(_draft);
        _lastPreview = preview;
        _canonicalPreviewReady = preview.Succeeded && preview.CanApply;
        _apply.Disabled = !_canonicalPreviewReady;
        RenderDiff(preview);
        RenderDiagnostics(preview.Diagnostics);
        if (!preview.Succeeded)
        {
            _worldViewport.ClearNavigationOverlayUx2NavB();
            _summary.Text = preview.Message;
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetStatus("Aperçu refusé · corrigez les diagnostics", error: true);
            return;
        }
        _worldViewport.ShowNavigationOverlayUx2NavB(preview.Overlay);
        _summary.Text = preview.Summary + "\n" + preview.Message;
        _summary.AddThemeColorOverride("font_color", preview.CanApply ? StudioStyle.Success : StudioStyle.Muted);
        SetStatus(preview.CanApply ? "Aperçu sec approuvé · application disponible" :
            "Aperçu sec valide · aucun changement à appliquer", error: false);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedNavigationUx2NavB();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            _canonicalPreviewReady = false;
            return;
        }
        _applied = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        SetEditingEnabled(false);
        UpdateTransactionButtons();
        _summary.Text = "Appliqué · navigation et ancres de la zone choisie seulement. Ctrl+Z reste disponible.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        if (ReferenceEquals(_session, _sourceSession))
        {
            NavigationChanged?.Invoke();
            if (_draft is { } draft)
            {
                BindViewportToArea(_session, draft.AreaId);
                if (_lastPreview is { Succeeded: true } currentPreview)
                {
                    _worldViewport.ShowNavigationOverlayUx2NavB(currentPreview.Overlay);
                }
            }
        }
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _applied = false;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        UpdateTransactionButtons();
        _summary.Text = "Annulé · la navigation précédente est restaurée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Accent);
        if (ReferenceEquals(_session, _sourceSession))
        {
            NavigationChanged?.Invoke();
            if (_draft is { } draft)
            {
                BindViewportToArea(_session, draft.AreaId);
            }
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _applied = true;
        if (_lastPreview is { Succeeded: true } preview)
        {
            _worldViewport.ShowNavigationOverlayUx2NavB(preview.Overlay);
        }
        UpdateTransactionButtons();
        _summary.Text = "Rétabli · la navigation approuvée est de nouveau présente.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        if (ReferenceEquals(_session, _sourceSession))
        {
            NavigationChanged?.Invoke();
            if (_draft is { } draft)
            {
                BindViewportToArea(_session, draft.AreaId);
                if (_lastPreview is { Succeeded: true } reboundPreview)
                {
                    _worldViewport.ShowNavigationOverlayUx2NavB(reboundPreview.Overlay);
                }
            }
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _save.Disabled = true;
            _summary.Text += "\nProjet enregistré · la réouverture relira la même navigation canonique.";
        }
    }

    private void ConnectInvalidation()
    {
        _cellSize.ValueChanged += _ => UpdateSurface();
        _agentName.TextChanged += _ => UpdateSelectedAgent();
        _agentRadius.ValueChanged += _ => UpdateSelectedAgent();
        _agentHeight.ValueChanged += _ => UpdateSelectedAgent();
        _agentDoors.Toggled += _ => UpdateSelectedAgent();
        _agentLinks.Toggled += _ => UpdateSelectedAgent();
        _agentFlight.Toggled += _ => UpdateSelectedAgent();
        _offMeshName.TextChanged += _ => UpdateSelectedOffMesh();
        _offMeshKind.ItemSelected += _ => UpdateSelectedOffMesh();
        _offMeshStartX.ValueChanged += _ => UpdateSelectedOffMesh();
        _offMeshStartY.ValueChanged += _ => UpdateSelectedOffMesh();
        _offMeshStartZ.ValueChanged += _ => UpdateSelectedOffMesh();
        _offMeshEndX.ValueChanged += _ => UpdateSelectedOffMesh();
        _offMeshEndY.ValueChanged += _ => UpdateSelectedOffMesh();
        _offMeshEndZ.ValueChanged += _ => UpdateSelectedOffMesh();
        _offMeshRadius.ValueChanged += _ => UpdateSelectedOffMesh();
        _offMeshBidirectional.Toggled += _ => UpdateSelectedOffMesh();
        _definitionLabel.TextChanged += _ => UpdateSelectedDefinition();
        _definitionSlug.TextChanged += _ => UpdateSelectedDefinition();
        _definitionKind.ItemSelected += _ => UpdateSelectedDefinition();
        _definitionScope.ItemSelected += _ => UpdateSelectedDefinition();
        _definitionTags.TextChanged += _ => UpdateSelectedDefinition();
        _spawnBlueprint.ItemSelected += _ => UpdateSelectedDefinition();
        _definitionRequired.Toggled += _ => UpdateSelectedDefinition();
        _positionX.ValueChanged += _ => UpdateSelectedInstance();
        _positionY.ValueChanged += _ => UpdateSelectedInstance();
        _positionZ.ValueChanged += _ => UpdateSelectedInstance();
        _displayOrder.ValueChanged += _ => UpdateSelectedInstance();
        _spawnProfile.TextChanged += _ => UpdateSelectedInstance();
        _spawnClearanceX.ValueChanged += _ => UpdateSelectedInstance();
        _spawnClearanceY.ValueChanged += _ => UpdateSelectedInstance();
        _spawnClearanceZ.ValueChanged += _ => UpdateSelectedInstance();
        _spawnPriority.ValueChanged += _ => UpdateSelectedInstance();
        _spawnTeam.TextChanged += _ => UpdateSelectedInstance();
        _spawnOrientation.ValueChanged += _ => UpdateSelectedInstance();
        _spawnNavigation.Toggled += _ => UpdateSelectedInstance();
        _spawnFallback.ItemSelected += _ => UpdateSelectedInstance();
        _portalDestination.ItemSelected += _ => UpdateSelectedInstance();
        _portalOrientation.ItemSelected += _ => UpdateSelectedInstance();
        _portalYaw.ValueChanged += _ => UpdateSelectedInstance();
        _portalVelocity.ItemSelected += _ => UpdateSelectedInstance();
        _portalPreload.Toggled += _ => UpdateSelectedInstance();
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        _session.CancelNavigationUx2NavB();
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _applied = false;
        _apply.Disabled = true;
        _save.Disabled = true;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        RenderDiff(null);
        RenderDiagnostics([]);
        UpdateTransactionButtons(keepApplyState: true);
    }

    private void ResetControls()
    {
        _updating = true;
        _cellSize.Value = 1;
        PopulateAgentSelector(null);
        PopulateOffMeshSelector(null);
        PopulateDefinitionSelector(null);
        PopulateInstanceSelector(null);
        _updating = false;
        PopulateSelectedAgent();
        PopulateSelectedOffMesh();
        PopulateSelectedDefinition();
        PopulateSelectedInstance();
        RenderDiff(null);
        RenderDiagnostics([]);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _cellSize.Editable = enabled;
        _agent.Disabled = !enabled;
        _addAgent.Disabled = !enabled;
        _offMesh.Disabled = !enabled;
        _addOffMesh.Disabled = !enabled;
        _definition.Disabled = !enabled;
        _addWaypoint.Disabled = !enabled;
        _addSpawn.Disabled = !enabled;
        _addTransition.Disabled = !enabled;
        _instance.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled)
        {
            SetAgentFieldsEnabled(false);
            SetOffMeshFieldsEnabled(false);
            SetDefinitionFieldsEnabled(false);
            SetInstanceFieldsEnabled(false);
        }
        else
        {
            SetAgentFieldsEnabled(_draft?.SelectedAgentId is not null);
            SetOffMeshFieldsEnabled(_draft?.SelectedOffMeshId is not null);
            SetDefinitionFieldsEnabled(_draft?.SelectedDefinitionId is not null);
            SetInstanceFieldsEnabled(_draft?.SelectedInstanceId is not null);
        }
    }

    private void SetAgentFieldsEnabled(bool enabled)
    {
        _agentName.Editable = enabled;
        _agentRadius.Editable = enabled;
        _agentHeight.Editable = enabled;
        _agentDoors.Disabled = !enabled;
        _agentLinks.Disabled = !enabled;
        _agentFlight.Disabled = !enabled;
        _removeAgent.Disabled = !enabled || _draft?.Agents.Count <= 1;
    }

    private void SetOffMeshFieldsEnabled(bool enabled)
    {
        _offMeshName.Editable = enabled;
        _offMeshKind.Disabled = !enabled;
        _offMeshStartX.Editable = enabled;
        _offMeshStartY.Editable = enabled;
        _offMeshStartZ.Editable = enabled;
        _offMeshEndX.Editable = enabled;
        _offMeshEndY.Editable = enabled;
        _offMeshEndZ.Editable = enabled;
        _offMeshRadius.Editable = enabled;
        _offMeshBidirectional.Disabled = !enabled;
        _removeOffMesh.Disabled = !enabled;
    }

    private void SetDefinitionFieldsEnabled(bool enabled)
    {
        _definitionLabel.Editable = enabled;
        _definitionSlug.Editable = enabled;
        _definitionKind.Disabled = !enabled;
        _definitionScope.Disabled = !enabled;
        _definitionTags.Editable = enabled;
        _spawnBlueprint.Disabled = !enabled || _definitionKind.Selected != (int)AnchorKind.Spawn;
        _definitionRequired.Disabled = !enabled;
        _removeDefinition.Disabled = !enabled;
        _addInstance.Disabled = !enabled;
    }

    private void SetInstanceFieldsEnabled(bool enabled)
    {
        _positionX.Editable = enabled;
        _positionY.Editable = enabled;
        _positionZ.Editable = enabled;
        _displayOrder.Editable = enabled;
        _spawnProfile.Editable = enabled;
        _spawnClearanceX.Editable = enabled;
        _spawnClearanceY.Editable = enabled;
        _spawnClearanceZ.Editable = enabled;
        _spawnPriority.Editable = enabled;
        _spawnTeam.Editable = enabled;
        _spawnOrientation.Editable = enabled;
        _spawnNavigation.Disabled = !enabled;
        _spawnFallback.Disabled = !enabled;
        _portalDestination.Disabled = !enabled;
        _portalOrientation.Disabled = !enabled;
        _portalYaw.Editable = enabled;
        _portalVelocity.Disabled = !enabled;
        _portalPreload.Disabled = !enabled;
        _removeInstance.Disabled = !enabled;
    }

    private void UpdateTransactionButtons(bool keepApplyState = false)
    {
        var history = _session.History;
        if (!keepApplyState)
        {
            _apply.Disabled = true;
        }
        _undo.Disabled = !_applied || history?.CanUndo != true;
        _redo.Disabled = _applied || history?.CanRedo != true;
        _save.Disabled = !_applied || !_session.IsDirty;
    }

    private void RenderDiff(StudioUx2NavBPreview? preview)
    {
        foreach (var child in _diff.GetChildren())
        {
            child.QueueFree();
        }
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Aucun aperçu frais.", 9, StudioStyle.Muted, wrap: true));
            return;
        }
        if (preview.Diff.Count == 0)
        {
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 9, StudioStyle.Muted, wrap: true));
            return;
        }
        foreach (var entry in preview.Diff)
        {
            _diff.AddChild(StudioStyle.Label("• " + entry, 9, StudioStyle.Text, wrap: true));
        }
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx2NavBDiagnostic> diagnostics)
    {
        foreach (var child in _diagnostics.GetChildren())
        {
            child.QueueFree();
        }
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                _lastPreview?.Succeeded == true ? "Validation canonique réussie." :
                "Les diagnostics apparaîtront après l’aperçu sec.", 9,
                _lastPreview?.Succeeded == true ? StudioStyle.Success : StudioStyle.Muted, wrap: true));
            return;
        }
        foreach (var diagnostic in diagnostics)
        {
            _diagnostics.AddChild(StudioStyle.Label("• " + diagnostic.Message, 9, StudioStyle.Danger, wrap: true));
        }
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }
        var scale = viewport.X >= 3_200 ? 1.5f : viewport.X >= 2_300 ? 1.15f : 1f;
        var width = Math.Clamp((viewport.X * .43f) / scale, 690f, 880f);
        var height = Math.Clamp((viewport.Y - 30f) / scale, 700f, 1_160f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(viewport.X - (width * scale) - 15f,
            Math.Max(15f, (viewport.Y - (height * scale)) / 2f));
    }

    private void BindViewportToArea(StudioSessionController session, Forge.Domain.Areas.AreaId areaId)
    {
        try
        {
            _worldViewport.RebuildArea(session, areaId, readOnly: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_B_VIEWPORT_FALLBACK {exception.Message}");
        }
    }

    private void RestoreSourceViewport()
    {
        try
        {
            if (_sourceSession.ActiveAreaId is { } activeArea)
            {
                _worldViewport.RebuildArea(_sourceSession, activeArea, readOnly: false, frameArea: false);
                _worldViewport.ApplyCameraProfile(_sourceSession.CameraProfile);
            }
            else
            {
                _worldViewport.ShowNoArea(_sourceSession);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_B_VIEWPORT_RESTORE_FAILED {exception.Message}");
        }
    }

    private static IReadOnlyList<string> ParseTagsUx2NavB(string value) => value.Split(',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string UniqueSlugUx2NavB(IEnumerable<string> existing, string proposed)
    {
        var values = existing.ToHashSet(StringComparer.Ordinal);
        var candidate = proposed;
        var suffix = 2;
        while (values.Contains(candidate))
        {
            candidate = $"{proposed}-{suffix++}";
        }
        return candidate;
    }

    private static string HumanTextUx2NavB(string? value, string fallback)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) || Guid.TryParse(text, out _) ? fallback : text;
    }

    private static IEnumerable<Label> DescendantLabelsUx2NavB(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label)
            {
                yield return label;
            }
            foreach (var descendant in DescendantLabelsUx2NavB(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool ContainsUuidUx2NavB(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
