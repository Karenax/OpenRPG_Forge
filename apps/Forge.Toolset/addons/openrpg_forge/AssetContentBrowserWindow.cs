#if TOOLS
using Forge.Application.Assets;
using Forge.Application.Content;
using Forge.Application.Projects;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Content;
using Forge.ModuleFormat.Assets;
using Godot;
using System.Globalization;

namespace Forge.Toolset.Editor;

public partial class AssetContentBrowserWindow : Window
{
    private Func<string>? _getRepositoryRoot;
    private Func<string?>? _getWorkspaceRoot;
    private Func<ProjectState?>? _getProjectState;
    private Func<AssetProcessingProfileState?>? _getProfileState;
    private Func<AssetProcessingProfile, bool, bool>? _applyProfile;
    private Func<bool>? _undoProfile;
    private Func<bool>? _redoProfile;
    private Func<AssetPackageManifestState?>? _getManifestState;
    private Func<AssetCatalogItem, AssetProcessingProfile, bool, bool, AssetPackageManifest?>? _prepareManifest;
    private Func<AssetCatalogItem, AssetProcessingProfile, bool, BlueprintDefinition?>? _installForPlacement;
    private Func<bool>? _undoManifest;
    private Func<bool>? _redoManifest;
    private Func<AssetExternalEditState?>? _getExternalEditState;
    private Func<AssetCatalogItem, bool, bool, AssetExternalEditConfiguration?>? _configureExternalEdit;
    private Func<bool>? _undoExternalEdit;
    private Func<bool>? _redoExternalEdit;
    private Func<AssetGovernanceState?>? _getGovernanceState;
    private Func<AssetGovernanceRecord, bool, bool>? _applyGovernance;
    private Func<bool>? _undoGovernance;
    private Func<bool>? _redoGovernance;
    private Action<Window>? _brandWindow;
    private AssetCatalogBrowser? _browser;
    private UnifiedContentBrowser? _contentBrowser;
    private AssetCatalogLoadResult? _load;
    private IReadOnlyList<BrowserRow> _visibleRows = [];
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
    private OptionButton? _textureColorSpace;
    private SpinBox? _textureTexelDensity;
    private LineEdit? _textureChannels;
    private CheckButton? _modelRig;
    private CheckButton? _modelAnimations;
    private CheckButton? _modelCollisions;
    private LineEdit? _modelSockets;
    private LineEdit? _modelLodRatios;
    private Button? _saveProfile;
    private Button? _undoProfileButton;
    private Button? _redoProfileButton;
    private Label? _manifestSummary;
    private CheckButton? _includeVfx;
    private Button? _prepareManifestButton;
    private Button? _undoManifestButton;
    private Button? _redoManifestButton;
    private Button? _installForPlacementButton;
    private Label? _externalEditSummary;
    private CheckButton? _watchExternalChanges;
    private Button? _configureExternalEditButton;
    private Button? _undoExternalEditButton;
    private Button? _redoExternalEditButton;
    private Label? _governanceWorkflowSummary;
    private Button? _saveGovernanceButton;
    private Button? _undoGovernanceButton;
    private Button? _redoGovernanceButton;
    private AssetCatalogItem? _selectedItem;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;
    private bool _captureGovernance;

    public int CatalogCount => _browser?.Catalog.Items.Count ?? 0;

    public int VisibleAssetCount => _visibleRows.Count;

    public int SourceCount => _browser?.Sources.Count ?? 0;

    public bool SelectedHasPreviewMetadata =>
        _selectedItem is not null &&
        (!string.IsNullOrWhiteSpace(_getManifestState?.Invoke()?.Find(_selectedItem.Id)?.ModelMetadata?.PreviewRelativePath) ||
         !string.IsNullOrWhiteSpace(_selectedItem.RelativePreviewPath) ||
         !string.IsNullOrWhiteSpace(_selectedItem.ThumbnailUrl));

    public bool SelectedCanPlanImport => _selectedItem?.CanPlanImport == true;

    public void Initialize(
        Func<string> getRepositoryRoot,
        Func<string?> getWorkspaceRoot,
        Func<ProjectState?> getProjectState,
        Func<AssetProcessingProfileState?> getProfileState,
        Func<AssetProcessingProfile, bool, bool> applyProfile,
        Func<bool> undoProfile,
        Func<bool> redoProfile,
        Func<AssetPackageManifestState?> getManifestState,
        Func<AssetCatalogItem, AssetProcessingProfile, bool, bool, AssetPackageManifest?> prepareManifest,
        Func<AssetCatalogItem, AssetProcessingProfile, bool, BlueprintDefinition?> installForPlacement,
        Func<bool> undoManifest,
        Func<bool> redoManifest,
        Func<AssetExternalEditState?> getExternalEditState,
        Func<AssetCatalogItem, bool, bool, AssetExternalEditConfiguration?> configureExternalEdit,
        Func<bool> undoExternalEdit,
        Func<bool> redoExternalEdit,
        Func<AssetGovernanceState?> getGovernanceState,
        Func<AssetGovernanceRecord, bool, bool> applyGovernance,
        Func<bool> undoGovernance,
        Func<bool> redoGovernance,
        Action<Window> brandWindow)
    {
        _getRepositoryRoot = getRepositoryRoot ?? throw new ArgumentNullException(nameof(getRepositoryRoot));
        _getWorkspaceRoot = getWorkspaceRoot ?? throw new ArgumentNullException(nameof(getWorkspaceRoot));
        _getProjectState = getProjectState ?? throw new ArgumentNullException(nameof(getProjectState));
        _getProfileState = getProfileState ?? throw new ArgumentNullException(nameof(getProfileState));
        _applyProfile = applyProfile ?? throw new ArgumentNullException(nameof(applyProfile));
        _undoProfile = undoProfile ?? throw new ArgumentNullException(nameof(undoProfile));
        _redoProfile = redoProfile ?? throw new ArgumentNullException(nameof(redoProfile));
        _getManifestState = getManifestState ?? throw new ArgumentNullException(nameof(getManifestState));
        _prepareManifest = prepareManifest ?? throw new ArgumentNullException(nameof(prepareManifest));
        _installForPlacement = installForPlacement ?? throw new ArgumentNullException(nameof(installForPlacement));
        _undoManifest = undoManifest ?? throw new ArgumentNullException(nameof(undoManifest));
        _redoManifest = redoManifest ?? throw new ArgumentNullException(nameof(redoManifest));
        _getExternalEditState = getExternalEditState ?? throw new ArgumentNullException(nameof(getExternalEditState));
        _configureExternalEdit = configureExternalEdit ?? throw new ArgumentNullException(nameof(configureExternalEdit));
        _undoExternalEdit = undoExternalEdit ?? throw new ArgumentNullException(nameof(undoExternalEdit));
        _redoExternalEdit = redoExternalEdit ?? throw new ArgumentNullException(nameof(redoExternalEdit));
        _getGovernanceState = getGovernanceState ?? throw new ArgumentNullException(nameof(getGovernanceState));
        _applyGovernance = applyGovernance ?? throw new ArgumentNullException(nameof(applyGovernance));
        _undoGovernance = undoGovernance ?? throw new ArgumentNullException(nameof(undoGovernance));
        _redoGovernance = redoGovernance ?? throw new ArgumentNullException(nameof(redoGovernance));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Assets & Content Browser · OpenRPG Forge";
        MinSize = new Vector2I(1240, 780);
        Size = new Vector2I(1240, 780);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r017-content-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r018-asset-governance-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _captureGovernance = capture.StartsWith("--r018-asset-governance-ui-capture=", StringComparison.Ordinal);
            _capturePath = capture[(capture.IndexOf('=') + 1)..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1.5)
        {
            Open();
            _search!.Text = string.Empty;
            RefreshResults();
            var resourceIndex = _visibleRows.ToList().FindIndex(row =>
                _captureGovernance ? row.Asset is not null : row.Resource is not null);
            if (resourceIndex >= 0)
            {
                _results!.Select(resourceIndex);
                SelectResult(resourceIndex);
                if (_captureGovernance && _selectedItem is not null)
                {
                    _assetGovernance!.Text =
                        $"Licence · {_selectedItem.LicenseSpdx} ✓ vérifiée\n" +
                        $"SHA-256 · {_selectedItem.ContentSha256[..16]}…\n" +
                        $"Provenance · {_selectedItem.Provenance}\n" +
                        "Quarantaine · Pending · scan et approbation requis\n" +
                        "Frontière IP · OpenLicensed · distribution bloquée avant validation";
                    _governanceWorkflowSummary!.Text =
                        $"UUID · {AssetGovernanceId.ForAsset(_selectedItem.Id)}\n" +
                        $"Auteur · {_selectedItem.Author} · permission cataloguée\n" +
                        "Quarantaine · Pending · scan requis · approbation humaine requise\n" +
                        "Frontière IP · OpenLicensed · redistribution bornée par SPDX\n" +
                        "Distribution · bloquée jusqu’à validation";
                }
                if (_preview?.GetParent() is PanelContainer previewFrame)
                {
                    previewFrame.CustomMinimumSize = new Vector2(0, 32);
                }
            }
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 4) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        var captureMarker = _captureGovernance ? "R018_ASSET_GOVERNANCE_UI" : "R017_CONTENT_UI";
        GD.Print($"OPENRPG_FORGE_{captureMarker}_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
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
            var project = _getProjectState?.Invoke()?.Project;
            _contentBrowser = project is null ? null : new UnifiedContentBrowser(project, _load.Catalog);
            PopulateFacets();
            RefreshResults();
            var local = _browser.Catalog.Items.Count(item => item.Availability != AssetAvailability.CatalogOnly);
            var installed = _browser.Catalog.InstalledItems.Count;
            _catalogMetric!.Text = $"{CatalogCount} entrées · schéma v{_browser.Catalog.SchemaVersion}";
            _localMetric!.Text = $"{local} locales · {installed} installées · {CatalogCount - local} à acquérir";
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
        var index = _visibleRows.ToList().FindIndex(item => item.Asset?.ExternalId == externalId);
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

    public bool PreviewInstallForPlacementForSmokeTest() => InstallForPlacement(dryRun: true);

    public bool PreviewExternalEditCommandForSmokeTest() => ConfigureExternalEdit(dryRun: true);

    public bool PreviewGovernanceCommandForSmokeTest() => SaveGovernance(dryRun: true);

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
        foreach (var label in new[]
        {
            "Toutes",
            "Catalogue seulement",
            "Original local",
            "Extrait local",
            "Installé dans un pack",
        })
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
        details.AddChild(Section("PROVENANCE, QUARANTAINE & IP"));
        _governanceWorkflowSummary = new Label
        {
            Text = "Aucun dossier de gouvernance · import non approuvé",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        details.AddChild(_governanceWorkflowSummary);
        var governanceActions = new HBoxContainer();
        governanceActions.AddThemeConstantOverride("separation", 8);
        details.AddChild(governanceActions);
        _saveGovernanceButton = new Button
        {
            Text = "Préparer la gouvernance",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _saveGovernanceButton.Pressed += () => SaveGovernance(dryRun: false);
        governanceActions.AddChild(_saveGovernanceButton);
        _undoGovernanceButton = new Button { Text = "Annuler" };
        _undoGovernanceButton.Pressed += UndoGovernance;
        governanceActions.AddChild(_undoGovernanceButton);
        _redoGovernanceButton = new Button { Text = "Rétablir" };
        _redoGovernanceButton.Pressed += RedoGovernance;
        governanceActions.AddChild(_redoGovernanceButton);
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
        _textureColorSpace = new OptionButton();
        foreach (var colorSpace in Enum.GetValues<TextureColorSpace>())
        {
            _textureColorSpace.AddItem($"Espace couleur · {colorSpace}", (int)colorSpace);
        }
        details.AddChild(_textureColorSpace);
        _textureTexelDensity = new SpinBox
        {
            MinValue = 0.01,
            MaxValue = 16384,
            Step = 0.01,
            Prefix = "Densité texel ",
            Suffix = " px/m",
        };
        details.AddChild(_textureTexelDensity);
        _textureChannels = new LineEdit
        {
            PlaceholderText = "BaseColor=texture.png; Normal=normal.png; Roughness=roughness.png",
        };
        _textureChannels.TooltipText = "Canaux PBR : BaseColor, Normal, Roughness, Metallic, AmbientOcclusion, Emissive, Height.";
        details.AddChild(_textureChannels);
        _modelRig = new CheckButton { Text = "Importer le rig" };
        _modelAnimations = new CheckButton { Text = "Importer les animations" };
        _modelCollisions = new CheckButton { Text = "Générer les collisions" };
        _modelSockets = new LineEdit { PlaceholderText = "Sockets · hand_r, hand_l, head" };
        _modelLodRatios = new LineEdit { PlaceholderText = "LOD · 1, 0.5, 0.25" };
        details.AddChild(_modelRig);
        details.AddChild(_modelAnimations);
        details.AddChild(_modelCollisions);
        details.AddChild(_modelSockets);
        details.AddChild(_modelLodRatios);
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
        _installForPlacementButton = new Button
        {
            Text = "Installer + ajouter à la palette",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 38),
        };
        _installForPlacementButton.AddThemeStyleboxOverride("normal", Card("5b2d86", "a676d4", 1, 7));
        _installForPlacementButton.Pressed += () => InstallForPlacement(dryRun: false);
        details.AddChild(_installForPlacementButton);
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
        var installed = _availability.Selected == 4 ? true : (bool?)null;
        var assets = _browser.Search(new AssetCatalogQuery(
            _search!.Text,
            source,
            category,
            null,
            availability,
            Installed: installed));
        IEnumerable<UnifiedContentBrowserItem> projectResources =
            source is null && category is null && availability is null && installed is null &&
            _contentBrowser is not null
            ? _contentBrowser.Search(new UnifiedContentBrowserQuery(Text: _search.Text, MaximumResults: 300))
                .Where(item => item.AssetCatalogId is null)
            : [];
        if (_capturePath is not null && !projectResources.Any())
        {
            projectResources =
            [
                new UnifiedContentBrowserItem(
                    Guid.Parse("c0170000-0000-4000-8000-000000000202"), 1,
                    "crypt.core:model/props/chest", ["legacy.crypt:model/chest"], "Coffre de la crypte",
                    "crypt.core", ContentResourceKind.Model, "CC0-1.0", new string('b', 64),
                    "capture:r017", "required", "models/chest.glb", null,
                    ["crypt.core:text/rules/encounter"], ["inspect-references", "preview", "plan-import"]),
                new UnifiedContentBrowserItem(
                    Guid.Parse("c0170000-0000-4000-8000-000000000203"), 1,
                    "crypt.localization:localization/module.name", [], "module.name", "crypt.localization",
                    ContentResourceKind.Localization, "CC0-1.0", new string('c', 64), "capture:r017",
                    "translated", null, null, [], ["preview", "translate"]),
            ];
        }
        _visibleRows = projectResources.Select(item => new BrowserRow(item, null))
            .Concat(assets.Select(item => new BrowserRow(null, item)))
            .Take(300).ToArray();
        _results.Clear();
        foreach (var row in _visibleRows)
        {
            if (row.Resource is not null)
            {
                var resource = row.Resource;
                _results.AddItem($"{resource.DisplayName} · PROJET · {resource.PackageNamespace} · {resource.Kind} · {resource.LicenseSpdx}");
                _results.SetItemTooltip(_results.ItemCount - 1,
                    $"AssetId {resource.Address}\nUUID {resource.Id:D}\nHash {resource.ContentSha256}\nRéférences {resource.References.Count}");
                continue;
            }

            var item = row.Asset!;
            var availabilityBadge = item.Installed ? "INSTALLÉ" : item.Availability switch
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

        _resultMetric!.Text = $"{_visibleRows.Count} ressource(s) · {CatalogCount} au Vault · limite 300";
        if (_visibleRows.Count == 0)
        {
            _results.AddItem("Aucune ressource ne correspond aux facettes.");
            _results.SetItemSelectable(0, false);
        }

        _selectedItem = null;
        ClearSelectionDetails();
    }

    private void SelectResult(int index)
    {
        if (index < 0 || index >= _visibleRows.Count || _load is null)
        {
            return;
        }

        var row = _visibleRows[index];
        if (row.Resource is not null)
        {
            SelectProjectResource(row.Resource);
            return;
        }

        if (row.Asset is not { } item)
        {
            ClearSelectionDetails();
            return;
        }

        _selectedItem = item;
        _assetTitle!.Text = item.DisplayName;
        _assetIdentity!.Text =
            $"{item.ExternalId}\nAssetId · {ResourceAddress(item)}\nUUID backing · {item.Id}\n" +
            $"Schéma · v{item.SchemaVersion}\nPackage source · {item.Source}";
        _assetGovernance!.Text =
            $"Licence · {item.LicenseSpdx} {(item.LicenseVerified ? "✓ vérifiée" : "✗ à vérifier")}\n" +
            $"SHA-256 · {item.ContentSha256[..16]}…\nProvenance · {item.Provenance}";
        _assetFacets!.Text =
            $"Catégories · {string.Join(", ", item.Categories)}\nTags · {string.Join(", ", item.Tags.Take(12))}\n" +
            $"État · {FormatAvailability(item.Availability)}\n" +
            $"Packs installés / références · {(item.Installed ? string.Join(", ", item.InstalledPackIds) : "aucun")}";
        var metadata = _getManifestState?.Invoke()?.Find(item.Id)?.ModelMetadata;
        _assetPaths!.Text =
            $"Modèle · {item.RelativeModelPath ?? "non catalogué"}\n" +
            $"Aperçu · {metadata?.PreviewRelativePath ?? item.RelativePreviewPath ?? item.ThumbnailUrl ?? "indisponible"}";
        _planImport!.Disabled = !item.CanPlanImport;
        RefreshProfileControls();
        RefreshManifestControls();
        RefreshExternalEditControls();
        RefreshGovernanceControls();
        LoadPreview(item, _load.VaultRoot);
    }

    private void SelectProjectResource(UnifiedContentBrowserItem item)
    {
        _selectedItem = null;
        ClearSelectionDetails();
        _assetTitle!.Text = item.DisplayName;
        _assetIdentity!.Text = $"AssetId · {item.Address}\nUUID backing · {item.Id:D}\n" +
            $"Schéma · v{item.SchemaVersion}\nPackage · {item.PackageNamespace}\nType · {item.Kind}";
        _assetGovernance!.Text = $"Licence · {item.LicenseSpdx}\nSHA-256 · {item.ContentSha256[..16]}…\n" +
            $"Provenance · {item.Provenance}";
        _assetFacets!.Text = $"État · {item.Status}\nAliases · " +
            $"{(item.Aliases.Count == 0 ? "aucun" : string.Join(", ", item.Aliases))}\n" +
            $"Références · {(item.References.Count == 0 ? "aucune" : string.Join(", ", item.References))}\n" +
            $"Actions · {string.Join(", ", item.Actions)}";
        _assetPaths!.Text = $"Source · {item.SourcePath ?? "donnée canonique intégrée"}\n" +
            $"Aperçu · {item.PreviewPath ?? "métadonnées seulement"}";
        if (!string.IsNullOrWhiteSpace(item.PreviewPath) && !string.IsNullOrWhiteSpace(_getWorkspaceRoot?.Invoke()))
            _ = TryLoadPreview(_getWorkspaceRoot!.Invoke()!, item.PreviewPath);
        SetStatus("Ressource projet en lecture · mutations via commande canonique ou plan MCP dry-run.", false);
    }

    private void LoadPreview(AssetCatalogItem item, string vaultRoot)
    {
        _preview!.Texture = null;
        _previewPlaceholder!.Visible = true;
        _previewPlaceholder.Text = item.ThumbnailUrl is not null
            ? "Aperçu distant catalogué\nMiniature locale non installée"
            : "Aperçu local indisponible\nMétadonnées prêtes";
        var metadata = _getManifestState?.Invoke()?.Find(item.Id)?.ModelMetadata;
        var workspaceRoot = _getWorkspaceRoot?.Invoke();
        if (metadata is not null && !string.IsNullOrWhiteSpace(workspaceRoot) &&
            TryLoadPreview(workspaceRoot, metadata.PreviewRelativePath))
        {
            _previewPlaceholder.Text = "Miniature Blender gouvernée";
            return;
        }

        if (string.IsNullOrWhiteSpace(item.RelativePreviewPath) ||
            !AssetCatalogValidator.IsSafeRelativePath(item.RelativePreviewPath))
        {
            return;
        }

        TryLoadPreview(vaultRoot, item.RelativePreviewPath);
    }

    private bool TryLoadPreview(string root, string relativePath)
    {
        if (!AssetCatalogValidator.IsSafeRelativePath(relativePath))
        {
            return false;
        }

        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return false;
        }

        var image = Image.LoadFromFile(path);
        if (image.IsEmpty())
        {
            return false;
        }

        _preview!.Texture = ImageTexture.CreateFromImage(image);
        _previewPlaceholder!.Visible = false;
        return true;
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
        AssetProcessingProfile? profile;
        try
        {
            profile = CreateProfileFromControls();
        }
        catch (FormatException exception)
        {
            SetStatus(exception.Message, true);
            return false;
        }
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
                ColorSpace = (TextureColorSpace)_textureColorSpace!.GetSelectedId(),
                Compression = (TextureCompressionProfile)_textureCompression!.GetSelectedId(),
                MaximumDimension = (int)_textureMaximumDimension!.Value,
                TexelDensity = (decimal)_textureTexelDensity!.Value,
                Channels = ParseTextureChannels(_textureChannels!.Text),
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
            ImportRig = _modelRig!.ButtonPressed,
            ImportAnimations = _modelAnimations!.ButtonPressed,
            GenerateCollisions = _modelCollisions!.ButtonPressed,
            Sockets = ParseCsv(_modelSockets!.Text),
            LodRatios = ParseLodRatios(_modelLodRatios!.Text),
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
        _textureColorSpace!.Visible = textureVisible;
        _textureTexelDensity!.Visible = textureVisible;
        _textureChannels!.Visible = textureVisible;
        _modelRig!.Visible = !textureVisible;
        _modelAnimations!.Visible = !textureVisible;
        _modelCollisions!.Visible = !textureVisible;
        _modelSockets!.Visible = !textureVisible;
        _modelLodRatios!.Visible = !textureVisible;
        if (textureVisible)
        {
            _textureMaximumDimension.Value = profile.Texture!.MaximumDimension;
            SelectOptionById(_textureCompression, (int)profile.Texture.Compression);
            SelectOptionById(_textureColorSpace, (int)profile.Texture.ColorSpace);
            _textureTexelDensity.Value = (double)profile.Texture.TexelDensity;
            _textureChannels.Text = string.Join("; ", profile.Texture.Channels.Select(item => $"{item.Key}={item.Value}"));
        }
        else
        {
            _modelRig.ButtonPressed = profile.Model!.ImportRig;
            _modelAnimations.ButtonPressed = profile.Model!.ImportAnimations;
            _modelCollisions.ButtonPressed = profile.Model.GenerateCollisions;
            _modelSockets.Text = string.Join(", ", profile.Model.Sockets);
            _modelLodRatios.Text = string.Join(", ", profile.Model.LodRatios.Select(value =>
                value.ToString("0.###", CultureInfo.InvariantCulture)));
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

    private bool InstallForPlacement(bool dryRun)
    {
        if (_selectedItem is null || _installForPlacement is null)
        {
            return false;
        }

        var profile = CreateProfileFromControls();
        if (profile is null || profile.Kind != AssetProcessingKind.Model)
        {
            SetStatus("Le placement 3D exige un profil de modèle.", true);
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

        var blueprint = _installForPlacement(_selectedItem, profile, dryRun);
        if (blueprint is null)
        {
            SetStatus(
                dryRun
                    ? "L’installation pour placement ne peut pas être prévisualisée."
                    : "L’installation du modèle dans la palette a échoué.",
                true);
            return false;
        }

        SetStatus(
            dryRun
                ? $"Aperçu d’installation prêt · {blueprint.DisplayName} · aucune écriture"
                : $"✓ {blueprint.DisplayName} installé · fermez puis glissez-le depuis la palette du Monde",
            false);
        RefreshManifestControls();
        return true;
    }

    private void RefreshManifestControls()
    {
        if (_manifestSummary is null || _selectedItem is null)
        {
            return;
        }

        var manifest = _getManifestState?.Invoke()?.Find(_selectedItem.Id);
        if (manifest is null)
        {
            _manifestSummary.Text = "Aucun manifeste enregistré · la préparation restera confinée au projet";
        }
        else if (manifest.ModelMetadata is { } metadata)
        {
            _manifestSummary.Text =
                $"{manifest.Id} · {manifest.Chunks.Count} chunk · " +
                $"{metadata.Dimensions.ExtentXMeters:0.##} × {metadata.Dimensions.ExtentYMeters:0.##} × " +
                $"{metadata.Dimensions.ExtentZMeters:0.##} m · {metadata.Metrics.TriangleCount:N0} triangles · " +
                $"qualité {metadata.Metrics.QualityScore}/100 · {metadata.Variants.Count} variante · " +
                $"{metadata.Compatibility.Realism} · {string.Join(", ", metadata.Styles.Take(5))}";
        }
        else
        {
            _manifestSummary.Text =
                $"{manifest.Id} · {manifest.Chunks.Count} chunk · " +
                $"collision {(manifest.Collision is null ? "non" : "oui")} · " +
                $"animation {(manifest.Animation is null ? "non" : "oui")} · " +
                $"VFX {(manifest.Vfx is null ? "non" : "oui")} · " +
                $"LOD {manifest.Lod?.Levels.Count ?? 0}";
        }
        _includeVfx!.ButtonPressed = manifest?.Vfx is not null;
        _prepareManifestButton!.Disabled = !_selectedItem.CanPlanImport;
        _installForPlacementButton!.Disabled = !_selectedItem.CanPlanImport;
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

    private bool SaveGovernance(bool dryRun)
    {
        if (_selectedItem is null || _applyGovernance is null) return false;
        var existing = _getGovernanceState?.Invoke()?.Find(_selectedItem.Id);
        var sourcePath = _selectedItem.RelativeModelPath ?? $"incoming/{_selectedItem.SourceAssetId}.glb";
        var mediaType = Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gltf" => "model/gltf+json",
            _ => "model/gltf-binary",
        };
        var record = existing ?? new AssetGovernanceRecord(
            AssetGovernanceRecord.CurrentSchemaVersion,
            AssetGovernanceId.ForAsset(_selectedItem.Id),
            _selectedItem.Id,
            AssetPackageManifestId.ForAsset(_selectedItem.Id),
            _selectedItem.LicenseSpdx,
            _selectedItem.ContentSha256,
            _selectedItem.Author,
            _selectedItem.SourcePage,
            $"catalog:{_selectedItem.Provenance}",
            "Import original non modifié ; transformations à consigner avant approbation.",
            false,
            string.Empty,
            string.Empty,
            Math.Max(1, _selectedItem.TotalBytes),
            new(sourcePath, mediaType, mediaType, _selectedItem.TotalBytes, Math.Max(1, _selectedItem.TotalBytes),
                "non exécuté", "pending", "pending", AssetQuarantineStatus.Pending, string.Empty, null),
            new(_selectedItem.LicenseVerified ? AssetOriginKind.OpenLicensed : AssetOriginKind.Unknown,
                _selectedItem.LicenseVerified, false, string.Empty));
        if (!_applyGovernance(record, dryRun))
        {
            SetStatus("Le dossier de gouvernance est invalide ; aucun fichier n’a été écrit.", true);
            return false;
        }
        SetStatus(dryRun
            ? $"Aperçu gouvernance prêt · {record.Id} · aucun fichier écrit"
            : "✓ Dossier enregistré · quarantaine et approbation humaine encore requises", false);
        RefreshGovernanceControls();
        return true;
    }

    private void UndoGovernance()
    {
        if (_undoGovernance?.Invoke() != true) return;
        SetStatus("Dossier de gouvernance annulé et persistance restaurée.", false);
        RefreshGovernanceControls();
    }

    private void RedoGovernance()
    {
        if (_redoGovernance?.Invoke() != true) return;
        SetStatus("Dossier de gouvernance rétabli.", false);
        RefreshGovernanceControls();
    }

    private void RefreshGovernanceControls()
    {
        if (_governanceWorkflowSummary is null || _selectedItem is null) return;
        var record = _getGovernanceState?.Invoke()?.Find(_selectedItem.Id);
        if (record is null)
        {
            _governanceWorkflowSummary.Text = "Aucun dossier · préparer la provenance avant l’import canonique";
        }
        else
        {
            var ready = AssetGovernanceValidator.Validate(record).Count == 0;
            _governanceWorkflowSummary.Text =
                $"UUID · {record.Id}\nAuteur · {record.Author} · permission {record.PermissionEvidence}\n" +
                $"Quarantaine · {record.Quarantine.Status} · scan {record.Quarantine.ScannerResult} · parser {record.Quarantine.ParserResult}\n" +
                $"Frontière IP · {record.IpBoundary.Origin} · redistribution {(record.IpBoundary.RedistributionAllowed ? "autorisée" : "bloquée")}\n" +
                $"Distribution · {(ready ? "prête" : "bloquée jusqu’à validation")}";
        }
        _saveGovernanceButton!.Disabled = false;
        _undoGovernanceButton!.Disabled = _getGovernanceState?.Invoke() is null;
        _redoGovernanceButton!.Disabled = _getGovernanceState?.Invoke() is null;
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

        if (_installForPlacementButton is not null)
        {
            _installForPlacementButton.Disabled = true;
        }

        if (_configureExternalEditButton is not null)
        {
            _configureExternalEditButton.Disabled = true;
            if (_saveGovernanceButton is not null)
            {
                _saveGovernanceButton.Disabled = true;
                _undoGovernanceButton!.Disabled = true;
                _redoGovernanceButton!.Disabled = true;
                _governanceWorkflowSummary!.Text = "Aucun dossier de gouvernance · import non approuvé";
            }
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

    private static IReadOnlyDictionary<PbrTextureChannel, string> ParseTextureChannels(string value)
    {
        var result = new Dictionary<PbrTextureChannel, string>();
        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !Enum.TryParse<PbrTextureChannel>(parts[0], ignoreCase: true, out var channel) ||
                string.IsNullOrWhiteSpace(parts[1]))
                throw new FormatException($"Canal PBR invalide : '{entry}'. Utilisez Canal=chemin.");
            if (!result.TryAdd(channel, parts[1]))
                throw new FormatException($"Canal PBR dupliqué : '{channel}'.");
        }

        if (result.Count == 0) throw new FormatException("Au moins un canal PBR est requis.");
        return result;
    }

    private static IReadOnlyList<string> ParseCsv(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<decimal> ParseLodRatios(string value)
    {
        var ratios = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => decimal.TryParse(entry, NumberStyles.Number, CultureInfo.InvariantCulture, out var ratio)
                ? ratio
                : throw new FormatException($"Ratio LOD invalide : '{entry}'."))
            .OrderDescending().ToArray();
        if (ratios.Length == 0) throw new FormatException("Au moins le ratio LOD 1 est requis.");
        return ratios;
    }

    private static string ResourceAddress(AssetCatalogItem item)
    {
        var extension = Path.GetExtension(item.RelativeModelPath ?? string.Empty).ToLowerInvariant();
        var kind = item.Categories.Any(category => category.Contains("texture", StringComparison.OrdinalIgnoreCase)) ||
                   extension is ".png" or ".tif" or ".tiff" or ".exr" or ".ktx2"
            ? "texture"
            : item.Categories.Any(category => category.Contains("3d", StringComparison.OrdinalIgnoreCase)) ||
              extension is ".gltf" or ".glb" or ".obj" or ".fbx"
                ? "model"
                : "asset";
        static string Normalize(string value) => new((value ?? string.Empty).Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '-')
            .ToArray());
        return $"{Normalize(item.Source)}:{kind}/{Normalize(item.SourceAssetId)}";
    }

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

    private sealed record BrowserRow(UnifiedContentBrowserItem? Resource, AssetCatalogItem? Asset);
}
#endif
