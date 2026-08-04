#if TOOLS
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssetContentBrowserWindow : Window
{
    private Func<string>? _getRepositoryRoot;
    private Func<AssetProcessingProfileState?>? _getProfileState;
    private Func<AssetProcessingProfile, bool, bool>? _applyProfile;
    private Func<bool>? _undoProfile;
    private Func<bool>? _redoProfile;
    private Func<AssetPackageManifestState?>? _getManifestState;
    private Func<AssetCatalogItem, AssetProcessingProfile, bool, bool, AssetPackageManifest?>? _prepareManifest;
    private Func<bool>? _undoManifest;
    private Func<bool>? _redoManifest;
    private Func<AssetExternalEditState?>? _getExternalEditState;
    private Func<AssetCatalogItem, bool, bool, AssetExternalEditConfiguration?>? _configureExternalEdit;
    private Func<bool>? _undoExternalEdit;
    private Func<bool>? _redoExternalEdit;
    private Action<Window>? _brandWindow;
    private AssetCatalogBrowser? _browser;
    private AssetCatalogLoadResult? _load;
    private IReadOnlyList<AssetCatalogItem> _visibleItems = [];
    private LineEdit? _search;
    private OptionButton? _source;
    private OptionButton? _category;
    private OptionButton? _availability;
    private ItemList? _results;
    private Label? _catalogMetric;
    private Label? _localMetric;
    private Label? _resultMetric;
    private Label? _status;
    private Label? _assetTitle;
    private Label? _assetIdentity;
    private Label? _assetGovernance;
    private Label? _assetFacets;
    private Label? _assetPaths;
    private TextureRect? _preview;
    private Label? _previewPlaceholder;
    private Button? _planImport;
    private OptionButton? _profileKind;
    private SpinBox? _profileBudgetMb;
    private SpinBox? _textureMaximumDimension;
    private OptionButton? _textureCompression;
    private CheckButton? _modelAnimations;
    private CheckButton? _modelCollisions;
    private Button? _saveProfile;
    private Button? _undoProfileButton;
    private Button? _redoProfileButton;
    private Label? _manifestSummary;
    private CheckButton? _includeVfx;
    private Button? _prepareManifestButton;
    private Button? _undoManifestButton;
    private Button? _redoManifestButton;
    private Label? _externalEditSummary;
    private CheckButton? _watchExternalChanges;
    private Button? _configureExternalEditButton;
    private Button? _undoExternalEditButton;
    private Button? _redoExternalEditButton;
    private AssetCatalogItem? _selectedItem;

    public int CatalogCount => _browser?.Catalog.Items.Count ?? 0;

    public int VisibleAssetCount => _visibleItems.Count;

    public int SourceCount => _browser?.Sources.Count ?? 0;

    public bool SelectedHasPreviewMetadata => !string.IsNullOrWhiteSpace(_selectedItem?.RelativePreviewPath) ||
        !string.IsNullOrWhiteSpace(_selectedItem?.ThumbnailUrl);

    public bool SelectedCanPlanImport => _selectedItem?.CanPlanImport == true;

    public void Initialize(
        Func<string> getRepositoryRoot,
        Func<AssetProcessingProfileState?> getProfileState,
        Func<AssetProcessingProfile, bool, bool> applyProfile,
        Func<bool> undoProfile,
        Func<bool> redoProfile,
        Func<AssetPackageManifestState?> getManifestState,
        Func<AssetCatalogItem, AssetProcessingProfile, bool, bool, AssetPackageManifest?> prepareManifest,
        Func<bool> undoManifest,
        Func<bool> redoManifest,
        Func<AssetExternalEditState?> getExternalEditState,
        Func<AssetCatalogItem, bool, bool, AssetExternalEditConfiguration?> configureExternalEdit,
        Func<bool> undoExternalEdit,
        Func<bool> redoExternalEdit,
        Action<Window> brandWindow)
    {
        _getRepositoryRoot = getRepositoryRoot ?? throw new ArgumentNullException(nameof(getRepositoryRoot));
        _getProfileState = getProfileState ?? throw new ArgumentNullException(nameof(getProfileState));
        _applyProfile = applyProfile ?? throw new ArgumentNullException(nameof(applyProfile));
        _undoProfile = undoProfile ?? throw new ArgumentNullException(nameof(undoProfile));
        _redoProfile = redoProfile ?? throw new ArgumentNullException(nameof(redoProfile));
        _getManifestState = getManifestState ?? throw new ArgumentNullException(nameof(getManifestState));
        _prepareManifest = prepareManifest ?? throw new ArgumentNullException(nameof(prepareManifest));
        _undoManifest = undoManifest ?? throw new ArgumentNullException(nameof(undoManifest));
        _redoManifest = redoManifest ?? throw new ArgumentNullException(nameof(redoManifest));
        _getExternalEditState = getExternalEditState ?? throw new ArgumentNullException(nameof(getExternalEditState));
        _configureExternalEdit = configureExternalEdit ?? throw new ArgumentNullException(nameof(configureExternalEdit));
        _undoExternalEdit = undoExternalEdit ?? throw new ArgumentNullException(nameof(undoExternalEdit));
        _redoExternalEdit = redoExternalEdit ?? throw new ArgumentNullException(nameof(redoExternalEdit));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Assets & Content Browser · OpenRPG Forge";
        MinSize = new Vector2I(1240, 780);
        Size = new Vector2I(1240, 780);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1240, 780));
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        _selectedItem = null;
        ClearSelectionDetails();
        try
        {
            var repositoryRoot = _getRepositoryRoot?.Invoke();
            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                SetStatus("Dépôt OpenRPG Forge introuvable.", true);
                return;
            }

            _load = AssetCatalogLoader.Load(repositoryRoot);
            if (_load.Catalog is null)
            {
                _browser = null;
                _results?.Clear();
                SetStatus(string.Join("\n", _load.Diagnostics.Select(item => item.Message)), true);
                return;
            }

            _browser = new AssetCatalogBrowser(_load.Catalog);
            PopulateFacets();
            RefreshResults();
            var local = _browser.Catalog.Items.Count(item => item.Availability != AssetAvailability.CatalogOnly);
            _catalogMetric!.Text = $"{CatalogCount} entrées · schéma v{_browser.Catalog.SchemaVersion}";
            _localMetric!.Text = $"{local} locales · {CatalogCount - local} à installer";
            SetStatus(
                _load.Diagnostics.Count == 0
                    ? $"✓ Catalogue prêt · {_browser.Catalog.LicensePolicy} · Vault {_load.VaultRoot}"
                    : $"Catalogue chargé avec {_load.Diagnostics.Count} diagnostic(s).",
                _load.Diagnostics.Count > 0);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            SetStatus($"Chargement impossible : {exception.Message}", true);
        }
    }

    public int FilterForSmokeTest(
        string text,
        string? source = null,
        string? category = null,
        AssetAvailability? availability = null)
    {
        if (_browser is null)
        {
            Refresh();
        }

        _search!.Text = text;
        SelectOption(_source!, source);
        SelectOption(_category!, category);
        _availability!.Selected = availability switch
        {
            AssetAvailability.CatalogOnly => 1,
            AssetAvailability.OriginalAvailable => 2,
            AssetAvailability.ExtractedAvailable => 3,
            _ => 0,
        };
        RefreshResults();
        return VisibleAssetCount;
    }

    public bool SelectAssetForSmokeTest(string externalId)
    {
        var index = _visibleItems.ToList().FindIndex(item => item.ExternalId == externalId);
        if (index < 0)
        {
            return false;
        }

        _results!.Select(index);
        SelectResult(index);
        return true;
    }

    public bool PreviewImportForSmokeTest() => PreviewImport();

    public bool PreviewProfileCommandForSmokeTest()
    {
        if (_profileBudgetMb is null)
        {
            return false;
        }

        _profileBudgetMb.Value = Math.Max(1, _profileBudgetMb.Value + 1);
        return SaveProfile(dryRun: true);
    }

    public bool PreviewManifestCommandForSmokeTest() => PrepareManifest(dryRun: true);

    public bool PreviewExternalEditCommandForSmokeTest() => ConfigureExternalEdit(dryRun: true);

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 20);
        }

        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);
        var title = new Label { Text = "ASSETS & CONTENT BROWSER", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Explorer le Vault local par facettes, vérifier provenance et licence, puis préparer un import sûr.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var metrics = new HBoxContainer();
        metrics.AddThemeConstantOverride("separation", 12);
        root.AddChild(metrics);
        _catalogMetric = AddMetric(metrics, "CATALOGUE", "Chargement en attente");
        _localMetric = AddMetric(metrics, "DISPONIBILITÉ", "Vault non analysé");
        _resultMetric = AddMetric(metrics, "RÉSULTATS", "Aucun filtre");

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);
        var filters = AddCard(body, new Vector2(245, 0));
        filters.AddChild(Section("RECHERCHE & FACETTES"));
        _search = new LineEdit { PlaceholderText = "Nom, tag, style…" };
        _search.TextChanged += _ => RefreshResults();
        filters.AddChild(_search);
        filters.AddChild(FieldLabel("SOURCE"));
        _source = new OptionButton();
        _source.ItemSelected += _ => RefreshResults();
        filters.AddChild(_source);
        filters.AddChild(FieldLabel("CATÉGORIE"));
        _category = new OptionButton();
        _category.ItemSelected += _ => RefreshResults();
        filters.AddChild(_category);
        filters.AddChild(FieldLabel("DISPONIBILITÉ"));
        _availability = new OptionButton();
        foreach (var label in new[] { "Toutes", "Catalogue seulement", "Original local", "Extrait local" })
        {
            _availability.AddItem(label);
        }

        _availability.ItemSelected += _ => RefreshResults();
        filters.AddChild(_availability);
        var facetHelp = new Label
        {
            Text = "Facettes disponibles : texte, source, catégorie, licence CC0 et état local. Les futures métriques 3D rejoindront ce même contrat.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        facetHelp.AddThemeColorOverride("font_color", new Color("aa9bc7"));
        filters.AddChild(facetHelp);

        var results = AddCard(body, new Vector2(450, 0), expand: true);
        results.AddChild(Section("RESSOURCES"));
        _results = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _results.AddThemeStyleboxOverride("panel", Card("0c0a13", "3d3150", 1, 6));
        _results.ItemSelected += index => SelectResult((int)index);
        results.AddChild(_results);

        var inspector = AddCard(body, new Vector2(455, 0));
        inspector.AddChild(Section("APERÇU & GOUVERNANCE"));
        var previewFrame = new PanelContainer { CustomMinimumSize = new Vector2(0, 190) };
        previewFrame.AddThemeStyleboxOverride("panel", Card("0c0a13", "3d3150", 1, 8));
        inspector.AddChild(previewFrame);
        _preview = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        previewFrame.AddChild(_preview);
        _previewPlaceholder = new Label
        {
            Text = "Sélectionnez une ressource",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _previewPlaceholder.AddThemeColorOverride("font_color", new Color("8f82a8"));
        previewFrame.AddChild(_previewPlaceholder);
        _assetTitle = new Label { Text = "Aucune sélection" };
        _assetTitle.AddThemeFontSizeOverride("font_size", 18);
        _assetTitle.AddThemeColorOverride("font_color", new Color("f5dfc6"));
        inspector.AddChild(_assetTitle);
        _assetIdentity = DetailLabel();
        _assetGovernance = DetailLabel();
        _assetFacets = DetailLabel();
        _assetPaths = DetailLabel();
        var detailsScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        inspector.AddChild(detailsScroll);
        var details = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        details.AddThemeConstantOverride("separation", 8);
        detailsScroll.AddChild(details);
        details.AddChild(_assetIdentity);
        details.AddChild(_assetGovernance);
        details.AddChild(_assetFacets);
        details.AddChild(_assetPaths);
        details.AddChild(Section("PROFIL D’IMPORT"));
        _profileKind = new OptionButton();
        _profileKind.AddItem("Modèle 3D", (int)AssetProcessingKind.Model);
        _profileKind.AddItem("Texture", (int)AssetProcessingKind.Texture);
        _profileKind.ItemSelected += _ => RefreshProfileKindControls();
        details.AddChild(_profileKind);
        _profileBudgetMb = new SpinBox
        {
            MinValue = 1,
            MaxValue = 2048,
            Step = 1,
            Prefix = "Budget ",
            Suffix = " Mio",
        };
        details.AddChild(_profileBudgetMb);
        _textureMaximumDimension = new SpinBox
        {
            MinValue = 64,
            MaxValue = 16384,
            Step = 64,
            Prefix = "Texture max ",
            Suffix = " px",
        };
        details.AddChild(_textureMaximumDimension);
        _textureCompression = new OptionButton();
        foreach (var compression in Enum.GetValues<TextureCompressionProfile>())
        {
            _textureCompression.AddItem(compression.ToString(), (int)compression);
        }

        details.AddChild(_textureCompression);
        _modelAnimations = new CheckButton { Text = "Importer les animations" };
        _modelCollisions = new CheckButton { Text = "Générer les collisions" };
        details.AddChild(_modelAnimations);
        details.AddChild(_modelCollisions);
        var profileActions = new HBoxContainer();
        profileActions.AddThemeConstantOverride("separation", 8);
        details.AddChild(profileActions);
        _saveProfile = new Button
        {
            Text = "Enregistrer le profil",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _saveProfile.Pressed += () => SaveProfile(dryRun: false);
        profileActions.AddChild(_saveProfile);
        _undoProfileButton = new Button { Text = "Annuler" };
        _undoProfileButton.Pressed += UndoProfile;
        profileActions.AddChild(_undoProfileButton);
        _redoProfileButton = new Button { Text = "Rétablir" };
        _redoProfileButton.Pressed += RedoProfile;
        profileActions.AddChild(_redoProfileButton);
        details.AddChild(Section("MANIFESTE D’ASSETS"));
        _manifestSummary = new Label
        {
            Text = "Aucun manifeste enregistré",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        details.AddChild(_manifestSummary);
        _includeVfx = new CheckButton { Text = "Inclure un descripteur VFX gouverné" };
        details.AddChild(_includeVfx);
        var manifestActions = new HBoxContainer();
        manifestActions.AddThemeConstantOverride("separation", 8);
        details.AddChild(manifestActions);
        _prepareManifestButton = new Button
        {
            Text = "Préparer + manifester",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _prepareManifestButton.Pressed += () => PrepareManifest(dryRun: false);
        manifestActions.AddChild(_prepareManifestButton);
        _undoManifestButton = new Button { Text = "Annuler" };
        _undoManifestButton.Pressed += UndoManifest;
        manifestActions.AddChild(_undoManifestButton);
        _redoManifestButton = new Button { Text = "Rétablir" };
        _redoManifestButton.Pressed += RedoManifest;
        manifestActions.AddChild(_redoManifestButton);
        details.AddChild(Section("ROUND-TRIP EXTERNE"));
        _externalEditSummary = new Label
        {
            Text = "Aucun round-trip configuré",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        details.AddChild(_externalEditSummary);
        _watchExternalChanges = new CheckButton
        {
            Text = "Surveiller les changements de la source",
            ButtonPressed = true,
        };
        details.AddChild(_watchExternalChanges);
        var externalEditActions = new HBoxContainer();
        externalEditActions.AddThemeConstantOverride("separation", 8);
        details.AddChild(externalEditActions);
        _configureExternalEditButton = new Button
        {
            Text = "Configurer Blender",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _configureExternalEditButton.Pressed += () => ConfigureExternalEdit(dryRun: false);
        externalEditActions.AddChild(_configureExternalEditButton);
        _undoExternalEditButton = new Button { Text = "Annuler" };
        _undoExternalEditButton.Pressed += UndoExternalEdit;
        externalEditActions.AddChild(_undoExternalEditButton);
        _redoExternalEditButton = new Button { Text = "Rétablir" };
        _redoExternalEditButton.Pressed += RedoExternalEdit;
        externalEditActions.AddChild(_redoExternalEditButton);
        _planImport = new Button
        {
            Text = "Prévisualiser l’import",
            Disabled = true,
            CustomMinimumSize = new Vector2(0, 40),
        };
        _planImport.AddThemeStyleboxOverride("normal", Card("d84f32", "ff9a73", 1, 7));
        _planImport.Pressed += () => PreviewImport();
        inspector.AddChild(_planImport);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        _status = new Label
        {
            Text = "Catalogue en attente",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        footer.AddChild(_status);
        var refresh = new Button { Text = "Actualiser", CustomMinimumSize = new Vector2(120, 38) };
        refresh.Pressed += Refresh;
        footer.AddChild(refresh);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(100, 38) };
        close.Pressed += Hide;
        footer.AddChild(close);
        CloseRequested += Hide;
    }

    private void PopulateFacets()
    {
        Populate(_source!, "Toutes les sources", _browser!.Sources);
        Populate(_category!, "Toutes les catégories", _browser.Categories);
        _availability!.Selected = 0;
    }

    private void RefreshResults()
    {
        if (_browser is null || _results is null)
        {
            return;
        }

        var source = SelectedFacet(_source!);
        var category = SelectedFacet(_category!);
        var availability = _availability!.Selected switch
        {
            1 => AssetAvailability.CatalogOnly,
            2 => AssetAvailability.OriginalAvailable,
            3 => AssetAvailability.ExtractedAvailable,
            _ => (AssetAvailability?)null,
        };
        _visibleItems = _browser.Search(new AssetCatalogQuery(
            _search!.Text,
            source,
            category,
            null,
            availability));
        _results.Clear();
        foreach (var item in _visibleItems)
        {
            var availabilityBadge = item.Availability switch
            {
                AssetAvailability.ExtractedAvailable => "EXTRAIT",
                AssetAvailability.OriginalAvailable => "LOCAL",
                _ => "CATALOGUE",
            };
            _results.AddItem(
                $"{item.DisplayName} · {availabilityBadge} · {item.Source} · {item.LicenseSpdx} · {FormatBytes(item.TotalBytes)}");
            _results.SetItemTooltip(
                _results.ItemCount - 1,
                $"UUID {item.Id}\nHash {item.ContentSha256}\n{item.ExternalId}\n{item.SourcePage}");
        }

        _resultMetric!.Text = $"{_visibleItems.Count} sur {CatalogCount} · limite 300";
        if (_visibleItems.Count == 0)
        {
            _results.AddItem("Aucune ressource ne correspond aux facettes.");
            _results.SetItemSelectable(0, false);
        }

        _selectedItem = null;
        ClearSelectionDetails();
    }

    private void SelectResult(int index)
    {
        if (index < 0 || index >= _visibleItems.Count || _load is null)
        {
            return;
        }

        _selectedItem = _visibleItems[index];
        var item = _selectedItem;
        _assetTitle!.Text = item.DisplayName;
        _assetIdentity!.Text =
            $"{item.ExternalId}\nUUID · {item.Id}\nSchéma · v{item.SchemaVersion}\nSource · {item.Source}";
        _assetGovernance!.Text =
            $"Licence · {item.LicenseSpdx} {(item.LicenseVerified ? "✓ vérifiée" : "✗ à vérifier")}\n" +
            $"SHA-256 · {item.ContentSha256[..16]}…\nProvenance · {item.Provenance}";
        _assetFacets!.Text =
            $"Catégories · {string.Join(", ", item.Categories)}\nTags · {string.Join(", ", item.Tags.Take(12))}\n" +
            $"État · {FormatAvailability(item.Availability)}";
        _assetPaths!.Text =
            $"Modèle · {item.RelativeModelPath ?? "non catalogué"}\n" +
            $"Aperçu · {item.RelativePreviewPath ?? item.ThumbnailUrl ?? "indisponible"}";
        _planImport!.Disabled = !item.CanPlanImport;
        RefreshProfileControls();
        RefreshManifestControls();
        RefreshExternalEditControls();
        LoadPreview(item, _load.VaultRoot);
    }

    private void LoadPreview(AssetCatalogItem item, string vaultRoot)
    {
        _preview!.Texture = null;
        _previewPlaceholder!.Visible = true;
        _previewPlaceholder.Text = item.ThumbnailUrl is not null
            ? "Aperçu distant catalogué\nMiniature locale non installée"
            : "Aperçu local indisponible\nMétadonnées prêtes";
        if (string.IsNullOrWhiteSpace(item.RelativePreviewPath) ||
            !AssetCatalogValidator.IsSafeRelativePath(item.RelativePreviewPath))
        {
            return;
        }

        var path = Path.Combine(vaultRoot, item.RelativePreviewPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return;
        }

        var image = Image.LoadFromFile(path);
        if (image.IsEmpty())
        {
            return;
        }

        _preview.Texture = ImageTexture.CreateFromImage(image);
        _previewPlaceholder.Visible = false;
    }

    private bool PreviewImport()
    {
        if (_selectedItem is null)
        {
            return false;
        }

        var kind = SelectedProfileKind();
        var profile = _getProfileState?.Invoke()?.Find(_selectedItem.Id, kind);
        var result = AssetImportPlanner.Plan(_selectedItem, profile);
        if (!result.Succeeded)
        {
            SetStatus(string.Join("\n", result.Diagnostics.Select(item => item.Message)), true);
            return false;
        }

        SetStatus(
            $"Aperçu prêt · {result.Plan!.SourceRelativePath} → {result.Plan.DestinationRelativeDirectory} · " +
            $"{result.Plan.Profile.CanonicalFormat.ToUpperInvariant()} · {result.Plan.Tasks.Count} tâche(s) · " +
            $"cache {result.Plan.CacheKey[..12]}… · aucun fichier écrit",
            false);
        return true;
    }

    private bool SaveProfile(bool dryRun)
    {
        var profile = CreateProfileFromControls();
        if (profile is null || _applyProfile is null)
        {
            return false;
        }

        if (!_applyProfile(profile, true))
        {
            SetStatus("Le profil d’import est invalide ; aucun fichier n’a été modifié.", true);
            return false;
        }

        if (dryRun)
        {
            SetStatus(
                $"Aperçu du profil {profile.Kind} prêt · diff transactionnel · aucun fichier écrit",
                false);
            return true;
        }

        if (!_applyProfile(profile, false))
        {
            SetStatus("La sauvegarde atomique du profil d’import a échoué.", true);
            return false;
        }

        SetStatus(
            $"✓ Profil {profile.Kind} enregistré · révision {_getProfileState?.Invoke()?.Revision}",
            false);
        RefreshProfileControls();
        return true;
    }

    private AssetProcessingProfile? CreateProfileFromControls()
    {
        if (_selectedItem is null || _profileBudgetMb is null)
        {
            return null;
        }

        AssetProcessingProfile basis;
        var kind = SelectedProfileKind();
        try
        {
            basis = _getProfileState?.Invoke()?.Find(_selectedItem.Id, kind) ??
                AssetImportPlanner.CreateDefaultProfile(_selectedItem, kind);
        }
        catch (ArgumentException exception)
        {
            SetStatus(exception.Message, true);
            return null;
        }

        var budget = checked((long)_profileBudgetMb.Value * 1024 * 1024);
        if (kind == AssetProcessingKind.Texture)
        {
            var texture = basis.Texture! with
            {
                Compression = (TextureCompressionProfile)_textureCompression!.GetSelectedId(),
                MaximumDimension = (int)_textureMaximumDimension!.Value,
            };
            return new AssetProcessingProfile(
                basis.AssetId,
                basis.SchemaVersion,
                basis.Kind,
                basis.SourceFormat,
                basis.CanonicalFormat,
                basis.LicenseSpdx,
                basis.ContentSha256,
                basis.Provenance,
                budget,
                texture: texture);
        }

        var model = basis.Model! with
        {
            ImportAnimations = _modelAnimations!.ButtonPressed,
            GenerateCollisions = _modelCollisions!.ButtonPressed,
        };
        return new AssetProcessingProfile(
            basis.AssetId,
            basis.SchemaVersion,
            basis.Kind,
            basis.SourceFormat,
            basis.CanonicalFormat,
            basis.LicenseSpdx,
            basis.ContentSha256,
            basis.Provenance,
            budget,
            model: model);
    }

    private void RefreshProfileControls()
    {
        if (_selectedItem is null || _profileKind is null)
        {
            return;
        }

        _profileKind.Disabled = false;
        ApplyProfileToControls(SelectedProfileKind());
        RefreshHistoryButtons();
    }

    private void RefreshProfileKindControls()
    {
        if (_selectedItem is null)
        {
            return;
        }

        ApplyProfileToControls(SelectedProfileKind());
    }

    private void ApplyProfileToControls(AssetProcessingKind kind)
    {
        if (_selectedItem is null || _profileBudgetMb is null)
        {
            return;
        }

        AssetProcessingProfile profile;
        try
        {
            profile = _getProfileState?.Invoke()?.Find(_selectedItem.Id, kind) ??
                AssetImportPlanner.CreateDefaultProfile(_selectedItem, kind);
        }
        catch (ArgumentException exception)
        {
            _saveProfile!.Disabled = true;
            SetStatus(exception.Message, true);
            return;
        }

        _saveProfile!.Disabled = false;
        _profileBudgetMb.Value = Math.Max(1, profile.MaximumOutputBytes / (1024d * 1024d));
        var textureVisible = kind == AssetProcessingKind.Texture;
        _textureMaximumDimension!.Visible = textureVisible;
        _textureCompression!.Visible = textureVisible;
        _modelAnimations!.Visible = !textureVisible;
        _modelCollisions!.Visible = !textureVisible;
        if (textureVisible)
        {
            _textureMaximumDimension.Value = profile.Texture!.MaximumDimension;
            SelectOptionById(_textureCompression, (int)profile.Texture.Compression);
        }
        else
        {
            _modelAnimations.ButtonPressed = profile.Model!.ImportAnimations;
            _modelCollisions.ButtonPressed = profile.Model.GenerateCollisions;
        }
    }

    private AssetProcessingKind SelectedProfileKind() =>
        _profileKind is null
            ? AssetProcessingKind.Model
            : (AssetProcessingKind)_profileKind.GetSelectedId();

    private void UndoProfile()
    {
        if (_undoProfile?.Invoke() == true)
        {
            SetStatus("Profil d’import annulé et document canonique restauré.", false);
            RefreshProfileControls();
        }
    }

    private void RedoProfile()
    {
        if (_redoProfile?.Invoke() == true)
        {
            SetStatus("Profil d’import rétabli et document canonique sauvegardé.", false);
            RefreshProfileControls();
        }
    }

    private void RefreshHistoryButtons()
    {
        var state = _getProfileState?.Invoke();
        _undoProfileButton!.Disabled = state is null || _undoProfile is null;
        _redoProfileButton!.Disabled = state is null || _redoProfile is null;
    }

    private bool PrepareManifest(bool dryRun)
    {
        if (_selectedItem is null || _prepareManifest is null)
        {
            return false;
        }

        var profile = CreateProfileFromControls();
        if (profile is null)
        {
            return false;
        }

        if (!dryRun)
        {
            if (!SaveProfile(dryRun: false))
            {
                return false;
            }

            profile = _getProfileState?.Invoke()?.Find(_selectedItem.Id, profile.Kind) ?? profile;
        }

        var manifest = _prepareManifest(
            _selectedItem,
            profile,
            _includeVfx?.ButtonPressed == true,
            dryRun);
        if (manifest is null)
        {
            SetStatus(
                dryRun
                    ? "Le manifeste ne peut pas être prévisualisé."
                    : "La préparation ou la sauvegarde du manifeste a échoué.",
                true);
            return false;
        }

        SetStatus(
            dryRun
                ? $"Aperçu du manifeste prêt · {manifest.Chunks.Count} chunk · aucun fichier écrit"
                : $"✓ Asset préparé · manifeste {manifest.Id} · {manifest.Chunks.Count} chunk",
            false);
        RefreshManifestControls();
        return true;
    }

    private void UndoManifest()
    {
        if (_undoManifest?.Invoke() == true)
        {
            SetStatus("Manifeste d’assets annulé et document canonique restauré.", false);
            RefreshManifestControls();
        }
    }

    private void RedoManifest()
    {
        if (_redoManifest?.Invoke() == true)
        {
            SetStatus("Manifeste d’assets rétabli et document canonique sauvegardé.", false);
            RefreshManifestControls();
        }
    }

    private void RefreshManifestControls()
    {
        if (_manifestSummary is null || _selectedItem is null)
        {
            return;
        }

        var manifest = _getManifestState?.Invoke()?.Find(_selectedItem.Id);
        _manifestSummary.Text = manifest is null
            ? "Aucun manifeste enregistré · la préparation restera confinée au projet"
            : $"{manifest.Id} · {manifest.Chunks.Count} chunk · " +
              $"collision {(manifest.Collision is null ? "non" : "oui")} · " +
              $"animation {(manifest.Animation is null ? "non" : "oui")} · " +
              $"VFX {(manifest.Vfx is null ? "non" : "oui")} · " +
              $"LOD {manifest.Lod?.Levels.Count ?? 0}";
        _includeVfx!.ButtonPressed = manifest?.Vfx is not null;
        _prepareManifestButton!.Disabled = !_selectedItem.CanPlanImport;
        var state = _getManifestState?.Invoke();
        _undoManifestButton!.Disabled = state is null || _undoManifest is null;
        _redoManifestButton!.Disabled = state is null || _redoManifest is null;
    }

    private bool ConfigureExternalEdit(bool dryRun)
    {
        if (_selectedItem is null || _configureExternalEdit is null)
        {
            return false;
        }

        var configuration = _configureExternalEdit(
            _selectedItem,
            _watchExternalChanges?.ButtonPressed != false,
            dryRun);
        if (configuration is null)
        {
            SetStatus(
                dryRun
                    ? "Le round-trip ne peut pas être prévisualisé ; préparez d’abord le manifeste du modèle."
                    : "La configuration du round-trip externe a échoué.",
                true);
            return false;
        }

        SetStatus(
            dryRun
                ? $"Aperçu Blender prêt · {configuration.Operations.Count} opérations typées · aucun fichier écrit"
                : $"✓ Round-trip Blender configuré · {configuration.Id} · requête worker générée",
            false);
        RefreshExternalEditControls();
        return true;
    }

    private void UndoExternalEdit()
    {
        if (_undoExternalEdit?.Invoke() == true)
        {
            SetStatus("Round-trip externe annulé et configuration canonique restaurée.", false);
            RefreshExternalEditControls();
        }
    }

    private void RedoExternalEdit()
    {
        if (_redoExternalEdit?.Invoke() == true)
        {
            SetStatus("Round-trip externe rétabli et configuration canonique sauvegardée.", false);
            RefreshExternalEditControls();
        }
    }

    private void RefreshExternalEditControls()
    {
        if (_externalEditSummary is null || _selectedItem is null)
        {
            return;
        }

        var configuration = _getExternalEditState?.Invoke()?.Find(_selectedItem.Id);
        _externalEditSummary.Text = configuration is null
            ? "Aucun round-trip configuré · Blender reste optionnel et hors du runtime"
            : $"{configuration.Tool} · {configuration.Operations.Count} opérations · " +
              $"{string.Join(", ", configuration.TargetPlatforms)} · " +
              $"surveillance {(configuration.WatchForChanges ? "active" : "inactive")}";
        _watchExternalChanges!.ButtonPressed = configuration?.WatchForChanges ?? true;
        var manifest = _getManifestState?.Invoke()?.Find(_selectedItem.Id);
        _configureExternalEditButton!.Disabled = manifest is null ||
            !manifest.Chunks.Any(chunk => chunk.Role == AssetChunkRole.Model);
        var state = _getExternalEditState?.Invoke();
        _undoExternalEditButton!.Disabled = state is null || _undoExternalEdit is null;
        _redoExternalEditButton!.Disabled = state is null || _redoExternalEdit is null;
    }

    private static void SelectOptionById(OptionButton option, int id)
    {
        for (var index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == id)
            {
                option.Selected = index;
                return;
            }
        }
    }

    private void ClearSelectionDetails()
    {
        if (_assetTitle is null)
        {
            return;
        }

        _assetTitle.Text = "Aucune sélection";
        _assetIdentity!.Text = "Choisissez une ressource pour inspecter son identité stable.";
        _assetGovernance!.Text = "Licence, hash et provenance apparaîtront ici.";
        _assetFacets!.Text = "Catégories, tags et disponibilité apparaîtront ici.";
        _assetPaths!.Text = "Les chemins du Vault restent en lecture seule.";
        _preview!.Texture = null;
        _previewPlaceholder!.Visible = true;
        _previewPlaceholder.Text = "Sélectionnez une ressource";
        _planImport!.Disabled = true;
        if (_saveProfile is not null)
        {
            _saveProfile.Disabled = true;
            _profileKind!.Disabled = true;
        }

        if (_prepareManifestButton is not null)
        {
            _prepareManifestButton.Disabled = true;
            _manifestSummary!.Text = "Aucun manifeste enregistré";
        }

        if (_configureExternalEditButton is not null)
        {
            _configureExternalEditButton.Disabled = true;
            _externalEditSummary!.Text = "Aucun round-trip configuré";
        }
    }

    private static void Populate(OptionButton option, string allLabel, IEnumerable<string> values)
    {
        option.Clear();
        option.AddItem(allLabel);
        foreach (var value in values)
        {
            option.AddItem(value);
        }

        option.Selected = 0;
    }

    private static void SelectOption(OptionButton option, string? value)
    {
        option.Selected = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        for (var index = 1; index < option.ItemCount; index++)
        {
            if (string.Equals(option.GetItemText(index), value, StringComparison.OrdinalIgnoreCase))
            {
                option.Selected = index;
                return;
            }
        }
    }

    private static string? SelectedFacet(OptionButton option) => option.Selected <= 0
        ? null
        : option.GetItemText(option.Selected);

    private static Label AddMetric(Container parent, string title, string value)
    {
        var card = AddCard(parent, expand: true);
        card.AddChild(Section(title));
        var label = DetailLabel();
        label.Text = value;
        label.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        card.AddChild(label);
        return label;
    }

    private static VBoxContainer AddCard(Container parent, Vector2? minimum = null, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimum ?? Vector2.Zero,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 14);
        }

        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 9);
        margin.AddChild(content);
        return content;
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color("ffb276"));
        return label;
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("9f8dbd"));
        return label;
    }

    private static Label DetailLabel() => new()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
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

    private static string FormatAvailability(AssetAvailability availability) => availability switch
    {
        AssetAvailability.ExtractedAvailable => "extrait et disponible localement",
        AssetAvailability.OriginalAvailable => "original disponible localement",
        _ => "catalogué, téléchargement requis",
    };

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_000_000_000 => $"{bytes / 1_000_000_000d:0.0} Go",
        >= 1_000_000 => $"{bytes / 1_000_000d:0.0} Mo",
        >= 1_000 => $"{bytes / 1_000d:0.0} Ko",
        _ => $"{bytes} o",
    };

    private void SetStatus(string text, bool error)
    {
        if (_status is null)
        {
            return;
        }

        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "83d6ae"));
    }
}
#endif
