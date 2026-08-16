using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceSettingsOverlay : Control
{
    private readonly StudioSessionController _session;
    private readonly List<EditorWorkspaceProfile> _profiles = [];
    private readonly List<AreaId?> _comparisonAreas = [];
    private readonly List<EditorCommandBinding> _visibleBindings = [];
    private readonly OptionButton _profile;
    private readonly LineEdit _profileName;
    private readonly Label _profileSummary;
    private readonly OptionButton _layoutPreset;
    private readonly CheckBox _explorerVisible;
    private readonly CheckBox _inspectorVisible;
    private readonly CheckBox _paletteVisible;
    private readonly CheckBox _diagnosticsVisible;
    private readonly CheckBox _assistantVisible;
    private readonly OptionButton _comparison;
    private readonly OptionButton _keymapPreset;
    private readonly LineEdit _keymapSearch;
    private readonly ItemList _commands;
    private readonly Label _commandTitle;
    private readonly LineEdit _gesture;
    private readonly Label _validation;
    private readonly Label _status;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Button _saveButton;
    private EditorWorkspaceProfile _draftWorkspace;
    private EditorKeymapProfile _draftKeymap;
    private string? _previewFingerprint;
    private bool _refreshing;

    internal event Action? PreferencesChanged;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;

    internal StudioWorkspaceSettingsOverlay(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _draftWorkspace = session.WorkspaceProfile;
        _draftKeymap = session.KeymapProfile;
        Name = "StudioWorkspaceSettingsOverlay";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        ZIndex = 90;

        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, .76f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var frame = StudioStyle.Panel("WorkspaceSettingsFrame", StudioStyle.Chrome, 12, 1);
        frame.AnchorLeft = .045f;
        frame.AnchorTop = .055f;
        frame.AnchorRight = .955f;
        frame.AnchorBottom = .945f;
        AddChild(frame);
        var margin = StudioStyle.Margin(16, 14, 16, 14);
        frame.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("Organiser l’espace de travail", 20));
        heading.AddChild(StudioStyle.Label(
            "Profils nommés, panneaux, zones et raccourcis · aperçu obligatoire avant application",
            11,
            StudioStyle.Muted));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer · Échap", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);

        var profiles = BuildColumn(body, "PROFIL & PANNEAUX", 330);
        profiles.AddChild(StudioStyle.Label("Profil enregistré", 11, StudioStyle.Muted));
        _profile = new OptionButton();
        StudioStyle.ApplyOptionButton(_profile);
        _profile.ItemSelected += SelectProfile;
        profiles.AddChild(_profile);
        _profileName = new LineEdit { PlaceholderText = "Nom du profil" };
        StudioStyle.ApplyLineEdit(_profileName);
        _profileName.TextChanged += _ => InvalidatePreview();
        profiles.AddChild(_profileName);
        _profileSummary = StudioStyle.Label(string.Empty, 11, StudioStyle.Muted, wrap: true);
        profiles.AddChild(_profileSummary);
        profiles.AddChild(new HSeparator());
        profiles.AddChild(StudioStyle.Label("Disposition", 11, StudioStyle.Muted));
        _layoutPreset = new OptionButton();
        StudioStyle.ApplyOptionButton(_layoutPreset);
        _layoutPreset.AddItem("Équilibrée");
        _layoutPreset.AddItem("Priorité à la 3D");
        _layoutPreset.AddItem("Diagnostic");
        _layoutPreset.ItemSelected += SelectLayoutPreset;
        profiles.AddChild(_layoutPreset);
        _explorerVisible = AddCheck(profiles, "Explorateur", LayoutChanged);
        _inspectorVisible = AddCheck(profiles, "Inspecteur", LayoutChanged);
        _paletteVisible = AddCheck(profiles, "Palette", LayoutChanged);
        _diagnosticsVisible = AddCheck(profiles, "Diagnostics", LayoutChanged);
        _assistantVisible = AddCheck(profiles, "Assistant", LayoutChanged);
        profiles.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var workspaceHistory = new HBoxContainer();
        var undoWorkspace = StudioStyle.Button("Annuler profil", compact: true);
        undoWorkspace.Pressed += UndoWorkspace;
        workspaceHistory.AddChild(undoWorkspace);
        var redoWorkspace = StudioStyle.Button("Rétablir", compact: true);
        redoWorkspace.Pressed += RedoWorkspace;
        workspaceHistory.AddChild(redoWorkspace);
        profiles.AddChild(workspaceHistory);

        var zones = BuildColumn(body, "ZONES OUVERTES", 300);
        zones.AddChild(StudioStyle.Label(
            "Les onglets gardent leur sélection et leur caméra. La comparaison reste en lecture seule.",
            11,
            StudioStyle.Muted,
            wrap: true));
        _comparison = new OptionButton();
        StudioStyle.ApplyOptionButton(_comparison);
        _comparison.ItemSelected += _ => InvalidatePreview();
        zones.AddChild(StudioStyle.Label("Comparer avec", 11, StudioStyle.Muted));
        zones.AddChild(_comparison);
        var zoneHint = StudioStyle.Panel("ZoneHint", StudioStyle.Surface, 8, 1);
        var zoneMargin = StudioStyle.Margin(10, 10, 10, 10);
        zoneHint.AddChild(zoneMargin);
        zoneMargin.AddChild(StudioStyle.Label(
            "Ouvrez et fermez les zones depuis la barre d’onglets au-dessus de la vue 3D. " +
            "Jusqu’à huit documents peuvent rester ouverts dans le même projet.",
            11,
            StudioStyle.Text,
            wrap: true));
        zones.AddChild(zoneHint);
        zones.AddChild(new HSeparator());
        zones.AddChild(StudioStyle.Label("Rôles disponibles", 11, StudioStyle.Muted));
        zones.AddChild(StudioStyle.Label(
            "Bâtisseur · Auteur · Scripteur · Artiste · MJ · Testeur · Débutant",
            11,
            StudioStyle.Text,
            wrap: true));
        zones.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        zones.AddChild(StudioStyle.Label(
            "Les préférences restent dans .forge et ne modifient jamais le module exporté.",
            10,
            StudioStyle.Success,
            wrap: true));

        var shortcuts = BuildColumn(body, "RACCOURCIS", 500);
        var presetRow = new HBoxContainer();
        presetRow.AddChild(StudioStyle.Label("Preset", 11, StudioStyle.Muted));
        _keymapPreset = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_keymapPreset);
        foreach (var preset in Enum.GetValues<EditorKeymapPreset>())
        {
            _keymapPreset.AddItem(PresetName(preset));
        }
        _keymapPreset.ItemSelected += SelectKeymapPreset;
        presetRow.AddChild(_keymapPreset);
        shortcuts.AddChild(presetRow);
        _keymapSearch = new LineEdit { PlaceholderText = "Rechercher une commande…", ClearButtonEnabled = true };
        StudioStyle.ApplyLineEdit(_keymapSearch);
        _keymapSearch.TextChanged += _ => RefreshBindings();
        shortcuts.AddChild(_keymapSearch);
        _commands = new ItemList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 230),
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _commands.ItemSelected += ShowBinding;
        shortcuts.AddChild(_commands);
        _commandTitle = StudioStyle.Label("Sélectionnez une commande", 13);
        shortcuts.AddChild(_commandTitle);
        var gestureRow = new HBoxContainer();
        _gesture = new LineEdit
        {
            PlaceholderText = "Ctrl+Shift+K, F6, Delete…",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplyLineEdit(_gesture);
        gestureRow.AddChild(_gesture);
        var assign = StudioStyle.Button("Attribuer", primary: true, compact: true);
        assign.Pressed += RebindSelected;
        gestureRow.AddChild(assign);
        shortcuts.AddChild(gestureRow);
        _validation = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        shortcuts.AddChild(_validation);
        var keymapHistory = new HBoxContainer();
        var undoKeymap = StudioStyle.Button("Annuler raccourci", compact: true);
        undoKeymap.Pressed += UndoKeymap;
        keymapHistory.AddChild(undoKeymap);
        var redoKeymap = StudioStyle.Button("Rétablir", compact: true);
        redoKeymap.Pressed += RedoKeymap;
        keymapHistory.AddChild(redoKeymap);
        shortcuts.AddChild(keymapHistory);

        _status = StudioStyle.Label(string.Empty, 11, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        footer.AddChild(StudioStyle.Label(
            "1 Prévisualiser  ·  2 Appliquer  ·  3 Enregistrer",
            11,
            StudioStyle.Muted));
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _previewButton = StudioStyle.Button("Prévisualiser", compact: true);
        _previewButton.Pressed += Preview;
        footer.AddChild(_previewButton);
        _applyButton = StudioStyle.Button("Appliquer", primary: true, compact: true);
        _applyButton.Disabled = true;
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        _saveButton = StudioStyle.Button("Enregistrer explicitement", primary: true, compact: true);
        _saveButton.Disabled = true;
        _saveButton.Pressed += Save;
        footer.AddChild(_saveButton);
        root.AddChild(footer);
    }

    internal void Open()
    {
        _draftWorkspace = _session.WorkspaceProfile;
        _draftKeymap = _session.KeymapProfile;
        _previewFingerprint = null;
        RefreshProfiles();
        LoadWorkspaceControls();
        LoadKeymapControls();
        _status.Text = "Modifiez une préférence, puis prévisualisez son diff avant application.";
        _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _applyButton.Disabled = true;
        _saveButton.Disabled = !(_session.WorkspaceProfileHasUnsavedChanges || _session.KeymapHasUnsavedChanges);
        Visible = true;
        MoveToFront();
        _profile.GrabFocus();
    }

    internal void Close()
    {
        Visible = false;
        _previewFingerprint = null;
    }

    internal bool PrepareEvidence(string mode)
    {
        Open();
        if (string.Equals(mode, "ux0-b-shortcuts", StringComparison.OrdinalIgnoreCase))
        {
            _keymapSearch.Text = "recherche";
            RefreshBindings(EditorKeymapCatalog.OpenSearch);
        }
        else if (string.Equals(mode, "ux0-b-conflict", StringComparison.OrdinalIgnoreCase))
        {
            _keymapSearch.Text = "recherche";
            RefreshBindings(EditorKeymapCatalog.OpenSearch);
            _gesture.Text = _draftKeymap.Bindings
                .First(binding => binding.CommandId == EditorKeymapCatalog.SaveProject)
                .Gesture.CanonicalText;
            RebindSelected();
        }
        else if (string.Equals(mode, "ux0-b-profile", StringComparison.OrdinalIgnoreCase))
        {
            _profileName.Text = "Bâtisseur diagnostic";
            _layoutPreset.Select(2);
            SelectLayoutPreset(2);
            Preview();
        }
        return IsOpen && _profiles.Count > 0 &&
            _draftKeymap.Bindings.Count == EditorKeymapCatalog.Commands.Count;
    }

    private void RefreshProfiles()
    {
        _refreshing = true;
        _profiles.Clear();
        _profiles.AddRange(_session.WorkspaceProfiles);
        if (_profiles.All(profile => profile.Id != _draftWorkspace.Id))
        {
            _profiles.Add(_draftWorkspace);
        }
        _profiles.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCulture));
        _profile.Clear();
        var selected = 0;
        for (var index = 0; index < _profiles.Count; index++)
        {
            var profile = _profiles[index];
            _profile.AddItem($"{profile.Name} · {RoleName(profile.Role)}");
            if (profile.Id == _draftWorkspace.Id)
            {
                selected = index;
            }
        }
        _profile.Select(selected);
        _refreshing = false;
    }

    private void SelectProfile(long index)
    {
        if (_refreshing || index < 0 || index >= _profiles.Count)
        {
            return;
        }

        _draftWorkspace = EditorWorkspaceProfilePlanner.ReconcileAreas(
            _profiles[(int)index],
            _session.Project!.Areas.Select(area => area.Id).ToArray(),
            _session.ActiveAreaId);
        LoadWorkspaceControls();
        InvalidatePreview();
        SetStatus($"Profil « {_draftWorkspace.Name} » chargé dans le brouillon", error: false);
    }

    private void LoadWorkspaceControls()
    {
        _refreshing = true;
        _profileName.Text = _draftWorkspace.Name;
        _profileSummary.Text = $"{RoleName(_draftWorkspace.Role)} · " +
            $"{_draftWorkspace.RolePolicy.VisiblePanels.Count} panneaux · " +
            $"{_draftWorkspace.OpenAreaTabs.Count} zone(s) ouverte(s)";
        var layout = _draftWorkspace.Shell.Layout;
        _layoutPreset.Select(layout.ExplorerWidth >= 280 || layout.ContextWidth >= 340 ? 2 :
            layout.ExplorerWidth <= 200 || !layout.InspectorVisible ? 1 : 0);
        _explorerVisible.ButtonPressed = layout.ExplorerVisible;
        _inspectorVisible.ButtonPressed = layout.InspectorVisible;
        _paletteVisible.ButtonPressed = layout.PaletteVisible;
        _diagnosticsVisible.ButtonPressed = layout.DiagnosticsVisible;
        _assistantVisible.ButtonPressed = layout.AssistantVisible;
        RefreshComparisonChoices();
        _refreshing = false;
    }

    private void RefreshComparisonChoices()
    {
        _comparisonAreas.Clear();
        _comparison.Clear();
        _comparisonAreas.Add(null);
        _comparison.AddItem("Aucune · une seule vue 3D");
        var selected = 0;
        foreach (var areaId in _draftWorkspace.OpenAreaTabs.Where(areaId => areaId != _draftWorkspace.ActiveAreaId))
        {
            var area = _session.Project!.Areas.FirstOrDefault(candidate => candidate.Id == areaId);
            if (area is null)
            {
                continue;
            }
            _comparisonAreas.Add(area.Id);
            _comparison.AddItem(area.DisplayName);
            if (area.Id == _draftWorkspace.ComparisonAreaId)
            {
                selected = _comparisonAreas.Count - 1;
            }
        }
        _comparison.Select(selected);
    }

    private void SelectLayoutPreset(long index)
    {
        if (_refreshing)
        {
            return;
        }
        _refreshing = true;
        switch (index)
        {
            case 1:
                _explorerVisible.ButtonPressed = true;
                _inspectorVisible.ButtonPressed = false;
                _paletteVisible.ButtonPressed = false;
                _diagnosticsVisible.ButtonPressed = false;
                _assistantVisible.ButtonPressed = true;
                break;
            case 2:
                _explorerVisible.ButtonPressed = true;
                _inspectorVisible.ButtonPressed = true;
                _paletteVisible.ButtonPressed = true;
                _diagnosticsVisible.ButtonPressed = true;
                _assistantVisible.ButtonPressed = true;
                break;
            default:
                _explorerVisible.ButtonPressed = true;
                _inspectorVisible.ButtonPressed = true;
                _paletteVisible.ButtonPressed = true;
                _diagnosticsVisible.ButtonPressed = true;
                _assistantVisible.ButtonPressed = true;
                break;
        }
        _refreshing = false;
        InvalidatePreview();
    }

    private void LayoutChanged()
    {
        if (!_refreshing)
        {
            InvalidatePreview();
        }
    }

    private EditorWorkspaceProfile BuildWorkspaceCandidate()
    {
        var source = _draftWorkspace;
        var (explorerWidth, contextWidth, bottomHeight) = _layoutPreset.Selected switch
        {
            1 => (188, 240, 100),
            2 => (300, 360, 180),
            _ => (228, 280, 120),
        };
        var layout = new EditorShellLayout(
            source.Shell.Layout.Id,
            EditorShellLayout.CurrentSchemaVersion,
            "studio:ux0-b-layout",
            explorerWidth,
            contextWidth,
            bottomHeight,
            _explorerVisible.ButtonPressed,
            _inspectorVisible.ButtonPressed,
            _diagnosticsVisible.ButtonPressed,
            _paletteVisible.ButtonPressed,
            _assistantVisible.ButtonPressed);
        var shell = new EditorShellConfiguration(
            source.Shell.Id,
            EditorShellConfiguration.CurrentSchemaVersion,
            "studio:ux0-b-layout",
            layout,
            source.Shell.ExplorerGrouping,
            source.Shell.ViewportMode,
            source.Shell.PaletteFilter,
            source.Shell.DirectManipulation);
        var comparisonId = _comparison.Selected >= 0 && _comparison.Selected < _comparisonAreas.Count
            ? _comparisonAreas[_comparison.Selected]
            : null;
        var withComparison = EditorWorkspaceProfilePlanner.SetComparison(source, comparisonId);
        var multiArea = new EditorMultiAreaProfile(
            source.MultiArea.Id,
            EditorMultiAreaProfile.CurrentSchemaVersion,
            "studio:ux0-b-layout",
            splitViewEnabled: comparisonId is not null,
            source.MultiArea.SplitAxis,
            source.MultiArea.SplitRatio,
            comparisonReadOnly: true,
            source.MultiArea.CrossAreaCopyEnabled);
        var productivity = EditorWorkspaceProfilePlanner.ConfigureProductivity(
            withComparison,
            withComparison.DisplayFilter,
            withComparison.RepeatedPlacementProfile,
            multiArea,
            withComparison.Help,
            withComparison.RolePolicy);
        return EditorWorkspaceProfilePlanner.ConfigureShell(productivity, shell);
    }

    private void LoadKeymapControls()
    {
        _refreshing = true;
        _keymapPreset.Select((int)_draftKeymap.Preset);
        _refreshing = false;
        RefreshBindings();
    }

    private void SelectKeymapPreset(long index)
    {
        if (_refreshing || index < 0 || index >= Enum.GetValues<EditorKeymapPreset>().Length)
        {
            return;
        }
        _draftKeymap = EditorKeymapPlanner.UsePreset(
            _draftKeymap,
            (EditorKeymapPreset)index,
            "studio:ux0-b-keymap");
        RefreshBindings();
        InvalidatePreview();
    }

    private void RefreshBindings(string? preferredCommandId = null)
    {
        var selectedId = preferredCommandId ?? SelectedBinding()?.CommandId;
        _visibleBindings.Clear();
        _visibleBindings.AddRange(EditorKeymapPlanner.Search(_draftKeymap, _keymapSearch.Text));
        _commands.Clear();
        foreach (var binding in _visibleBindings)
        {
            _commands.AddItem($"{binding.DisplayName}    {binding.Gesture.CanonicalText}");
            _commands.SetItemTooltip(_commands.ItemCount - 1, binding.Category);
        }
        var selected = _visibleBindings.FindIndex(binding => binding.CommandId == selectedId);
        if (selected < 0 && _visibleBindings.Count > 0)
        {
            selected = 0;
        }
        if (selected >= 0)
        {
            _commands.Select(selected);
            ShowBinding(selected);
        }
        else
        {
            _commandTitle.Text = "Aucune commande trouvée";
            _gesture.Text = string.Empty;
        }
        RefreshValidation();
    }

    private void ShowBinding(long index)
    {
        if (index < 0 || index >= _visibleBindings.Count)
        {
            return;
        }
        var binding = _visibleBindings[(int)index];
        _commandTitle.Text = $"{binding.DisplayName} · {binding.Category}";
        _gesture.Text = binding.Gesture.CanonicalText;
    }

    private void RebindSelected()
    {
        var binding = SelectedBinding();
        if (binding is null || !EditorKeyGesture.TryParse(_gesture.Text, out var gesture) || gesture is null)
        {
            SetStatus("Raccourci invalide · exemple attendu : Ctrl+Shift+K, F6 ou Delete.", error: true);
            return;
        }
        _draftKeymap = EditorKeymapPlanner.Rebind(
            _draftKeymap,
            binding.CommandId,
            gesture,
            "studio:ux0-b-keymap");
        RefreshBindings(binding.CommandId);
        InvalidatePreview();
        var diagnostics = EditorKeymapProfileValidator.Validate(_draftKeymap);
        SetStatus(diagnostics.Count == 0
            ? "Attribution ajoutée au brouillon · prévisualisez avant application."
            : "Conflit détecté · corrigez l’attribution avant de prévisualiser.",
            diagnostics.Count > 0);
    }

    private void RefreshValidation()
    {
        var diagnostics = EditorKeymapProfileValidator.Validate(_draftKeymap);
        if (diagnostics.Count == 0)
        {
            _validation.Text = $"✓ {_draftKeymap.Bindings.Count} commandes · aucun conflit";
            _validation.AddThemeColorOverride("font_color", StudioStyle.Success);
            _previewButton.Disabled = false;
            return;
        }
        _validation.Text = string.Join("\n", diagnostics.Take(3).Select(diagnostic =>
            diagnostic.Code == EditorKeymapProfileValidator.ConflictCode
                ? "Conflit · une touche est attribuée à plusieurs commandes"
                : diagnostic.Message));
        _validation.AddThemeColorOverride("font_color", StudioStyle.Danger);
        _previewButton.Disabled = true;
    }

    private void Preview()
    {
        EditorWorkspaceProfile candidate;
        try
        {
            candidate = BuildWorkspaceCandidate();
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            SetStatus($"Disposition invalide · {exception.Message}", error: true);
            return;
        }
        var workspace = _session.PreviewWorkspaceProfile(candidate);
        var keymap = _session.PreviewKeymap(_draftKeymap);
        if (!workspace.Succeeded || !keymap.Succeeded)
        {
            SetStatus(!workspace.Succeeded ? workspace.Message : keymap.Message, error: true);
            _applyButton.Disabled = true;
            return;
        }
        _draftWorkspace = candidate;
        _previewFingerprint = Fingerprint(candidate, _draftKeymap);
        _applyButton.Disabled = false;
        SetStatus($"✓ Dry-run valide · {workspace.Message} · {keymap.Message}", error: false);
    }

    private void Apply()
    {
        EditorWorkspaceProfile candidate;
        try
        {
            candidate = BuildWorkspaceCandidate();
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            SetStatus($"Disposition invalide · {exception.Message}", error: true);
            return;
        }
        if (_previewFingerprint != Fingerprint(candidate, _draftKeymap))
        {
            SetStatus("Le brouillon a changé · relancez la prévisualisation.", error: true);
            _applyButton.Disabled = true;
            return;
        }
        var workspace = _session.ApplyWorkspaceProfile(candidate);
        if (!workspace.Succeeded)
        {
            SetStatus(workspace.Message, error: true);
            return;
        }
        var keymap = _session.ApplyKeymap(_draftKeymap);
        if (!keymap.Succeeded)
        {
            _session.UndoWorkspaceProfile(persist: false);
            SetStatus(keymap.Message, error: true);
            return;
        }
        _draftWorkspace = _session.WorkspaceProfile;
        _draftKeymap = _session.KeymapProfile;
        _applyButton.Disabled = true;
        _saveButton.Disabled = false;
        PreferencesChanged?.Invoke();
        SetStatus("✓ Préférences appliquées en mémoire · utilisez Enregistrer pour les conserver.", error: false);
    }

    private void Save()
    {
        var requestedName = _profileName.Text.Trim();
        var workspace = requestedName.Length > 0 &&
                        !string.Equals(requestedName, _session.WorkspaceProfile.Name, StringComparison.Ordinal)
            ? _session.SaveNamedWorkspaceProfile(requestedName)
            : _session.SaveWorkspaceProfile();
        if (!workspace.Succeeded)
        {
            SetStatus(workspace.Message, error: true);
            return;
        }
        var keymap = _session.SaveKeymap();
        if (!keymap.Succeeded)
        {
            SetStatus($"Disposition enregistrée, mais raccourcis non enregistrés · {keymap.Message}", error: true);
            return;
        }
        _draftWorkspace = _session.WorkspaceProfile;
        _draftKeymap = _session.KeymapProfile;
        RefreshProfiles();
        _saveButton.Disabled = true;
        PreferencesChanged?.Invoke();
        SetStatus($"✓ {workspace.Message} · {keymap.Message}", error: false);
    }

    private void UndoWorkspace()
    {
        var result = _session.UndoWorkspaceProfile(persist: false);
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _draftWorkspace = _session.WorkspaceProfile;
        LoadWorkspaceControls();
        InvalidatePreview();
        PreferencesChanged?.Invoke();
    }

    private void RedoWorkspace()
    {
        var result = _session.RedoWorkspaceProfile(persist: false);
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _draftWorkspace = _session.WorkspaceProfile;
        LoadWorkspaceControls();
        InvalidatePreview();
        PreferencesChanged?.Invoke();
    }

    private void UndoKeymap()
    {
        var result = _session.UndoKeymap();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _draftKeymap = _session.KeymapProfile;
        LoadKeymapControls();
        InvalidatePreview();
    }

    private void RedoKeymap()
    {
        var result = _session.RedoKeymap();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            return;
        }
        _draftKeymap = _session.KeymapProfile;
        LoadKeymapControls();
        InvalidatePreview();
    }

    private void InvalidatePreview()
    {
        _previewFingerprint = null;
        _applyButton.Disabled = true;
        _saveButton.Disabled = !(_session.WorkspaceProfileHasUnsavedChanges || _session.KeymapHasUnsavedChanges);
    }

    private EditorCommandBinding? SelectedBinding()
    {
        var selected = _commands.GetSelectedItems();
        return selected.Length > 0 && selected[0] >= 0 && selected[0] < _visibleBindings.Count
            ? _visibleBindings[selected[0]]
            : null;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private static CheckBox AddCheck(VBoxContainer parent, string text, Action changed)
    {
        var check = new CheckBox { Text = text };
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        check.Toggled += _ => changed();
        parent.AddChild(check);
        return check;
    }

    private static VBoxContainer BuildColumn(HBoxContainer parent, string heading, float minimumWidth)
    {
        var panel = StudioStyle.Panel(heading.Replace(' ', '_'), StudioStyle.Surface, 9, 1);
        panel.CustomMinimumSize = new Vector2(minimumWidth, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(11, 10, 11, 10);
        panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel(heading));
        return column;
    }

    private static string Fingerprint(EditorWorkspaceProfile workspace, EditorKeymapProfile keymap) =>
        string.Join('|',
            workspace.Id.Value,
            workspace.Name,
            workspace.Shell.Layout.ExplorerWidth,
            workspace.Shell.Layout.ContextWidth,
            workspace.Shell.Layout.BottomHeight,
            workspace.Shell.Layout.ExplorerVisible,
            workspace.Shell.Layout.InspectorVisible,
            workspace.Shell.Layout.DiagnosticsVisible,
            workspace.Shell.Layout.PaletteVisible,
            workspace.Shell.Layout.AssistantVisible,
            workspace.ComparisonAreaId,
            keymap.Preset,
            string.Join(';', keymap.Bindings.Select(binding =>
                $"{binding.CommandId}={binding.Gesture.CanonicalText}")));

    private static string PresetName(EditorKeymapPreset preset) => preset switch
    {
        EditorKeymapPreset.Aurora => "Aurora",
        EditorKeymapPreset.Blender => "Blender",
        EditorKeymapPreset.Custom => "Personnalisé",
        _ => "Standard",
    };

    private static string RoleName(EditorRole role) => role switch
    {
        EditorRole.Builder => "Bâtisseur",
        EditorRole.Writer => "Auteur",
        EditorRole.Scripter => "Scripteur",
        EditorRole.Artist => "Artiste",
        EditorRole.GameMaster => "MJ",
        EditorRole.Tester => "Testeur",
        _ => "Débutant",
    };
}
