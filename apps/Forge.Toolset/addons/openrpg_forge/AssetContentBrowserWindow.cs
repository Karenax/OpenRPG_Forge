#if TOOLS
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssetContentBrowserWindow : Window
{
    private Func<string>? _getRepositoryRoot;
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
    private AssetCatalogItem? _selectedItem;

    public int CatalogCount => _browser?.Catalog.Items.Count ?? 0;

    public int VisibleAssetCount => _visibleItems.Count;

    public int SourceCount => _browser?.Sources.Count ?? 0;

    public bool SelectedHasPreviewMetadata => !string.IsNullOrWhiteSpace(_selectedItem?.RelativePreviewPath) ||
        !string.IsNullOrWhiteSpace(_selectedItem?.ThumbnailUrl);

    public bool SelectedCanPlanImport => _selectedItem?.CanPlanImport == true;

    public void Initialize(Func<string> getRepositoryRoot, Action<Window> brandWindow)
    {
        _getRepositoryRoot = getRepositoryRoot ?? throw new ArgumentNullException(nameof(getRepositoryRoot));
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

        var result = AssetImportPlanner.Plan(_selectedItem);
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
