#if TOOLS
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Godot;

namespace Forge.Toolset.Editor;

public partial class CreatorWorkspaceWindow : Window
{
    private Func<ProjectState?>? _getState;
    private Func<EditorWorkspaceProfile>? _load;
    private Action<EditorWorkspaceProfile>? _save;
    private Func<ArtisticStyleProfile>? _loadArtisticStyle;
    private Func<ArtisticStyleProfile, bool>? _applyArtisticStyle;
    private Func<AreaId, bool>? _activateArea;
    private Func<EditorDisplayFilter, int>? _applyFilter;
    private Func<int, float, PlacementDistribution, int, bool>? _repeatPlace;
    private Action<Window>? _brand;
    private readonly List<AreaId> _areas = [];
    private OptionButton? _role;
    private OptionButton? _activeArea;
    private OptionButton? _comparisonArea;
    private OptionButton? _kind;
    private LineEdit? _tags;
    private LineEdit? _author;
    private LineEdit? _state;
    private CheckBox? _isolate;
    private CheckBox? _ghost;
    private CheckBox? _helpEnabled;
    private CheckBox? _repeatEnabled;
    private SpinBox? _uiScale;
    private SpinBox? _textScale;
    private LineEdit? _primaryFont;
    private LineEdit? _fallbackFonts;
    private ItemList? _openTabs;
    private SpinBox? _count;
    private SpinBox? _spacing;
    private OptionButton? _distribution;
    private RichTextLabel? _comparison;
    private RichTextLabel? _help;
    private SpinBox? _metersPerUnit;
    private SpinBox? _texelDensity;
    private LineEdit? _shaderFamily;
    private LineEdit? _era;
    private OptionButton? _realism;
    private LineEdit? _palette;
    private TextEdit? _assetStyles;
    private RichTextLabel? _compatibility;
    private Label? _status;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<EditorWorkspaceProfile> load,
        Action<EditorWorkspaceProfile> save,
        Func<ArtisticStyleProfile> loadArtisticStyle,
        Func<ArtisticStyleProfile, bool> applyArtisticStyle,
        Func<AreaId, bool> activateArea,
        Func<EditorDisplayFilter, int> applyFilter,
        Func<int, float, PlacementDistribution, int, bool> repeatPlace,
        Action<Window> brand)
    {
        _getState = getState; _load = load; _save = save;
        _loadArtisticStyle = loadArtisticStyle; _applyArtisticStyle = applyArtisticStyle; _activateArea = activateArea;
        _applyFilter = applyFilter; _repeatPlace = repeatPlace; _brand = brand;
        Title = "Espace de travail · OpenRPG Forge";
        MinSize = new Vector2I(1420, 860); Size = MinSize; Transient = true; Visible = false;
        BuildUi();
    }

    public void Open()
    {
        Refresh();
        PopupCentered(MinSize);
        _brand?.Invoke(this);
    }

    private void Refresh()
    {
        var state = _getState?.Invoke();
        if (state is null) return;
        var profile = _load!.Invoke();
        _role!.Selected = (int)profile.Role;
        _repeatEnabled!.ButtonPressed = profile.RepeatPlacement;
        _helpEnabled!.ButtonPressed = profile.ContextualHelpEnabled;
        _isolate!.ButtonPressed = profile.DisplayFilter.IsolateSelection;
        _ghost!.ButtonPressed = profile.DisplayFilter.GhostHidden;
        _tags!.Text = string.Join(", ", profile.DisplayFilter.RequiredTags);
        _author!.Text = profile.DisplayFilter.Author;
        _state!.Text = profile.DisplayFilter.State;
        _uiScale!.Value = profile.UiScale;
        _textScale!.Value = profile.TextScale;
        _primaryFont!.Text = profile.PrimaryFontFamily;
        _fallbackFonts!.Text = string.Join(", ", profile.FallbackFontFamilies);
        _areas.Clear(); _activeArea!.Clear(); _comparisonArea!.Clear();
        _openTabs!.Clear();
        _comparisonArea.AddItem("Aucune comparaison");
        foreach (var area in state.Project.Areas.OrderBy(area => area.DisplayName, StringComparer.Ordinal))
        {
            _areas.Add(area.Id); _activeArea.AddItem(area.DisplayName); _comparisonArea.AddItem(area.DisplayName);
            _openTabs.AddItem(area.DisplayName);
            _openTabs.SetItemTooltip(_openTabs.ItemCount - 1, $"{area.Id}\n{area.Dimensions.Width} Ã— {area.Dimensions.Depth}");
        }
        for (var index = 0; index < _areas.Count; index++)
        {
            if (profile.OpenAreaTabs.Contains(_areas[index])) _openTabs.Select(index, true);
        }
        SelectArea(_activeArea, profile.ActiveAreaId, 0);
        SelectArea(_comparisonArea, profile.ComparisonAreaId, 1);
        LoadArtisticStyle(_loadArtisticStyle!.Invoke());
        RefreshComparison(); RefreshHelp(); PreviewArtisticCompatibility();
    }

    private void BuildUi()
    {
        var panel = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        panel.AddThemeStyleboxOverride("panel", Card("100c18", "62458a")); AddChild(panel);
        var margin = new MarginContainer(); foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 20);
        panel.AddChild(margin); var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "ESPACE DE TRAVAIL CRÉATEUR", ThemeTypeVariation = "HeaderSmall" }; title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Profils, filtres, zones simultanées, aide locale et placement en série." });
        var columns = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; columns.AddThemeConstantOverride("separation", 12); root.AddChild(columns);

        var profile = Column(columns, "PROFIL & AFFICHAGE");
        profile.AddChild(new Label { Text = "Métier" }); _role = new OptionButton(); foreach (var role in Enum.GetValues<EditorRole>()) _role.AddItem(RoleName(role)); profile.AddChild(_role);
        profile.AddChild(new Label { Text = "Type visible" }); _kind = new OptionButton(); _kind.AddItem("Tous les types"); foreach (var kind in Enum.GetValues<BlueprintKind>()) _kind.AddItem(kind.ToString()); profile.AddChild(_kind);
        _tags = new LineEdit { PlaceholderText = "Tags requis, séparés par virgules" }; profile.AddChild(_tags);
        _author = new LineEdit { PlaceholderText = "Auteur / provenance" }; profile.AddChild(_author);
        _state = new LineEdit { PlaceholderText = "État: projet, pack…" }; profile.AddChild(_state);
        _isolate = new CheckBox { Text = "Isoler la sélection" }; profile.AddChild(_isolate);
        _ghost = new CheckBox { Text = "Fantômiser les éléments masqués" }; profile.AddChild(_ghost);
        var apply = new Button { Text = "Appliquer les filtres" }; apply.Pressed += ApplyFilter; profile.AddChild(apply);
        profile.AddChild(new HSeparator());
        profile.AddChild(new Label { Text = "LISIBILITÉ & MISE À L'ÉCHELLE" });
        _uiScale = Spin(EditorWorkspaceProfile.MinimumUiScale, EditorWorkspaceProfile.MaximumUiScale, 1, 0.05);
        _textScale = Spin(EditorWorkspaceProfile.MinimumTextScale, EditorWorkspaceProfile.MaximumTextScale, 1, 0.05);
        profile.AddChild(Row("Interface", _uiScale));
        profile.AddChild(Row("Texte", _textScale));
        _primaryFont = new LineEdit { PlaceholderText = "Police TrueType principale" }; profile.AddChild(_primaryFont);
        _fallbackFonts = new LineEdit { PlaceholderText = "Fallbacks : Noto Sans, Segoe UI…" }; profile.AddChild(_fallbackFonts);
        profile.AddChild(new Label { Text = "Le texte se recompose sans agrandir le viewport 3D.", AutowrapMode = TextServer.AutowrapMode.WordSmart });

        var zones = Column(columns, "ZONES OUVERTES & COMPARAISON");
        zones.AddChild(new Label { Text = "Onglets visibles (8 maximum)" });
        _openTabs = new ItemList { SelectMode = ItemList.SelectModeEnum.Multi, CustomMinimumSize = new Vector2(0, 118) };
        zones.AddChild(_openTabs);
        zones.AddChild(new Label { Text = "Zone active (protège les commandes de la mauvaise zone)" }); _activeArea = new OptionButton(); _activeArea.ItemSelected += ActivateSelectedArea; zones.AddChild(_activeArea);
        zones.AddChild(new Label { Text = "Vue de comparaison" }); _comparisonArea = new OptionButton(); _comparisonArea.ItemSelected += _ => RefreshComparison(); zones.AddChild(_comparisonArea);
        _comparison = new RichTextLabel { FitContent = true, CustomMinimumSize = new Vector2(330, 150), BbcodeEnabled = true }; zones.AddChild(_comparison);
        zones.AddChild(new HSeparator()); zones.AddChild(new Label { Text = "PLACEMENT EN SÉRIE (une seule annulation)" });
        _repeatEnabled = new CheckBox { Text = "Conserver l’outil de placement actif", ButtonPressed = true }; zones.AddChild(_repeatEnabled);
        _count = Spin(2, 64, 6, 1); _spacing = Spin(0.25, 32, 2, 0.25); zones.AddChild(Row("Nombre", _count)); zones.AddChild(Row("Espacement", _spacing));
        _distribution = new OptionButton(); foreach (var item in Enum.GetValues<PlacementDistribution>()) _distribution.AddItem(item.ToString()); zones.AddChild(_distribution);
        var place = new Button { Text = "Placer la série depuis X/Z" }; place.Pressed += PlaceSeries; zones.AddChild(place);

        var help = Column(columns, "STYLE ARTISTIQUE & AIDE");
        _metersPerUnit = Spin(0.001, 1000, 1, 0.05); _texelDensity = Spin(16, 8192, 512, 16);
        help.AddChild(Row("Mètres / unité", _metersPerUnit)); help.AddChild(Row("Texels / mètre", _texelDensity));
        _shaderFamily = new LineEdit { PlaceholderText = "pbr-metallic-roughness" }; help.AddChild(_shaderFamily);
        _era = new LineEdit { PlaceholderText = "original-fantasy" }; help.AddChild(_era);
        _realism = new OptionButton(); foreach (var value in Enum.GetValues<ArtisticRealism>()) _realism.AddItem(value.ToString()); help.AddChild(_realism);
        _palette = new LineEdit { PlaceholderText = "earth, stone, vegetation…" }; help.AddChild(_palette);
        help.AddChild(new Label { Text = "Profils blueprint : UUID | échelle | texels | shader | époque | réalisme | palette" });
        _assetStyles = new TextEdit { CustomMinimumSize = new Vector2(0, 105), PlaceholderText = "Une ligne par blueprint profilé" }; help.AddChild(_assetStyles);
        var analyze = new Button { Text = "Analyser la compatibilité" }; analyze.Pressed += PreviewArtisticCompatibility; help.AddChild(analyze);
        _compatibility = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(0, 90) }; help.AddChild(_compatibility);
        _helpEnabled = new CheckBox { Text = "Afficher les conseils", ButtonPressed = true }; _helpEnabled.Toggled += _ => RefreshHelp(); help.AddChild(_helpEnabled);
        _help = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(330, 100) }; help.AddChild(_help);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; _status.AddThemeColorOverride("font_color", new Color("79d5ad")); root.AddChild(_status);
        var footer = new HBoxContainer(); footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); var save = new Button { Text = "Enregistrer le profil" }; save.Pressed += Save; footer.AddChild(save); var close = new Button { Text = "Fermer" }; close.Pressed += Hide; footer.AddChild(close); root.AddChild(footer);
    }

    private void ApplyFilter() { var filter = BuildFilter(); var visible = _applyFilter?.Invoke(filter) ?? 0; _status!.Text = $"✓ Filtre appliqué · {visible} entité(s) visible(s)"; RefreshHelp(); }

    private EditorDisplayFilter BuildFilter()
    {
        IReadOnlyList<BlueprintKind> kinds = _kind!.Selected == 0 ? Enum.GetValues<BlueprintKind>() : [(BlueprintKind)(_kind.Selected - 1)];
        return new(kinds, _tags!.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), _author!.Text, _state!.Text, _isolate!.ButtonPressed, _ghost!.ButtonPressed);
    }

    private void Save()
    {
        var active = SelectedArea(_activeArea!, 0); var comparison = SelectedArea(_comparisonArea!, 1);
        var open = _openTabs!.GetSelectedItems().Select(index => _areas[index]).Take(8).ToList();
        if (active is not null && !open.Contains(active)) open.Add(active);
        if (comparison is not null && !open.Contains(comparison)) open.Add(comparison);
        if (open.Count > 8)
        {
            _status!.Text = "Huit onglets de zone au maximum.";
            return;
        }

        var existing = _load!.Invoke();
        var value = new EditorWorkspaceProfile(EditorWorkspaceProfile.CreateDefault((EditorRole)_role!.Selected, active).Id,
            RoleName((EditorRole)_role.Selected), (EditorRole)_role.Selected, BuildFilter(), _repeatEnabled!.ButtonPressed,
            open, active, comparison, _helpEnabled!.ButtonPressed, "openrpg-forge-user-profile",
            uiScale: _uiScale!.Value, textScale: _textScale!.Value,
            primaryFontFamily: _primaryFont!.Text,
            fallbackFontFamilies: SplitList(_fallbackFonts!.Text),
            personalFavoriteBlueprintIds: existing.PersonalFavoriteBlueprintIds,
            recentBlueprintIds: existing.RecentBlueprintIds);
        ArtisticStyleProfile artisticStyle;
        try
        {
            artisticStyle = BuildArtisticStyle();
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            _status!.Text = $"Profil artistique invalide · {exception.Message}";
            return;
        }

        if (_applyArtisticStyle?.Invoke(artisticStyle) != true)
        {
            _status!.Text = "Le profil artistique a été refusé par la validation canonique.";
            return;
        }

        _save?.Invoke(value); _status!.Text = "✓ Profil scalable, onglets et style enregistrés";
    }

    private void LoadArtisticStyle(ArtisticStyleProfile profile)
    {
        _metersPerUnit!.Value = profile.MetersPerUnit;
        _texelDensity!.Value = profile.TargetTexelDensity;
        _shaderFamily!.Text = profile.ShaderFamily;
        _era!.Text = profile.Era;
        _realism!.Selected = (int)profile.Realism;
        _palette!.Text = string.Join(", ", profile.Palette);
        _assetStyles!.Text = string.Join('\n', profile.Assets.Select(asset =>
            $"{asset.BlueprintId} | {asset.MetersPerUnit:0.###} | {asset.TexelDensity} | {asset.ShaderFamily} | {asset.Era} | {asset.Realism} | {string.Join(',', asset.Palette)}"));
    }

    private ArtisticStyleProfile BuildArtisticStyle()
    {
        var state = _getState?.Invoke() ?? throw new InvalidOperationException("Aucun projet ouvert.");
        var assets = new List<ArtisticAssetStyle>();
        foreach (var line in _assetStyles!.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('|', StringSplitOptions.TrimEntries);
            if (fields.Length != 7)
            {
                throw new FormatException("Chaque ligne d'asset doit contenir sept champs séparés par |.");
            }

            assets.Add(new ArtisticAssetStyle(
                BlueprintId.Parse(fields[0]),
                double.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
                fields[3], fields[4], Enum.Parse<ArtisticRealism>(fields[5], ignoreCase: true),
                SplitList(fields[6]), "creator-style-profile"));
        }

        return new ArtisticStyleProfile(
            ArtisticStyleProfileId.From(state.Project.Module.Id.Value),
            _metersPerUnit!.Value,
            (int)_texelDensity!.Value,
            20,
            50,
            _shaderFamily!.Text,
            _era!.Text,
            (ArtisticRealism)_realism!.Selected,
            SplitList(_palette!.Text),
            assets,
            "creator-style-profile");
    }

    private void PreviewArtisticCompatibility()
    {
        if (_compatibility is null) return;
        try
        {
            var profile = BuildArtisticStyle();
            var diagnostics = ArtisticCompatibilityValidator.Analyze(profile);
            _compatibility.Text = diagnostics.Count == 0
                ? "[color=#79d5ad]✓ Profil cohérent · aucune rupture d'échelle, shader, époque ou palette[/color]"
                : $"[color=#ffc66d]{diagnostics.Count} avertissement(s)[/color]\n" +
                  string.Join('\n', diagnostics.Take(5).Select(item => $"△ {item.Message}"));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
        {
            _compatibility.Text = $"[color=#ff8c8c]{exception.Message}[/color]";
        }
    }

    private static string[] SplitList(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private void ActivateSelectedArea(long _) { var id = SelectedArea(_activeArea!, 0); if (id is not null) { _activateArea?.Invoke(id); RefreshComparison(); } }
    private void PlaceSeries() { var ok = _repeatPlace?.Invoke((int)_count!.Value, (float)_spacing!.Value, (PlacementDistribution)_distribution!.Selected, 2026) == true; _status!.Text = ok ? "✓ Série placée par une commande atomique" : "Placement refusé · vérifiez blueprint, zone et collisions"; }
    private void RefreshComparison()
    {
        if (_comparison is null) return; var project = _getState?.Invoke()?.Project; var active = SelectedArea(_activeArea!, 0); var other = SelectedArea(_comparisonArea!, 1);
        if (project is null || active is null || other is null) { _comparison.Text = "[color=#a99bbb]Sélectionnez une seconde zone pour comparer structure, population et transitions.[/color]"; return; }
        var a = project.Areas.Single(area => area.Id == active); var b = project.Areas.Single(area => area.Id == other);
        _comparison.Text = $"[b]{a.DisplayName}[/b]  ↔  [b]{b.DisplayName}[/b]\nDimensions: {a.Dimensions.Width}×{a.Dimensions.Depth} / {b.Dimensions.Width}×{b.Dimensions.Depth}\nEntités: {a.Entities.Count} / {b.Entities.Count}\nTransitions: {a.Portals.Count} / {b.Portals.Count}";
    }

    private void RefreshHelp() => _help!.Text = !_helpEnabled!.ButtonPressed ? "Aide masquée." :
        "[b]Affichage[/b]\nCombinez type, tags, auteur et état. L’isolation suit la sélection; le mode fantôme conserve le contexte.\n\n[b]Zones[/b]\nLa zone active est l’unique cible des commandes. La comparaison est en lecture seule.\n\n[b]Placement[/b]\nLigne, grille ou dispersion déterministe partent des coordonnées X/Z du bandeau Monde et forment une seule entrée d’historique.\n\n[b]Dépendances[/b]\nBlueprint sélectionné + zone active + grille valide.\n\n[color=#79d5ad]Sources locales: docs/aurora-parity/G7_COMPLETION_PLAN.md; OpenRPG_Forge_MEGACONTEXT.md[/color]";

    private AreaId? SelectedArea(OptionButton button, int offset) { var index = button.Selected - offset; return index >= 0 && index < _areas.Count ? _areas[index] : null; }
    private void SelectArea(OptionButton button, AreaId? id, int offset) { var index = id is null ? -1 : _areas.FindIndex(value => value == id); button.Selected = index < 0 ? 0 : index + offset; }
    private static VBoxContainer Column(Container parent, string heading) { var box = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 8); var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; panel.AddThemeStyleboxOverride("panel", Card("171122", "46325f")); var margin = new MarginContainer(); foreach (var side in new[] { "left", "top", "right", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 14); panel.AddChild(margin); margin.AddChild(box); box.AddChild(new Label { Text = heading }); parent.AddChild(panel); return box; }
    private static HBoxContainer Row(string label, Control control) { var row = new HBoxContainer(); row.AddChild(new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); row.AddChild(control); return row; }
    private static SpinBox Spin(double min, double max, double value, double step) => new() { MinValue = min, MaxValue = max, Value = value, Step = step, CustomMinimumSize = new Vector2(110, 0) };
    private static StyleBoxFlat Card(string background, string border) => new() { BgColor = new Color(background), BorderColor = new Color(border), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
    private static string RoleName(EditorRole role) => role switch { EditorRole.Builder => "Bâtisseur", EditorRole.Writer => "Auteur", EditorRole.Scripter => "Scripteur", EditorRole.Artist => "Artiste", EditorRole.GameMaster => "MJ", EditorRole.Tester => "Testeur", _ => "Débutant" };
}
#endif
