#if TOOLS
using Forge.Application.Blueprints;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Authoring;
using Godot;

namespace Forge.Toolset.Editor;

public partial class BlueprintCatalogWindow : Window
{
    private readonly List<BlueprintId> _visibleBlueprintIds = [];
    private readonly BlueprintKind[] _kinds = Enum.GetValues<BlueprintKind>();
    private readonly BlueprintPaletteView[] _paletteViews = Enum.GetValues<BlueprintPaletteView>();
    private readonly BlueprintPaletteOrigin[] _paletteOrigins = Enum.GetValues<BlueprintPaletteOrigin>();

    private Func<ProjectState?>? _getState;
    private Func<BlueprintId, string, string, BlueprintKind, BlueprintAuthoringMetadata, bool>? _upsertBlueprint;
    private Func<BlueprintId, string, string, BlueprintAuthoringMetadata, bool>? _createFromSelection;
    private Func<BlueprintId, BlueprintPaletteOrigin, bool, BlueprintProvenance, bool>? _configurePalette;
    private Func<EditorWorkspaceProfile>? _getWorkspaceProfile;
    private Action<BlueprintId>? _recordRecent;
    private Func<BlueprintId, bool>? _togglePersonalFavorite;
    private Action<CreatorSelection>? _publishSelection;
    private Action<Window>? _brandWindow;
    private BlueprintId? _draftBlueprintId;

    private LineEdit? _search;
    private OptionButton? _paletteView;
    private ItemList? _blueprintList;
    private Label? _catalogSummary;
    private Label? _contextFilterSummary;
    private Label? _librarySummary;
    private LineEdit? _displayName;
    private LineEdit? _slug;
    private OptionButton? _kind;
    private Label? _identity;
    private Label? _inheritance;
    private LineEdit? _version;
    private LineEdit? _packageNamespace;
    private LineEdit? _manifestPath;
    private LineEdit? _previewPath;
    private LineEdit? _exposedParameters;
    private LineEdit? _dependencies;
    private LineEdit? _kit;
    private LineEdit? _styles;
    private LineEdit? _compatibility;
    private LineEdit? _tags;
    private ItemList? _referenceList;
    private Label? _referenceSummary;
    private Label? _status;
    private Button? _createFromSelectionButton;
    private Button? _saveDefinitionButton;
    private OptionButton? _paletteOrigin;
    private CheckButton? _favorite;
    private CheckButton? _personalFavorite;
    private LineEdit? _provenanceSource;
    private LineEdit? _provenanceAuthor;
    private LineEdit? _provenanceLicense;
    private LineEdit? _permissionEvidence;
    private LineEdit? _provenanceHash;
    private CheckButton? _aiGenerated;
    private TextEdit? _modifications;
    private Label? _readOnlyBadge;

    public int VisibleBlueprintCount => _visibleBlueprintIds.Count;

    public int ReferenceCount => _referenceList?.ItemCount ?? 0;

    public bool SelectedIsReadOnly => _saveDefinitionButton?.Disabled ?? false;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<BlueprintId, string, string, BlueprintKind, BlueprintAuthoringMetadata, bool> upsertBlueprint,
        Func<BlueprintId, string, string, BlueprintAuthoringMetadata, bool> createFromSelection,
        Func<BlueprintId, BlueprintPaletteOrigin, bool, BlueprintProvenance, bool> configurePalette,
        Func<EditorWorkspaceProfile> getWorkspaceProfile,
        Action<BlueprintId> recordRecent,
        Func<BlueprintId, bool> togglePersonalFavorite,
        Action<CreatorSelection> publishSelection,
        Action<Window> brandWindow)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(upsertBlueprint);
        ArgumentNullException.ThrowIfNull(createFromSelection);
        ArgumentNullException.ThrowIfNull(configurePalette);
        ArgumentNullException.ThrowIfNull(publishSelection);
        ArgumentNullException.ThrowIfNull(brandWindow);
        _getState = getState;
        _upsertBlueprint = upsertBlueprint;
        _createFromSelection = createFromSelection;
        _configurePalette = configurePalette;
        _getWorkspaceProfile = getWorkspaceProfile;
        _recordRecent = recordRecent;
        _togglePersonalFavorite = togglePersonalFavorite;
        _publishSelection = publishSelection;
        _brandWindow = brandWindow;

        Title = "Blueprints · OpenRPG Forge";
        MinSize = new Vector2I(1360, 760);
        Size = new Vector2I(1360, 760);
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
        PopupCentered(new Vector2I(1360, 760));
        _brandWindow?.Invoke(this);
    }

    public void Refresh(BlueprintId? preferredBlueprintId = null)
    {
        var state = _getState?.Invoke();
        if (state is null || _blueprintList is null)
        {
            return;
        }

        var keepId = preferredBlueprintId ?? _draftBlueprintId;
        var query = _search?.Text.Trim() ?? string.Empty;
        var view = _paletteView is null
            ? BlueprintPaletteView.All
            : _paletteViews[Math.Clamp(_paletteView.Selected, 0, _paletteViews.Length - 1)];
        _visibleBlueprintIds.Clear();
        _blueprintList.Clear();
        var profile = _getWorkspaceProfile?.Invoke();
        var filter = profile?.Shell.PaletteFilter ?? EditorPaletteFilter.ShowAll;
        var catalog = BlueprintPaletteCatalog.Build(state.Project, profile);
        foreach (var item in catalog.Filter(view, query, filter))
        {
            _visibleBlueprintIds.Add(item.BlueprintId);
            var favorite = item.IsFavorite ? "★ " : string.Empty;
            var readOnly = item.IsReadOnly ? " · lecture seule" : string.Empty;
            _blueprintList.AddItem(
                $"{favorite}{item.DisplayName}\n{FormatOrigin(item.Origin)} · {FormatKind(item.Kind)}{readOnly}");
            _blueprintList.SetItemTooltip(
                _blueprintList.ItemCount - 1,
                $"{item.Provenance.Source}\n{item.Provenance.Author} · {item.Provenance.LicenseSpdx}\n" +
                $"Kit {item.Kit} · styles {string.Join(", ", item.Styles)} · tags {string.Join(", ", item.Tags)}");
        }

        _catalogSummary!.Text =
            $"{_visibleBlueprintIds.Count} affiché(s) · {catalog.Items.Count} au total · {FormatView(view)}";
        _contextFilterSummary!.Text =
            $"Filtres · types {filter.Kinds.Count} · kits {FormatCount(filter.Kits)} · styles {FormatCount(filter.Styles)} · " +
            $"licences {FormatCount(filter.Licenses)} · compatibilité {FormatCount(filter.Compatibility)} · " +
            $"tags {FormatCount(filter.Tags)}{(filter.FavoritesOnly ? " · favoris" : string.Empty)}";
        var library = BlueprintLibraryIndex.Build(state.Project);
        _librarySummary!.Text =
            $"{library.CategoryPaths.Count} catégories · {library.Collections.Count} collections · variantes : suffixe --nom";
        var selectedIndex = keepId is null ? -1 : _visibleBlueprintIds.FindIndex(id => id == keepId);
        if (selectedIndex >= 0)
        {
            _blueprintList.Select(selectedIndex);
            LoadBlueprint(state, keepId!);
        }
        else if (_visibleBlueprintIds.Count > 0)
        {
            _blueprintList.Select(0);
            LoadBlueprint(state, _visibleBlueprintIds[0]);
        }
        else
        {
            ClearEditor();
        }
    }

    public bool SelectBlueprintForSmokeTest(BlueprintId blueprintId)
    {
        Refresh(blueprintId);
        var index = _visibleBlueprintIds.FindIndex(id => id == blueprintId);
        if (index < 0)
        {
            return false;
        }

        var state = _getState?.Invoke();
        if (state is null)
        {
            return false;
        }

        _blueprintList!.Select(index);
        LoadBlueprint(state, blueprintId);
        return true;
    }

    public bool SelectViewForSmokeTest(BlueprintPaletteView view)
    {
        var index = Array.IndexOf(_paletteViews, view);
        if (_paletteView is null || index < 0)
        {
            return false;
        }

        _paletteView.Selected = index;
        Refresh();
        return true;
    }

    public bool ConfigurePaletteForSmokeTest(
        BlueprintId blueprintId,
        BlueprintPaletteOrigin origin,
        bool isFavorite,
        BlueprintProvenance provenance)
    {
        if (!SelectBlueprintForSmokeTest(blueprintId))
        {
            return false;
        }

        _paletteOrigin!.Selected = Array.IndexOf(_paletteOrigins, origin);
        _favorite!.ButtonPressed = isFavorite;
        _provenanceSource!.Text = provenance.Source;
        _provenanceAuthor!.Text = provenance.Author;
        _provenanceLicense!.Text = provenance.LicenseSpdx;
        _permissionEvidence!.Text = provenance.PermissionEvidence;
        _provenanceHash!.Text = provenance.ContentHash;
        _aiGenerated!.ButtonPressed = provenance.IsAiGenerated;
        _modifications!.Text = string.Join('\n', provenance.Modifications);
        return SavePaletteMetadata();
    }

    private void BuildUi()
    {
        var background = new PanelContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        background.AddThemeStyleboxOverride("panel", Card("15111f", "62458a", 1, 0));
        AddChild(background);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        background.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(titleRow);
        var titleColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titleRow.AddChild(titleColumn);
        var title = new Label { Text = "BLUEPRINTS", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        titleColumn.AddChild(title);
        titleColumn.AddChild(new Label
        {
            Text = "Palettes Standard et Custom · origine, droits et provenance restent visibles à chaque étape.",
        });
        var newButton = new Button { Text = "+ Nouveau blueprint", CustomMinimumSize = new Vector2(190, 40) };
        newButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        newButton.Pressed += CreateNew;
        titleRow.AddChild(newButton);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        var catalogCard = AddCard(body, new Vector2(290, 0));
        catalogCard.AddChild(new Label { Text = "CATALOGUE" });
        _paletteView = new OptionButton { CustomMinimumSize = new Vector2(0, 38) };
        foreach (var view in _paletteViews)
        {
            _paletteView.AddItem(FormatView(view));
        }
        catalogCard.AddChild(_paletteView);
        _search = new LineEdit { PlaceholderText = "Nom, type, licence, auteur…" };
        catalogCard.AddChild(_search);
        _catalogSummary = new Label();
        _catalogSummary.AddThemeColorOverride("font_color", new Color("a899b9"));
        catalogCard.AddChild(_catalogSummary);
        _contextFilterSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _contextFilterSummary.AddThemeColorOverride("font_color", new Color("b9aac8"));
        catalogCard.AddChild(_contextFilterSummary);
        _librarySummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _librarySummary.AddThemeColorOverride("font_color", new Color("79d5ad"));
        catalogCard.AddChild(_librarySummary);
        _blueprintList = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AllowReselect = true,
        };
        catalogCard.AddChild(_blueprintList);

        var editorCard = AddCard(body, new Vector2(300, 0));
        editorCard.AddChild(new Label { Text = "DÉFINITION" });
        _identity = new Label { Text = "Nouvelle définition" };
        _identity.AddThemeColorOverride("font_color", new Color("ffb07c"));
        editorCard.AddChild(_identity);
        editorCard.AddChild(new Label { Text = "Nom affiché" });
        _displayName = new LineEdit { PlaceholderText = "Ex. Colonne de la crypte" };
        editorCard.AddChild(_displayName);
        editorCard.AddChild(new Label { Text = "Slug stable" });
        _slug = new LineEdit { PlaceholderText = "colonne-de-la-crypte" };
        editorCard.AddChild(_slug);
        editorCard.AddChild(new Label { Text = "Type" });
        _kind = new OptionButton();
        foreach (var kind in _kinds)
        {
            _kind.AddItem(FormatKind(kind));
        }
        editorCard.AddChild(_kind);
        _inheritance = new Label
        {
            Text = "Les instances héritent du blueprint et conservent leurs propriétés personnalisées.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _inheritance.AddThemeColorOverride("font_color", new Color("b9aac8"));
        editorCard.AddChild(_inheritance);
        var versionRow = new HBoxContainer();
        versionRow.AddThemeConstantOverride("separation", 8);
        editorCard.AddChild(versionRow);
        _version = new LineEdit { PlaceholderText = "Version · 1.0.0", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _packageNamespace = new LineEdit { PlaceholderText = "Namespace · project.mon.blueprint", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        versionRow.AddChild(_version);
        versionRow.AddChild(_packageNamespace);
        _manifestPath = new LineEdit { PlaceholderText = "Manifeste · content/blueprints/…" };
        editorCard.AddChild(_manifestPath);
        _previewPath = new LineEdit { PlaceholderText = "Miniature · content/blueprints/….preview.png" };
        editorCard.AddChild(_previewPath);
        _exposedParameters = new LineEdit { PlaceholderText = "Paramètres exposés · scale, tint" };
        editorCard.AddChild(_exposedParameters);
        _dependencies = new LineEdit { PlaceholderText = "Dépendances · pack://namespace/version/resource" };
        editorCard.AddChild(_dependencies);
        _kit = new LineEdit { PlaceholderText = "Kit · crypt-stone" };
        editorCard.AddChild(_kit);
        _styles = new LineEdit { PlaceholderText = "Styles · gothic, ruined" };
        editorCard.AddChild(_styles);
        _compatibility = new LineEdit { PlaceholderText = "Compatibilité · medieval, humanoid-rig" };
        editorCard.AddChild(_compatibility);
        _tags = new LineEdit { PlaceholderText = "Tags · interior, stone, favorite" };
        editorCard.AddChild(_tags);
        var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        editorCard.AddChild(spacer);
        _createFromSelectionButton = new Button
        {
            Text = "Créer depuis l’instance sélectionnée",
            CustomMinimumSize = new Vector2(0, 42),
        };
        editorCard.AddChild(_createFromSelectionButton);
        _saveDefinitionButton = new Button
        {
            Text = "Enregistrer la définition",
            CustomMinimumSize = new Vector2(0, 42),
        };
        _saveDefinitionButton.AddThemeColorOverride("font_color", Colors.White);
        _saveDefinitionButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        _saveDefinitionButton.Pressed += Save;
        editorCard.AddChild(_saveDefinitionButton);

        var provenanceCard = AddCard(body, new Vector2(330, 0));
        provenanceCard.AddChild(new Label { Text = "ORIGINE & PROVENANCE" });
        _readOnlyBadge = new Label { Text = "CUSTOM · éditable" };
        _readOnlyBadge.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        provenanceCard.AddChild(_readOnlyBadge);
        _paletteOrigin = new OptionButton { CustomMinimumSize = new Vector2(0, 36) };
        foreach (var origin in _paletteOrigins)
        {
            _paletteOrigin.AddItem(FormatOrigin(origin));
        }
        provenanceCard.AddChild(_paletteOrigin);
        _favorite = new CheckButton { Text = "★ Ajouter aux favoris" };
        provenanceCard.AddChild(_favorite);
        _personalFavorite = new CheckButton { Text = "☆ Favori personnel (non exporté)" };
        _personalFavorite.Toggled += TogglePersonalFavorite;
        provenanceCard.AddChild(_personalFavorite);
        provenanceCard.AddChild(new Label { Text = "Source canonique" });
        _provenanceSource = new LineEdit { PlaceholderText = "project://… ou pack://…" };
        provenanceCard.AddChild(_provenanceSource);
        var authorLicense = new HBoxContainer();
        authorLicense.AddThemeConstantOverride("separation", 8);
        provenanceCard.AddChild(authorLicense);
        _provenanceAuthor = new LineEdit
        {
            PlaceholderText = "Auteur",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        authorLicense.AddChild(_provenanceAuthor);
        _provenanceLicense = new LineEdit
        {
            PlaceholderText = "Licence SPDX",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        authorLicense.AddChild(_provenanceLicense);
        provenanceCard.AddChild(new Label { Text = "Preuve de permission" });
        _permissionEvidence = new LineEdit { PlaceholderText = "spdx:CC0-1.0, contrat ou autorisation" };
        provenanceCard.AddChild(_permissionEvidence);
        provenanceCard.AddChild(new Label { Text = "Hash de contenu SHA-256" });
        _provenanceHash = new LineEdit { PlaceholderText = "64 caractères hexadécimaux ou unverified" };
        provenanceCard.AddChild(_provenanceHash);
        _aiGenerated = new CheckButton { Text = "Origine IA déclarée" };
        provenanceCard.AddChild(_aiGenerated);
        _modifications = new TextEdit
        {
            PlaceholderText = "Modifications, une par ligne",
            CustomMinimumSize = new Vector2(0, 66),
        };
        provenanceCard.AddChild(_modifications);
        var saveProvenanceButton = new Button
        {
            Text = "Prévisualiser et appliquer",
            CustomMinimumSize = new Vector2(0, 42),
        };
        saveProvenanceButton.AddThemeStyleboxOverride("normal", Card("38264f", "8060aa", 1, 8));
        saveProvenanceButton.Pressed += () => SavePaletteMetadata();
        provenanceCard.AddChild(saveProvenanceButton);

        var referenceCard = AddCard(body, new Vector2(270, 0), expand: true);
        referenceCard.AddChild(new Label { Text = "RÉFÉRENCES INVERSÉES" });
        _referenceSummary = new Label { Text = "Aucun usage" };
        _referenceSummary.AddThemeColorOverride("font_color", new Color("ffb07c"));
        referenceCard.AddChild(_referenceSummary);
        _referenceList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        referenceCard.AddChild(_referenceList);
        var help = new Label
        {
            Text = "Les entités et locuteurs de dialogues sont recalculés depuis le projet canonique.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        help.AddThemeColorOverride("font_color", new Color("a899b9"));
        referenceCard.AddChild(help);

        _status = new Label { Text = "Prêt." };
        _status.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        root.AddChild(_status);
    }

    private VBoxContainer AddCard(HBoxContainer parent, Vector2 minimumSize, bool expand = false)
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

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        _search!.TextChanged += _ => Refresh();
        _paletteView!.ItemSelected += _ => Refresh();
        _blueprintList!.ItemSelected += SelectBlueprint;
        _createFromSelectionButton!.Pressed += CreateFromSelection;
    }

    private void SelectBlueprint(long index)
    {
        var state = _getState?.Invoke();
        if (state is null || index < 0 || index >= _visibleBlueprintIds.Count)
        {
            return;
        }

        LoadBlueprint(state, _visibleBlueprintIds[(int)index]);
        _recordRecent?.Invoke(_visibleBlueprintIds[(int)index]);
    }

    private void LoadBlueprint(ProjectState state, BlueprintId blueprintId)
    {
        var blueprint = state.Project.Blueprints.Single(candidate => candidate.Id == blueprintId);
        _draftBlueprintId = blueprint.Id;
        _identity!.Text = $"{blueprint.Id.ToString()[..8]} · {FormatKind(blueprint.Kind)}";
        _displayName!.Text = blueprint.DisplayName;
        _slug!.Text = blueprint.Slug;
        _kind!.Selected = Array.IndexOf(_kinds, blueprint.Kind);
        _paletteOrigin!.Selected = Array.IndexOf(_paletteOrigins, blueprint.PaletteOrigin);
        _favorite!.ButtonPressed = blueprint.IsFavorite;
        _personalFavorite!.SetPressedNoSignal(
            _getWorkspaceProfile?.Invoke().PersonalFavoriteBlueprintIds.Contains(blueprint.Id) == true);
        _provenanceSource!.Text = blueprint.Provenance.Source;
        _provenanceAuthor!.Text = blueprint.Provenance.Author;
        _provenanceLicense!.Text = blueprint.Provenance.LicenseSpdx;
        _permissionEvidence!.Text = blueprint.Provenance.PermissionEvidence;
        _provenanceHash!.Text = blueprint.Provenance.ContentHash;
        _aiGenerated!.ButtonPressed = blueprint.Provenance.IsAiGenerated;
        _modifications!.Text = string.Join('\n', blueprint.Provenance.Modifications);
        _version!.Text = blueprint.Authoring.Version;
        _packageNamespace!.Text = blueprint.Authoring.PackageNamespace;
        _manifestPath!.Text = blueprint.Authoring.ManifestPath;
        _previewPath!.Text = blueprint.Authoring.PreviewPath ?? string.Empty;
        _exposedParameters!.Text = string.Join(", ", blueprint.Authoring.ExposedParameters);
        _dependencies!.Text = string.Join(", ", blueprint.Authoring.Dependencies);
        _kit!.Text = blueprint.Authoring.Kit;
        _styles!.Text = string.Join(", ", blueprint.Authoring.Styles);
        _compatibility!.Text = string.Join(", ", blueprint.Authoring.Compatibility);
        _tags!.Text = string.Join(", ", blueprint.Authoring.Tags);
        SetReadOnlyState(blueprint.IsReadOnly());
        RefreshReferences(state, blueprint);
        _publishSelection?.Invoke(new CreatorSelection(
            CreatorSelectionKind.Blueprint,
            blueprint.Id.ToString(),
            blueprint.DisplayName,
            $"blueprints/{blueprint.Id}"));
        SetStatus($"Blueprint « {blueprint.DisplayName} » chargé.", false);
    }

    private void RefreshReferences(ProjectState state, BlueprintDefinition blueprint)
    {
        var references = BlueprintReferenceIndex.Build(state.Project).Find(blueprint.Id);
        _referenceList!.Clear();
        foreach (var reference in references)
        {
            _referenceList.AddItem($"{FormatReferenceKind(reference.Kind)}\n{reference.DisplayName}");
            _referenceList.SetItemTooltip(_referenceList.ItemCount - 1, reference.DocumentPath);
        }

        _referenceSummary!.Text = references.Count == 0
            ? "Aucun usage · suppression sans dépendance"
            : $"{references.Count} usage(s) dans le projet";
    }

    private void CreateNew()
    {
        _draftBlueprintId = BlueprintId.From(Guid.NewGuid());
        _identity!.Text = "Nouvelle définition · non enregistrée";
        _displayName!.Text = "Nouveau blueprint";
        _slug!.Text = $"nouveau-blueprint-{(_getState?.Invoke()?.Project.Blueprints.Count ?? 0) + 1}";
        _kind!.Selected = 0;
        _paletteOrigin!.Selected = Array.IndexOf(_paletteOrigins, BlueprintPaletteOrigin.ProjectContent);
        _favorite!.ButtonPressed = false;
        _personalFavorite!.SetPressedNoSignal(false);
        _provenanceSource!.Text = $"project://blueprints/{_draftBlueprintId}/{_slug.Text}";
        _provenanceAuthor!.Text = "Project author";
        _provenanceLicense!.Text = "UNLICENSED";
        _permissionEvidence!.Text = "project-author";
        _provenanceHash!.Text = "unverified";
        _aiGenerated!.ButtonPressed = false;
        _modifications!.Text = string.Empty;
        LoadDefaultAuthoring(_draftBlueprintId, _slug.Text);
        _personalFavorite!.SetPressedNoSignal(false);
        SetReadOnlyState(false);
        _referenceList!.Clear();
        _referenceSummary!.Text = "Aucun usage";
        SetStatus("Définition prête. Ajustez son nom, son slug et son type.", false);
    }

    private void ClearEditor()
    {
        _draftBlueprintId = null;
        _identity!.Text = "Aucun blueprint visible";
        _displayName!.Text = string.Empty;
        _slug!.Text = string.Empty;
        _provenanceSource!.Text = string.Empty;
        _provenanceAuthor!.Text = string.Empty;
        _provenanceLicense!.Text = string.Empty;
        _permissionEvidence!.Text = string.Empty;
        _provenanceHash!.Text = string.Empty;
        _modifications!.Text = string.Empty;
        _version!.Text = string.Empty;
        _packageNamespace!.Text = string.Empty;
        _manifestPath!.Text = string.Empty;
        _previewPath!.Text = string.Empty;
        _exposedParameters!.Text = string.Empty;
        _dependencies!.Text = string.Empty;
        _kit!.Text = string.Empty;
        _styles!.Text = string.Empty;
        _compatibility!.Text = string.Empty;
        _tags!.Text = string.Empty;
        SetReadOnlyState(false);
        _referenceList!.Clear();
        _referenceSummary!.Text = "Aucun usage";
    }

    private void Save()
    {
        var blueprintId = _draftBlueprintId ?? BlueprintId.From(Guid.NewGuid());
        if (!ValidateDraft(out var displayName, out var slug, out var kind))
        {
            return;
        }

        if (!TryBuildAuthoring(out var authoring)) return;
        if (_upsertBlueprint?.Invoke(blueprintId, slug, displayName, kind, authoring) == true)
        {
            _draftBlueprintId = blueprintId;
            Refresh(blueprintId);
            SetStatus($"Blueprint « {displayName} » enregistré par commande transactionnelle.", false);
        }
        else
        {
            SetStatus("Le blueprint n’a pas pu être enregistré. Consultez les diagnostics.", true);
        }
    }

    private void CreateFromSelection()
    {
        if (!ValidateDraft(out var displayName, out var slug, out _))
        {
            return;
        }

        var blueprintId = BlueprintId.From(Guid.NewGuid());
        if (!TryBuildAuthoring(out var authoring)) return;
        if (_createFromSelection?.Invoke(blueprintId, slug, displayName, authoring) == true)
        {
            _draftBlueprintId = blueprintId;
            Refresh(blueprintId);
            SetStatus("Blueprint dérivé et instance retargetée sans perdre ses overrides.", false);
        }
        else
        {
            SetStatus("Sélectionnez une instance dans le Monde ou l’Explorateur avant de dériver.", true);
        }
    }

    private bool SavePaletteMetadata()
    {
        if (_draftBlueprintId is null)
        {
            SetStatus("Sélectionnez d’abord un blueprint enregistré.", true);
            return false;
        }

        var source = _provenanceSource!.Text.Trim();
        var author = _provenanceAuthor!.Text.Trim();
        var license = _provenanceLicense!.Text.Trim();
        var permissionEvidence = _permissionEvidence!.Text.Trim();
        var hash = _provenanceHash!.Text.Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(author) ||
            string.IsNullOrWhiteSpace(license) || string.IsNullOrWhiteSpace(permissionEvidence) ||
            string.IsNullOrWhiteSpace(hash))
        {
            SetStatus("Source, auteur, licence SPDX, permission et hash sont obligatoires.", true);
            return false;
        }

        var modifications = _modifications!.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        BlueprintProvenance provenance;
        try
        {
            provenance = new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                source,
                author,
                license,
                hash,
                _aiGenerated!.ButtonPressed,
                modifications,
                permissionEvidence);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
            return false;
        }

        var origin = _paletteOrigins[Math.Clamp(_paletteOrigin!.Selected, 0, _paletteOrigins.Length - 1)];
        if (_configurePalette?.Invoke(_draftBlueprintId, origin, _favorite!.ButtonPressed, provenance) != true)
        {
            SetStatus("La provenance a été refusée. Consultez le diagnostic canonique.", true);
            return false;
        }

        Refresh(_draftBlueprintId);
        SetStatus("Aperçu validé puis origine et provenance appliquées par commande transactionnelle.", false);
        return true;
    }

    private void SetReadOnlyState(bool readOnly)
    {
        _displayName!.Editable = !readOnly;
        _slug!.Editable = !readOnly;
        _kind!.Disabled = readOnly;
        _saveDefinitionButton!.Disabled = readOnly;
        _paletteOrigin!.Disabled = readOnly;
        _provenanceSource!.Editable = !readOnly;
        _provenanceAuthor!.Editable = !readOnly;
        _provenanceLicense!.Editable = !readOnly;
        _permissionEvidence!.Editable = !readOnly;
        _provenanceHash!.Editable = !readOnly;
        _aiGenerated!.Disabled = readOnly;
        _modifications!.Editable = !readOnly;
        _version!.Editable = !readOnly;
        _packageNamespace!.Editable = !readOnly;
        _manifestPath!.Editable = !readOnly;
        _previewPath!.Editable = !readOnly;
        _exposedParameters!.Editable = !readOnly;
        _dependencies!.Editable = !readOnly;
        _kit!.Editable = !readOnly;
        _styles!.Editable = !readOnly;
        _compatibility!.Editable = !readOnly;
        _tags!.Editable = !readOnly;
        _readOnlyBadge!.Text = readOnly ? "STANDARD · lecture seule" : "CUSTOM · éditable";
        _readOnlyBadge.AddThemeColorOverride(
            "font_color",
            new Color(readOnly ? "ffb07c" : "9ee6b3"));
    }

    private void TogglePersonalFavorite(bool enabled)
    {
        if (_draftBlueprintId is null || _togglePersonalFavorite is null)
        {
            return;
        }

        var isFavorite = _togglePersonalFavorite(_draftBlueprintId);
        _personalFavorite!.SetPressedNoSignal(isFavorite);
        SetStatus(isFavorite ? "Ajouté aux favoris personnels." : "Retiré des favoris personnels.", false);
    }

    private void LoadDefaultAuthoring(BlueprintId id, string slug)
    {
        var provenance = BlueprintProvenance.CreateProjectDefault(id, slug);
        var metadata = BlueprintAuthoringMetadata.CreateDefault(id, slug, BlueprintPaletteOrigin.ProjectContent, provenance);
        _version!.Text = metadata.Version;
        _packageNamespace!.Text = metadata.PackageNamespace;
        _manifestPath!.Text = metadata.ManifestPath;
        _previewPath!.Text = metadata.PreviewPath ?? string.Empty;
        _exposedParameters!.Text = string.Empty;
        _dependencies!.Text = string.Empty;
        _kit!.Text = metadata.Kit;
        _styles!.Text = string.Empty;
        _compatibility!.Text = string.Empty;
        _tags!.Text = string.Empty;
    }

    private bool TryBuildAuthoring(out BlueprintAuthoringMetadata metadata)
    {
        try
        {
            metadata = new BlueprintAuthoringMetadata(
                BlueprintAuthoringMetadata.CurrentSchemaVersion,
                _version!.Text,
                _packageNamespace!.Text,
                _manifestPath!.Text,
                SplitList(_exposedParameters!.Text),
                SplitList(_dependencies!.Text),
                _previewPath!.Text,
                BlueprintAuthoringMetadata.DefaultPlatformByteBudget,
                _kit!.Text,
                SplitList(_styles!.Text),
                SplitList(_compatibility!.Text),
                SplitList(_tags!.Text));
            return true;
        }
        catch (ArgumentException exception)
        {
            metadata = null!;
            SetStatus(exception.Message, true);
            return false;
        }
    }

    private static IReadOnlyList<string> SplitList(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FormatCount(IReadOnlyList<string> values) => values.Count == 0 ? "tous" : values.Count.ToString();

    private bool ValidateDraft(out string displayName, out string slug, out BlueprintKind kind)
    {
        displayName = _displayName!.Text.Trim();
        slug = _slug!.Text.Trim();
        kind = _kinds[Math.Clamp(_kind!.Selected, 0, _kinds.Length - 1)];
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(slug))
        {
            SetStatus("Le nom et le slug sont obligatoires.", true);
            return false;
        }

        if (!string.Equals(slug, SlugGenerator.FromDisplayName(slug, "blueprint"), StringComparison.Ordinal))
        {
            SetStatus("Le slug doit être canonique : minuscules, chiffres et tirets.", true);
            return false;
        }

        return true;
    }

    private void SetStatus(string message, bool error)
    {
        _status!.Text = message;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "9ee6b3"));
    }

    private static string FormatKind(BlueprintKind kind) => kind switch
    {
        BlueprintKind.Placeable => "Décor plaçable",
        BlueprintKind.Door => "Porte",
        BlueprintKind.Creature => "Créature",
        BlueprintKind.PlayerSpawn => "Point de départ",
        BlueprintKind.Trigger => "Déclencheur",
        BlueprintKind.Item => "Objet",
        _ => kind.ToString(),
    };

    private static string FormatOrigin(BlueprintPaletteOrigin origin) => origin switch
    {
        BlueprintPaletteOrigin.InstalledPack => "Pack installé",
        BlueprintPaletteOrigin.ProjectContent => "Contenu projet",
        BlueprintPaletteOrigin.Generated => "Contenu généré",
        BlueprintPaletteOrigin.Temporary => "Contenu temporaire",
        _ => origin.ToString(),
    };

    private static string FormatView(BlueprintPaletteView view) => view switch
    {
        BlueprintPaletteView.All => "Toutes les palettes",
        BlueprintPaletteView.InstalledPacks => "Standard · packs installés",
        BlueprintPaletteView.ProjectContent => "Custom · projet",
        BlueprintPaletteView.Generated => "Généré",
        BlueprintPaletteView.Favorites => "★ Favoris",
        BlueprintPaletteView.Recent => "Récents",
        BlueprintPaletteView.Temporary => "Temporaire",
        _ => view.ToString(),
    };

    private static string FormatReferenceKind(BlueprintReferenceKind kind) => kind switch
    {
        BlueprintReferenceKind.EntityInstance => "INSTANCE",
        BlueprintReferenceKind.DialogueSpeaker => "DIALOGUE",
        _ => kind.ToString().ToUpperInvariant(),
    };
}
#endif
