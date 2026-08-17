using Forge.Application.Creator;
using Forge.Domain.Entities;
using Forge.Domain.Blueprints;
using Forge.Domain.Authoring;
using Forge.Domain.Spatial;
using Forge.Studio.Core.Navigation;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell : Control
{
    private const float CompactWidth = 1280;
    private readonly StudioSessionController _session;
    private readonly Dictionary<string, Button> _sectionButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _pageButtons = new(StringComparer.Ordinal);
    private readonly VBoxContainer _pageList;
    private readonly LineEdit _search;
    private readonly OptionButton _areaSelector;
    private Label _breadcrumb = null!;
    private readonly Label _pageTitle;
    private readonly Label _pageSubtitle;
    private readonly Label _status;
    private readonly Label _inspectorTitle;
    private readonly VBoxContainer _inspectorContent;
    private readonly PanelContainer _inspectorPanel;
    private Button _inspectorToggle = null!;
    private readonly Button _primaryAction;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Label _saveState = null!;
    private Label _saveSummary = null!;
    private readonly StudioSpecializedPage _specializedPage;
    private readonly StudioAssetLibraryPage _assetLibraryPage;
    private readonly StudioProjectFoundationPage _projectPage;
    private bool _compactInspectorRequested;
    private StudioEntityEditorSection? _entityEditorSection;
    private Vector3Value? _placementPosition;

    internal event Action? BackToHubRequested;

    internal event Action? NavigationChanged;

    internal string SectionId { get; private set; }

    internal string PageId { get; private set; }

    internal int SectionCount => StudioPageCatalog.Sections.Count;

    internal int PageCount => StudioPageCatalog.Pages.Count;

    internal bool AssetLibraryLoaded => _assetLibraryPage.IsLoaded;

    internal StudioWorldViewport WorldViewport { get; }

    internal StudioWorkspaceShell(
        StudioSessionController session,
        string initialSectionId,
        string initialPageId)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (!_session.IsOpen || _session.Project is null)
        {
            throw new InvalidOperationException("The Studio shell requires an open project session.");
        }

        SectionId = ResolveSection(initialSectionId).Id;
        PageId = ResolvePage(initialPageId, SectionId).Id;
        Name = "StudioWorkspaceShell";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = StudioStyle.Background,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        var top = BuildTopBar();
        root.AddChild(top);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 1);
        root.AddChild(body);

        body.AddChild(BuildSectionRail());

        _navigationPanel = StudioStyle.Panel("PageNavigation", StudioStyle.Chrome, 0, 0);
        _navigationPanel.CustomMinimumSize = new Vector2(
            _session.WorkspaceProfile.Shell.Layout.ExplorerWidth, 0);
        body.AddChild(_navigationPanel);
        var pageMargin = StudioStyle.Margin(8, 9, 8, 8);
        _navigationPanel.AddChild(pageMargin);
        var pageColumn = new VBoxContainer();
        pageColumn.AddThemeConstantOverride("separation", 6);
        pageMargin.AddChild(pageColumn);
        pageColumn.AddChild(StudioStyle.SectionLabel("ZONE ACTIVE"));
        _areaSelector = new OptionButton();
        StudioStyle.ApplyOptionButton(_areaSelector);
        _areaSelector.ItemSelected += SelectArea;
        pageColumn.AddChild(_areaSelector);
        _search = new LineEdit { PlaceholderText = "Filtrer les pages…", ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(_search);
        _search.TextChanged += _ => RebuildPageList();
        _pageList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pageList.AddThemeConstantOverride("separation", 3);
        BuildUx0Navigation(pageColumn);

        var center = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        center.AddThemeConstantOverride("separation", 0);
        body.AddChild(center);

        center.AddChild(BuildAreaTabs());

        var heading = StudioStyle.Panel("PageHeading", StudioStyle.Chrome, 0, 0);
        center.AddChild(heading);
        var headingMargin = StudioStyle.Margin(12, 7, 12, 7);
        heading.AddChild(headingMargin);
        var headingRow = new HBoxContainer();
        headingRow.AddThemeConstantOverride("separation", 8);
        headingMargin.AddChild(headingRow);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pageTitle = StudioStyle.Label(string.Empty, 18);
        _pageSubtitle = StudioStyle.Label(string.Empty, 11, StudioStyle.Muted, wrap: true);
        headingText.AddChild(_pageTitle);
        headingText.AddChild(_pageSubtitle);
        headingRow.AddChild(headingText);
        BuildUx1AActions(headingRow);
        BuildUx1BActions(headingRow);
        BuildUx1CActions(headingRow);
        BuildUx1DActions(headingRow);
        BuildUx1EActions(headingRow);
        BuildUx1FActions(headingRow);
        BuildUx1GActions(headingRow);
        BuildUx1HActions(headingRow);
        BuildUx1IActions(headingRow);
        BuildUx1JActions(headingRow);
        BuildUx1KActions(headingRow);
        BuildUx1LActions(headingRow);
        BuildUx1MActions(headingRow);
        BuildUx1NActions(headingRow);
        BuildUx1OActions(headingRow);
        BuildUx1PActions(headingRow);
        BuildUx1QActions(headingRow);
        BuildUx1RActions(headingRow);
        BuildUx1SActions(headingRow);
        BuildUx1TActions(headingRow);
        BuildUx1UActions(headingRow);
        BuildUx1VActions(headingRow);
        BuildUx1WActions(headingRow);
        BuildUx1XActions(headingRow);
        BuildUx1YActions(headingRow);
        BuildUx1ZActions(headingRow);
        BuildUx1AAActions(headingRow);
        BuildUx1ABActions(headingRow);
        BuildUx1ACActions(headingRow);
        BuildUx1ADActions(headingRow);
        BuildUx1AEActions(headingRow);
        BuildUx1AFActions(headingRow);
        BuildUx1AGActions(headingRow);
        BuildUx1AHActions(headingRow);
        BuildUx1AIActions(headingRow);
        BuildUx1AJActions(headingRow);
        BuildUx2AActions(headingRow);
        BuildUx2LwBActions(headingRow);
        BuildUx2LwCActions(headingRow);
        BuildUx2LwDActions(headingRow);
        BuildUx2LwEActions(headingRow);
        BuildUx2LwFActions(headingRow);
        BuildUx2LwGActions(headingRow);
        BuildUx2LwHActions(headingRow);
        BuildUx2LwIActions(headingRow);
        BuildUx2LwJActions(headingRow);
        BuildUx2LwKActions(headingRow);
        BuildUx2LwLActions(headingRow);
        BuildUx2BActions(headingRow);
        BuildUx2CActions(headingRow);
        BuildUx2EnvBActions(headingRow);
        BuildUx2EnvCActions(headingRow);
        BuildUx2EnvDActions(headingRow);
        BuildUx2EnvEActions(headingRow);
        BuildUx2EnvFActions(headingRow);
        BuildUx2EnvGActions(headingRow);
        BuildUx2EnvHActions(headingRow);
        BuildUx2EnvIActions(headingRow);
        BuildUx2EnvJActions(headingRow);
        BuildUx2EnvKActions(headingRow);
        BuildUx2NavBActions(headingRow);
        BuildUx2NavCActions(headingRow);
        BuildUx2NavDActions(headingRow);
        BuildUx2NavEActions(headingRow);
        BuildUx2NavFActions(headingRow);
        BuildUx2NavGActions(headingRow);
        BuildUx2NavHActions(headingRow);
        BuildUx2NavIActions(headingRow);
        BuildUx2NavJActions(headingRow);
        BuildUx2NavKActions(headingRow);
        BuildUx2NavLActions(headingRow);
        BuildUx2NavMActions(headingRow);
        BuildUx4AActions(headingRow);
        BuildUx4BActions(headingRow);
        BuildUx4CActions(headingRow);
        BuildUx4DActions(headingRow);
        BuildUx4EActions(headingRow);
        BuildUx4FActions(headingRow);
        BuildUx4GActions(headingRow);
        _primaryAction = StudioStyle.Button("Migration en cours", primary: true, compact: true);
        _primaryAction.Disabled = true;
        _primaryAction.TooltipText = "Cette action sera activée seulement après son branchement transactionnel et ses tests.";
        headingRow.AddChild(_primaryAction);

        var pageStack = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        center.AddChild(pageStack);
        _worldSplit = new BoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _worldSplit.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        pageStack.AddChild(_worldSplit);
        WorldViewport = new StudioWorldViewport();
        WorldViewport.EntitySelected += SelectEntity;
        WorldViewport.EntitiesSelected += SelectEntities;
        WorldViewport.EntityManipulationRequested += ManipulateEntities;
        WorldViewport.EntityContextActionRequested += HandleEntityContextAction;
        WorldViewport.EntityPlacementRequested += BeginAddEntity;
        WorldViewport.ClipboardPasteRequested += PasteClipboard;
        WorldViewport.RepeatedPlacementRequested += PlaceRepeatedEntities;
        WorldViewport.InteractionStatusChanged += message => SetStatus(message);
        _primaryAction.Pressed += HandlePrimaryAction;
        _worldSplit.AddChild(WorldViewport);
        ComparisonViewport = new StudioWorldViewport { Visible = false };
        ComparisonViewport.InteractionStatusChanged += message => SetStatus(message);
        _worldSplit.AddChild(ComparisonViewport);
        _specializedPage = new StudioSpecializedPage();
        _specializedPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        pageStack.AddChild(_specializedPage);
        InitializeUx0JPage(pageStack);
        _projectPage = new StudioProjectFoundationPage(_session);
        _projectPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _projectPage.StatusChanged += SetStatus;
        _projectPage.SectionChanged += UpdateProjectSectionHeader;
        _projectPage.ProjectChanged += () =>
        {
            PopulateAreas();
            UpdateCommandState();
            RebuildExplorer();
            RefreshInspector();
            NavigationChanged?.Invoke();
        };
        pageStack.AddChild(_projectPage);
        _assetLibraryPage = new StudioAssetLibraryPage(_session);
        _assetLibraryPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _assetLibraryPage.StatusChanged += (message, error) => SetStatus(message, error);
        _assetLibraryPage.ProjectChanged += () =>
        {
            UpdateCommandState();
            RebuildExplorer();
            RefreshInspector();
            NavigationChanged?.Invoke();
        };
        _assetLibraryPage.InstalledAssetPlacementRequested += PlaceInstalledAsset;
        pageStack.AddChild(_assetLibraryPage);

        _inspectorPanel = StudioStyle.Panel("ContextInspector", StudioStyle.Chrome, 0, 0);
        _inspectorPanel.CustomMinimumSize = new Vector2(280, 0);
        body.AddChild(_inspectorPanel);
        var inspectorMargin = StudioStyle.Margin(10, 10, 10, 8);
        _inspectorPanel.AddChild(inspectorMargin);
        var inspectorColumn = new VBoxContainer();
        inspectorColumn.AddThemeConstantOverride("separation", 6);
        inspectorMargin.AddChild(inspectorColumn);
        inspectorColumn.AddChild(StudioStyle.SectionLabel("INSPECTEUR CONTEXTUEL"));
        _inspectorTitle = StudioStyle.Label(string.Empty, 16);
        inspectorColumn.AddChild(_inspectorTitle);
        var separator = new HSeparator();
        separator.AddThemeColorOverride("separator", StudioStyle.Border);
        inspectorColumn.AddChild(separator);
        var inspectorScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        inspectorColumn.AddChild(inspectorScroll);
        _inspectorContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inspectorContent.AddThemeConstantOverride("separation", 6);
        inspectorScroll.AddChild(_inspectorContent);

        var statusPanel = StudioStyle.Panel("StatusBar", StudioStyle.Chrome, 0, 0);
        statusPanel.CustomMinimumSize = new Vector2(0, 24);
        root.AddChild(statusPanel);
        var statusMargin = StudioStyle.Margin(8, 3, 8, 3);
        statusPanel.AddChild(statusMargin);
        var statusRow = new HBoxContainer();
        statusMargin.AddChild(statusRow);
        _status = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted);
        statusRow.AddChild(_status);
        statusRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        BuildUx0JStatusAccess(statusRow);
        _saveSummary = StudioStyle.Label(string.Empty, 10, StudioStyle.Success);
        statusRow.AddChild(_saveSummary);

        PopulateAreas();
        InitializeUx0Overlays();
        InitializeUx0JOverlay();
        InitializeUx0BOverlays();
        InitializeUx0COverlays();
        InitializeUx1AOverlay();
        InitializeUx1BOverlay();
        InitializeUx1COverlay();
        InitializeUx1DOverlay();
        InitializeUx1EOverlay();
        InitializeUx1WOverlay();
        InitializeUx1XOverlay();
        InitializeUx1YOverlay();
        InitializeUx1ABOverlay();
        InitializeUx1ACOverlay();
        InitializeUx1ADOverlay();
        InitializeUx1AEOverlay();
        InitializeUx1AFWorkspace();
        InitializeUx1AGOverlay();
        InitializeUx1AHOverlay();
        InitializeUx1AIOverlay();
        InitializeUx1AJOverlay();
        InitializeUx2AOverlay();
        InitializeUx2LwBOverlay();
        InitializeUx2LwCOverlay();
        InitializeUx2LwDOverlay();
        InitializeUx2LwEOverlay();
        InitializeUx2LwFOverlay();
        InitializeUx2LwGOverlay();
        InitializeUx2LwHOverlay();
        InitializeUx2LwIOverlay();
        InitializeUx2LwJOverlay();
        InitializeUx2LwKOverlay();
        InitializeUx2LwLOverlay();
        InitializeUx2BWorkspace();
        InitializeUx2CWorkspace();
        InitializeUx2EnvBOverlay();
        InitializeUx2EnvCOverlay();
        InitializeUx2EnvDOverlay();
        InitializeUx2EnvEOverlay();
        InitializeUx2EnvFOverlay();
        InitializeUx2EnvGOverlay();
        InitializeUx2EnvHOverlay();
        InitializeUx2EnvIOverlay();
        InitializeUx2EnvJOverlay();
        InitializeUx2EnvKOverlay();
        InitializeUx2NavBOverlay();
        InitializeUx2NavCOverlay();
        InitializeUx2NavDOverlay();
        InitializeUx2NavEOverlay();
        InitializeUx2NavFOverlay();
        InitializeUx2NavGOverlay();
        InitializeUx2NavHOverlay();
        InitializeUx2NavIOverlay();
        InitializeUx2NavJOverlay();
        InitializeUx2NavKOverlay();
        InitializeUx2NavLOverlay();
        InitializeUx2NavMOverlay();
        InitializeUx4AOverlay();
        InitializeUx4BOverlay();
        InitializeUx4COverlay();
        InitializeUx4DOverlay();
        InitializeUx4EOverlay();
        InitializeUx4FOverlay();
        InitializeUx4GOverlay();
        InitializeUx5APage(pageStack);
        if (_session.ActiveArea is null)
        {
            WorldViewport.ShowNoArea(_session);
        }
        else
        {
            WorldViewport.Rebuild(_session);
            WorldViewport.ApplyCameraProfile(_session.CameraProfile);
        }
        RefreshAreaTabs();
        RefreshComparisonViewport();
        SwitchPage(PageId, notify: false);
        UpdateCommandState();
        Resized += UpdateResponsiveLayout;
    }

    public override void _Ready() => UpdateResponsiveLayout();

    internal bool RunEditingSmoke()
    {
        var entity = _session.ActiveArea?.Entities.FirstOrDefault();
        if (entity is null || !_session.SelectEntity(entity.Id))
        {
            return false;
        }

        var initialCount = _session.ActiveArea!.Entities.Count;
        var original = entity.Transform;
        var moved = new TransformValue(
            new Vector3Value(original.Position.X + .25f, original.Position.Y, original.Position.Z),
            original.Rotation,
            original.Scale);
        var apply = _session.TransformEntity(entity.Id, moved);
        var appliedPosition = _session.ActiveArea!.Entities.Single(item => item.Id == entity.Id).Transform.Position;
        var undo = _session.Undo();
        var undonePosition = _session.ActiveArea.Entities.Single(item => item.Id == entity.Id).Transform.Position;
        var redo = _session.Redo();
        var redonePosition = _session.ActiveArea.Entities.Single(item => item.Id == entity.Id).Transform.Position;
        var restore = _session.Undo();
        var restoredPosition = _session.ActiveArea.Entities.Single(item => item.Id == entity.Id).Transform.Position;

        var originalClick = Forge.Domain.Entities.EntityClickTriggerSettings.Read(entity.Overrides);
        var clickSettings = new Forge.Domain.Entities.EntityClickTriggerSettings(
            true,
            Forge.Domain.Entities.EntityClickTriggerAction.ShowMessage,
            "Déclencheur de test Studio",
            null,
            4);
        var clickApply = _session.SetEntityOverrides(
            entity.Id,
            Forge.Domain.Entities.EntityClickTriggerSettings.Merge(entity.Overrides, clickSettings));
        var clickApplied = Forge.Domain.Entities.EntityClickTriggerSettings.Read(
            _session.ActiveArea.Entities.Single(item => item.Id == entity.Id).Overrides);
        var clickUndo = clickApply.Succeeded && _session.Undo().Succeeded;
        var clickUndone = Forge.Domain.Entities.EntityClickTriggerSettings.Read(
            _session.ActiveArea.Entities.Single(item => item.Id == entity.Id).Overrides);
        var clickRedo = clickUndo && _session.Redo().Succeeded;
        var clickRedone = Forge.Domain.Entities.EntityClickTriggerSettings.Read(
            _session.ActiveArea.Entities.Single(item => item.Id == entity.Id).Overrides);
        var clickRestore = clickRedo && _session.Undo().Succeeded;

        var place = _session.PlaceEntity(
            entity.BlueprintId,
            new Vector3Value(original.Position.X + 3, original.Position.Y, original.Position.Z + 3));
        var placed = place.EntityId is null
            ? null
            : _session.ActiveArea.Entities.FirstOrDefault(item => item.Id == place.EntityId);
        var placeCount = _session.ActiveArea.Entities.Count;
        var placeUndo = place.Succeeded && _session.Undo().Succeeded;
        var placeRestoredCount = _session.ActiveArea.Entities.Count;

        var duplicate = _session.DuplicateEntity(entity.Id);
        var duplicated = duplicate.EntityId is null
            ? null
            : _session.ActiveArea.Entities.FirstOrDefault(item => item.Id == duplicate.EntityId);
        var duplicateCount = _session.ActiveArea.Entities.Count;
        var duplicateUndo = duplicate.Succeeded && _session.Undo().Succeeded;
        var duplicateRedo = duplicateUndo && _session.Redo().Succeeded;
        var duplicateRedone = duplicate.EntityId is not null &&
            _session.ActiveArea.Entities.Any(item => item.Id == duplicate.EntityId);
        var duplicateRestore = duplicateRedo && _session.Undo().Succeeded;

        var remove = _session.RemoveEntity(entity.Id);
        var removeCount = _session.ActiveArea.Entities.Count;
        var removeUndo = remove.Succeeded && _session.Undo().Succeeded;
        var removeRestored = _session.ActiveArea.Entities.Any(item => item.Id == entity.Id);
        var removeRedo = removeUndo && _session.Redo().Succeeded;
        var removedAgain = _session.ActiveArea.Entities.All(item => item.Id != entity.Id);
        var removeRestore = removeRedo && _session.Undo().Succeeded;

        while (_session.CanUndo)
        {
            _session.Undo();
        }
        _session.SelectEntity(entity.Id);
        WorldViewport.Rebuild(_session, frameArea: false);
        UpdateCommandState();

        var valid = apply.Succeeded && undo.Succeeded && redo.Succeeded && restore.Succeeded &&
            appliedPosition == moved.Position && undonePosition == original.Position &&
            redonePosition == moved.Position && restoredPosition == original.Position &&
            clickApply.Succeeded && clickApplied.Enabled &&
            clickApplied.Action == Forge.Domain.Entities.EntityClickTriggerAction.ShowMessage &&
            clickApplied.Message == "Déclencheur de test Studio" &&
            clickUndo && clickUndone == originalClick && clickRedo && clickRedone.Enabled && clickRestore &&
            place.Succeeded && placed is not null && placeCount == initialCount + 1 &&
            placeUndo && placeRestoredCount == initialCount &&
            duplicate.Succeeded && duplicated is not null && duplicateCount == initialCount + 1 &&
            duplicated.BlueprintId == entity.BlueprintId &&
            duplicated.Tags.SequenceEqual(entity.Tags) &&
            duplicated.Overrides.Properties.SequenceEqual(entity.Overrides.Properties) &&
            duplicated.BlueprintVersionRequirement == entity.BlueprintVersionRequirement &&
            duplicated.Transform.Rotation == entity.Transform.Rotation &&
            duplicated.Transform.Scale == entity.Transform.Scale &&
            duplicateUndo && duplicateRedo && duplicateRedone && duplicateRestore &&
            remove.Succeeded && removeCount == initialCount - 1 &&
            removeUndo && removeRestored && removeRedo && removedAgain && removeRestore &&
            !_session.IsDirty && WorldViewport.PrepareEditingEvidence(_session, showContextMenu: false);
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_EDIT_{(valid ? "PASS" : "FAILED")} " +
            $"entity={entity.Id} delta_x=0.25 undo={undo.Succeeded} redo={redo.Succeeded} " +
            $"add={place.Succeeded} duplicate={duplicate.Succeeded} delete={remove.Succeeded} " +
            $"click_trigger={clickApply.Succeeded} " +
            $"gizmo={WorldViewport.GizmoVisible} dirty={_session.IsDirty}");
        return valid;
    }

    internal bool PrepareEditingEvidence(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return true;
        }

        if (mode.StartsWith("ux0-a", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0AEvidence(mode);
        }
        if (mode.StartsWith("ux0-b", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0BEvidence(mode);
        }
        if (mode.StartsWith("ux0-c", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0CEvidence(mode);
        }
        if (mode.StartsWith("ux0-d", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0DEvidence(mode);
        }
        if (mode.StartsWith("ux0-e", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0EEvidence(mode);
        }
        if (mode.StartsWith("ux0-f", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0FEvidence(mode);
        }
        if (mode.StartsWith("ux0-g", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0GEvidence(mode);
        }
        if (mode.StartsWith("ux0-h", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0HEvidence(mode);
        }
        if (mode.StartsWith("ux0-i", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0IEvidence(mode);
        }
        if (mode.StartsWith("ux0-j", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx0JEvidence(mode);
        }
        if (mode.StartsWith("ux1-aa", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AAEvidence(mode);
        }
        if (mode.StartsWith("ux1-ab", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1ABEvidence(mode);
        }
        if (mode.StartsWith("ux1-ac", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1ACEvidence(mode);
        }
        if (mode.StartsWith("ux1-ad", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1ADEvidence(mode);
        }
        if (mode.StartsWith("ux1-ae", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AEEvidence(mode);
        }
        if (mode.StartsWith("ux1-af", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AFEvidence(mode);
        }
        if (mode.StartsWith("ux1-ag", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AGEvidence(mode);
        }
        if (mode.StartsWith("ux1-ah", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AHEvidence(mode);
        }
        if (mode.StartsWith("ux1-ai", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AIEvidence(mode);
        }
        if (mode.StartsWith("ux1-aj", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AJEvidence(mode);
        }
        if (mode.StartsWith("ux2-area-034", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvJEvidence(mode);
        }
        if (mode.StartsWith("ux2-a", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2AEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-b", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwBEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-c", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwCEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-d", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwDEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-e", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwEEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-f", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwFEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-g", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwGEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-h", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwHEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-i", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-encounter-008-010-014", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwIEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-j", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-encounter-016-017-019", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwJEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-k", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-encounter-018", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwKEvidence(mode);
        }
        if (mode.StartsWith("ux2-lw-l", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-encounter-020", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2LwLEvidence(mode);
        }
        if (mode.StartsWith("ux2-b", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2BEvidence(mode);
        }
        if (mode.StartsWith("ux2-c", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2CEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-b", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvBEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-c", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvCEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-d", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvDEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-e", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvEEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-f", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvFEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-g", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvGEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-h", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvHEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-i", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvIEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-j", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvJEvidence(mode);
        }
        if (mode.StartsWith("ux2-env-k", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2EnvKEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-b", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavBEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-c", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavCEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-d", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavDEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-e", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavEEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-f", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavFEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-g", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavGEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-h", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavHEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-i", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavIEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-j", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-area-033", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavJEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-k", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-wpt-010", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavKEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-l", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-wpt-012", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavLEvidence(mode);
        }
        if (mode.StartsWith("ux2-nav-m", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("ux2-wpt-013", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx2NavMEvidence(mode);
        }
        if (mode.StartsWith("ux4-a", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx4AEvidence(mode);
        }
        if (mode.StartsWith("ux4-b", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx4BEvidence(mode);
        }
        if (mode.StartsWith("ux4-c", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx4CEvidence(mode);
        }
        if (mode.StartsWith("ux4-d", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx4DEvidence(mode);
        }
        if (mode.StartsWith("ux4-e", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx4EEvidence(mode);
        }
        if (mode.StartsWith("ux4-f", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx4FEvidence(mode);
        }
        if (mode.StartsWith("ux4-g", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx4GEvidence(mode);
        }
        if (mode.StartsWith("ux5-a", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx5AEvidence(mode);
        }
        if (mode.StartsWith("ux1-a", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1AEvidence(mode);
        }
        if (mode.StartsWith("ux1-b", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1BEvidence(mode);
        }
        if (mode.StartsWith("ux1-c", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1CEvidence(mode);
        }
        if (mode.StartsWith("ux1-d", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1DEvidence(mode);
        }
        if (mode.StartsWith("ux1-e", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1EEvidence(mode);
        }
        if (mode.StartsWith("ux1-f", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1FEvidence(mode);
        }
        if (mode.StartsWith("ux1-g", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1GEvidence(mode);
        }
        if (mode.StartsWith("ux1-h", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1HEvidence(mode);
        }
        if (mode.StartsWith("ux1-i", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1IEvidence(mode);
        }
        if (mode.StartsWith("ux1-j", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1JEvidence(mode);
        }
        if (mode.StartsWith("ux1-k", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1KEvidence(mode);
        }
        if (mode.StartsWith("ux1-l", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1LEvidence(mode);
        }
        if (mode.StartsWith("ux1-m", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1MEvidence(mode);
        }
        if (mode.StartsWith("ux1-n", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1NEvidence(mode);
        }
        if (mode.StartsWith("ux1-o", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1OEvidence(mode);
        }
        if (mode.StartsWith("ux1-p", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1PEvidence(mode);
        }
        if (mode.StartsWith("ux1-q", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1QEvidence(mode);
        }
        if (mode.StartsWith("ux1-r", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1REvidence(mode);
        }
        if (mode.StartsWith("ux1-s", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1SEvidence(mode);
        }
        if (mode.StartsWith("ux1-t", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1TEvidence(mode);
        }
        if (mode.StartsWith("ux1-u", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1UEvidence(mode);
        }
        if (mode.StartsWith("ux1-v", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1VEvidence(mode);
        }
        if (mode.StartsWith("ux1-w", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1WEvidence(mode);
        }
        if (mode.StartsWith("ux1-x", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1XEvidence(mode);
        }
        if (mode.StartsWith("ux1-y", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1YEvidence(mode);
        }
        if (mode.StartsWith("ux1-z", StringComparison.OrdinalIgnoreCase))
        {
            return PrepareUx1ZEvidence(mode);
        }

        if (string.Equals(mode, "assets", StringComparison.OrdinalIgnoreCase))
        {
            SwitchPage("assets", notify: false);
            return _assetLibraryPage.PrepareEvidence();
        }
        if (string.Equals(mode, "asset-install", StringComparison.OrdinalIgnoreCase))
        {
            SwitchPage("assets", notify: false);
            return _assetLibraryPage.RunInstallEvidence();
        }

        var showContext = string.Equals(mode, "context", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "delete", StringComparison.OrdinalIgnoreCase);
        if (!WorldViewport.PrepareEditingEvidence(_session, showContext))
        {
            return false;
        }

        if (string.Equals(mode, "rotation", StringComparison.OrdinalIgnoreCase))
        {
            return WorldViewport.PrepareRotationEvidence();
        }

        if (string.Equals(mode, "delete", StringComparison.OrdinalIgnoreCase))
        {
            return WorldViewport.PrepareDeleteConfirmationEvidence();
        }
        if (string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase))
        {
            BeginAddEntity(WorldViewport.SuggestedPlacementPosition);
            return _placementPosition is not null;
        }

        if (string.Equals(mode, "editor", StringComparison.OrdinalIgnoreCase) &&
            _session.SelectedEntityId is not null)
        {
            HandleEntityContextAction(_session.SelectedEntityId, StudioEntityContextAction.EditAll);
        }
        if (string.Equals(mode, "trigger", StringComparison.OrdinalIgnoreCase))
        {
            var placeable = _session.ActiveArea?.Entities.FirstOrDefault(entity =>
                _session.Project!.Blueprints.First(blueprint => blueprint.Id == entity.BlueprintId).Kind ==
                BlueprintKind.Placeable);
            if (placeable is not null) SelectEntity(placeable.Id);
            if (_session.SelectedEntityId is not null)
            {
                HandleEntityContextAction(_session.SelectedEntityId, StudioEntityContextAction.EditClickTrigger);
            }
        }
        return true;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (key.Keycode == Key.Escape && _areaLayersOverlay.IsOpen)
        {
            _areaLayersOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _areaLoadingOverlay.IsOpen)
        {
            _areaLoadingOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _areaDocumentOperationsOverlay.IsOpen)
        {
            _areaDocumentOperationsOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _areaCreationOverlay.IsOpen)
        {
            _areaCreationOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _globalSearchOverlay.IsOpen)
        {
            _globalSearchOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _safeRenameOverlay.IsOpen)
        {
            _safeRenameOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _displayFilterOverlay.IsOpen)
        {
            _displayFilterOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _workspaceSettingsOverlay.IsOpen)
        {
            _workspaceSettingsOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && _viewportToolsOverlay.IsOpen)
        {
            _viewportToolsOverlay.Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (TryExecuteKeymap(key))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private Control BuildTopBar()
    {
        var top = StudioStyle.Panel("TopBar", StudioStyle.Chrome, 0, 0);
        top.CustomMinimumSize = new Vector2(0, 48);
        var margin = StudioStyle.Margin(9, 6, 9, 6);
        top.AddChild(margin);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        margin.AddChild(row);

        var mark = StudioStyle.Panel("BrandMark", StudioStyle.Accent, 8, 0);
        mark.CustomMinimumSize = new Vector2(34, 34);
        var markText = StudioStyle.Label("OF", 12, StudioStyle.Background);
        markText.HorizontalAlignment = HorizontalAlignment.Center;
        markText.VerticalAlignment = VerticalAlignment.Center;
        mark.AddChild(markText);
        row.AddChild(mark);

        var module = new VBoxContainer { CustomMinimumSize = new Vector2(164, 0) };
        module.AddChild(StudioStyle.Label(_session.Project!.Module.DisplayName, 13));
        _breadcrumb = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted);
        module.AddChild(_breadcrumb);
        row.AddChild(module);
        row.AddChild(new VSeparator());

        _undoButton = StudioStyle.Button("↶", compact: true);
        _undoButton.TooltipText = "Annuler la dernière retouche.";
        _undoButton.CustomMinimumSize = new Vector2(30, 28);
        _undoButton.Pressed += Undo;
        row.AddChild(_undoButton);
        _redoButton = StudioStyle.Button("↷", compact: true);
        _redoButton.TooltipText = "Rétablir la dernière retouche annulée.";
        _redoButton.CustomMinimumSize = new Vector2(30, 28);
        _redoButton.Pressed += Redo;
        row.AddChild(_redoButton);
        _saveButton = StudioStyle.Button("Enregistrer", primary: true, compact: true);
        _saveButton.TooltipText = "Écrire atomiquement les modifications dans le module.";
        _saveButton.Pressed += Save;
        row.AddChild(_saveButton);
        _globalSearchButton = StudioStyle.Button("Rechercher · Ctrl+K", compact: true);
        _globalSearchButton.TooltipText = "Rechercher pages, ressources, objets et actions.";
        _globalSearchButton.Pressed += () => OpenGlobalSearch();
        row.AddChild(_globalSearchButton);
        _filterButton = StudioStyle.Button("Filtres", compact: true);
        _filterButton.Pressed += OpenDisplayFilters;
        row.AddChild(_filterButton);
        _workspaceButton = StudioStyle.Button("Espace de travail", compact: true);
        _workspaceButton.TooltipText = "Profils, panneaux, onglets, comparaison et raccourcis configurables.";
        _workspaceButton.Pressed += OpenWorkspaceSettings;
        row.AddChild(_workspaceButton);
        _viewportToolsButton = StudioStyle.Button("Outils 3D", compact: true);
        _viewportToolsButton.TooltipText = "Modes 3D, caméra, sélection multiple, gizmos et alignements.";
        _viewportToolsButton.Pressed += OpenViewportTools;
        row.AddChild(_viewportToolsButton);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _saveState = StudioStyle.Label(string.Empty, 10, StudioStyle.Success);
        row.AddChild(_saveState);
        _inspectorToggle = StudioStyle.Button("Inspecteur", compact: true);
        _inspectorToggle.Visible = false;
        _inspectorToggle.Pressed += () =>
        {
            _compactInspectorRequested = !_compactInspectorRequested;
            UpdateResponsiveLayout();
        };
        row.AddChild(_inspectorToggle);
        var change = StudioStyle.Button("Changer de module", compact: true);
        change.Pressed += () =>
        {
            if (_session.IsDirty)
            {
                SetStatus("Enregistrez ou annulez les modifications avant de changer de module.", error: true);
                return;
            }
            BackToHubRequested?.Invoke();
        };
        row.AddChild(change);
        return top;
    }

    private Control BuildSectionRail()
    {
        var rail = StudioStyle.Panel("SectionRail", StudioStyle.Chrome, 0, 0);
        rail.CustomMinimumSize = new Vector2(72, 0);
        var margin = StudioStyle.Margin(6, 8, 6, 7);
        rail.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);
        foreach (var section in StudioPageCatalog.Sections)
        {
            var button = StudioStyle.Button(section.Title, compact: true);
            button.CustomMinimumSize = new Vector2(60, 36);
            button.TooltipText = section.Pages.Count + " pages";
            button.Pressed += () => SwitchSection(section.Id);
            _sectionButtons[section.Id] = button;
            column.AddChild(button);
        }
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var help = StudioStyle.Button("?", compact: true);
        help.TooltipText = "Aide contextuelle : tâche actuelle, prochaine action et sources locales.";
        help.Pressed += () => ShowContextualHelp();
        column.AddChild(help);
        return rail;
    }

    private void PopulateAreas()
    {
        _areaSelector.Clear();
        if (_session.Project!.Areas.Count == 0)
        {
            _areaSelector.AddItem("Aucune zone dans ce projet");
            _areaSelector.SetItemDisabled(0, true);
            _areaSelector.Select(0);
            _areaSelector.Disabled = true;
            return;
        }
        _areaSelector.Disabled = false;
        var selected = 0;
        for (var index = 0; index < _session.Project!.Areas.Count; index++)
        {
            var area = _session.Project.Areas[index];
            _areaSelector.AddItem(area.DisplayName);
            _areaSelector.SetItemTooltip(index, $"{area.Entities.Count} objets · {area.Kind}");
            if (area.Id == _session.ActiveAreaId)
            {
                selected = index;
            }
        }
        _areaSelector.Select(selected);
    }

    private void SelectArea(long index)
    {
        if (index < 0 || index >= _session.Project!.Areas.Count)
        {
            return;
        }

        var area = _session.Project.Areas[(int)index];
        var result = _session.ActivateAreaTab(area.Id, persist: true);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        RebuildActiveArea($"{area.DisplayName} · {area.Entities.Count} objets · onglet actif");
    }

    private void SwitchSection(string sectionId)
    {
        ShowNavigationMode(explorer: false);
        var section = ResolveSection(sectionId);
        SectionId = section.Id;
        if (section.Pages.All(page => page.Id != PageId))
        {
            PageId = section.Pages[0].Id;
        }
        RebuildPageList();
        SwitchPage(PageId);
    }

    private void SwitchPage(string pageId, bool notify = true)
    {
        var page = ResolvePage(pageId, SectionId);
        if (page.Id != "living-world")
        {
            CloseUx2AOverlay();
            CloseUx2LwBOverlay();
            CloseUx2LwCOverlay();
            CloseUx2LwDOverlay();
            CloseUx2LwEOverlay();
            CloseUx2LwFOverlay();
            CloseUx2LwGOverlay();
            CloseUx2LwHOverlay();
            CloseUx2LwIOverlay();
            CloseUx2LwJOverlay();
            CloseUx2LwKOverlay();
            CloseUx2LwLOverlay();
        }
        if (page.Id != "terrain")
        {
            CloseUx2BWorkspace();
            CloseUx2CWorkspace();
        }
        if (page.Id != "environment")
        {
            CloseUx2EnvBOverlay();
            CloseUx2EnvCOverlay();
            CloseUx2EnvDOverlay();
            CloseUx2EnvEOverlay();
            CloseUx2EnvFOverlay();
            CloseUx2EnvGOverlay();
            CloseUx2EnvHOverlay();
            CloseUx2EnvIOverlay();
            CloseUx2EnvKOverlay();
        }
        if (page.Id is not ("environment" or "navigation"))
        {
            CloseUx2EnvJOverlay();
        }
        if (page.Id != "navigation")
        {
            CloseUx2NavBOverlay();
            CloseUx2NavCOverlay();
            CloseUx2NavDOverlay();
            CloseUx2NavEOverlay();
            CloseUx2NavFOverlay();
            CloseUx2NavGOverlay();
            CloseUx2NavHOverlay();
            CloseUx2NavIOverlay();
            CloseUx2NavJOverlay();
            CloseUx2NavKOverlay();
            CloseUx2NavLOverlay();
            CloseUx2NavMOverlay();
        }
        if (page.Id != "social-content")
        {
            CloseUx4AOverlay();
            CloseUx4BOverlay();
            CloseUx4COverlay();
        }
        if (page.Id != "creatures")
        {
            CloseUx4DOverlay();
            CloseUx4EOverlay();
            CloseUx4FOverlay();
            CloseUx4GOverlay();
        }
        CloseUx1WOverlay();
        CloseUx1XOverlay();
        CloseUx1YOverlay();
        CloseUx1ZOverlay();
        CloseUx1AAOverlay();
        CloseUx1ABOverlay();
        CloseUx1ACOverlay();
        CloseUx1ADOverlay();
        CloseUx1AEOverlay();
        CloseUx1AFWorkspace();
        CloseUx1AGOverlay();
        CloseUx1AHOverlay();
        CloseUx1AIOverlay();
        CloseUx1AJOverlay();
        var section = StudioPageCatalog.SectionForPage(page.Id);
        SectionId = section.Id;
        PageId = page.Id;
        _breadcrumb.Text = $"{section.Title} / {page.Title}";
        _pageTitle.Text = page.Title;
        _pageSubtitle.Text = page.Subtitle;
        var isAssetLibrary = page.Id == "assets";
        var isProjectConfiguration = page.Id == "module";
        var isDiagnostics = page.Id == "validation";
        var isDialogues = page.Id == "dialogues";
        var canAddEntity = page.Id == "zones";
        _primaryAction.Text = canAddEntity
            ? _session.ActiveArea is null ? "Créer la première zone" : "Ajouter un objet"
            : isProjectConfiguration ? "Prévisualiser" : isDiagnostics ? "Tout vérifier" : page.PrimaryAction;
        _primaryAction.Disabled = !canAddEntity && !isProjectConfiguration && !isDiagnostics;
        _primaryAction.Visible = !isAssetLibrary && !isDialogues;
        _primaryAction.TooltipText = canAddEntity
            ? _session.ActiveArea is null
                ? "Ouvrir l’assistant intégré pour créer le premier document de zone."
                : "Ouvrir la palette et ajouter près de la sélection ou au centre de la vue."
            : isProjectConfiguration
            ? "Valider le formulaire sans modifier ni enregistrer le projet."
            : isDiagnostics
            ? "Relire les diagnostics canoniques et l’activité de la session."
            : "Cette action sera activée seulement après son branchement transactionnel et ses tests.";
        UpdateUx1AActionState();
        UpdateUx1BActionState();
        UpdateUx1CActionState();
        UpdateUx1DActionState();
        UpdateUx1EActionState();
        UpdateUx1FActionState();
        UpdateUx1GActionState();
        UpdateUx1HActionState();
        UpdateUx1IActionState();
        UpdateUx1JActionState();
        UpdateUx1KActionState();
        UpdateUx1LActionState();
        UpdateUx1MActionState();
        UpdateUx1NActionState();
        UpdateUx1OActionState();
        UpdateUx1PActionState();
        UpdateUx1QActionState();
        UpdateUx1RActionState();
        UpdateUx1SActionState();
        UpdateUx1TActionState();
        UpdateUx1UActionState();
        UpdateUx1VActionState();
        UpdateUx1WActionState();
        UpdateUx1XActionState();
        UpdateUx1YActionState();
        UpdateUx1ZActionState();
        UpdateUx1AAActionState();
        UpdateUx1ABActionState();
        UpdateUx1ACActionState();
        UpdateUx1ADActionState();
        UpdateUx1AEActionState();
        UpdateUx1AFActionState();
        UpdateUx1AGActionState();
        UpdateUx1AHActionState();
        UpdateUx1AIActionState();
        UpdateUx1AJActionState();
        UpdateUx2AActionState();
        UpdateUx2LwBActionState();
        UpdateUx2LwCActionState();
        UpdateUx2LwDActionState();
        UpdateUx2LwEActionState();
        UpdateUx2LwFActionState();
        UpdateUx2LwGActionState();
        UpdateUx2LwHActionState();
        UpdateUx2LwIActionState();
        UpdateUx2LwJActionState();
        UpdateUx2LwKActionState();
        UpdateUx2LwLActionState();
        UpdateUx2BActionState();
        UpdateUx2CActionState();
        UpdateUx2EnvBActionState();
        UpdateUx2EnvCActionState();
        UpdateUx2EnvDActionState();
        UpdateUx2EnvEActionState();
        UpdateUx2EnvFActionState();
        UpdateUx2EnvGActionState();
        UpdateUx2EnvHActionState();
        UpdateUx2EnvIActionState();
        UpdateUx2EnvJActionState();
        UpdateUx2EnvKActionState();
        UpdateUx2NavBActionState();
        UpdateUx2NavCActionState();
        UpdateUx2NavDActionState();
        UpdateUx2NavEActionState();
        UpdateUx2NavFActionState();
        UpdateUx2NavGActionState();
        UpdateUx2NavHActionState();
        UpdateUx2NavIActionState();
        UpdateUx2NavJActionState();
        UpdateUx2NavKActionState();
        UpdateUx2NavLActionState();
        UpdateUx2NavMActionState();
        UpdateUx4AActionState();
        UpdateUx4BActionState();
        UpdateUx4CActionState();
        UpdateUx4DActionState();
        UpdateUx4EActionState();
        UpdateUx4FActionState();
        UpdateUx4GActionState();

        var usesWorld = !isAssetLibrary && !isProjectConfiguration && !isDiagnostics &&
                        page.Layout != StudioPageLayout.Specialized;
        _worldSplit.Visible = usesWorld;
        _specializedPage.Visible = !usesWorld && !isAssetLibrary && !isProjectConfiguration && !isDiagnostics &&
                                   !isDialogues;
        _projectPage.Visible = isProjectConfiguration;
        _assetLibraryPage.Visible = isAssetLibrary;
        if (isDiagnostics)
        {
            _diagnosticsPage.Activate();
        }
        else
        {
            _diagnosticsPage.Deactivate();
        }
        if (isAssetLibrary)
        {
            _placementPosition = null;
            _assetLibraryPage.Activate();
        }
        else if (isProjectConfiguration)
        {
            _placementPosition = null;
            _projectPage.RefreshFromSession();
            UpdateProjectSectionHeader(_projectPage.CurrentSection);
        }
        else if (isDiagnostics)
        {
            _placementPosition = null;
        }
        else if (!usesWorld && !isDialogues)
        {
            _placementPosition = null;
            _specializedPage.ShowPage(page, _session);
        }

        RebuildPageList();
        RefreshSectionStyles();
        RefreshInspector();
        UpdateResponsiveLayout();
        _status.Text = isAssetLibrary
            ? "Bibliothèque intégrée · recherche, vignettes, aperçu 3D et import local"
            : isProjectConfiguration
            ? "Configuration transactionnelle · aperçu, application, annulation et sauvegarde séparés"
            : isDiagnostics
            ? "Messages structurés · cibles nommées · corrections prévisualisables · export expurgé"
            : usesWorld
            ? _session.ActiveArea is null
                ? "Projet sans zone · créez sa première zone depuis la configuration du projet"
                : $"{_session.ActiveArea.DisplayName} · {WorldViewport.EntityCount} objets affichés · vue 3D conservée"
            : $"{page.Title} · données réelles en lecture · édition planifiée";
        UpdateUx5APage(page.Id);
        if (notify)
        {
            NavigationChanged?.Invoke();
        }
    }

    private void UpdateProjectSectionHeader(StudioProjectFoundationSection section)
    {
        if (PageId != "module")
        {
            return;
        }

        var page = ResolvePage("module", "project");
        _breadcrumb.Text = section switch
        {
            StudioProjectFoundationSection.Events => "Projet / Événements du module",
            StudioProjectFoundationSection.Globals => "Projet / Données et guide joueur",
            _ => $"Projet / {page.Title}",
        };
        _pageTitle.Text = section switch
        {
            StudioProjectFoundationSection.Events => "Événements du module",
            StudioProjectFoundationSection.Globals => "Données et guide joueur",
            _ => page.Title,
        };
        _pageSubtitle.Text = section switch
        {
            StudioProjectFoundationSection.Events =>
                "Relier chargement, connexions, inventaire, utilisation et cycle de vie à des scripts nommés.",
            StudioProjectFoundationSection.Globals =>
                "Configurer les variables typées, vérifier le cache ForgeScript et écrire les huit pages localisées du guide joueur.",
            _ => page.Subtitle,
        };
        _primaryAction.TooltipText = section switch
        {
            StudioProjectFoundationSection.Events =>
                "Valider les liaisons d’événements sans modifier ni enregistrer le projet.",
            StudioProjectFoundationSection.Globals =>
                "Valider la surface de données active sans modifier ni enregistrer le projet.",
            _ => "Valider le formulaire sans modifier ni enregistrer le projet.",
        };
    }

    private void RebuildPageList()
    {
        ClearChildren(_pageList);
        _pageButtons.Clear();
        var filter = _search.Text.Trim();
        foreach (var page in StudioPageCatalog.GetSection(SectionId).Pages)
        {
            if (filter.Length > 0 &&
                !page.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !page.Subtitle.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var button = StudioStyle.Button(page.Title, compact: true);
            button.Alignment = HorizontalAlignment.Left;
            button.TooltipText = page.Subtitle;
            button.Pressed += () => SwitchPage(page.Id);
            _pageButtons[page.Id] = button;
            _pageList.AddChild(button);
        }
        RefreshPageStyles();
    }

    private void RefreshSectionStyles()
    {
        foreach (var pair in _sectionButtons)
        {
            var active = pair.Key == SectionId;
            pair.Value.AddThemeStyleboxOverride(
                "normal",
                StudioStyle.Box(active ? StudioStyle.AccentDark : StudioStyle.SurfaceRaised,
                    active ? StudioStyle.Accent : StudioStyle.Border,
                    7,
                    active ? 2 : 1));
            pair.Value.AddThemeColorOverride("font_color", active ? StudioStyle.Text : StudioStyle.Muted);
        }
        RefreshPageStyles();
    }

    private void RefreshPageStyles()
    {
        foreach (var pair in _pageButtons)
        {
            var active = pair.Key == PageId;
            pair.Value.AddThemeStyleboxOverride(
                "normal",
                StudioStyle.Box(active ? StudioStyle.Selection : StudioStyle.Surface,
                    active ? StudioStyle.Success : StudioStyle.Border,
                    7,
                    active ? 2 : 1));
            pair.Value.AddThemeColorOverride("font_color", active ? StudioStyle.Text : StudioStyle.Muted);
        }
    }

    private void SelectEntity(EntityId entityId)
    {
        var changed = _session.SelectedEntityId != entityId;
        if (!_session.SelectEntity(entityId))
        {
            return;
        }
        if (changed)
        {
            _entityEditorSection = null;
            _placementPosition = null;
        }
        WorldViewport.SetSelection(entityId);
        SyncExplorerSelection();
        RefreshInspector();
        SetStatus("Objet sélectionné · flèches pour déplacer · plans colorés pour tourner · clic droit pour éditer");
        NavigationChanged?.Invoke();
    }

    private void SelectEntities(IReadOnlyList<EntityId> entityIds, EntityId? activeEntityId)
    {
        if (!_session.SelectEntities(entityIds, activeEntityId))
        {
            WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
            SetStatus("Sélection multiple refusée par le profil actif.", error: true);
            return;
        }

        _entityEditorSection = null;
        _placementPosition = null;
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        SyncExplorerSelection();
        RefreshInspector();
        SetStatus(_session.SelectedEntityIds.Count switch
        {
            0 => "Sélection vidée",
            1 => "Objet sélectionné · Ctrl+clic pour ajouter à la sélection",
            _ => $"{_session.SelectedEntityIds.Count} objets sélectionnés · transformation atomique activée",
        });
        NavigationChanged?.Invoke();
    }

    private void ManipulateEntities(
        EditorTransformTool tool,
        Vector3Value value,
        EditorTransformAlignment alignment)
    {
        var result = _session.ManipulateSelectedEntities(tool, value, alignment);
        if (!result.Succeeded)
        {
            WorldViewport.Rebuild(_session, frameArea: false);
            WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
            SetStatus(result.Message, error: true);
            return;
        }

        WorldViewport.Rebuild(_session, frameArea: false);
        WorldViewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);
        WorldViewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void TransformEntity(EntityId entityId, Forge.Domain.Spatial.TransformValue transform)
    {
        var result = _session.TransformEntity(entityId, transform);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        WorldViewport.RefreshEntity(_session, entityId);
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · non enregistrée");
        NavigationChanged?.Invoke();
    }

    private void HandleEntityContextAction(EntityId entityId, StudioEntityContextAction action)
    {
        var preserveSelection = (action is StudioEntityContextAction.Copy or StudioEntityContextAction.Cut) &&
            _session.SelectedEntityIds.Contains(entityId);
        if (!preserveSelection && !_session.SelectEntity(entityId))
        {
            return;
        }

        if (action == StudioEntityContextAction.Copy)
        {
            CopySelection();
            return;
        }
        if (action == StudioEntityContextAction.Cut)
        {
            CutSelection();
            return;
        }

        if (action == StudioEntityContextAction.Duplicate)
        {
            DuplicateEntity(entityId);
            return;
        }
        if (action == StudioEntityContextAction.Delete)
        {
            RemoveEntity(entityId);
            return;
        }
        if (action == StudioEntityContextAction.FindReferences)
        {
            var entity = _session.ActiveArea!.Entities.First(item => item.Id == entityId);
            var blueprint = _session.Project!.Blueprints.FirstOrDefault(item => item.Id == entity.BlueprintId);
            ShowReferences(
                Forge.Application.Creator.CreatorContentKind.Entity,
                entityId.ToString(),
                blueprint?.DisplayName ?? "Objet sans blueprint");
            return;
        }

        _placementPosition = null;
        _entityEditorSection = action switch
        {
            StudioEntityContextAction.EditTransform => StudioEntityEditorSection.Transform,
            StudioEntityContextAction.EditProperties => StudioEntityEditorSection.Properties,
            StudioEntityContextAction.EditTags => StudioEntityEditorSection.Tags,
            StudioEntityContextAction.EditClickTrigger => StudioEntityEditorSection.ClickTrigger,
            _ => StudioEntityEditorSection.All,
        };
        _compactInspectorRequested = true;
        UpdateResponsiveLayout();
        RefreshInspector();
        SetStatus("Édition contextuelle ouverte dans l'inspecteur · aucune fenêtre supplémentaire");
    }

    private void HandlePrimaryAction()
    {
        if (PageId == "module")
        {
            _projectPage.PreviewActiveConfiguration();
            return;
        }
        if (PageId == "zones" && _session.ActiveArea is null)
        {
            OpenAreaCreation();
            return;
        }
        if (PageId == "zones")
        {
            BeginAddEntity(WorldViewport.SuggestedPlacementPosition);
            return;
        }
        if (PageId == "validation")
        {
            _diagnosticsPage.Refresh();
            SetStatus("Validation actualisée depuis le document canonique");
        }
    }

    private void BeginAddEntity(Vector3Value position)
    {
        if (StudioPageCatalog.GetPage(PageId).Layout == StudioPageLayout.Specialized)
        {
            return;
        }

        _placementPosition = position;
        _entityEditorSection = null;
        _compactInspectorRequested = true;
        UpdateResponsiveLayout();
        RefreshInspector();
        SetStatus("Choisissez un élément dans la palette · l'ajout restera annulable jusqu'à la sauvegarde");
    }

    private void PlaceInstalledAsset(BlueprintId blueprintId)
    {
        SwitchPage("zones");
        PlaceEntity(blueprintId, WorldViewport.SuggestedPlacementPosition);
    }

    private void PlaceEntity(BlueprintId blueprintId, Vector3Value position)
    {
        var result = _session.PlaceEntity(blueprintId, position);
        if (!result.Succeeded || result.EntityId is null)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        _placementPosition = null;
        _entityEditorSection = null;
        WorldViewport.Rebuild(_session, frameArea: false);
        WorldViewport.SetSelection(result.EntityId);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void PlaceRepeatedEntities(StudioRepeatedPlacementRequest request)
    {
        var result = _session.PlaceRepeatedEntities(request);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        var keepPalette = request.Profile.BrushMode && request.Profile.ContinuousStroke;
        if (!keepPalette)
        {
            _placementPosition = null;
            _entityEditorSection = null;
            WorldViewport.CancelPlacementTool(notify: false);
        }
        WorldViewport.Rebuild(_session, frameArea: false);
        if (!keepPalette)
        {
            WorldViewport.HidePlacementPreview();
        }
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · Ctrl+Z annule tout le lot · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void ArmPlacementTool(StudioRepeatedPlacementRequest request)
    {
        WorldViewport.ArmPlacementTool(request);
        SetStatus(request.Profile.BrushMode
            ? "Pinceau actif · maintenez le clic gauche pour tracer · relâchez pour appliquer"
            : "Placement actif · cliquez dans la vue 3D");
    }

    private void PasteClipboard(Vector3Value position, SemanticPasteMode mode)
    {
        var result = _session.PasteClipboard(position, mode);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        _placementPosition = null;
        _entityEditorSection = null;
        WorldViewport.CancelPlacementTool(notify: false);
        WorldViewport.Rebuild(_session, frameArea: false);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · Ctrl+Z pour annuler · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void CopySelection()
    {
        var result = _session.CopySelectedEntities();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded && _placementPosition is not null)
        {
            RefreshInspector();
        }
    }

    private void CutSelection()
    {
        var result = _session.CutSelectedEntities();
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }
        WorldViewport.Rebuild(_session, frameArea: false);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void DuplicateSelection()
    {
        var result = _session.DuplicateSelectedEntities();
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }
        WorldViewport.Rebuild(_session, frameArea: false);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · Ctrl+Z pour annuler · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void DuplicateEntity(EntityId entityId)
    {
        var result = _session.DuplicateEntity(entityId);
        if (!result.Succeeded || result.EntityId is null)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        _placementPosition = null;
        _entityEditorSection = null;
        WorldViewport.Rebuild(_session, frameArea: false);
        WorldViewport.SetSelection(result.EntityId);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void RemoveEntity(EntityId entityId)
    {
        var result = _session.RemoveEntity(entityId);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        _placementPosition = null;
        _entityEditorSection = null;
        WorldViewport.Rebuild(_session, frameArea: false);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message + " · non enregistré");
        NavigationChanged?.Invoke();
    }

    private void EntityEditApplied(EntityId entityId, bool rebuildVisual, string message)
    {
        if (rebuildVisual)
        {
            WorldViewport.Rebuild(_session, frameArea: false);
        }
        else
        {
            WorldViewport.RefreshEntity(_session, entityId);
        }
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(message + " · non enregistrée");
        NavigationChanged?.Invoke();
    }

    private void Undo()
    {
        ApplyHistoryResult(_session.Undo());
    }

    private void Redo()
    {
        ApplyHistoryResult(_session.Redo());
    }

    private void ApplyHistoryResult(StudioActionResult result)
    {
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }

        WorldViewport.Rebuild(_session, frameArea: false);
        RebuildExplorer();
        RefreshInspector();
        UpdateCommandState();
        SetStatus(result.Message);
        NavigationChanged?.Invoke();
    }

    private void Save()
    {
        var result = _session.Save();
        UpdateCommandState();
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void RefreshInspector()
    {
        ClearChildren(_inspectorContent);
        _inspectorPanel.CustomMinimumSize = new Vector2(280, 0);
        var page = StudioPageCatalog.GetPage(PageId);
        var area = _session.ActiveArea;
        if (area is null)
        {
            _inspectorTitle.Text = "Projet sans zone";
            AddInspectorValue("PROJET", _session.Project!.Module.DisplayName);
            AddInspectorValue("MODÈLE", _session.Project.Module.Metadata.Foundation?.Template switch
            {
                Forge.Domain.Projects.ProjectTemplateKind.CoreMenu => "Interface ou menu",
                Forge.Domain.Projects.ProjectTemplateKind.CoreSimulation => "Simulation",
                _ => "Aventure à compléter",
            });
            AddInspectorValue("PROCHAINE ACTION", "Ouvrez Identité & configuration pour vérifier le projet, puis ajoutez une zone lorsque le produit en a besoin.");
            return;
        }
        var selected = _session.SelectedEntityId is null
            ? null
            : area.Entities.FirstOrDefault(entity => entity.Id == _session.SelectedEntityId);

        if (_referenceTargetKind is not null && !string.IsNullOrWhiteSpace(_referenceTargetId))
        {
            BuildReferenceInspector();
            return;
        }
        if (_contextualHelpVisible)
        {
            BuildContextualHelpInspector(page, selected);
            return;
        }

        if (_placementPosition is not null)
        {
            _inspectorPanel.CustomMinimumSize = new Vector2(360, 0);
            _inspectorTitle.Text = "Ajouter un objet";
            var addInspector = new StudioAddEntityInspector(_session, _placementPosition.Value);
            addInspector.PlaceRequested += PlaceRepeatedEntities;
            addInspector.PlacementToolRequested += ArmPlacementTool;
            addInspector.PreviewChanged += WorldViewport.ShowPlacementPreview;
            addInspector.PasteRequested += PasteClipboard;
            addInspector.StatusChanged += SetStatus;
            addInspector.Cancelled += () =>
            {
                _placementPosition = null;
                WorldViewport.CancelPlacementTool(notify: false);
                WorldViewport.HidePlacementPreview();
                RefreshInspector();
                SetStatus("Ajout annulé · le module n'a pas été modifié");
            };
            _inspectorContent.AddChild(addInspector);
            addInspector.PublishPreview();
            return;
        }

        if (!WorldViewport.IsPlacementToolArmed)
        {
            WorldViewport.HidePlacementPreview();
        }

        var multiSelection = area.Entities
            .Where(entity => _session.SelectedEntityIds.Contains(entity.Id))
            .ToArray();
        if (multiSelection.Length > 1)
        {
            _inspectorPanel.CustomMinimumSize = new Vector2(330, 0);
            _inspectorTitle.Text = $"{multiSelection.Length} objets sélectionnés";
            AddInspectorValue("ZONE", area.DisplayName);
            AddInspectorValue(
                "OBJET ACTIF",
                selected is null
                    ? "Dernier objet de la sélection"
                    : _session.Project!.Blueprints.FirstOrDefault(item => item.Id == selected.BlueprintId)?.DisplayName ??
                      "Objet sans blueprint");
            var commonTags = multiSelection
                .Select(entity => entity.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase))
                .Aggregate((left, right) =>
                {
                    left.IntersectWith(right);
                    return left;
                });
            AddInspectorValue(
                "PROPRIÉTÉS COMMUNES",
                commonTags.Count == 0
                    ? "Transformations communes · aucun tag partagé"
                    : "Tags partagés · " + string.Join(", ", commonTags.Order(StringComparer.OrdinalIgnoreCase)));
            var transform = _session.WorkspaceProfile.Shell.DirectManipulation.Transform;
            AddInspectorValue(
                "GIZMO ACTIF",
                $"{TransformToolLabel(transform.Tool)} · {CoordinateSpaceLabel(transform.CoordinateSpace)} · " +
                $"pivot {PivotModeLabel(transform.PivotMode)}");
            var focus = StudioStyle.Button("Cadrer toute la sélection", compact: true);
            focus.Pressed += WorldViewport.FocusSelection;
            _inspectorContent.AddChild(focus);
            var alignGround = StudioStyle.Button("Aligner au sol", compact: true);
            alignGround.Pressed += () => ManipulateEntities(
                EditorTransformTool.Translate,
                Vector3Value.Zero,
                EditorTransformAlignment.Ground);
            _inspectorContent.AddChild(alignGround);
            var alignX = StudioStyle.Button("Aligner sur le bord gauche", compact: true);
            alignX.Pressed += () => ManipulateEntities(
                EditorTransformTool.Translate,
                Vector3Value.Zero,
                EditorTransformAlignment.MinimumX);
            _inspectorContent.AddChild(alignX);
            var tools = StudioStyle.Button("Ouvrir les outils 3D", primary: true);
            tools.Pressed += OpenViewportTools;
            _inspectorContent.AddChild(tools);
            _inspectorContent.AddChild(StudioStyle.Label(
                "Ctrl+clic ajoute ou retire un objet. Toutes les transformations de ce groupe sont prévisualisées, atomiques et annulables.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }

        if (selected is not null)
        {
            var blueprint = _session.Project!.Blueprints.FirstOrDefault(item => item.Id == selected.BlueprintId);
            _inspectorTitle.Text = blueprint?.DisplayName ?? "Objet sans blueprint";
            if (_entityEditorSection is not null)
            {
                _inspectorPanel.CustomMinimumSize = new Vector2(360, 0);
                var editor = new StudioEntityInspector(_session, selected.Id, _entityEditorSection.Value);
                editor.Applied += EntityEditApplied;
                editor.Failed += message => SetStatus(message, error: true);
                _inspectorContent.AddChild(editor);
                var close = StudioStyle.Button("Revenir au résumé", compact: true);
                close.Pressed += () =>
                {
                    _entityEditorSection = null;
                    RefreshInspector();
                };
                _inspectorContent.AddChild(close);
                return;
            }

            AddInspectorValue("TYPE", blueprint is null ? "Inconnu" : BlueprintKindLabel(blueprint.Kind));
            AddInspectorValue("ZONE", area.DisplayName);
            AddInspectorValue("POSITION",
                $"X {selected.Transform.Position.X:0.##} · Y {selected.Transform.Position.Y:0.##} · Z {selected.Transform.Position.Z:0.##}");
            AddInspectorValue("TAGS", selected.Tags.Count == 0 ? "Aucun" : string.Join(", ", selected.Tags));
            var clickTrigger = Forge.Domain.Entities.EntityClickTriggerSettings.Read(selected.Overrides);
            AddInspectorValue(
                "CLIC GAUCHE DANS LE JEU",
                clickTrigger.Enabled
                    ? clickTrigger.Action switch
                    {
                        Forge.Domain.Entities.EntityClickTriggerAction.ShowMessage => "Afficher un message",
                        Forge.Domain.Entities.EntityClickTriggerAction.StartDialogue => "Lancer un dialogue",
                        _ => "Afficher / masquer un objet",
                    }
                    : "Désactivé");
            var click = StudioStyle.Button("Configurer le déclencheur au clic", compact: true);
            click.Pressed += () =>
            {
                _entityEditorSection = StudioEntityEditorSection.ClickTrigger;
                RefreshInspector();
            };
            _inspectorContent.AddChild(click);
            var references = StudioStyle.Button("Trouver les références", compact: true);
            references.TooltipText = "Afficher les éléments du module qui utilisent cet objet.";
            references.Pressed += () => ShowReferences(
                Forge.Application.Creator.CreatorContentKind.Entity,
                selected.Id.ToString(),
                blueprint?.DisplayName ?? "Objet sans blueprint");
            _inspectorContent.AddChild(references);
            var edit = StudioStyle.Button("Modifier tous les paramètres", primary: true);
            edit.TooltipText = "Même éditeur que le menu contextuel du clic droit.";
            edit.Pressed += () =>
            {
                _entityEditorSection = StudioEntityEditorSection.All;
                RefreshInspector();
            };
            _inspectorContent.AddChild(edit);
            _inspectorContent.AddChild(StudioStyle.Label(
                "Dans la vue 3D : maintenez une flèche X, Y ou Z pour déplacer. Maintenez un plan coloré entre deux axes pour tourner. Clic droit pour modifier, dupliquer, ajouter à côté ou supprimer.",
                10,
                StudioStyle.Muted,
                wrap: true));
            return;
        }

        _inspectorTitle.Text = page.RightPanelTitle;
        AddInspectorValue("PAGE", page.Title);
        if (page.Layout == StudioPageLayout.Specialized)
        {
            AddInspectorValue("MODULE", _session.Project!.Module.DisplayName);
            AddInspectorValue("RESSOURCES", ResourceCount(page.Id).ToString());
            AddInspectorValue("MODE", "Document");
        }
        else
        {
            AddInspectorValue("ZONE ACTIVE", area.DisplayName);
            AddInspectorValue("OBJETS", area.Entities.Count.ToString());
            AddInspectorValue("MODE", "Vue 3D permanente");
        }
        _inspectorContent.AddChild(StudioStyle.Label(
            page.Layout == StudioPageLayout.Specialized
                ? "Choisissez une ressource à gauche pour afficher ici son contexte, ses dépendances et sa validation."
                : "Clic droit sur le sol pour ajouter un objet, ou sélectionnez un objet pour le modifier.",
            11,
            StudioStyle.Muted,
            wrap: true));
    }

    private int ResourceCount(string pageId) => pageId switch
    {
        "dialogues" => _session.Project!.Dialogues.Count,
        "quests" => _session.Project!.Quests.Count,
        "scripts" => Directory.EnumerateFiles(
            _session.Session!.RootDirectory,
            "*.forge",
            SearchOption.AllDirectories).Count(),
        "packages" => _session.AssetManifests.Count,
        _ => 1,
    };

    private static string BlueprintKindLabel(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Placeable => "Objet de décor",
        BlueprintKind.Door => "Porte",
        BlueprintKind.Creature => "Créature",
        BlueprintKind.PlayerSpawn => "Départ joueur",
        BlueprintKind.Trigger => "Déclencheur",
        BlueprintKind.Item => "Objet d’inventaire",
        _ => "Objet",
    };

    private static string TransformToolLabel(EditorTransformTool tool) => tool switch
    {
        EditorTransformTool.Rotate => "Rotation",
        EditorTransformTool.Scale => "Échelle",
        _ => "Translation",
    };

    private static string CoordinateSpaceLabel(EditorCoordinateSpace space) =>
        space == EditorCoordinateSpace.Local ? "repère local" : "repère monde";

    private static string PivotModeLabel(EditorPivotMode pivot) => pivot switch
    {
        EditorPivotMode.Individual => "individuel",
        EditorPivotMode.ActiveObject => "objet actif",
        _ => "centre de la sélection",
    };

    private void AddInspectorValue(string name, string value)
    {
        _inspectorContent.AddChild(StudioStyle.SectionLabel(name));
        var field = StudioStyle.Panel("InspectorValue", StudioStyle.Surface, 7);
        field.AddChild(StudioStyle.Label(value, 11, StudioStyle.Text, wrap: true));
        _inspectorContent.AddChild(field);
    }

    private void UpdateCommandState()
    {
        _undoButton.Disabled = !_session.CanUndo;
        _redoButton.Disabled = !_session.CanRedo;
        _saveButton.Disabled = !_session.IsDirty;
        _saveState.Text = _session.IsDirty
            ? "● MODIFICATIONS NON ENREGISTRÉES"
            : "● MODULE ENREGISTRÉ";
        _saveState.AddThemeColorOverride(
            "font_color",
            _session.IsDirty ? StudioStyle.Warning : StudioStyle.Success);
        _saveSummary.Text = _session.IsDirty
            ? "Modifications locales · Ctrl+S ou Enregistrer"
            : "Toutes les modifications sont enregistrées";
        _saveSummary.AddThemeColorOverride(
            "font_color",
            _session.IsDirty ? StudioStyle.Warning : StudioStyle.Success);
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride(
            "font_color",
            error ? StudioStyle.Danger : StudioStyle.Muted);
        _session.RecordStudioStatus(message, error);
        if (_diagnosticsPage is not null && _diagnosticsPage.Visible)
        {
            _diagnosticsPage.Refresh();
        }
    }

    private void UpdateResponsiveLayout()
    {
        var compact = Size.X > 0 && Size.X < CompactWidth;
        var assetLibrary = PageId == "assets";
        var fullPageForm = assetLibrary || PageId is "module" or "validation";
        var layout = _session.WorkspaceProfile.Shell.Layout;
        var navigationVisible = layout.ExplorerVisible || layout.PaletteVisible;
        var contextVisible = layout.InspectorVisible || layout.DiagnosticsVisible || layout.AssistantVisible;
        _navigationPanel.Visible = navigationVisible;
        _navigationPanel.CustomMinimumSize = new Vector2(layout.ExplorerWidth, 0);
        _inspectorPanel.CustomMinimumSize = new Vector2(layout.ContextWidth, 0);
        _inspectorToggle.Visible = compact && !fullPageForm;
        _inspectorPanel.Visible = !fullPageForm && contextVisible && (!compact || _compactInspectorRequested);
        _inspectorToggle.Text = _inspectorPanel.Visible ? "Masquer l'inspecteur" : "Inspecteur";
        _saveState.Visible = !compact;
        _saveButton.Text = compact ? "Sauver" : "Enregistrer";
        _globalSearchButton.Text = compact ? "Ctrl+K" : "Rechercher · Ctrl+K";
        _workspaceButton.Text = compact ? "Espace" : "Espace de travail";
        _messagesButton.Text = compact ? "Messages" : "Messages et état";
    }

    private static StudioSectionDefinition ResolveSection(string id)
    {
        try
        {
            return StudioPageCatalog.GetSection(id);
        }
        catch (KeyNotFoundException)
        {
            return StudioPageCatalog.Sections[0];
        }
    }

    private static StudioPageDefinition ResolvePage(string id, string sectionId)
    {
        try
        {
            return StudioPageCatalog.GetPage(id);
        }
        catch (KeyNotFoundException)
        {
            return StudioPageCatalog.GetSection(sectionId).Pages[0];
        }
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
