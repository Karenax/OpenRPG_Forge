#if TOOLS
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Authoring;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class GlobalSearchWindow : Window
{
    private readonly List<CreatorSearchDocument> _visibleDocuments = [];
    private readonly List<CreatorReference> _visibleReferences = [];
    private readonly CreatorContentKind[] _kinds = Enum.GetValues<CreatorContentKind>();

    private Func<ProjectState?>? _getState;
    private Func<IReadOnlyList<CreatorDiagnostic>>? _getDiagnostics;
    private Func<string?>? _getWorkspaceRoot;
    private Func<SafeRenamePreview, bool>? _applyRename;
    private Action<CreatorSelection>? _publishSelection;
    private Action<Window>? _brandWindow;
    private CreatorSearchDocument? _selectedDocument;
    private SafeRenamePreview? _approvedPreview;
    private WorkspaceResourceRenamePreview? _approvedResourcePreview;

    private LineEdit? _query;
    private OptionButton? _kindFilter;
    private LineEdit? _tagFilter;
    private ItemList? _results;
    private Label? _resultSummary;
    private Label? _identity;
    private Label? _path;
    private Label? _provenance;
    private ItemList? _references;
    private Label? _referenceSummary;
    private Button? _targetButton;
    private LineEdit? _displayName;
    private LineEdit? _slug;
    private Label? _renameHint;
    private Button? _previewButton;
    private Button? _applyButton;
    private Label? _status;
    private CheckBox? _includeScripts;
    private CheckBox? _includeAssets;
    private CheckBox? _includeDiagnostics;
    private SpinBox? _resultLimit;

    public int VisibleResultCount => _visibleDocuments.Count;

    public int VisibleReferenceCount => _visibleReferences.Count;

    public void Initialize(
        Func<ProjectState?> getState,
        Func<IReadOnlyList<CreatorDiagnostic>> getDiagnostics,
        Func<string?> getWorkspaceRoot,
        Func<SafeRenamePreview, bool> applyRename,
        Action<CreatorSelection> publishSelection,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _getDiagnostics = getDiagnostics ?? throw new ArgumentNullException(nameof(getDiagnostics));
        _getWorkspaceRoot = getWorkspaceRoot ?? throw new ArgumentNullException(nameof(getWorkspaceRoot));
        _applyRename = applyRename ?? throw new ArgumentNullException(nameof(applyRename));
        _publishSelection = publishSelection ?? throw new ArgumentNullException(nameof(publishSelection));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));

        Title = "Recherche et références · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = new Vector2I(1180, 760);
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        ConnectSignals();
    }

    public void Open()
    {
        LoadProfile();
        Refresh();
        Exclusive = true;
        PopupCentered(new Vector2I(1180, 760));
        _brandWindow?.Invoke(this);
        _query?.GrabFocus();
    }

    public void Refresh(CreatorContentKind? preferredKind = null, string? preferredId = null)
    {
        if (_results is null)
        {
            return;
        }

        var previousKind = preferredKind ?? _selectedDocument?.Kind;
        var previousId = preferredId ?? _selectedDocument?.Id;
        var state = _getState?.Invoke();
        _visibleDocuments.Clear();
        _results.Clear();
        if (state is null)
        {
            ClearDetails("Aucun projet ouvert.");
            return;
        }

        CreatorContentKind? selectedKind = _kindFilter!.Selected <= 0
            ? null
            : _kinds[_kindFilter.Selected - 1];
        var catalog = BuildResourceCatalog(state.Project);
        var index = CreatorSearchIndex.Build(
            state.Project,
            _includeDiagnostics!.ButtonPressed ? _getDiagnostics?.Invoke() : [],
            catalog.Documents.Where(document =>
                (document.Kind == CreatorContentKind.Script && _includeScripts!.ButtonPressed) ||
                (document.Kind == CreatorContentKind.Asset && _includeAssets!.ButtonPressed)).ToArray());
        var matches = index.Search(new CreatorSearchQuery(
            _query!.Text,
            selectedKind,
            string.IsNullOrWhiteSpace(_tagFilter!.Text) ? null : _tagFilter.Text,
            Limit: (int)_resultLimit!.Value));
        foreach (var result in matches)
        {
            _visibleDocuments.Add(result.Document);
            _results.AddItem($"{FormatKind(result.Document.Kind)}  ·  {result.Document.DisplayName}\n{result.Document.DocumentPath}");
            _results.SetItemTooltip(_results.ItemCount - 1,
                $"Score {result.Score} · {string.Join(", ", result.Document.Tags)}");
        }

        _resultSummary!.Text = $"{_visibleDocuments.Count} résultat(s) · index dérivé v{CreatorSearchDocument.CurrentSchemaVersion}";
        var selectedIndex = _visibleDocuments.FindIndex(document =>
            document.Kind == previousKind && document.Id == previousId);
        if (selectedIndex < 0 && _visibleDocuments.Count > 0)
        {
            selectedIndex = 0;
        }

        if (selectedIndex >= 0)
        {
            _results.Select(selectedIndex);
            SelectResult(selectedIndex);
        }
        else
        {
            ClearDetails("Aucun résultat pour ces filtres.");
        }
    }

    public int SearchForSmokeTest(string text, CreatorContentKind? kind = null, string? tag = null)
    {
        _query!.Text = text;
        _kindFilter!.Selected = kind is null ? 0 : Array.IndexOf(_kinds, kind.Value) + 1;
        _tagFilter!.Text = tag ?? string.Empty;
        Refresh();
        return VisibleResultCount;
    }

    public bool SelectDocumentForSmokeTest(CreatorContentKind kind, string id)
    {
        var index = _visibleDocuments.FindIndex(document => document.Kind == kind && document.Id == id);
        if (index < 0)
        {
            return false;
        }

        _results!.Select(index);
        SelectResult(index);
        return true;
    }

    public bool PreviewRenameForSmokeTest(string displayName, string? slug)
    {
        _displayName!.Text = displayName;
        _slug!.Text = slug ?? string.Empty;
        PreviewRename();
        return (_approvedPreview is not null || _approvedResourcePreview is not null) && !_applyButton!.Disabled;
    }

    public bool ApplyRenameForSmokeTest()
    {
        if (_approvedPreview is null && _approvedResourcePreview is null)
        {
            return false;
        }

        ApplyRename();
        return _approvedPreview is null && _approvedResourcePreview is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
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

        var title = new Label { Text = "RECHERCHE GLOBALE & RÉFÉRENCES", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Retrouvez le contenu canonique, inspectez ses usages puis renommez sans rompre les UUID.",
        });

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 8);
        root.AddChild(filters);
        _query = new LineEdit
        {
            PlaceholderText = "Texte, identifiant, chemin, propriété, action…",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(450, 40),
        };
        filters.AddChild(_query);
        _kindFilter = new OptionButton { CustomMinimumSize = new Vector2(190, 40) };
        _kindFilter.AddItem("Tous les contenus");
        foreach (var kind in _kinds)
        {
            _kindFilter.AddItem(FormatKind(kind));
        }
        filters.AddChild(_kindFilter);
        _tagFilter = new LineEdit
        {
            PlaceholderText = "Tag exact (optionnel)",
            CustomMinimumSize = new Vector2(230, 40),
        };
        filters.AddChild(_tagFilter);
        var refreshButton = new Button { Text = "Actualiser", CustomMinimumSize = new Vector2(130, 40) };
        refreshButton.Pressed += () => Refresh();
        filters.AddChild(refreshButton);
        var profileFilters = new HBoxContainer();
        profileFilters.AddThemeConstantOverride("separation", 12);
        root.AddChild(profileFilters);
        _includeScripts = new CheckBox { Text = "Scripts", ButtonPressed = true };
        _includeAssets = new CheckBox { Text = "Assets", ButtonPressed = true };
        _includeDiagnostics = new CheckBox { Text = "Diagnostics", ButtonPressed = true };
        profileFilters.AddChild(new Label { Text = "Sources indexées :" });
        profileFilters.AddChild(_includeScripts);
        profileFilters.AddChild(_includeAssets);
        profileFilters.AddChild(_includeDiagnostics);
        _resultLimit = new SpinBox { MinValue = 1, MaxValue = 1000, Value = 500, CustomMinimumSize = new Vector2(85, 40) };
        profileFilters.AddChild(new Label { Text = "Limite" });
        profileFilters.AddChild(_resultLimit);
        var saveProfile = new Button { Text = "Mémoriser", CustomMinimumSize = new Vector2(110, 40) };
        saveProfile.Pressed += SaveProfile;
        profileFilters.AddChild(saveProfile);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        var resultsCard = AddCard(body, new Vector2(390, 0));
        resultsCard.AddChild(Section("RÉSULTATS"));
        _resultSummary = MutedLabel();
        resultsCard.AddChild(_resultSummary);
        _results = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AllowReselect = true,
        };
        resultsCard.AddChild(_results);

        var detailsCard = AddCard(body, new Vector2(350, 0), expand: true);
        detailsCard.AddChild(Section("CONTENU & USAGES"));
        _identity = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _identity.AddThemeFontSizeOverride("font_size", 18);
        _identity.AddThemeColorOverride("font_color", new Color("ffb07c"));
        detailsCard.AddChild(_identity);
        _path = MutedLabel();
        _path.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailsCard.AddChild(_path);
        _provenance = MutedLabel();
        detailsCard.AddChild(_provenance);
        _targetButton = new Button { Text = "Cibler dans le projet", CustomMinimumSize = new Vector2(0, 38) };
        detailsCard.AddChild(_targetButton);
        _referenceSummary = new Label();
        _referenceSummary.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        detailsCard.AddChild(_referenceSummary);
        _references = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        detailsCard.AddChild(_references);

        var renameCard = AddCard(body, new Vector2(330, 0));
        renameCard.AddChild(Section("RENOMMAGE SÛR"));
        _renameHint = new Label
        {
            Text = "Sélectionnez un agrégat renommable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _renameHint.AddThemeColorOverride("font_color", new Color("b9aac8"));
        renameCard.AddChild(_renameHint);
        renameCard.AddChild(new Label { Text = "Nom affiché" });
        _displayName = new LineEdit();
        renameCard.AddChild(_displayName);
        renameCard.AddChild(new Label { Text = "Slug canonique" });
        _slug = new LineEdit();
        renameCard.AddChild(_slug);
        var preserved = new Label
        {
            Text = "Les UUID et références restent stables. La prévisualisation est obligatoire.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        preserved.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        renameCard.AddChild(preserved);
        var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        renameCard.AddChild(spacer);
        _previewButton = new Button { Text = "Prévisualiser", CustomMinimumSize = new Vector2(0, 42) };
        renameCard.AddChild(_previewButton);
        _applyButton = new Button
        {
            Text = "Appliquer le renommage",
            CustomMinimumSize = new Vector2(0, 42),
            Disabled = true,
        };
        _applyButton.AddThemeColorOverride("font_color", Colors.White);
        _applyButton.AddThemeStyleboxOverride("normal", Card("d84f32", "ff8b63", 1, 8));
        renameCard.AddChild(_applyButton);

        _status = new Label { Text = "Prêt." };
        _status.AddThemeColorOverride("font_color", new Color("9ee6b3"));
        root.AddChild(_status);
    }

    private void ConnectSignals()
    {
        CloseRequested += Hide;
        _query!.TextChanged += _ => Refresh();
        _kindFilter!.ItemSelected += _ => Refresh();
        _includeScripts!.Toggled += _ => Refresh();
        _includeAssets!.Toggled += _ => Refresh();
        _includeDiagnostics!.Toggled += _ => Refresh();
        _resultLimit!.ValueChanged += _ => Refresh();
        _tagFilter!.TextSubmitted += _ => Refresh();
        _results!.ItemSelected += SelectResult;
        _targetButton!.Pressed += PublishSelectedTarget;
        _displayName!.TextChanged += _ => InvalidatePreview();
        _slug!.TextChanged += _ => InvalidatePreview();
        _previewButton!.Pressed += PreviewRename;
        _applyButton!.Pressed += ApplyRename;
    }

    private void SelectResult(long index)
    {
        var state = _getState?.Invoke();
        if (state is null || index < 0 || index >= _visibleDocuments.Count)
        {
            return;
        }

        _selectedDocument = _visibleDocuments[(int)index];
        _identity!.Text = $"{FormatKind(_selectedDocument.Kind)} · {_selectedDocument.DisplayName}";
        _path!.Text = _selectedDocument.DocumentPath;
        _provenance!.Text = $"Provenance : {_selectedDocument.Provenance} · UUID {_selectedDocument.Id}";
        _targetButton!.Disabled = _selectedDocument.Selection is null;
        _visibleReferences.Clear();
        var catalog = BuildResourceCatalog(state.Project);
        _visibleReferences.AddRange(CreatorReferenceIndex.Build(state.Project, catalog.References)
            .Find(_selectedDocument.Kind, _selectedDocument.Id));
        _references!.Clear();
        foreach (var reference in _visibleReferences)
        {
            _references.AddItem($"{FormatUsage(reference.Usage)}\n{reference.DisplayName}");
            _references.SetItemTooltip(_references.ItemCount - 1, reference.DocumentPath);
        }
        _referenceSummary!.Text = _visibleReferences.Count == 0
            ? "Aucune référence inverse"
            : $"{_visibleReferences.Count} usage(s) · chemins canoniques";

        var resourceRename = _selectedDocument.Kind is CreatorContentKind.Script or CreatorContentKind.Asset;
        var canRename = SafeRenameHandler.IsSupported(_selectedDocument.Kind) || resourceRename;
        _displayName!.Editable = canRename && !resourceRename;
        _slug!.Editable = canRename && _selectedDocument.Kind != CreatorContentKind.Project;
        _previewButton!.Disabled = !canRename;
        _displayName.Text = canRename ? _selectedDocument.DisplayName : string.Empty;
        var preview = SafeRenameHandler.IsSupported(_selectedDocument.Kind)
            ? SafeRenamePreview.Create(
                state.Project,
                _selectedDocument.Kind,
                _selectedDocument.Id,
                _selectedDocument.DisplayName,
                null)
            : null;
        _slug.Text = resourceRename ? _selectedDocument.DocumentPath : preview?.PreviousSlug ?? string.Empty;
        _renameHint!.Text = canRename
            ? $"{FormatKind(_selectedDocument.Kind)} · {ShortId(_selectedDocument.Id)} · {_visibleReferences.Count} référence(s) préservée(s)"
            : "Ce contenu se cible et s'inspecte, mais son nom appartient à son agrégat parent.";
        InvalidatePreview();
    }

    private void PublishSelectedTarget()
    {
        if (_selectedDocument?.Selection is not null)
        {
            _publishSelection?.Invoke(_selectedDocument.Selection);
            SetStatus($"Cible publiée · {_selectedDocument.DisplayName}", false);
        }
    }

    private void PreviewRename()
    {
        var state = _getState?.Invoke();
        if (state is null || _selectedDocument is null)
        {
            SetStatus("Sélection non renommable.", true);
            return;
        }

        var resourceRename = _selectedDocument.Kind is CreatorContentKind.Script or CreatorContentKind.Asset;
        if ((!resourceRename && string.IsNullOrWhiteSpace(_displayName!.Text)) ||
            (_selectedDocument.Kind != CreatorContentKind.Project && string.IsNullOrWhiteSpace(_slug!.Text)))
        {
            SetStatus("Le nom et le slug requis doivent être renseignés.", true);
            return;
        }

        if (resourceRename)
        {
            var root = _getWorkspaceRoot?.Invoke();
            if (string.IsNullOrWhiteSpace(root))
            {
                SetStatus("Le dossier du projet est requis pour renommer une ressource.", true);
                return;
            }
            try
            {
                _approvedResourcePreview = WorkspaceResourceRenameService.Preview(root,
                    _selectedDocument.DocumentPath, _slug!.Text, "toolset:global-search-r034");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                SetStatus($"Renommage de ressource refusé : {exception.Message}", true);
                return;
            }
            _applyButton!.Disabled = false;
            SetStatus($"Aperçu validé · {_approvedResourcePreview.ReferenceEdits.Count} référence(s) seront migrées; UUID conservé.", false);
            return;
        }

        if (!SafeRenameHandler.IsSupported(_selectedDocument.Kind))
        {
            SetStatus("Sélection non renommable.", true);
            return;
        }
        _approvedPreview = SafeRenamePreview.Create(state.Project, _selectedDocument.Kind, _selectedDocument.Id,
            _displayName!.Text, _selectedDocument.Kind == CreatorContentKind.Project ? null : _slug!.Text);
        if (_approvedPreview is null)
        {
            SetStatus("La cible n'existe plus dans la révision courante.", true);
            return;
        }

        _applyButton!.Disabled = false;
        SetStatus(
            $"Aperçu validé · {_approvedPreview.PreservedReferenceCount} référence(s) resteront liées au même UUID.",
            false);
    }

    private void ApplyRename()
    {
        if (_approvedResourcePreview is { } resourcePreview)
        {
            try
            {
                WorkspaceResourceRenameService.Apply(resourcePreview);
                _approvedResourcePreview = null;
                _applyButton!.Disabled = true;
                Refresh();
                SetStatus($"Ressource renommée · UUID {resourcePreview.ResourceId.ToString()[..8]} conservé · références migrées.", false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                SetStatus($"Renommage transactionnel annulé : {exception.Message}", true);
            }
            return;
        }
        var preview = _approvedPreview;
        if (preview is null)
        {
            return;
        }

        if (_applyRename?.Invoke(preview) != true)
        {
            SetStatus("Renommage refusé par la validation transactionnelle.", true);
            return;
        }

        _approvedPreview = null;
        _approvedResourcePreview = null;
        _applyButton!.Disabled = true;
        Refresh(preview.TargetKind, preview.TargetId);
        SetStatus($"Renommage appliqué · UUID {ShortId(preview.TargetId)} conservé.", false);
    }

    private void InvalidatePreview()
    {
        _approvedPreview = null;
        _approvedResourcePreview = null;
        if (_applyButton is not null)
        {
            _applyButton.Disabled = true;
        }
    }

    private void ClearDetails(string message)
    {
        _selectedDocument = null;
        _identity!.Text = message;
        _path!.Text = string.Empty;
        _provenance!.Text = string.Empty;
        _visibleReferences.Clear();
        _references!.Clear();
        _referenceSummary!.Text = "Aucun usage";
        _targetButton!.Disabled = true;
        _displayName!.Text = string.Empty;
        _displayName.Editable = false;
        _slug!.Text = string.Empty;
        _slug.Editable = false;
        _previewButton!.Disabled = true;
        InvalidatePreview();
    }

    private void LoadProfile()
    {
        var state = _getState?.Invoke();
        var root = _getWorkspaceRoot?.Invoke();
        if (state is null || string.IsNullOrWhiteSpace(root) || _includeScripts is null) return;
        try
        {
            var profile = CreatorSearchProfileStore.Load(root, state.Project.Id.Value);
            _includeScripts.ButtonPressed = profile.IncludeScripts;
            _includeAssets!.ButtonPressed = profile.IncludeAssets;
            _includeDiagnostics!.ButtonPressed = profile.IncludeDiagnostics;
            _resultLimit!.Value = profile.ResultLimit;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            SetStatus($"Profil de recherche invalide : {exception.Message}", true);
        }
    }

    private void SaveProfile()
    {
        var state = _getState?.Invoke();
        var root = _getWorkspaceRoot?.Invoke();
        if (state is null || string.IsNullOrWhiteSpace(root)) return;
        try
        {
            var current = CreatorSearchProfileStore.Load(root, state.Project.Id.Value);
            CreatorSearchProfileStore.Save(root, current with
            {
                Provenance = "toolset:global-search-r034",
                ResultLimit = (int)_resultLimit!.Value,
                IncludeScripts = _includeScripts!.ButtonPressed,
                IncludeAssets = _includeAssets!.ButtonPressed,
                IncludeDiagnostics = _includeDiagnostics!.ButtonPressed,
            });
            SetStatus("Profil de recherche mémorisé dans .forge/editor-search.json.", false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SetStatus($"Profil non enregistré : {exception.Message}", true);
        }
    }

    private CreatorWorkspaceResourceCatalog BuildResourceCatalog(Forge.Domain.Projects.ProjectDefinition project)
    {
        var root = _getWorkspaceRoot?.Invoke();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return CreatorWorkspaceResourceCatalog.Build(project, []);
        var resources = new List<CreatorWorkspaceResource>();
        if (_includeScripts?.ButtonPressed == true)
        {
            var scriptsRoot = Path.Combine(root, "scripts");
            if (Directory.Exists(scriptsRoot))
                foreach (var path in Directory.EnumerateFiles(scriptsRoot, "*.forge", SearchOption.AllDirectories))
                    resources.Add(CreatorWorkspaceResource.Create(CreatorContentKind.Script,
                        Path.GetRelativePath(root, path), File.ReadAllText(path), ["script", "forgescript"],
                        id: WorkspaceResourceIdentityStore.Resolve(root, Path.GetRelativePath(root, path))));
        }
        if (_includeAssets?.ButtonPressed == true)
        {
            foreach (var folder in new[] { "assets", "content/assets" })
            {
                var assetRoot = Path.Combine(root, folder);
                if (!Directory.Exists(assetRoot)) continue;
                foreach (var path in Directory.EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories)
                             .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.godot{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
                    resources.Add(CreatorWorkspaceResource.Create(CreatorContentKind.Asset,
                        Path.GetRelativePath(root, path), Path.GetFileNameWithoutExtension(path),
                        ["asset", Path.GetExtension(path).TrimStart('.')],
                        id: WorkspaceResourceIdentityStore.Resolve(root, Path.GetRelativePath(root, path))));
            }
        }
        return CreatorWorkspaceResourceCatalog.Build(project, resources
            .GroupBy(resource => resource.Id).Select(group => group.First()).ToArray());
    }

    private void SetStatus(string message, bool error)
    {
        _status!.Text = message;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8c8c" : "9ee6b3"));
    }

    private static Label Section(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color("ff9858"));
        return label;
    }

    private static Label MutedLabel()
    {
        var label = new Label();
        label.AddThemeColorOverride("font_color", new Color("a899b9"));
        return label;
    }

    private static VBoxContainer AddCard(HBoxContainer parent, Vector2 minimumSize, bool expand = false)
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

    private static string FormatKind(CreatorContentKind kind) => kind switch
    {
        CreatorContentKind.Project => "Projet",
        CreatorContentKind.Module => "Module",
        CreatorContentKind.ProjectMetadata => "Métadonnées",
        CreatorContentKind.ProjectVariable => "Variable de projet",
        CreatorContentKind.Area => "Zone",
        CreatorContentKind.Entity => "Entité",
        CreatorContentKind.Portal => "Transition",
        CreatorContentKind.Blueprint => "Blueprint",
        CreatorContentKind.Dialogue => "Dialogue",
        CreatorContentKind.DialogueNode => "Nœud de dialogue",
        CreatorContentKind.DialogueChoice => "Choix de dialogue",
        CreatorContentKind.Quest => "Quête",
        CreatorContentKind.QuestState => "État de quête",
        CreatorContentKind.QuestTransition => "Transition de quête",
        CreatorContentKind.Script => "Script",
        CreatorContentKind.Asset => "Asset",
        CreatorContentKind.LocalizedText => "Texte localisé",
        CreatorContentKind.Diagnostic => "Diagnostic",
        _ => kind.ToString(),
    };

    private static string FormatUsage(CreatorReferenceUsageKind usage) => usage switch
    {
        CreatorReferenceUsageKind.ModuleEntryArea => "Zone d'entrée du module",
        CreatorReferenceUsageKind.EntityBlueprint => "Blueprint d'instance",
        CreatorReferenceUsageKind.PortalSourceEntity => "Source de transition",
        CreatorReferenceUsageKind.PortalDestinationArea => "Zone de destination",
        CreatorReferenceUsageKind.PortalDestinationEntity => "Ancre de destination",
        CreatorReferenceUsageKind.DialogueEntryNode => "Entrée de dialogue",
        CreatorReferenceUsageKind.DialogueSpeakerBlueprint => "Locuteur de dialogue",
        CreatorReferenceUsageKind.DialogueChoiceTargetNode => "Cible de choix",
        CreatorReferenceUsageKind.QuestInitialState => "État initial de quête",
        CreatorReferenceUsageKind.QuestTransitionFromState => "Départ de transition",
        CreatorReferenceUsageKind.QuestTransitionToState => "Arrivée de transition",
        CreatorReferenceUsageKind.LocalizedTextKey => "Clé de texte localisé",
        CreatorReferenceUsageKind.ProjectVariableKey => "Variable de projet",
        CreatorReferenceUsageKind.QuestStateValue => "Valeur d’état de quête",
        CreatorReferenceUsageKind.ScriptPath => "Chemin de script",
        CreatorReferenceUsageKind.AssetPath => "Chemin d’asset",
        _ => usage.ToString(),
    };

    private static string ShortId(string id) => id.Length > 8 ? id[..8] : id;
}
#endif
