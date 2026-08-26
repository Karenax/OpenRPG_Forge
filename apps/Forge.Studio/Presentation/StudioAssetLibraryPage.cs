using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Assets;
using Forge.Studio.Core.Assets;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssetLibraryPage : Control
{
    private const int PageSize = 56;
    private readonly StudioSessionController _session;
    private readonly LineEdit _search;
    private readonly OptionButton _sourceFilter;
    private readonly OptionButton _packFilter;
    private readonly OptionButton _formatFilter;
    private readonly OptionButton _kindFilter;
    private readonly OptionButton _previewFilter;
    private readonly OptionButton _sort;
    private readonly CheckButton _localOnly;
    private readonly GridContainer _grid;
    private readonly Label _resultState;
    private readonly Label _loadState;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly TextureRect _detailThumbnail;
    private readonly Label _detailThumbnailState;
    private readonly Label _detailTitle;
    private readonly Label _detailSource;
    private readonly Label _detailMetadata;
    private readonly Label _detailLicense;
    private readonly StudioAssetTurntablePreview _turntable;
    private readonly Button _install;
    private readonly Button _installAndPlace;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private StudioAssetLibraryIndex? _index;
    private StudioAssetLibraryItem? _selected;
    private Task<StudioAssetLibraryIndex>? _loadTask;
    private Task<StudioAssetPreparationResult>? _installTask;
    private bool _installing;
    private bool _placeAfterInstall;
    private int _offset;

    private enum PreviewFilterMode
    {
        All,
        Thumbnail,
        Preview3D,
        Installable,
        Installed,
        MissingPreview,
    }

    private enum SortMode
    {
        VisualsFirst,
        Name,
        Source,
        Pack,
        Smallest,
        Largest,
    }

    internal event Action<string, bool>? StatusChanged;

    internal event Action? ProjectChanged;

    internal event Action<BlueprintId>? InstalledAssetPlacementRequested;

    internal bool IsLoaded => _index is not null;

    internal int IndexedModelCount => _index?.Items.Count ?? 0;

    internal StudioAssetLibraryPage(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Name = "StudioAssetLibraryPage";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var rootMargin = StudioStyle.Margin(7, 7, 7, 7);
        rootMargin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(rootMargin);
        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        rootMargin.AddChild(root);

        var filters = StudioStyle.Panel("AssetFilters", StudioStyle.Chrome, 6);
        filters.CustomMinimumSize = new Vector2(196, 0);
        root.AddChild(filters);
        var filterMargin = StudioStyle.Margin(7, 7, 7, 7);
        filters.AddChild(filterMargin);
        var filterColumn = new VBoxContainer();
        filterColumn.AddThemeConstantOverride("separation", 4);
        filterMargin.AddChild(filterColumn);
        filterColumn.AddChild(StudioStyle.SectionLabel("TROUVER UN MODÈLE"));
        _search = new LineEdit
        {
            PlaceholderText = "porte, arbre, rocher…",
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(_search);
        _search.TextChanged += _ => ResetAndRender();
        filterColumn.AddChild(_search);
        filterColumn.AddChild(StudioStyle.Label("Source", 10, StudioStyle.Muted));
        _sourceFilter = new OptionButton();
        StudioStyle.ApplyOptionButton(_sourceFilter);
        _sourceFilter.ItemSelected += _ => ResetAndRender();
        filterColumn.AddChild(_sourceFilter);
        filterColumn.AddChild(StudioStyle.Label("Pack", 9, StudioStyle.Muted));
        _packFilter = new OptionButton();
        StudioStyle.ApplyOptionButton(_packFilter);
        _packFilter.ItemSelected += _ => ResetAndRender();
        filterColumn.AddChild(_packFilter);
        filterColumn.AddChild(StudioStyle.Label("Format", 10, StudioStyle.Muted));
        _formatFilter = new OptionButton();
        StudioStyle.ApplyOptionButton(_formatFilter);
        _formatFilter.ItemSelected += _ => ResetAndRender();
        filterColumn.AddChild(_formatFilter);
        filterColumn.AddChild(StudioStyle.Label("Type", 9, StudioStyle.Muted));
        _kindFilter = new OptionButton();
        StudioStyle.ApplyOptionButton(_kindFilter);
        _kindFilter.ItemSelected += _ => ResetAndRender();
        filterColumn.AddChild(_kindFilter);
        filterColumn.AddChild(StudioStyle.Label("Aperçu", 9, StudioStyle.Muted));
        _previewFilter = new OptionButton();
        StudioStyle.ApplyOptionButton(_previewFilter);
        _previewFilter.AddItem("Tous");
        _previewFilter.AddItem("Avec vignette");
        _previewFilter.AddItem("Aperçu 3D disponible");
        _previewFilter.AddItem("Installables");
        _previewFilter.AddItem("Dans la palette");
        _previewFilter.AddItem("Sans aperçu");
        _previewFilter.ItemSelected += _ => ResetAndRender();
        filterColumn.AddChild(_previewFilter);
        filterColumn.AddChild(StudioStyle.Label("Trier", 9, StudioStyle.Muted));
        _sort = new OptionButton();
        StudioStyle.ApplyOptionButton(_sort);
        _sort.AddItem("Aperçus d'abord");
        _sort.AddItem("Nom A → Z");
        _sort.AddItem("Source puis nom");
        _sort.AddItem("Pack puis nom");
        _sort.AddItem("Plus légers d'abord");
        _sort.AddItem("Plus lourds d'abord");
        _sort.ItemSelected += _ => ResetAndRender();
        filterColumn.AddChild(_sort);
        _localOnly = new CheckButton
        {
            Text = "Fichiers présents seulement",
            ButtonPressed = true,
        };
        _localOnly.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        _localOnly.Toggled += _ => ResetAndRender();
        filterColumn.AddChild(_localOnly);
        var resetFilters = StudioStyle.Button("Réinitialiser les filtres", compact: true);
        resetFilters.Pressed += ResetFilters;
        filterColumn.AddChild(resetFilters);
        filterColumn.AddChild(new HSeparator());
        _loadState = StudioStyle.Label("La bibliothèque sera chargée à l'ouverture de cette page.", 10,
            StudioStyle.Muted, wrap: true);
        filterColumn.AddChild(_loadState);
        filterColumn.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        filterColumn.AddChild(StudioStyle.Label(
            "Les fichiers restent dans le Vault. Seul le modèle choisi est copié et gouverné dans le module.",
            9,
            StudioStyle.Muted,
            wrap: true));

        var results = StudioStyle.Panel("AssetResults", StudioStyle.Chrome, 6);
        results.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(results);
        var resultsMargin = StudioStyle.Margin(6, 6, 6, 6);
        results.AddChild(resultsMargin);
        var resultsColumn = new VBoxContainer();
        resultsColumn.AddThemeConstantOverride("separation", 6);
        resultsMargin.AddChild(resultsColumn);
        var resultHeader = new HBoxContainer();
        resultHeader.AddChild(StudioStyle.SectionLabel("MODÈLES DU VAULT"));
        resultHeader.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _resultState = StudioStyle.Label("Chargement en attente", 10, StudioStyle.Muted);
        resultHeader.AddChild(_resultState);
        resultsColumn.AddChild(resultHeader);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        resultsColumn.AddChild(scroll);
        _grid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 6);
        scroll.AddChild(_grid);
        var pager = new HBoxContainer();
        pager.AddThemeConstantOverride("separation", 5);
        _previous = StudioStyle.Button("← Précédents", compact: true);
        _previous.Pressed += PreviousPage;
        pager.AddChild(_previous);
        pager.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _next = StudioStyle.Button("Suivants →", compact: true);
        _next.Pressed += NextPage;
        pager.AddChild(_next);
        resultsColumn.AddChild(pager);

        var details = StudioStyle.Panel("AssetDetails", StudioStyle.Chrome, 6);
        details.CustomMinimumSize = new Vector2(300, 0);
        root.AddChild(details);
        var detailMargin = StudioStyle.Margin(7, 7, 7, 7);
        details.AddChild(detailMargin);
        var detailScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        detailMargin.AddChild(detailScroll);
        var detailColumn = new VBoxContainer();
        detailColumn.AddThemeConstantOverride("separation", 6);
        detailScroll.AddChild(detailColumn);
        detailColumn.AddChild(StudioStyle.SectionLabel("MODÈLE SÉLECTIONNÉ"));
        _detailTitle = StudioStyle.Label("Aucun modèle", 16, StudioStyle.Text, wrap: true);
        detailColumn.AddChild(_detailTitle);
        _detailSource = StudioStyle.Label("Choisissez une vignette.", 10, StudioStyle.Muted, wrap: true);
        detailColumn.AddChild(_detailSource);
        _detailThumbnail = new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 130),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        detailColumn.AddChild(_detailThumbnail);
        _detailThumbnailState = StudioStyle.Label(string.Empty, 9, StudioStyle.Muted, wrap: true);
        _detailThumbnailState.HorizontalAlignment = HorizontalAlignment.Center;
        _detailThumbnailState.Visible = false;
        detailColumn.AddChild(_detailThumbnailState);
        _turntable = new StudioAssetTurntablePreview();
        detailColumn.AddChild(_turntable);
        _detailMetadata = StudioStyle.Label(string.Empty, 10, StudioStyle.Text, wrap: true);
        detailColumn.AddChild(_detailMetadata);
        _detailLicense = StudioStyle.Label(string.Empty, 10, StudioStyle.Success, wrap: true);
        detailColumn.AddChild(_detailLicense);
        _install = StudioStyle.Button("Ajouter à la palette", primary: true, compact: true);
        _install.Disabled = true;
        _install.Pressed += () => InstallSelected(placeAfterInstall: false);
        detailColumn.AddChild(_install);
        _installAndPlace = StudioStyle.Button("Installer puis placer dans la zone", compact: true);
        _installAndPlace.Disabled = true;
        _installAndPlace.Pressed += () => InstallSelected(placeAfterInstall: true);
        detailColumn.AddChild(_installAndPlace);
        detailColumn.AddChild(StudioStyle.Label(
            "Après installation, le modèle apparaît aussi dans Ajouter un objet. L'ajout au projet et le placement restent annulables avec Ctrl+Z.",
            9,
            StudioStyle.Muted,
            wrap: true));

        SetProcess(false);
        Resized += UpdateResponsiveLayout;
    }

    public override void _Ready() => UpdateResponsiveLayout();

    public override void _Process(double delta)
    {
        _ = delta;
        if (_loadTask is { IsCompleted: true } load)
        {
            _loadTask = null;
            CompleteLoad(load);
        }
        if (_installTask is { IsCompleted: true } install)
        {
            _installTask = null;
            CompletePreparation(install);
        }
        if (_loadTask is null && _installTask is null) SetProcess(false);
    }

    internal void Activate()
    {
        if (_index is not null || _loadTask is not null) return;
        var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(_session.Session!.RootDirectory);
        if (repositoryRoot is null)
        {
            _loadState.Text = "Dépôt OpenRPG Forge introuvable depuis ce module.";
            _loadState.AddThemeColorOverride("font_color", StudioStyle.Danger);
            StatusChanged?.Invoke(_loadState.Text, true);
            return;
        }
        var vaultRoot = AssetCatalogLoader.ResolveVaultRoot(repositoryRoot);
        _loadState.Text = "Indexation des modèles locaux…";
        _resultState.Text = "Chargement…";
        _loadTask = Task.Run(() => StudioAssetLibraryLoader.Load(repositoryRoot, vaultRoot));
        SetProcess(true);
    }

    internal bool PrepareEvidence()
    {
        if (_index is null || _index.Items.Count == 0) return false;
        _previewFilter.Select((int)PreviewFilterMode.Preview3D);
        _sort.Select((int)SortMode.Smallest);
        ResetAndRender();
        var filtered = FilteredItems().ToArray();
        var filtersValid = filtered.Length > 0 && filtered.All(item => item.CanPreview3D);
        var expectedCards = Math.Min(PageSize, filtered.Length);
        var densityValid = _grid.GetChildCount() == expectedCards && expectedCards > 24;
        GD.Print($"OPENRPG_FORGE_STUDIO_ASSET_FILTER_PASS filtered={filtered.Length} " +
                 $"cards={_grid.GetChildCount()} columns={_grid.Columns} dense={densityValid.ToString().ToLowerInvariant()}");
        var noThumbnailSample = _index.Items
            .Where(item => item.CanPreview3D && !item.HasThumbnail)
            .OrderBy(item => item.FileSize)
            .ThenBy(item => item.ExternalId, StringComparer.Ordinal)
            .FirstOrDefault();
        var fallbackPreviewValid = noThumbnailSample is null ||
                                   _turntable.Load(noThumbnailSample, _index.VaultRoot);
        GD.Print($"OPENRPG_FORGE_STUDIO_ASSET_PREVIEW_FALLBACK_{(fallbackPreviewValid ? "PASS" : "FAILED")} " +
                 $"asset={noThumbnailSample?.ExternalId ?? "none"}");
        ResetFilters();
        var first = FilteredItems().FirstOrDefault() ?? _index.Items[0];
        Select(first);
        return filtersValid && densityValid && fallbackPreviewValid &&
               _selected is not null && _grid.GetChildCount() > 0;
    }

    internal bool RunInstallEvidence()
    {
        if (_index is null) return false;
        var candidate = _index.Items
            .Where(item => item.CanInstall)
            .OrderBy(item => item.FileSize)
            .ThenBy(item => item.ExternalId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is null) return false;
        var result = _session.InstallAsset(candidate, _index.VaultRoot);
        var anchor = _session.ActiveArea?.Entities.FirstOrDefault()?.Transform.Position ?? Vector3Value.Zero;
        var placed = result.BlueprintId is null
            ? null
            : _session.PlaceEntity(
                result.BlueprintId,
                new Vector3Value(anchor.X + 3, anchor.Y, anchor.Z + 3));
        var placedEntity = placed?.EntityId is null
            ? null
            : _session.ActiveArea?.Entities.FirstOrDefault(entity => entity.Id == placed.EntityId);
        var assetId = Forge.Domain.Assets.AssetCatalogItemId.ForExternalId(candidate.ExternalId);
        var installed = result.Succeeded && result.BlueprintId is not null &&
                        _session.FindAssetBlueprint(candidate.ExternalId)?.Id == result.BlueprintId &&
                        _session.AssetManifests.Any(manifest => manifest.AssetId == assetId) &&
                        placed?.Succeeded == true && placedEntity?.Overrides.Properties.Any(property =>
                            property.Key == "visual.asset" && property.Value == assetId.ToString()) == true;
        GD.Print($"OPENRPG_FORGE_STUDIO_ASSET_INSTALL_{(installed ? "PASS" : "FAILED")} " +
                 $"asset={candidate.ExternalId} blueprint={result.BlueprintId} entity={placed?.EntityId} " +
                 $"message={result.Message}");
        if (installed) Select(candidate);
        return installed;
    }

    private void CompleteLoad(Task<StudioAssetLibraryIndex> task)
    {
        if (task.IsFaulted)
        {
            var message = task.Exception?.GetBaseException().Message ?? "Erreur inconnue";
            _loadState.Text = $"Bibliothèque indisponible · {message}";
            _loadState.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _resultState.Text = "0 modèle";
            StatusChanged?.Invoke(_loadState.Text, true);
            return;
        }
        _index = task.Result;
        PopulateFilters();
        _loadState.Text = $"{_index.Items.Count:N0} modèles indexés · " +
                          $"{_index.LocalModelCount:N0} locaux · {_index.InstallableModelCount:N0} prêts à installer";
        _loadState.AddThemeColorOverride("font_color", StudioStyle.Success);
        RenderResults();
        StatusChanged?.Invoke(
            $"Bibliothèque prête · {_index.Items.Count:N0} modèles · aucun serveur externe lancé",
            false);
        GD.Print($"OPENRPG_FORGE_STUDIO_ASSET_LIBRARY_READY models={_index.Items.Count} " +
                 $"local={_index.LocalModelCount} installable={_index.InstallableModelCount} " +
                 $"thumbnails={_index.ThumbnailCount} preview3d={_index.Preview3DCount} " +
                 $"missing={_index.MissingPreviewCount} windows=0");
    }

    private void PopulateFilters()
    {
        _sourceFilter.Clear();
        _sourceFilter.AddItem("Toutes les sources");
        foreach (var source in _index!.Sources) _sourceFilter.AddItem(source);

        _packFilter.Clear();
        _packFilter.AddItem("Tous les packs");
        _packFilter.AddItem("Sans pack");
        _packFilter.SetItemMetadata(1, string.Empty);
        foreach (var pack in _index.Items
                     .Where(item => !string.IsNullOrWhiteSpace(item.PackName))
                     .Select(item => item.PackName!)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var index = _packFilter.ItemCount;
            _packFilter.AddItem(pack);
            _packFilter.SetItemMetadata(index, pack);
        }

        _formatFilter.Clear();
        _formatFilter.AddItem("Tous les formats");
        foreach (var format in _index.Formats) _formatFilter.AddItem(format.ToUpperInvariant());
        var glb = Enumerable.Range(0, _formatFilter.ItemCount)
            .FirstOrDefault(index => string.Equals(
                _formatFilter.GetItemText(index),
                "GLB",
                StringComparison.OrdinalIgnoreCase));
        if (glb > 0) _formatFilter.Select(glb);

        _kindFilter.Clear();
        _kindFilter.AddItem("Tous les types");
        foreach (var kind in _index.Items
                     .Select(item => item.Kind)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var index = _kindFilter.ItemCount;
            _kindFilter.AddItem(KindLabel(kind));
            _kindFilter.SetItemMetadata(index, kind);
        }
    }

    private void ResetAndRender()
    {
        _offset = 0;
        RenderResults();
    }

    private void ResetFilters()
    {
        _search.Clear();
        _sourceFilter.Select(0);
        _packFilter.Select(0);
        _kindFilter.Select(0);
        _previewFilter.Select(0);
        _sort.Select(0);
        _localOnly.SetPressedNoSignal(true);
        var glb = Enumerable.Range(0, _formatFilter.ItemCount)
            .FirstOrDefault(index => string.Equals(
                _formatFilter.GetItemText(index),
                "GLB",
                StringComparison.OrdinalIgnoreCase));
        _formatFilter.Select(glb);
        ResetAndRender();
    }

    private bool MatchesPreviewFilter(StudioAssetLibraryItem item, PreviewFilterMode mode) => mode switch
    {
        PreviewFilterMode.Thumbnail => item.HasThumbnail,
        PreviewFilterMode.Preview3D => item.CanPreview3D,
        PreviewFilterMode.Installable => item.CanInstall,
        PreviewFilterMode.Installed => _session.FindAssetBlueprint(item.ExternalId) is not null,
        PreviewFilterMode.MissingPreview => !item.HasThumbnail && !item.CanPreview3D,
        _ => true,
    };

    private void RenderResults()
    {
        ClearChildren(_grid);
        if (_index is null) return;
        var matches = FilteredItems().ToArray();
        _offset = Math.Clamp(_offset, 0, Math.Max(0, ((matches.Length - 1) / PageSize) * PageSize));
        var visible = matches.Skip(_offset).Take(PageSize).ToArray();
        foreach (var item in visible) _grid.AddChild(BuildCard(item));
        var first = matches.Length == 0 ? 0 : _offset + 1;
        var last = Math.Min(_offset + PageSize, matches.Length);
        var previewable = matches.Count(item => item.CanPreview3D);
        _resultState.Text = $"{first:N0}–{last:N0} sur {matches.Length:N0} · {previewable:N0} en 3D";
        _previous.Disabled = _offset == 0;
        _next.Disabled = _offset + PageSize >= matches.Length;
    }

    private IEnumerable<StudioAssetLibraryItem> FilteredItems()
    {
        if (_index is null) return [];
        var terms = _search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())
            .ToArray();
        var source = _sourceFilter.Selected <= 0 ? null : _sourceFilter.GetItemText(_sourceFilter.Selected);
        var pack = _packFilter.Selected <= 0
            ? null
            : _packFilter.GetItemMetadata(_packFilter.Selected).AsString();
        var format = _formatFilter.Selected <= 0
            ? null
            : _formatFilter.GetItemText(_formatFilter.Selected).ToLowerInvariant();
        var kind = _kindFilter.Selected <= 0
            ? null
            : _kindFilter.GetItemMetadata(_kindFilter.Selected).AsString();
        var previewMode = (PreviewFilterMode)_previewFilter.Selected;
        var filtered = _index.Items.Where(item =>
                (!_localOnly.ButtonPressed || item.AvailableLocally) &&
                (source is null || string.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase)) &&
                (pack is null || (pack.Length == 0
                    ? string.IsNullOrWhiteSpace(item.PackName)
                    : string.Equals(item.PackName, pack, StringComparison.OrdinalIgnoreCase))) &&
                (format is null || string.Equals(item.Format, format, StringComparison.OrdinalIgnoreCase)) &&
                (kind is null || string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase)) &&
                MatchesPreviewFilter(item, previewMode) &&
                terms.All(term => item.SearchText.Contains(term, StringComparison.Ordinal)))
            .ToArray();
        return (SortMode)_sort.Selected switch
        {
            SortMode.Name => filtered
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ExternalId, StringComparer.Ordinal),
            SortMode.Source => filtered
                .OrderBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            SortMode.Pack => filtered
                .OrderBy(item => item.PackName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            SortMode.Smallest => filtered
                .OrderBy(item => item.FileSize <= 0 ? long.MaxValue : item.FileSize)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            SortMode.Largest => filtered
                .OrderByDescending(item => item.FileSize)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            _ => filtered
                .OrderByDescending(item => item.HasThumbnail && item.CanPreview3D)
                .ThenByDescending(item => item.HasThumbnail)
                .ThenByDescending(item => item.CanPreview3D)
                .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.PackName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
        };
    }

    private Control BuildCard(StudioAssetLibraryItem item)
    {
        var selected = _selected?.ExternalId == item.ExternalId;
        var panel = StudioStyle.Panel(
            $"AssetCard_{item.ExternalId}",
            selected ? StudioStyle.Selection : StudioStyle.Surface,
            6,
            selected ? 2 : 1);
        panel.CustomMinimumSize = new Vector2(116, 116);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (selected)
        {
            panel.AddThemeStyleboxOverride("panel", StudioStyle.Box(
                StudioStyle.Selection,
                StudioStyle.Accent,
                6,
                2));
        }
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        panel.AddChild(column);
        var thumbnail = LoadTexture(item.RelativePreviewPath);
        var preview = new TextureButton
        {
            CustomMinimumSize = new Vector2(0, 68),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = $"Sélectionner {item.DisplayName}",
            TextureNormal = thumbnail,
        };
        preview.Pressed += () => Select(item);
        if (thumbnail is null)
        {
            var placeholder = StudioStyle.Label(
                item.CanPreview3D ? "APERÇU 3D" : $"{item.Format.ToUpperInvariant()} · SANS IMAGE",
                9,
                item.CanPreview3D ? StudioStyle.Muted : StudioStyle.Warning,
                wrap: true);
            placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            placeholder.HorizontalAlignment = HorizontalAlignment.Center;
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.MouseFilter = MouseFilterEnum.Ignore;
            preview.AddChild(placeholder);
        }
        column.AddChild(preview);
        var choose = StudioStyle.Button(item.DisplayName, selected, compact: true);
        choose.CustomMinimumSize = new Vector2(0, 22);
        choose.AddThemeFontSizeOverride("font_size", 10);
        choose.Alignment = HorizontalAlignment.Left;
        choose.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        choose.TooltipText = item.DisplayName;
        choose.Pressed += () => Select(item);
        column.AddChild(choose);
        var state = _session.FindAssetBlueprint(item.ExternalId) is not null
            ? "PALETTE"
            : item.HasThumbnail && item.CanPreview3D ? $"3D + IMAGE · {item.Format.ToUpperInvariant()}" :
            item.CanPreview3D ? $"3D · {item.Format.ToUpperInvariant()}" :
            item.HasThumbnail ? $"IMAGE · {item.Format.ToUpperInvariant()}" :
            item.AvailableLocally ? $"SANS APERÇU · {item.Format.ToUpperInvariant()}" : "FICHIER ABSENT";
        var stateColor = _session.FindAssetBlueprint(item.ExternalId) is not null
            ? StudioStyle.Success
            : item.HasThumbnail || item.CanPreview3D ? StudioStyle.Muted : StudioStyle.Warning;
        var label = StudioStyle.Label(state, 8, stateColor);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(label);
        return panel;
    }

    private void Select(StudioAssetLibraryItem item)
    {
        _selected = item;
        _detailTitle.Text = item.DisplayName;
        _detailSource.Text = string.IsNullOrWhiteSpace(item.PackName)
            ? item.Source
            : $"{item.Source} · {item.PackName}";
        _detailThumbnail.Texture = LoadTexture(item.RelativePreviewPath);
        _detailThumbnail.Visible = _detailThumbnail.Texture is not null;
        _detailThumbnailState.Visible = _detailThumbnail.Texture is null;
        _detailThumbnailState.Text = item.CanPreview3D
            ? "Pas de vignette enregistrée · utilisez l'aperçu 3D ci-dessous."
            : "Aucune vignette ni scène 3D prévisualisable pour ce fichier.";
        _turntable.Load(item, _index!.VaultRoot);
        var size = item.FileSize <= 0 ? "taille inconnue" : FormatBytes(item.FileSize);
        _detailMetadata.Text =
            $"Format choisi : {item.Format.ToUpperInvariant()} · {size}\n" +
            $"Variantes : {string.Join(", ", item.Variants.Select(variant => variant.Format.ToUpperInvariant()).Distinct())}\n" +
            $"Identifiant : {item.ExternalId}";
        _detailLicense.Text = item.LicenseVerified
            ? $"Licence vérifiée : {item.LicenseSpdx}"
            : $"Licence non vérifiée : {item.LicenseSpdx}";
        _detailLicense.AddThemeColorOverride(
            "font_color",
            item.LicenseVerified ? StudioStyle.Success : StudioStyle.Danger);
        var installed = _session.FindAssetBlueprint(item.ExternalId) is not null;
        _install.Text = installed ? "Déjà dans la palette" : "Ajouter à la palette";
        _install.Disabled = installed || !item.CanInstall || _installing;
        _installAndPlace.Text = installed ? "Placer ce modèle dans la zone" : "Installer puis placer dans la zone";
        _installAndPlace.Disabled = !item.CanInstall || _installing;
        _install.TooltipText = InstallTooltip(item, installed);
        _installAndPlace.TooltipText = _install.TooltipText;
        RenderResults();
    }

    private void InstallSelected(bool placeAfterInstall)
    {
        if (_selected is null || _index is null || !_selected.CanInstall || _installing) return;
        _installing = true;
        _placeAfterInstall = placeAfterInstall;
        _install.Disabled = true;
        _installAndPlace.Disabled = true;
        _install.Text = "Installation…";
        _installAndPlace.Text = "Installation…";
        StatusChanged?.Invoke($"Import de {_selected.DisplayName} depuis le Vault local…", false);
        var item = _selected;
        var vaultRoot = _index.VaultRoot;
        var workspaceRoot = _session.Session!.RootDirectory;
        _installTask = Task.Run(() => _session.PrepareAssetInstall(item, vaultRoot, workspaceRoot));
        SetProcess(true);
    }

    private void CompletePreparation(Task<StudioAssetPreparationResult> task)
    {
        if (task.IsFaulted)
        {
            var message = task.Exception?.GetBaseException().Message ?? "Erreur inconnue";
            CompleteInstall(new StudioAssetInstallResult(false, $"Import refusé · {message}"));
            return;
        }

        var preparation = task.Result;
        CompleteInstall(!preparation.Succeeded || preparation.Prepared is null
            ? new StudioAssetInstallResult(false, preparation.Message)
            : _session.CommitAssetInstall(preparation.Prepared));
    }

    private void CompleteInstall(StudioAssetInstallResult result)
    {
        _installing = false;
        if (!result.Succeeded || result.BlueprintId is null)
        {
            StatusChanged?.Invoke(result.Message, true);
            if (_selected is not null) Select(_selected);
            _placeAfterInstall = false;
            return;
        }
        ProjectChanged?.Invoke();
        StatusChanged?.Invoke(result.Message + " · non enregistré", false);
        if (_selected is not null) Select(_selected);
        if (_placeAfterInstall) InstalledAssetPlacementRequested?.Invoke(result.BlueprintId);
        _placeAfterInstall = false;
        GD.Print($"OPENRPG_FORGE_STUDIO_ASSET_INSTALL_PASS blueprint={result.BlueprintId} " +
                 $"reused={result.Reused.ToString().ToLowerInvariant()}");
    }

    private Texture2D? LoadTexture(string? relativePath)
    {
        if (_index is null || string.IsNullOrWhiteSpace(relativePath)) return null;
        if (_textures.TryGetValue(relativePath, out var cached)) return cached;
        var root = Path.GetFullPath(_index.VaultRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(_index.VaultRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
        try
        {
            var image = Image.LoadFromFile(path);
            if (image.IsEmpty()) return null;
            if (image.GetWidth() > 320 || image.GetHeight() > 200)
            {
                var ratio = Math.Min(320f / image.GetWidth(), 200f / image.GetHeight());
                image.Resize(
                    Math.Max(1, (int)Math.Round(image.GetWidth() * ratio)),
                    Math.Max(1, (int)Math.Round(image.GetHeight() * ratio)),
                    Image.Interpolation.Lanczos);
            }
            var texture = ImageTexture.CreateFromImage(image);
            if (_textures.Count >= 160) _textures.Clear();
            _textures[relativePath] = texture;
            return texture;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private void PreviousPage()
    {
        _offset = Math.Max(0, _offset - PageSize);
        RenderResults();
    }

    private void NextPage()
    {
        _offset += PageSize;
        RenderResults();
    }

    private void UpdateResponsiveLayout()
    {
        _grid.Columns = Size.X switch
        {
            < 900 => 3,
            < 1250 => 4,
            < 1600 => 5,
            < 2050 => 6,
            _ => 7,
        };
    }

    private static string InstallTooltip(StudioAssetLibraryItem item, bool installed)
    {
        if (installed) return "Ce modèle est déjà disponible dans la palette du module.";
        if (!item.AvailableLocally) return "Le fichier n'est pas installé dans le Vault local.";
        if (!item.LicenseVerified) return "L'import est bloqué tant que la licence n'est pas vérifiée.";
        if (!string.Equals(item.Format, "glb", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Format, "gltf", StringComparison.OrdinalIgnoreCase))
            return "Cette version peut être prévisualisée, mais l'ajout exige une variante glTF ou GLB gouvernée.";
        return "Copier ce modèle et ses dépendances gouvernées dans le module, puis créer son blueprint de palette.";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024L * 1024L => $"{bytes / (1024d * 1024d * 1024d):0.##} Gio",
        >= 1024L * 1024L => $"{bytes / (1024d * 1024d):0.##} Mio",
        >= 1024L => $"{bytes / 1024d:0.##} Kio",
        _ => $"{bytes} octets",
    };

    private static string KindLabel(string kind) => kind.ToLowerInvariant() switch
    {
        "animation" => "Animations",
        "model" => "Modèles 3D",
        _ => kind,
    };

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
