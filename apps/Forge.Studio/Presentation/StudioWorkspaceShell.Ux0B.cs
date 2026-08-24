using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private readonly Dictionary<AreaId, StudioViewportCameraState> _areaCameras = [];
    private readonly List<AreaId> _availableAreaIds = [];
    private readonly List<AreaId?> _comparisonAreaIds = [];
    private PanelContainer _navigationPanel = null!;
    private BoxContainer _worldSplit = null!;
    private HBoxContainer _areaTabs = null!;
    private OptionButton _openAreaMenu = null!;
    private OptionButton _comparisonMenu = null!;
    private Button _workspaceButton = null!;
    private StudioWorkspaceSettingsOverlay _workspaceSettingsOverlay = null!;
    private bool _refreshingAreaMenus;

    internal StudioWorldViewport ComparisonViewport { get; private set; } = null!;

    private Control BuildAreaTabs()
    {
        var panel = StudioStyle.Panel("AreaDocumentTabs", StudioStyle.Chrome, 0, 0);
        panel.CustomMinimumSize = new Vector2(0, 38);
        var margin = StudioStyle.Margin(8, 4, 8, 4);
        panel.AddChild(margin);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        margin.AddChild(row);
        row.AddChild(StudioStyle.SectionLabel("ZONES"));
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        row.AddChild(scroll);
        _areaTabs = new HBoxContainer();
        _areaTabs.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_areaTabs);
        _openAreaMenu = new OptionButton { CustomMinimumSize = new Vector2(154, 0) };
        StudioStyle.ApplyOptionButton(_openAreaMenu);
        _openAreaMenu.ItemSelected += OpenAreaFromMenu;
        row.AddChild(_openAreaMenu);
        _comparisonMenu = new OptionButton { CustomMinimumSize = new Vector2(188, 0) };
        StudioStyle.ApplyOptionButton(_comparisonMenu);
        _comparisonMenu.ItemSelected += SelectComparison;
        row.AddChild(_comparisonMenu);
        return panel;
    }

    private void InitializeUx0BOverlays()
    {
        _workspaceSettingsOverlay = new StudioWorkspaceSettingsOverlay(_session);
        _workspaceSettingsOverlay.PreferencesChanged += ApplyWorkspacePreferences;
        _workspaceSettingsOverlay.StatusChanged += SetStatus;
        AddChild(_workspaceSettingsOverlay);
    }

    private void OpenWorkspaceSettings()
    {
        _globalSearchOverlay.Close();
        _displayFilterOverlay.Close();
        _workspaceSettingsOverlay.Open();
    }

    private void ApplyWorkspacePreferences()
    {
        PopulateAreas();
        RefreshAreaTabs();
        RebuildActiveArea("Profil appliqué · zone, sélection et caméra restaurées", notify: false);
        RefreshComparisonViewport();
        RebuildExplorer();
        RefreshFilterSummary();
        UpdateResponsiveLayout();
        NavigationChanged?.Invoke();
    }

    private void RefreshAreaTabs()
    {
        ClearChildren(_areaTabs);
        foreach (var areaId in _session.WorkspaceProfile.OpenAreaTabs)
        {
            var area = _session.Project!.Areas.FirstOrDefault(candidate => candidate.Id == areaId);
            if (area is null)
            {
                continue;
            }
            var group = new HBoxContainer();
            group.AddThemeConstantOverride("separation", 1);
            var active = area.Id == _session.ActiveAreaId;
            var tab = StudioStyle.Button(area.DisplayName, compact: true);
            tab.TooltipText = active
                ? $"Zone active · {area.Entities.Count} objets"
                : $"Activer sans perdre la caméra · {area.Entities.Count} objets";
            tab.AddThemeStyleboxOverride(
                "normal",
                StudioStyle.Box(
                    active ? StudioStyle.Selection : StudioStyle.Surface,
                    active ? StudioStyle.Success : StudioStyle.Border,
                    6,
                    active ? 2 : 1));
            tab.Pressed += () => ActivateArea(area.Id);
            group.AddChild(tab);
            var close = StudioStyle.Button("×", compact: true);
            close.TooltipText = $"Fermer l’onglet {area.DisplayName}";
            close.Disabled = _session.WorkspaceProfile.OpenAreaTabs.Count == 1;
            close.Pressed += () => CloseArea(area.Id);
            group.AddChild(close);
            _areaTabs.AddChild(group);
        }
        RefreshAreaMenus();
    }

    private void RefreshAreaMenus()
    {
        _refreshingAreaMenus = true;
        _availableAreaIds.Clear();
        _openAreaMenu.Clear();
        _openAreaMenu.AddItem("Ouvrir une zone…");
        _openAreaMenu.SetItemDisabled(0, true);
        foreach (var area in _session.Project!.Areas.Where(area =>
                     !_session.WorkspaceProfile.OpenAreaTabs.Contains(area.Id)))
        {
            _availableAreaIds.Add(area.Id);
            _openAreaMenu.AddItem(area.DisplayName);
        }
        _openAreaMenu.Select(0);
        _openAreaMenu.Disabled = _availableAreaIds.Count == 0;

        _comparisonAreaIds.Clear();
        _comparisonMenu.Clear();
        _comparisonAreaIds.Add(null);
        _comparisonMenu.AddItem("Comparer…");
        var selected = 0;
        foreach (var areaId in _session.WorkspaceProfile.OpenAreaTabs.Where(areaId => areaId != _session.ActiveAreaId))
        {
            var area = _session.Project.Areas.FirstOrDefault(candidate => candidate.Id == areaId);
            if (area is null)
            {
                continue;
            }
            _comparisonAreaIds.Add(area.Id);
            _comparisonMenu.AddItem(area.DisplayName + " · lecture seule");
            if (area.Id == _session.WorkspaceProfile.ComparisonAreaId)
            {
                selected = _comparisonAreaIds.Count - 1;
            }
        }
        _comparisonMenu.Select(selected);
        _comparisonMenu.Disabled = _comparisonAreaIds.Count == 1;
        _refreshingAreaMenus = false;
    }

    private void OpenAreaFromMenu(long index)
    {
        if (_refreshingAreaMenus || index <= 0 || index - 1 >= _availableAreaIds.Count)
        {
            return;
        }
        var areaId = _availableAreaIds[(int)index - 1];
        ActivateArea(areaId);
    }

    private void ActivateArea(AreaId areaId)
    {
        RememberCamera();
        var result = _session.ActivateAreaTab(areaId, persist: true);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }
        PopulateAreas();
        RebuildActiveArea(result.Message);
    }

    private void CloseArea(AreaId areaId)
    {
        RememberCamera();
        var result = _session.CloseAreaTab(areaId, persist: true);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }
        _areaCameras.Remove(areaId);
        PopulateAreas();
        RebuildActiveArea(result.Message);
    }

    private void SelectComparison(long index)
    {
        if (_refreshingAreaMenus || index < 0 || index >= _comparisonAreaIds.Count)
        {
            return;
        }
        var result = _session.ConfigureAreaComparison(_comparisonAreaIds[(int)index], persist: true);
        if (!result.Succeeded)
        {
            SetStatus(result.Message, error: true);
            return;
        }
        RefreshAreaMenus();
        RefreshComparisonViewport();
        SetStatus(result.Message);
        NavigationChanged?.Invoke();
    }

    private void RememberCamera()
    {
        if (WorldViewport.ViewAreaId is not null)
        {
            _areaCameras[WorldViewport.ViewAreaId] = WorldViewport.CaptureCameraState();
        }
    }

    private void RebuildActiveArea(string message, bool notify = true)
    {
        _entityEditorSection = null;
        _placementPosition = null;
        var areaId = _session.ActiveAreaId!;
        var restoreCamera = _areaCameras.TryGetValue(areaId, out var camera);
        WorldViewport.Rebuild(_session, frameArea: !restoreCamera);
        if (camera is not null)
        {
            WorldViewport.RestoreCameraState(camera);
        }
        WorldViewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);
        WorldViewport.SetSelection(_session.SelectedEntityId);
        RebuildExplorer();
        RefreshInspector();
        RefreshAreaTabs();
        RefreshComparisonViewport();
        SetStatus(message);
        if (notify)
        {
            NavigationChanged?.Invoke();
        }
    }

    private void RefreshComparisonViewport()
    {
        var profile = _session.WorkspaceProfile;
        var comparison = profile.MultiArea.SplitViewEnabled ? profile.ComparisonAreaId : null;
        if (comparison is null)
        {
            ComparisonViewport.Visible = false;
            WorldViewport.SizeFlagsStretchRatio = 1;
            return;
        }

        _worldSplit.Vertical = profile.MultiArea.SplitAxis == EditorSplitAxis.Vertical;
        WorldViewport.SizeFlagsStretchRatio = profile.MultiArea.SplitRatio;
        ComparisonViewport.SizeFlagsStretchRatio = 1f - profile.MultiArea.SplitRatio;
        ComparisonViewport.Visible = true;
        ComparisonViewport.RebuildArea(_session, comparison, readOnly: true);
        ComparisonViewport.SetDisplayFilter(profile.DisplayFilter);
    }

    private bool TryExecuteKeymap(InputEventKey key)
    {
        var binding = _session.KeymapProfile.Resolve(
            key.Keycode.ToString(),
            key.CtrlPressed,
            key.AltPressed,
            key.ShiftPressed,
            key.MetaPressed);
        if (binding is null)
        {
            return false;
        }

        switch (binding.CommandId)
        {
            case EditorKeymapCatalog.SaveProject:
                Save();
                break;
            case EditorKeymapCatalog.Undo:
                Undo();
                break;
            case EditorKeymapCatalog.Redo:
                Redo();
                break;
            case EditorKeymapCatalog.DuplicateSelection when _session.SelectedEntityIds.Count > 0:
                DuplicateSelection();
                break;
            case EditorKeymapCatalog.DeleteSelection when _session.SelectedEntityId is not null:
                RemoveEntity(_session.SelectedEntityId);
                break;
            case EditorKeymapCatalog.FocusSelection:
                WorldViewport.FocusSelection();
                SetStatus("Sélection cadrée par le raccourci configuré");
                break;
            case EditorKeymapCatalog.OpenSearch:
                OpenGlobalSearch();
                break;
            case EditorKeymapCatalog.OpenWorld:
                SwitchPage("zones");
                break;
            case EditorKeymapCatalog.OpenBlueprints:
                SwitchPage("blueprints");
                break;
            case EditorKeymapCatalog.OpenDialogues:
                SwitchPage("dialogues");
                break;
            case EditorKeymapCatalog.CopySelection:
                CopySelection();
                break;
            case EditorKeymapCatalog.CutSelection:
                CutSelection();
                break;
            case EditorKeymapCatalog.PasteSelection:
                PasteClipboard(WorldViewport.SuggestedPlacementPosition, SemanticPasteMode.Instance);
                break;
            case EditorKeymapCatalog.RunPlaytest:
                LaunchRuntimePlaytestUx7G();
                break;
            default:
                return false;
        }
        return true;
    }

    internal bool RunUx0BSmoke()
    {
        var originalWorkspace = _session.WorkspaceProfile;
        var originalKeymap = _session.KeymapProfile;
        var areas = _session.Project!.Areas.Take(3).ToArray();
        if (areas.Length < 3)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX0B_FAILED reason=areas_missing");
            return false;
        }

        var openSecond = _session.OpenAreaTab(areas[1].Id, activate: true, persist: false);
        var secondEntity = _session.ActiveArea!.Entities.FirstOrDefault();
        var secondSelected = secondEntity is not null && _session.SelectEntity(secondEntity.Id);
        var openThird = _session.OpenAreaTab(areas[2].Id, activate: false, persist: false);
        var activateFirst = _session.ActivateAreaTab(areas[0].Id, persist: false);
        var firstEntity = _session.ActiveArea!.Entities.FirstOrDefault();
        if (firstEntity is not null)
        {
            _session.SelectEntity(firstEntity.Id);
        }
        var restoreSecond = _session.ActivateAreaTab(areas[1].Id, persist: false);
        var selectionRestored = secondEntity is not null && _session.SelectedEntityId == secondEntity.Id;
        var comparison = _session.ConfigureAreaComparison(areas[2].Id, persist: false);
        RefreshComparisonViewport();
        var comparisonRendered = ComparisonViewport.Visible && ComparisonViewport.IsReadOnly &&
                                 ComparisonViewport.ViewAreaId == areas[2].Id &&
                                 ComparisonViewport.EntityCount > 0;
        var tabsValid = _session.WorkspaceProfile.OpenAreaTabs.Count == 3 &&
                        _session.WorkspaceProfile.OpenAreaTabs.Distinct().Count() == 3;

        var current = _session.WorkspaceProfile;
        var layout = new EditorShellLayout(
            current.Shell.Layout.Id,
            EditorShellLayout.CurrentSchemaVersion,
            "studio:ux0-b-smoke",
            300,
            360,
            180,
            explorerVisible: true,
            inspectorVisible: true,
            diagnosticsVisible: true,
            paletteVisible: true,
            assistantVisible: true);
        var shell = new EditorShellConfiguration(
            current.Shell.Id,
            EditorShellConfiguration.CurrentSchemaVersion,
            "studio:ux0-b-smoke",
            layout,
            current.Shell.ExplorerGrouping,
            current.Shell.ViewportMode,
            current.Shell.PaletteFilter,
            current.Shell.DirectManipulation);
        var workspaceCandidate = EditorWorkspaceProfilePlanner.ConfigureShell(current, shell);
        var workspacePreview = _session.PreviewWorkspaceProfile(workspaceCandidate);
        var workspaceApply = _session.ApplyWorkspaceProfile(workspaceCandidate);
        var workspaceUndo = _session.UndoWorkspaceProfile(persist: false);
        var workspaceRedo = _session.RedoWorkspaceProfile(persist: false);

        var keymapCandidate = EditorKeymapPlanner.Rebind(
            originalKeymap,
            EditorKeymapCatalog.OpenSearch,
            new EditorKeyGesture("K", ctrl: true, shift: true),
            "studio:ux0-b-smoke");
        var keymapPreview = _session.PreviewKeymap(keymapCandidate);
        var keymapApply = _session.ApplyKeymap(keymapCandidate);
        var keymapResolved = _session.KeymapProfile.Resolve("K", ctrl: true, alt: false, shift: true, meta: false)
            ?.CommandId == EditorKeymapCatalog.OpenSearch;
        var conflictCandidate = EditorKeymapPlanner.Rebind(
            _session.KeymapProfile,
            EditorKeymapCatalog.SaveProject,
            new EditorKeyGesture("K", ctrl: true, shift: true),
            "studio:ux0-b-smoke-conflict");
        var conflictBlocked = !_session.PreviewKeymap(conflictCandidate).Succeeded;
        var keymapUndo = _session.UndoKeymap();
        var keymapRedo = _session.RedoKeymap();

        var roundTrip = VerifyUx0BRoundTrip(workspaceCandidate, keymapCandidate);
        _session.ApplyWorkspaceProfile(originalWorkspace);
        _session.ApplyKeymap(originalKeymap);
        RefreshAreaTabs();
        RebuildActiveArea("UX0-B smoke restauré", notify: false);

        var valid = openSecond.Succeeded && secondSelected && openThird.Succeeded && activateFirst.Succeeded &&
            restoreSecond.Succeeded && selectionRestored && comparison.Succeeded && comparisonRendered && tabsValid &&
            _session.WorkspaceProfile.Id == originalWorkspace.Id &&
            workspacePreview.Succeeded && workspaceApply.Succeeded && workspaceUndo.Succeeded && workspaceRedo.Succeeded &&
            keymapPreview.Succeeded && keymapApply.Succeeded && keymapResolved && conflictBlocked &&
            keymapUndo.Succeeded && keymapRedo.Succeeded && roundTrip;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0B_{(valid ? "PASS" : "FAILED")} " +
            $"tabs=3 selection_restored={selectionRestored.ToString().ToLowerInvariant()} " +
            $"comparison_read_only={comparisonRendered.ToString().ToLowerInvariant()} " +
            $"layout_dry_run={workspacePreview.Succeeded.ToString().ToLowerInvariant()} " +
            $"workspace_undo={workspaceUndo.Succeeded.ToString().ToLowerInvariant()} " +
            $"keymap_commands={keymapCandidate.Bindings.Count} conflict_blocked={conflictBlocked.ToString().ToLowerInvariant()} " +
            $"keymap_undo={keymapUndo.Succeeded.ToString().ToLowerInvariant()} roundtrip={roundTrip.ToString().ToLowerInvariant()} " +
            "windows=1 persisted=false");
        return valid;
    }

    internal bool PrepareUx0BEvidence(string mode)
    {
        if (string.Equals(mode, "ux0-b-tabs", StringComparison.OrdinalIgnoreCase))
        {
            var areas = _session.Project!.Areas.Take(3).ToArray();
            foreach (var area in areas)
            {
                _session.OpenAreaTab(area.Id, activate: area == areas[0], persist: false);
            }
            _session.ConfigureAreaComparison(areas[1].Id, persist: false);
            PopulateAreas();
            RebuildActiveArea("Trois zones ouvertes · comparaison protégée", notify: false);
            return _session.WorkspaceProfile.OpenAreaTabs.Count >= 3 && ComparisonViewport.Visible;
        }
        return _workspaceSettingsOverlay.PrepareEvidence(mode);
    }

    private static bool VerifyUx0BRoundTrip(
        EditorWorkspaceProfile workspace,
        EditorKeymapProfile keymap)
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux0b-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            EditorWorkspaceProfileStore.Save(root, workspace);
            var keymapSave = EditorKeymapProfileStore.Save(root, keymap);
            var workspaceReloaded = EditorWorkspaceProfileStore.TryLoad(root);
            var keymapReloaded = EditorKeymapProfileStore.Load(root);
            return keymapSave.Succeeded && workspaceReloaded is not null && keymapReloaded.Succeeded &&
                   workspaceReloaded.Shell.Layout.ExplorerWidth == workspace.Shell.Layout.ExplorerWidth &&
                   workspaceReloaded.OpenAreaTabs.SequenceEqual(workspace.OpenAreaTabs) &&
                   keymapReloaded.Profile.Resolve("K", ctrl: true, alt: false, shift: true, meta: false)
                       ?.CommandId == EditorKeymapCatalog.OpenSearch;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
