#if TOOLS
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Godot;

namespace Forge.Toolset.Editor;

public partial class CreatorProductivityWindow : Window
{
    private Func<ProjectState?>? _getState;
    private Func<EditorWorkspaceProfile>? _load;
    private Action<EditorWorkspaceProfile>? _save;
    private Func<EditorDisplayFilter, int>? _applyFilter;
    private Func<EditorRepeatedPlacementProfile, int, bool>? _place;
    private Func<AreaId, AreaId, bool>? _copySelection;
    private Action<AreaId, AreaId, EditorSplitAxis>? _openSplit;
    private Action<Window>? _brand;
    private EditorWorkspaceProfileCommandHistory? _history;
    private readonly List<Guid> _layerIds = [];
    private readonly List<AreaId> _areaIds = [];
    private ItemList? _layers;
    private ItemList? _volumes;
    private CheckBox? _brush;
    private CheckBox? _continuous;
    private OptionButton? _distribution;
    private OptionButton? _alignment;
    private SpinBox? _spacing;
    private SpinBox? _count;
    private SpinBox? _surfaceOffset;
    private OptionButton? _activeArea;
    private OptionButton? _comparisonArea;
    private CheckBox? _split;
    private OptionButton? _splitAxis;
    private SpinBox? _splitRatio;
    private LineEdit? _helpQuery;
    private CheckBox? _assisted;
    private RichTextLabel? _helpResults;
    private OptionButton? _role;
    private RichTextLabel? _roleSummary;
    private Label? _status;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;

    public void Initialize(Func<ProjectState?> getState, Func<EditorWorkspaceProfile> load,
        Action<EditorWorkspaceProfile> save, Func<EditorDisplayFilter, int> applyFilter,
        Func<EditorRepeatedPlacementProfile, int, bool> place,
        Func<AreaId, AreaId, bool> copySelection,
        Action<AreaId, AreaId, EditorSplitAxis> openSplit, Action<Window> brand)
    {
        _getState = getState; _load = load; _save = save; _applyFilter = applyFilter; _place = place;
        _copySelection = copySelection; _openSplit = openSplit; _brand = brand;
        Title = "Productivité Creator · OpenRPG Forge";
        MinSize = new Vector2I(1760, 900); Size = MinSize; Transient = true; Exclusive = false;
        Unresizable = true; Visible = false;
        BuildUi();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r059-shell-productivity-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _capturePath = capture["--r059-shell-productivity-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1.5)
        {
            Open(); PrepareCapture(); _captureOpened = true; return;
        }
        if (!_captureOpened || _captureDelay < 4) return;
        if (string.IsNullOrWhiteSpace(_capturePath))
        {
            GD.PushError("OPENRPG_FORGE_R059_SHELL_PRODUCTIVITY_UI_CAPTURE_FAILED reason=empty-path");
            _capturePath = null; SetProcess(false); GetTree().Quit(1); return;
        }
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_R059_SHELL_PRODUCTIVITY_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null; SetProcess(false); GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh(); PopupCentered(MinSize); _brand?.Invoke(this);
    }

    private void BuildUi()
    {
        var panel = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        panel.AddThemeStyleboxOverride("panel", Card("0d0915", "62458a")); AddChild(panel);
        var margin = new MarginContainer(); foreach (var side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 22); panel.AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "PRODUCTIVITÉ CREATOR · PROFILS CANONIQUES", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Filtrer, peindre, comparer deux zones, consulter l’aide sourcée et adapter l’interface à chaque métier." });
        var top = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; top.AddThemeConstantOverride("separation", 12); root.AddChild(top);
        var filters = Column(top, "FILTRES · CALQUES & VOLUMES");
        filters.AddChild(new Label { Text = "Calques visibles (aucun = tous)" });
        _layers = new ItemList { SelectMode = ItemList.SelectModeEnum.Multi, CustomMinimumSize = new Vector2(0, 104) }; filters.AddChild(_layers);
        filters.AddChild(new Label { Text = "Familles de volumes" });
        _volumes = new ItemList { SelectMode = ItemList.SelectModeEnum.Multi, CustomMinimumSize = new Vector2(0, 82) };
        foreach (var kind in Enum.GetValues<EditorVolumeKind>()) _volumes.AddItem(kind.ToString()); filters.AddChild(_volumes);
        var applyFilter = new Button { Text = "Appliquer au viewport", TooltipText = "Combine ces facettes avec type, tag, auteur et état du profil" };
        applyFilter.Pressed += ApplyFilter; filters.AddChild(applyFilter);

        var placement = Column(top, "PINCEAU · RÉPÉTITION & ALIGNEMENT");
        _brush = new CheckBox { Text = "Mode pinceau" }; placement.AddChild(_brush);
        _continuous = new CheckBox { Text = "Trait continu" }; placement.AddChild(_continuous);
        _distribution = new OptionButton(); foreach (var value in Enum.GetValues<EditorPlacementDistribution>()) _distribution.AddItem(value.ToString()); placement.AddChild(Row("Distribution", _distribution));
        _alignment = new OptionButton(); foreach (var value in Enum.GetValues<EditorPlacementAlignment>()) _alignment.AddItem(value.ToString()); placement.AddChild(Row("Alignement", _alignment));
        _spacing = Spin(.05, 128, 2, .05); _count = Spin(1, 256, 6, 1); _surfaceOffset = Spin(-16, 16, 0, .05);
        placement.AddChild(Row("Espacement", _spacing)); placement.AddChild(Row("Instances", _count)); placement.AddChild(Row("Offset surface", _surfaceOffset));
        var previewPlacement = new Button { Text = "Prévisualiser le plan" }; previewPlacement.Pressed += PreviewPlacement; placement.AddChild(previewPlacement);
        var place = new Button { Text = "Placer · une seule annulation" }; place.Pressed += Place; placement.AddChild(place);

        var areas = Column(top, "MULTI-ZONES · VUE PARTAGÉE");
        _activeArea = new OptionButton(); _comparisonArea = new OptionButton(); areas.AddChild(Row("Zone active", _activeArea)); areas.AddChild(Row("Comparaison", _comparisonArea));
        _split = new CheckBox { Text = "Activer la vue partagée" }; areas.AddChild(_split);
        _splitAxis = new OptionButton(); foreach (var value in Enum.GetValues<EditorSplitAxis>()) _splitAxis.AddItem(value.ToString()); areas.AddChild(Row("Division", _splitAxis));
        _splitRatio = Spin(.25, .75, .5, .05); areas.AddChild(Row("Ratio", _splitRatio));
        areas.AddChild(new Label { Text = "La gauche reste la cible active ; la droite est protégée en lecture seule.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var openSplit = new Button { Text = "Ouvrir les deux viewports 3D" }; openSplit.Pressed += OpenSplit; areas.AddChild(openSplit);
        var copy = new Button { Text = "Copier la sélection vers la comparaison" }; copy.Pressed += CopySelection; areas.AddChild(copy);

        var bottom = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; bottom.AddThemeConstantOverride("separation", 12); root.AddChild(bottom);
        var help = Column(bottom, "AIDE CONTEXTUELLE · LOCALE & SOURCÉE");
        _helpQuery = new LineEdit { PlaceholderText = "Rechercher propriété, dépendance, exemple…", Text = "placement" }; help.AddChild(_helpQuery);
        _assisted = new CheckBox { Text = "Synthèse assistée · citations obligatoires", ButtonPressed = true }; help.AddChild(_assisted);
        var search = new Button { Text = "Rechercher dans la documentation intégrée" }; search.Pressed += SearchHelp; help.AddChild(search);
        _helpResults = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 92) }; help.AddChild(_helpResults);

        var roles = Column(bottom, "PROFILS MÉTIER · PANNEAUX & PERMISSIONS");
        _role = new OptionButton(); foreach (var value in Enum.GetValues<EditorRole>()) _role.AddItem(RoleName(value)); _role.ItemSelected += _ => RefreshRoleSummary(); roles.AddChild(Row("Profil actif", _role));
        _roleSummary = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 92) }; roles.AddChild(_roleSummary);
        roles.AddChild(new Label { Text = "Les profils restent locaux à .forge/editor-profiles.json et ne modifient pas le module exporté.", AutowrapMode = TextServer.AutowrapMode.WordSmart });

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; _status.AddThemeColorOverride("font_color", new Color("79d5ad")); root.AddChild(_status);
        var footer = new HBoxContainer(); var undo = new Button { Text = "Annuler profil" }; undo.Pressed += Undo; footer.AddChild(undo);
        var redo = new Button { Text = "Rétablir profil" }; redo.Pressed += Redo; footer.AddChild(redo);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var save = new Button { Text = "Enregistrer le profil" }; save.Pressed += Save; footer.AddChild(save);
        var close = new Button { Text = "Fermer" }; close.Pressed += Hide; footer.AddChild(close); root.AddChild(footer);
    }

    private void Refresh()
    {
        var state = _getState?.Invoke(); if (state is null) return;
        var profile = _load!.Invoke();
        if (_history is null || _history.Current.Id != profile.Id) _history = new(profile);
        _layerIds.Clear(); _layers!.Clear();
        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
            foreach (var layer in area.Authoring.Layers)
            { _layerIds.Add(layer.Id); _layers.AddItem($"{area.DisplayName} / {layer.Name}"); }
        if (_layerIds.Count == 0)
        {
            _layers.AddItem("Aucun calque nommé · toutes les entités restent visibles");
            _layers.SetItemDisabled(0, true);
        }
        for (var i = 0; i < _layerIds.Count; i++) if (profile.DisplayFilter.VisibleLayerIds.Contains(_layerIds[i])) _layers.Select(i, false);
        _volumes!.DeselectAll(); for (var i = 0; i < _volumes.ItemCount; i++) if (profile.DisplayFilter.VisibleVolumeKinds.Contains((EditorVolumeKind)i)) _volumes.Select(i, false);
        var repeat = profile.RepeatedPlacementProfile; _brush!.ButtonPressed = repeat.BrushMode; _continuous!.ButtonPressed = repeat.ContinuousStroke;
        _distribution!.Selected = (int)repeat.Distribution; _alignment!.Selected = (int)repeat.Alignment; _spacing!.Value = repeat.Spacing; _surfaceOffset!.Value = repeat.SurfaceOffset;
        _split!.ButtonPressed = profile.MultiArea.SplitViewEnabled; _splitAxis!.Selected = (int)profile.MultiArea.SplitAxis; _splitRatio!.Value = profile.MultiArea.SplitRatio;
        _areaIds.Clear(); _activeArea!.Clear(); _comparisonArea!.Clear();
        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        { _areaIds.Add(area.Id); _activeArea.AddItem(area.DisplayName); _comparisonArea.AddItem(area.DisplayName); }
        SelectArea(_activeArea, profile.ActiveAreaId); SelectArea(_comparisonArea, profile.ComparisonAreaId);
        _role!.Selected = (int)profile.Role; _assisted!.ButtonPressed = profile.Help.SourcedAssistantEnabled;
        RefreshRoleSummary(); SearchHelp(); _status!.Text = "✓ Profil R059 chargé · UUID, schéma et provenance valides";
    }

    private EditorWorkspaceProfile BuildCandidate()
    {
        var existing = _load!.Invoke();
        var role = (EditorRole)_role!.Selected;
        var identity = role == existing.Role ? existing : EditorWorkspaceProfile.CreateDefault(role,
            SelectedArea(_activeArea!) ?? existing.ActiveAreaId);
        var filter = new EditorDisplayFilter(existing.DisplayFilter.VisibleKinds, existing.DisplayFilter.RequiredTags,
            existing.DisplayFilter.Author, existing.DisplayFilter.State, existing.DisplayFilter.IsolateSelection,
            existing.DisplayFilter.GhostHidden, _layers!.GetSelectedItems().Select(i => _layerIds[i]).ToArray(),
            _volumes!.GetSelectedItems().Select(i => (EditorVolumeKind)i).ToArray(), identity.DisplayFilter.Id,
            EditorDisplayFilter.CurrentSchemaVersion, "creator-r059-ui");
        var repeated = new EditorRepeatedPlacementProfile(identity.RepeatedPlacementProfile.Id, 1, "creator-r059-ui", true,
            _brush!.ButtonPressed, _continuous!.ButtonPressed, (EditorPlacementDistribution)_distribution!.Selected,
            (EditorPlacementAlignment)_alignment!.Selected, (float)_spacing!.Value, 4, 2026, (float)_surfaceOffset!.Value);
        var multi = new EditorMultiAreaProfile(identity.MultiArea.Id, 1, "creator-r059-ui", _split!.ButtonPressed,
            (EditorSplitAxis)_splitAxis!.Selected, (float)_splitRatio!.Value, true, true);
        var help = new EditorContextualHelpProfile(identity.Help.Id, 1, "creator-r059-ui", true,
            _assisted!.ButtonPressed, existing.Help.Locale, existing.Help.ActiveTopicKey);
        var active = SelectedArea(_activeArea!); var comparison = SelectedArea(_comparisonArea!);
        var tabs = existing.OpenAreaTabs.Concat(new[] { active, comparison }.OfType<AreaId>()).Distinct().Take(8).ToArray();
        return new(identity.Id, RoleName(role), role, filter, true, tabs, active, comparison, true,
            "creator-r059-ui", EditorWorkspaceProfile.CurrentSchemaVersion, existing.UiScale, existing.TextScale,
            existing.PrimaryFontFamily, existing.FallbackFontFamilies, existing.PersonalFavoriteBlueprintIds,
            existing.RecentBlueprintIds, existing.Shell, repeated, multi, help,
            EditorRolePolicy.CreateDefault(identity.Id.Value, role));
    }

    private void ApplyFilter() { var candidate = BuildCandidate(); var count = _applyFilter?.Invoke(candidate.DisplayFilter) ?? 0; _status!.Text = $"✓ Filtre canonique appliqué · {count} entité(s) visible(s)"; }
    private void PreviewPlacement() { var plan = RepeatedPlacementPlanner.Plan(BuildCandidate().RepeatedPlacementProfile, new(0, 0, 0), (int)_count!.Value); _status!.Text = $"✓ Plan valide · {plan.Poses.Count} poses · {plan.Poses[0].Alignment} · pinceau={plan.BrushMode}"; }
    private void Place() { var profile = BuildCandidate(); if (!profile.RolePolicy.Allows(EditorPermission.PlaceEntities)) { _status!.Text = "Placement refusé · permission PlaceEntities absente"; return; } _status!.Text = _place?.Invoke(profile.RepeatedPlacementProfile, (int)_count!.Value) == true ? "✓ Placement atomique appliqué · undo groupé" : "Placement refusé · sélection, surface ou collision invalide"; }
    private void OpenSplit() { var a = SelectedArea(_activeArea!); var b = SelectedArea(_comparisonArea!); if (a is null || b is null || a == b) { _status!.Text = "Vue partagée refusée · choisissez deux zones différentes"; return; } _openSplit?.Invoke(a, b, (EditorSplitAxis)_splitAxis!.Selected); _status!.Text = "✓ Deux viewports 3D ouverts · comparaison protégée"; }
    private void CopySelection() { var a = SelectedArea(_activeArea!); var b = SelectedArea(_comparisonArea!); _status!.Text = a is not null && b is not null && a != b && _copySelection?.Invoke(a, b) == true ? "✓ Sélection copiée vers la comparaison par transaction" : "Copie refusée · sélection ou zones invalides"; }
    private void SearchHelp() { var result = EditorContextualHelpService.Query(BuildHelpProfile(), _helpQuery!.Text, _assisted!.ButtonPressed); _helpResults!.Text = result.Diagnostics.Count > 0 ? $"[color=#ff8c8c]{result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}[/color]" : $"[color=#79d5ad]✓ {result.Topics.Count} sujet(s) · {result.Citations.Count} source(s)[/color]\n{result.Answer}\n\n[color=#a99bbb]{string.Join('\n', result.Citations)}[/color]"; }
    private EditorContextualHelpProfile BuildHelpProfile() { var current = _load!.Invoke().Help; return new(current.Id, 1, "creator-r059-ui", true, _assisted!.ButtonPressed, current.Locale, current.ActiveTopicKey); }
    private void RefreshRoleSummary() { var current = _load?.Invoke(); if (current is null) return; var policy = EditorRolePolicy.CreateDefault(current.Id.Value, (EditorRole)_role!.Selected); _roleSummary!.Text = $"[color=#ffb16f]PANNEAUX · {policy.VisiblePanels.Count}[/color]\n{string.Join(" · ", policy.VisiblePanels)}\n\n[color=#ffb16f]PERMISSIONS[/color]\n{string.Join(" · ", Enum.GetValues<EditorPermission>().Where(value => value != EditorPermission.None && policy.Allows(value)))}"; }

    private void Save()
    {
        EditorWorkspaceProfile candidate;
        try { candidate = BuildCandidate(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { _status!.Text = $"Profil invalide · {exception.Message}"; return; }
        var state = _getState?.Invoke(); if (state is null) return;
        var diagnostics = EditorWorkspaceProjectValidator.Validate(candidate, state.Project);
        if (diagnostics.Count > 0) { _status!.Text = $"Profil refusé · {diagnostics[0].Code} · {diagnostics[0].Message}"; return; }
        if (_history is null || _history.Current.Id != candidate.Id)
            _history = new(EditorWorkspaceProfile.CreateDefault(candidate.Role, candidate.ActiveAreaId));
        var result = _history.Execute(new(Guid.NewGuid(), EditorWorkspaceProfileCommandHistory.CurrentSchemaVersion,
            "creator-r059-ui", candidate));
        if (!result.Succeeded) { _status!.Text = $"Profil refusé · {result.Diagnostics[0].Code}"; return; }
        _save?.Invoke(result.Profile); _status!.Text = "✓ Profil, filtres, placement, split, aide et permissions enregistrés";
    }

    private void Undo() { var result = _history?.Undo(); if (result?.Succeeded != true) { _status!.Text = "Aucun profil à annuler"; return; } _save?.Invoke(result.Profile); Refresh(); _status!.Text = "✓ Profil précédent restauré"; }
    private void Redo() { var result = _history?.Redo(); if (result?.Succeeded != true) { _status!.Text = "Aucun profil à rétablir"; return; } _save?.Invoke(result.Profile); Refresh(); _status!.Text = "✓ Profil rétabli"; }
    private void PrepareCapture() { if (_volumes!.ItemCount > 0) { _volumes.DeselectAll(); _volumes.Select((int)EditorVolumeKind.Trigger, false); _volumes.Select((int)EditorVolumeKind.Navigation, false); } _brush!.ButtonPressed = true; _continuous!.ButtonPressed = true; _alignment!.Selected = (int)EditorPlacementAlignment.SurfaceNormal; _split!.ButtonPressed = true; _assisted!.ButtonPressed = true; _role!.Selected = (int)EditorRole.Builder; RefreshRoleSummary(); SearchHelp(); _status!.Text = "✓ R059 prêt · filtres, pinceau, split, aide sourcée et profil Builder valides"; }
    private AreaId? SelectedArea(OptionButton button) => button.Selected >= 0 && button.Selected < _areaIds.Count ? _areaIds[button.Selected] : null;
    private void SelectArea(OptionButton button, AreaId? id) { var index = id is null ? -1 : _areaIds.FindIndex(candidate => candidate == id); button.Selected = index < 0 ? (_areaIds.Count == 0 ? -1 : 0) : index; }
    private static VBoxContainer Column(Container parent, string heading) { var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 7); var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; panel.AddThemeStyleboxOverride("panel", Card("171122", "46325f")); var margin = new MarginContainer(); foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 13); panel.AddChild(margin); margin.AddChild(box); var title = new Label { Text = heading }; title.AddThemeColorOverride("font_color", new Color("ffb16f")); box.AddChild(title); parent.AddChild(panel); return box; }
    private static HBoxContainer Row(string label, Control control) { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); row.AddChild(control); return row; }
    private static SpinBox Spin(double min, double max, double value, double step) => new() { MinValue = min, MaxValue = max, Value = value, Step = step, CustomMinimumSize = new Vector2(110, 0) };
    private static StyleBoxFlat Card(string bg, string border) => new() { BgColor = new Color(bg), BorderColor = new Color(border), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
    private static string RoleName(EditorRole role) => role switch { EditorRole.Builder => "Bâtisseur", EditorRole.Writer => "Auteur", EditorRole.Scripter => "Scripteur", EditorRole.Artist => "Artiste", EditorRole.GameMaster => "MJ", EditorRole.Tester => "Testeur", _ => "Débutant" };
}
#endif
