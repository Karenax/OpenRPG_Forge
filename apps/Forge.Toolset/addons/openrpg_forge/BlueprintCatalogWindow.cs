#if TOOLS
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Godot;
using System.Globalization;

namespace Forge.Toolset.Editor;

public partial class BlueprintCatalogWindow : Window
{
    private readonly List<BlueprintId> _visibleBlueprintIds = [];
    private readonly BlueprintKind[] _kinds = Enum.GetValues<BlueprintKind>();
    private readonly BlueprintPaletteView[] _paletteViews = Enum.GetValues<BlueprintPaletteView>();
    private readonly BlueprintPaletteOrigin[] _paletteOrigins = Enum.GetValues<BlueprintPaletteOrigin>();

    private Func<ProjectState?>? _getState;
    private Func<BlueprintId, string, string, BlueprintKind, BlueprintAuthoringMetadata, bool>? _upsertBlueprint;
    private Func<BlueprintId, bool>? _deleteBlueprint;
    private Func<BlueprintId, string, string, BlueprintAuthoringMetadata, bool>? _createFromSelection;
    private Func<BlueprintId, BlueprintPaletteOrigin, bool, BlueprintProvenance, bool>? _configurePalette;
    private Func<UpdateBlueprintFromEntityCommand, bool>? _updateFromEntity;
    private Func<ConfigureBlueprintPrefabCommand, bool>? _configurePrefab;
    private Func<PlacePrefabCompositionCommand, bool>? _placeComposition;
    private Func<EditorWorkspaceProfile>? _getWorkspaceProfile;
    private Action<BlueprintId>? _recordRecent;
    private Func<BlueprintId, bool>? _togglePersonalFavorite;
    private Action<CreatorSelection>? _publishSelection;
    private Action<Window>? _brandWindow;
    private BlueprintId? _draftBlueprintId;
    private IReadOnlyList<EntityPropertyOverride> _loadedPromotedDefaults = [];
    private Guid _draftVariantId = Guid.NewGuid();
    private Guid _draftCompositionId = Guid.NewGuid();
    private Guid _draftBudgetId = Guid.NewGuid();

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
    private LineEdit? _categoryPaths;
    private ItemList? _referenceList;
    private Label? _referenceSummary;
    private LineEdit? _sourceEntityId;
    private LineEdit? _promotedKeys;
    private LineEdit? _excludedInstances;
    private OptionButton? _versionBump;
    private Label? _updatePreview;
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
    private LineEdit? _authorCollections;
    private LineEdit? _variantBaseBlueprintId;
    private TextEdit? _variantOverrides;
    private CheckButton? _variantRequired;
    private TextEdit? _compositionNodes;
    private LineEdit? _compositionRules;
    private LineEdit? _compositionParameters;
    private LineEdit? _compositionScripts;
    private CheckButton? _compositionRequired;
    private LineEdit? _assetMetrics;
    private TextEdit? _platformBudgets;
    private StructuredCollectionField? _variantOverridesEditor;
    private StructuredCollectionField? _compositionNodesEditor;
    private StructuredCollectionField? _platformBudgetsEditor;
    private CheckButton? _budgetRequired;
    private Label? _prefabPreview;
    private string? _r058CapturePath;
    private double _r058CaptureDelay;
    private bool _r058CaptureOpened;

    public int VisibleBlueprintCount => _visibleBlueprintIds.Count;

    public int ReferenceCount => _referenceList?.ItemCount ?? 0;

    public bool SelectedIsReadOnly => _saveDefinitionButton?.Disabled ?? false;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<BlueprintId, string, string, BlueprintKind, BlueprintAuthoringMetadata, bool> upsertBlueprint,
        Func<BlueprintId, bool> deleteBlueprint,
        Func<BlueprintId, string, string, BlueprintAuthoringMetadata, bool> createFromSelection,
        Func<BlueprintId, BlueprintPaletteOrigin, bool, BlueprintProvenance, bool> configurePalette,
        Func<UpdateBlueprintFromEntityCommand, bool> updateFromEntity,
        Func<ConfigureBlueprintPrefabCommand, bool> configurePrefab,
        Func<PlacePrefabCompositionCommand, bool> placeComposition,
        Func<EditorWorkspaceProfile> getWorkspaceProfile,
        Action<BlueprintId> recordRecent,
        Func<BlueprintId, bool> togglePersonalFavorite,
        Action<CreatorSelection> publishSelection,
        Action<Window> brandWindow)
    {
        ArgumentNullException.ThrowIfNull(getState);
        ArgumentNullException.ThrowIfNull(upsertBlueprint);
        ArgumentNullException.ThrowIfNull(deleteBlueprint);
        ArgumentNullException.ThrowIfNull(createFromSelection);
        ArgumentNullException.ThrowIfNull(configurePalette);
        ArgumentNullException.ThrowIfNull(updateFromEntity);
        ArgumentNullException.ThrowIfNull(configurePrefab);
        ArgumentNullException.ThrowIfNull(placeComposition);
        ArgumentNullException.ThrowIfNull(publishSelection);
        ArgumentNullException.ThrowIfNull(brandWindow);
        _getState = getState;
        _upsertBlueprint = upsertBlueprint;
        _deleteBlueprint = deleteBlueprint;
        _createFromSelection = createFromSelection;
        _configurePalette = configurePalette;
        _updateFromEntity = updateFromEntity;
        _configurePrefab = configurePrefab;
        _placeComposition = placeComposition;
        _getWorkspaceProfile = getWorkspaceProfile;
        _recordRecent = recordRecent;
        _togglePersonalFavorite = togglePersonalFavorite;
        _publishSelection = publishSelection;
        _brandWindow = brandWindow;

        Title = "Blueprints · OpenRPG Forge";
        MinSize = new Vector2I(1760, 900);
        Size = new Vector2I(1760, 900);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r058-prefab-budget-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _r058CapturePath = capture["--r058-prefab-budget-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_r058CapturePath is null) return;
        _r058CaptureDelay += delta;
        if (!_r058CaptureOpened && _r058CaptureDelay >= 1.5)
        {
            Open();
            PrepareR058Capture();
            _r058CaptureOpened = true;
            return;
        }
        if (!_r058CaptureOpened || _r058CaptureDelay < 4) return;
        var destination = Path.GetFullPath(_r058CapturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_R058_PREFAB_BUDGET_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _r058CapturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1760, 900));
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

    public bool UpdateFromEntityForSmokeTest(BlueprintId blueprintId, EntityId sourceEntityId,
        IReadOnlyList<string> promotedKeys, bool dryRun)
    {
        if (!SelectBlueprintForSmokeTest(blueprintId)) return false;
        _sourceEntityId!.Text = sourceEntityId.ToString();
        _promotedKeys!.Text = string.Join(", ", promotedKeys);
        _excludedInstances!.Text = string.Empty;
        _versionBump!.Selected = (int)BlueprintVersionBump.Patch;
        return UpdateFromEntity(dryRun);
    }

    public bool ConfigurePrefabForSmokeTest(BlueprintId blueprintId, BlueprintId baseBlueprintId,
        BlueprintId childBlueprintId, bool dryRun)
    {
        if (!SelectBlueprintForSmokeTest(blueprintId)) return false;
        _authorCollections!.Text = "Crypt/Architecture, Smoke/Prefabs";
        _variantBaseBlueprintId!.Text = baseBlueprintId.ToString();
        _variantOverrides!.Text = "Material|body|asset:ember\nStat|armor|18\nScript|on_use|scripts/ember.as";
        _variantRequired!.ButtonPressed = true;
        var rootId = Guid.Parse("b0580000-0000-4000-8000-000000000301");
        _compositionNodes!.Text = $"{rootId:D}|{childBlueprintId}|root|floor|2|0|3";
        _compositionRules!.Text = "socket-compatible, deterministic-order";
        _compositionParameters!.Text = "intensity, theme";
        _compositionScripts!.Text = "scripts/assemble.as";
        _compositionRequired!.ButtonPressed = true;
        _assetMetrics!.Text = "12000, 2, 3, 8000000, 1, 3, 2";
        _platformBudgets!.Text = "Windows|20000|4|6|16000000|2|4|3|true\nLinux|18000|4|6|14000000|2|4|3|false";
        _budgetRequired!.ButtonPressed = true;
        return ConfigurePrefab(dryRun);
    }

    public bool PlaceCompositionForSmokeTest(bool dryRun) => PlaceComposition(dryRun);

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
        var deleteButton = new Button { Text = "Supprimer", CustomMinimumSize = new Vector2(130, 40) };
        deleteButton.TooltipText = "Supprimer le blueprint sélectionné si aucune dépendance ne l'empêche";
        deleteButton.Pressed += DeleteCurrent;
        titleRow.AddChild(deleteButton);

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
        _categoryPaths = new LineEdit { PlaceholderText = "Catégories · Monde/Forêt/Arbres, Décors/Pierre" };
        editorCard.AddChild(_categoryPaths);
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

        var prefabCard = AddCard(body, new Vector2(370, 0));
        prefabCard.AddThemeConstantOverride("separation", 5);
        prefabCard.AddChild(new Label { Text = "VARIANTES · COMPOSITIONS · BUDGETS" });
        _authorCollections = new LineEdit { PlaceholderText = "Collections auteur · Crypte/Architecture" };
        prefabCard.AddChild(_authorCollections);
        _variantBaseBlueprintId = new LineEdit { PlaceholderText = "UUID du blueprint de base (optionnel)" };
        prefabCard.AddChild(_variantBaseBlueprintId);
        _variantOverrides = new TextEdit
        {
            PlaceholderText = "Overrides · Material|body|asset:ember (un par ligne)",
            CustomMinimumSize = new Vector2(0, 44),
        };
        prefabCard.AddChild(_variantOverrides);
        _variantOverridesEditor = StructuredCollectionField.Attach(_variantOverrides, "override",
            new("Type", Options: Enum.GetNames<PrefabVariantOverrideKind>(),
                DefaultValue: Enum.GetNames<PrefabVariantOverrideKind>()[0]),
            new("Propriété", "Material"), new("Valeur", "asset:ember"));
        _variantRequired = new CheckButton { Text = "Variante requise au runtime" };
        prefabCard.AddChild(_variantRequired);
        _compositionNodes = new TextEdit
        {
            PlaceholderText = "Nœuds · uuid|blueprint|parent/root|socket|x|y|z",
            CustomMinimumSize = new Vector2(0, 48),
        };
        prefabCard.AddChild(_compositionNodes);
        _compositionNodesEditor = StructuredCollectionField.Attach(_compositionNodes, "nœud de composition",
            new("Identifiant", Guid.Empty.ToString("D")),
            new("Blueprint", "UUID du blueprint"),
            new("Parent", "root", "root"), new("Socket", "floor"),
            new("Position X", "0", "0"), new("Position Y", "0", "0"),
            new("Position Z", "0", "0"));
        _compositionRules = new LineEdit { PlaceholderText = "Règles · socket-compatible" };
        prefabCard.AddChild(_compositionRules);
        _compositionParameters = new LineEdit { PlaceholderText = "Paramètres · intensity, theme" };
        prefabCard.AddChild(_compositionParameters);
        _compositionScripts = new LineEdit { PlaceholderText = "Scripts · scripts/assemble.as" };
        prefabCard.AddChild(_compositionScripts);
        _compositionRequired = new CheckButton { Text = "Composition requise au runtime" };
        prefabCard.AddChild(_compositionRequired);
        _assetMetrics = new LineEdit
        {
            PlaceholderText = "Mesures · triangles, matériaux, textures, octets, animations, LOD, collisions",
        };
        prefabCard.AddChild(_assetMetrics);
        _platformBudgets = new TextEdit
        {
            PlaceholderText = "Budget · Windows|20000|4|6|16000000|2|4|3|true",
            CustomMinimumSize = new Vector2(0, 44),
        };
        prefabCard.AddChild(_platformBudgets);
        _platformBudgetsEditor = StructuredCollectionField.Attach(_platformBudgets, "budget de plateforme",
            new("Plateforme", Options: Enum.GetNames<AssetTargetPlatform>(),
                DefaultValue: AssetTargetPlatform.Windows.ToString()),
            new("Triangles", "20000", "20000"), new("Matériaux", "4", "4"),
            new("Textures", "6", "6"), new("Octets texture", "16000000", "16000000"),
            new("Animations", "2", "2"), new("LOD", "4", "4"),
            new("Collisions", "3", "3"),
            new("Bloquant", Options: ["true", "false"], DefaultValue: "true"));
        _budgetRequired = new CheckButton { Text = "Budget requis au runtime" };
        prefabCard.AddChild(_budgetRequired);
        var prefabActions = new HBoxContainer();
        prefabActions.AddThemeConstantOverride("separation", 6);
        prefabCard.AddChild(prefabActions);
        var previewPrefab = new Button { Text = "Aperçu", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        previewPrefab.Pressed += () => ConfigurePrefab(dryRun: true);
        prefabActions.AddChild(previewPrefab);
        var applyPrefab = new Button { Text = "Appliquer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        applyPrefab.Pressed += () => ConfigurePrefab(dryRun: false);
        prefabActions.AddChild(applyPrefab);
        var placeComposition = new Button { Text = "Placer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        placeComposition.Pressed += () => PlaceComposition(dryRun: false);
        prefabActions.AddChild(placeComposition);
        _prefabPreview = new Label
        {
            Text = "État vide · aucun profil prefab ou budget configuré.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _prefabPreview.AddThemeColorOverride("font_color", new Color("a899b9"));
        prefabCard.AddChild(_prefabPreview);

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
        referenceCard.AddChild(new Label { Text = "MISE À JOUR ASSISTÉE" });
        _sourceEntityId = new LineEdit { PlaceholderText = "UUID de l’instance source" };
        referenceCard.AddChild(_sourceEntityId);
        _promotedKeys = new LineEdit { PlaceholderText = "Overrides à promouvoir · visual.scale, tint" };
        referenceCard.AddChild(_promotedKeys);
        _excludedInstances = new LineEdit { PlaceholderText = "UUID exclus · séparés par virgules" };
        referenceCard.AddChild(_excludedInstances);
        _versionBump = new OptionButton();
        foreach (var bump in Enum.GetValues<BlueprintVersionBump>()) _versionBump.AddItem(bump.ToString());
        referenceCard.AddChild(_versionBump);
        var updateActions = new HBoxContainer();
        referenceCard.AddChild(updateActions);
        var previewUpdate = new Button { Text = "Aperçu", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        previewUpdate.Pressed += () => UpdateFromEntity(dryRun: true);
        updateActions.AddChild(previewUpdate);
        var applyUpdate = new Button { Text = "Appliquer", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        applyUpdate.Pressed += () => UpdateFromEntity(dryRun: false);
        updateActions.AddChild(applyUpdate);
        _updatePreview = new Label
        {
            Text = "Sélectionnez une instance pour comparer ses overrides.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _updatePreview.AddThemeColorOverride("font_color", new Color("a899b9"));
        referenceCard.AddChild(_updatePreview);

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
        _categoryPaths!.Text = string.Join(", ", blueprint.Authoring.CategoryPaths);
        _loadedPromotedDefaults = blueprint.Authoring.PromotedDefaults;
        LoadPrefabProfiles(state, blueprint);
        var firstInstance = state.Project.Areas.SelectMany(area => area.Entities)
            .FirstOrDefault(entity => entity.BlueprintId == blueprint.Id);
        _sourceEntityId!.Text = firstInstance?.Id.ToString() ?? string.Empty;
        _promotedKeys!.Text = firstInstance is null
            ? string.Empty
            : string.Join(", ", firstInstance.Overrides.Properties.Select(item => item.Key));
        _excludedInstances!.Text = string.Empty;
        _updatePreview!.Text = firstInstance is null
            ? "État vide · aucune instance de ce blueprint dans les zones."
            : $"Instance source {firstInstance.Id} · {firstInstance.Overrides.Properties.Count} override(s).";
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

    private void DeleteCurrent()
    {
        var state = _getState?.Invoke();
        if (state is null || _draftBlueprintId is null ||
            state.Project.Blueprints.All(item => item.Id != _draftBlueprintId))
        {
            SetStatus("Sélectionnez d’abord un blueprint enregistré.", true);
            return;
        }

        if (_deleteBlueprint?.Invoke(_draftBlueprintId) != true)
        {
            SetStatus("Suppression refusée : consultez les dépendances et diagnostics du projet.", true);
            return;
        }

        Refresh();
        SetStatus("Blueprint supprimé. Utilisez Annuler pour le restaurer.", false);
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
        _categoryPaths!.Text = string.Empty;
        _loadedPromotedDefaults = [];
        ClearPrefabProfiles();
        _sourceEntityId!.Text = string.Empty;
        _promotedKeys!.Text = string.Empty;
        _excludedInstances!.Text = string.Empty;
        _updatePreview!.Text = "Sélectionnez une instance pour comparer ses overrides.";
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

    private bool UpdateFromEntity(bool dryRun)
    {
        var state = _getState?.Invoke();
        if (state is null || _draftBlueprintId is null)
        {
            SetStatus("Sélectionnez un blueprint enregistré avant sa mise à jour.", true);
            return false;
        }

        try
        {
            var sourceId = EntityId.Parse(_sourceEntityId!.Text);
            var excluded = SplitList(_excludedInstances!.Text).Select(EntityId.Parse).ToArray();
            var keys = SplitList(_promotedKeys!.Text);
            var bump = Enum.GetValues<BlueprintVersionBump>()[Math.Clamp(_versionBump!.Selected, 0,
                Enum.GetValues<BlueprintVersionBump>().Length - 1)];
            var plan = BlueprintUpdatePlanner.Build(state.Project, _draftBlueprintId, sourceId, keys, bump, excluded);
            _updatePreview!.Text =
                $"{plan.PreviousVersion} → {plan.TargetVersion} · {plan.PromotedDefaults.Count} défaut(s) · " +
                $"{plan.Instances.Count} instance(s) · " +
                $"{plan.Instances.Count(item => item.Action == "excluded")} exclue(s) · " +
                $"{plan.Instances.Count(item => item.Action == "pinned-incompatible")} pin incompatible";
            var command = new UpdateBlueprintFromEntityCommand(CommandId.From(Guid.NewGuid()), state.Revision,
                _draftBlueprintId, sourceId, keys, bump, excluded, dryRun, SplitList(_categoryPaths!.Text));
            if (_updateFromEntity?.Invoke(command) != true)
            {
                SetStatus("La mise à jour a été refusée par la commande canonique.", true);
                return false;
            }

            if (!dryRun) Refresh(_draftBlueprintId);
            SetStatus(dryRun ? "Aperçu de migration prêt, aucun document modifié." :
                "Blueprint et instances migrés atomiquement ; undo/redo disponible.", false);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or BlueprintUpdatePlanException)
        {
            _updatePreview!.Text = $"Erreur localisée · {exception.Message}";
            SetStatus("Configuration de mise à jour invalide.", true);
            return false;
        }
    }

    private bool ConfigurePrefab(bool dryRun)
    {
        var state = _getState?.Invoke();
        if (state is null || _draftBlueprintId is null)
        {
            SetPrefabError("Sélectionnez un blueprint enregistré avant de configurer son prefab.");
            return false;
        }
        if (!TryBuildPrefabProfiles(out var variant, out var composition, out var budget)) return false;
        var command = new ConfigureBlueprintPrefabCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            _draftBlueprintId, variant, composition, budget, SplitList(_authorCollections!.Text), dryRun);
        if (_configurePrefab?.Invoke(command) != true)
        {
            SetPrefabError("Commande refusée · vérifiez UUID, références, cycles et budgets.");
            return false;
        }
        var evaluations = budget?.Platforms.Select(item => BlueprintPrefabRuntimeService.Evaluate(budget.Metrics, item))
            .ToArray() ?? [];
        _prefabPreview!.Text =
            $"{(dryRun ? "APERÇU VALIDE · aucun fichier modifié" : "APPLIQUÉ · undo/redo disponible")}\n" +
            $"Variante · {variant?.Overrides.Count ?? 0} delta(s) · Composition · {composition?.Nodes.Count ?? 0} nœud(s)\n" +
            $"Budgets · {evaluations.Count(item => item.Status == AssetBudgetEvaluationStatus.Passed)} conformes · " +
            $"{evaluations.Count(item => item.Status == AssetBudgetEvaluationStatus.Warning)} avertissement(s) · " +
            $"{evaluations.Count(item => item.Status == AssetBudgetEvaluationStatus.Failed)} bloquant(s)";
        _prefabPreview.AddThemeColorOverride("font_color", new Color("79d5ad"));
        if (!dryRun) Refresh(_draftBlueprintId);
        SetStatus(dryRun ? "Aperçu prefab prêt, projet inchangé." :
            "Variante, composition, collections et budgets enregistrés atomiquement.", false);
        return true;
    }

    private bool PlaceComposition(bool dryRun)
    {
        var state = _getState?.Invoke();
        var area = state?.Project.Areas.FirstOrDefault();
        if (state is null || area is null || _draftBlueprintId is null)
        {
            SetPrefabError("Une composition enregistrée et une zone cible sont requises.");
            return false;
        }
        var command = new PlacePrefabCompositionCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            _draftBlueprintId, area.Id, Vector3Value.Zero, dryRun);
        if (_placeComposition?.Invoke(command) != true)
        {
            SetPrefabError("Placement refusé · la hiérarchie doit être valide et enregistrée.");
            return false;
        }
        _prefabPreview!.Text = dryRun
            ? $"APERÇU PLACEMENT · zone {area.DisplayName} · aucun document modifié"
            : $"PLACÉ · groupe atomique dans {area.DisplayName} · undo/redo disponible";
        _prefabPreview.AddThemeColorOverride("font_color", new Color("79d5ad"));
        return true;
    }

    private bool TryBuildPrefabProfiles(out PrefabVariantDefinition? variant,
        out PrefabCompositionDefinition? composition, out BlueprintAssetBudgetProfile? budget)
    {
        variant = null;
        composition = null;
        budget = null;
        try
        {
            var baseText = _variantBaseBlueprintId!.Text.Trim();
            var overrideLines = SplitLines(_variantOverrides!.Text);
            if (baseText.Length > 0)
            {
                var overrides = overrideLines.Select(ParseVariantOverride).ToArray();
                variant = new PrefabVariantDefinition(_draftVariantId,
                    PrefabVariantDefinition.CurrentSchemaVersion, "editor:r058", BlueprintId.Parse(baseText),
                    overrides, _variantRequired!.ButtonPressed);
            }
            else if (overrideLines.Count > 0)
            {
                throw new FormatException("Une variante avec overrides requiert l’UUID du blueprint de base.");
            }

            var nodeLines = SplitLines(_compositionNodes!.Text);
            if (nodeLines.Count > 0)
                composition = new PrefabCompositionDefinition(_draftCompositionId,
                    PrefabCompositionDefinition.CurrentSchemaVersion, "editor:r058",
                    nodeLines.Select(ParseCompositionNode).ToArray(), SplitList(_compositionRules!.Text),
                    SplitList(_compositionParameters!.Text), SplitList(_compositionScripts!.Text),
                    _compositionRequired!.ButtonPressed);

            var metricsText = _assetMetrics!.Text.Trim();
            var platformLines = SplitLines(_platformBudgets!.Text);
            if (metricsText.Length > 0 || platformLines.Count > 0)
            {
                if (metricsText.Length == 0 || platformLines.Count == 0)
                    throw new FormatException("Les mesures et au moins un budget de plateforme sont requis ensemble.");
                budget = new BlueprintAssetBudgetProfile(_draftBudgetId,
                    BlueprintAssetBudgetProfile.CurrentSchemaVersion, "editor:r058", ParseMetrics(metricsText),
                    platformLines.Select(ParsePlatformBudget).ToArray(), _budgetRequired!.ButtonPressed);
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            SetPrefabError($"Erreur localisée · {exception.Message}");
            return false;
        }
    }

    private static PrefabVariantOverride ParseVariantOverride(string line)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !Enum.TryParse<PrefabVariantOverrideKind>(parts[0], true, out var kind))
            throw new FormatException($"Override invalide « {line} » ; utilisez Type|clé|valeur.");
        return new(kind, parts[1], parts[2]);
    }

    private static PrefabCompositionNode ParseCompositionNode(string line)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 7) throw new FormatException($"Nœud invalide « {line} » ; 7 champs sont requis.");
        var parent = parts[2].Length == 0 || parts[2].Equals("root", StringComparison.OrdinalIgnoreCase) || parts[2] == "-"
            ? (Guid?)null : Guid.Parse(parts[2]);
        var socket = parts[3].Length == 0 || parts[3] == "-" ? null : parts[3];
        var position = new Vector3Value(ParseFloat(parts[4]), ParseFloat(parts[5]), ParseFloat(parts[6]));
        return new(Guid.Parse(parts[0]), BlueprintId.Parse(parts[1]), parent, socket,
            new TransformValue(position, QuaternionValue.Identity, Vector3Value.One));
    }

    private static AssetBudgetMetrics ParseMetrics(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 7) throw new FormatException("Les mesures d’asset requièrent exactement 7 nombres.");
        return new(ParseInt(parts[0]), ParseInt(parts[1]), ParseInt(parts[2]), ParseLong(parts[3]),
            ParseInt(parts[4]), ParseInt(parts[5]), ParseInt(parts[6]));
    }

    private static AssetPlatformBudget ParsePlatformBudget(string line)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 9 || !Enum.TryParse<AssetTargetPlatform>(parts[0], true, out var platform) ||
            !bool.TryParse(parts[8], out var gate))
            throw new FormatException($"Budget invalide « {line} » ; utilisez plateforme|7 limites|gate.");
        return new(platform, ParseInt(parts[1]), ParseInt(parts[2]), ParseInt(parts[3]), ParseLong(parts[4]),
            ParseInt(parts[5]), ParseInt(parts[6]), ParseInt(parts[7]), gate);
    }

    private static float ParseFloat(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static long ParseLong(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> SplitLines(string value) => value
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void SetPrefabError(string message)
    {
        _prefabPreview!.Text = message;
        _prefabPreview.AddThemeColorOverride("font_color", new Color("ff8c8c"));
        SetStatus("Configuration prefab invalide.", true);
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
        _categoryPaths!.Editable = !readOnly;
        _authorCollections!.Editable = !readOnly;
        _variantBaseBlueprintId!.Editable = !readOnly;
        _variantOverrides!.Editable = !readOnly;
        _variantOverridesEditor?.SetEditable(!readOnly);
        _variantRequired!.Disabled = readOnly;
        _compositionNodes!.Editable = !readOnly;
        _compositionNodesEditor?.SetEditable(!readOnly);
        _compositionRules!.Editable = !readOnly;
        _compositionParameters!.Editable = !readOnly;
        _compositionScripts!.Editable = !readOnly;
        _compositionRequired!.Disabled = readOnly;
        _assetMetrics!.Editable = !readOnly;
        _platformBudgets!.Editable = !readOnly;
        _platformBudgetsEditor?.SetEditable(!readOnly);
        _budgetRequired!.Disabled = readOnly;
        _sourceEntityId!.Editable = !readOnly;
        _promotedKeys!.Editable = !readOnly;
        _excludedInstances!.Editable = !readOnly;
        _versionBump!.Disabled = readOnly;
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
        _categoryPaths!.Text = string.Empty;
        _loadedPromotedDefaults = [];
        ClearPrefabProfiles();
    }

    private void LoadPrefabProfiles(ProjectState state, BlueprintDefinition blueprint)
    {
        var authoring = blueprint.Authoring;
        _authorCollections!.Text = string.Join(", ", authoring.AuthorCollections);
        if (authoring.PrefabVariant is { } variant)
        {
            _draftVariantId = variant.Id;
            _variantBaseBlueprintId!.Text = variant.BaseBlueprintId.ToString();
            _variantOverrides!.Text = string.Join('\n', variant.Overrides.Select(item =>
                $"{item.Kind}|{item.Key}|{item.Value}"));
            _variantRequired!.ButtonPressed = variant.RequiredAtRuntime;
        }
        else
        {
            _draftVariantId = Guid.NewGuid();
            _variantBaseBlueprintId!.Text = string.Empty;
            _variantOverrides!.Text = string.Empty;
            _variantRequired!.ButtonPressed = false;
        }
        if (authoring.PrefabComposition is { } composition)
        {
            _draftCompositionId = composition.Id;
            _compositionNodes!.Text = string.Join('\n', composition.Nodes.Select(item =>
                $"{item.Id:D}|{item.BlueprintId}|{item.ParentNodeId?.ToString("D") ?? "root"}|" +
                $"{item.Socket ?? "-"}|{FormatNumber(item.Transform.Position.X)}|" +
                $"{FormatNumber(item.Transform.Position.Y)}|{FormatNumber(item.Transform.Position.Z)}"));
            _compositionRules!.Text = string.Join(", ", composition.AssemblyRules);
            _compositionParameters!.Text = string.Join(", ", composition.Parameters);
            _compositionScripts!.Text = string.Join(", ", composition.AssemblyScripts);
            _compositionRequired!.ButtonPressed = composition.RequiredAtRuntime;
        }
        else
        {
            _draftCompositionId = Guid.NewGuid();
            _compositionNodes!.Text = string.Empty;
            _compositionRules!.Text = string.Empty;
            _compositionParameters!.Text = string.Empty;
            _compositionScripts!.Text = string.Empty;
            _compositionRequired!.ButtonPressed = false;
        }
        if (authoring.AssetBudget is { } budget)
        {
            _draftBudgetId = budget.Id;
            var metrics = budget.Metrics;
            _assetMetrics!.Text = $"{metrics.TriangleCount}, {metrics.MaterialCount}, {metrics.TextureCount}, " +
                $"{metrics.MemoryBytes}, {metrics.AnimationCount}, {metrics.LodLevelCount}, {metrics.CollisionShapeCount}";
            _platformBudgets!.Text = string.Join('\n', budget.Platforms.Select(item =>
                $"{item.Platform}|{item.MaximumTriangles}|{item.MaximumMaterials}|{item.MaximumTextures}|" +
                $"{item.MaximumMemoryBytes}|{item.MaximumAnimations}|{item.MaximumLodLevels}|" +
                $"{item.MaximumCollisionShapes}|{item.GateBuild.ToString().ToLowerInvariant()}"));
            _budgetRequired!.ButtonPressed = budget.RequiredAtRuntime;
        }
        else
        {
            _draftBudgetId = Guid.NewGuid();
            _assetMetrics!.Text = string.Empty;
            _platformBudgets!.Text = string.Empty;
            _budgetRequired!.ButtonPressed = false;
        }
        var evaluations = new BlueprintPrefabRuntimeService(state.Project).EvaluateBudget(blueprint.Id, false);
        _prefabPreview!.Text = authoring.PrefabVariant is null && authoring.PrefabComposition is null &&
                               authoring.AssetBudget is null
            ? "État vide · aucun profil prefab ou budget configuré."
            : $"Profil chargé · {authoring.PrefabVariant?.Overrides.Count ?? 0} delta(s) · " +
              $"{authoring.PrefabComposition?.Nodes.Count ?? 0} nœud(s) · " +
              $"{evaluations.Count(item => item.Status == AssetBudgetEvaluationStatus.Passed)} budget(s) conforme(s)";
        _prefabPreview.AddThemeColorOverride("font_color", new Color(authoring.PrefabVariant is null &&
            authoring.PrefabComposition is null && authoring.AssetBudget is null ? "a899b9" : "79d5ad"));
    }

    private void ClearPrefabProfiles()
    {
        _draftVariantId = Guid.NewGuid();
        _draftCompositionId = Guid.NewGuid();
        _draftBudgetId = Guid.NewGuid();
        _authorCollections!.Text = string.Empty;
        _variantBaseBlueprintId!.Text = string.Empty;
        _variantOverrides!.Text = string.Empty;
        _variantRequired!.ButtonPressed = false;
        _compositionNodes!.Text = string.Empty;
        _compositionRules!.Text = string.Empty;
        _compositionParameters!.Text = string.Empty;
        _compositionScripts!.Text = string.Empty;
        _compositionRequired!.ButtonPressed = false;
        _assetMetrics!.Text = string.Empty;
        _platformBudgets!.Text = string.Empty;
        _budgetRequired!.ButtonPressed = false;
        _prefabPreview!.Text = "État vide · aucun profil prefab ou budget configuré.";
        _prefabPreview.AddThemeColorOverride("font_color", new Color("a899b9"));
    }

    private void PrepareR058Capture()
    {
        var project = _getState?.Invoke()?.Project;
        if (project is null || project.Blueprints.Count < 2) return;
        var target = project.Blueprints.FirstOrDefault(candidate => !candidate.IsReadOnly() &&
            project.Blueprints.Any(other => other.Id != candidate.Id && other.Kind == candidate.Kind));
        if (target is null) return;
        var baseBlueprint = project.Blueprints.First(other => other.Id != target.Id && other.Kind == target.Kind);
        ConfigurePrefabForSmokeTest(target.Id, baseBlueprint.Id, baseBlueprint.Id, dryRun: true);
    }

    private static string FormatNumber(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private bool TryBuildAuthoring(out BlueprintAuthoringMetadata metadata)
    {
        try
        {
            if (!TryBuildPrefabProfiles(out var variant, out var composition, out var budget))
            {
                metadata = null!;
                return false;
            }
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
                SplitList(_tags!.Text),
                SplitList(_categoryPaths!.Text),
                _loadedPromotedDefaults,
                SplitList(_authorCollections!.Text),
                variant,
                composition,
                budget);
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
