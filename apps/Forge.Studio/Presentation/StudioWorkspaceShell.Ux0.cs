using System.Globalization;
using System.Text;
using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Studio.Core.Navigation;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWorkspaceShell
{
    private readonly Dictionary<TreeItem, CreatorExplorerNode> _explorerNodes = [];
    private readonly Dictionary<string, TreeItem> _explorerSelectionItems = new(StringComparer.Ordinal);
    private readonly List<StudioGlobalSearchEntry> _globalSearchEntries = [];
    private VBoxContainer _pagesNavigation = null!;
    private VBoxContainer _explorerNavigation = null!;
    private Button _pagesModeButton = null!;
    private Button _explorerModeButton = null!;
    private OptionButton _explorerGrouping = null!;
    private Tree _explorerTree = null!;
    private StudioGlobalSearchOverlay _globalSearchOverlay = null!;
    private StudioDisplayFilterOverlay _displayFilterOverlay = null!;
    private Button _globalSearchButton = null!;
    private Button _filterButton = null!;
    private bool _syncingExplorer;
    private bool _loadingExplorerGrouping;
    private int _explorerNodeCount;
    private CreatorContentKind? _referenceTargetKind;
    private string? _referenceTargetId;
    private string _referenceTargetTitle = string.Empty;
    private bool _contextualHelpVisible;
    private string _helpQueryText = string.Empty;

    private static readonly IReadOnlyList<(string Id, string Title, string Description)> GlobalActions =
    [
        ("save", "Enregistrer le module", "Écrire atomiquement les modifications locales · Ctrl+S"),
        ("undo", "Annuler la dernière modification", "Restaurer l’état précédent · Ctrl+Z"),
        ("redo", "Rétablir la modification", "Rejouer la modification annulée · Ctrl+Y"),
        ("filters", "Afficher les filtres", "Types, tags, auteur, état, calques, volumes et vues enregistrées"),
        ("workspace", "Organiser l’espace de travail", "Profils nommés, onglets de zones et raccourcis configurables"),
        ("explorer", "Ouvrir l’explorateur du projet", "Hiérarchie compacte synchronisée avec la sélection 3D"),
        ("rename", "Renommer en sécurité", "Choisir un contenu, prévisualiser son nouveau nom et préserver ses références"),
        ("diagnostics", "Ouvrir les messages et erreurs", "Problèmes localisés, activité, corrections et export de diagnostic"),
        ("new-area", "Créer une nouvelle zone", "Assistant en trois étapes, aperçu sans écriture et document indépendant"),
        ("resize-area", "Redimensionner la zone active", "Dimensions, ancrage et objets hors limites contrôlés avant application"),
        ("duplicate-area", "Copier la zone active", "Copie complète ou gabarit vide avec de nouvelles identités internes"),
        ("loading-screen", "Configurer l’écran de chargement", "Média importé, astuces, progression réelle et repli accessible"),
        ("area-layers", "Organiser les calques de la zone", "Groupes, visibilité, verrouillage, responsables, sélection et export partiel"),
        ("trigger-foundation", "Composer un déclencheur Quand Si Alors", "Volume, activateurs, conditions et actions typées · aperçu obligatoire avant application"),
        ("placeable-blueprint", "Configurer un blueprint de décor", "Visuel, collision, rôle, composants, scripts, transformation et aperçu 3D"),
        ("static-decoration", "Configurer un décor statique", "Regroupement, instanciation, occlusion, fusion et collision optionnelle"),
        ("usable-placeable", "Configurer un objet utilisable", "Actions proposées, portée, ciblage, autorité, conditions et événement OnUsed"),
        ("placeable-container", "Configurer un conteneur", "Capacité, masse, propriétaire, accès, sauvegarde, butin, interface et multijoueur"),
        ("placeable-identity", "Configurer l’identité et l’apparence", "Nom court, langues, tags, notes, meshes, matériaux, icône, portrait et VFX"),
        ("placeable-transform", "Configurer la transformation précise", "Coordonnées, rotation, échelle, pivot, grille, parent, surface, animation et gizmo 3D"),
        ("placeable-initial-animation", "Configurer l’animation initiale", "État chargé, animations, apparences, interactions, transitions et aperçu"),
        ("placeable-destructibility", "Configurer la destructibilité", "Santé, armure, résistances, étapes visuelles, destruction, débris et réparation"),
        ("placeable-plot-protection", "Configurer la protection plot", "Actions protégées, droits, feedback et dérogation du maître de jeu auditée"),
        ("placeable-faction", "Configurer la faction du placeable", "Propriétaire, appartenances, hostilité, crime, accès, ciblage et aperçu sans écriture"),
        ("placeable-conversation", "Configurer la conversation du placeable", "Dialogues, interaction, état, utilisateur, objet, événement, priorité et aperçu sans écriture"),
        ("placeable-lock", "Configurer le verrouillage du placeable", "État, clés, justificatifs, compétence, code, piratage, droits, reverrouillage et brouillage"),
        ("placeable-credential", "Configurer la clé consommée", "Conservation, consommation, charges, partage, permission virtuelle, audit et aperçu sans écriture"),
        ("placeable-trap", "Configurer le piège du placeable", "Déclencheur, effet, détection, désamorçage, réarmement, propriétaire, alliés, VFX et aperçu sans écriture"),
        ("placeable-loot", "Composer le trésor initial", "Objets, catégories, poids, quantités, qualité, moment, graine, état sauvegardé et aperçu déterministe"),
        ("placeable-container-events", "Configurer ouverture et fermeture", "Liaisons ForgeScript, acteur, résultat, état précédent, autorité et aperçu sans écriture"),
        ("placeable-lock-events", "Configurer verrouillage et déverrouillage", "Événements avant et après, méthode, justificatif, acteur, veto contrôlé et aperçu sans écriture"),
        ("help", "Afficher l’aide contextuelle", "Tâche actuelle, prochaine action, exemples et sources locales"),
        ("add", "Ajouter un objet dans la zone", "Ouvrir la palette près de la sélection"),
    ];

    private void BuildUx0Navigation(VBoxContainer pageColumn)
    {
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 4);
        _explorerModeButton = StudioStyle.Button("Explorer", compact: true);
        _explorerModeButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _explorerModeButton.Pressed += () => ShowNavigationMode(explorer: true);
        modeRow.AddChild(_explorerModeButton);
        _pagesModeButton = StudioStyle.Button("Pages", compact: true);
        _pagesModeButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _pagesModeButton.Pressed += () => ShowNavigationMode(explorer: false);
        modeRow.AddChild(_pagesModeButton);
        pageColumn.AddChild(modeRow);

        _pagesNavigation = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _pagesNavigation.AddThemeConstantOverride("separation", 6);
        _pagesNavigation.AddChild(StudioStyle.SectionLabel("PAGES DE LA SECTION"));
        _pagesNavigation.AddChild(_search);
        var pageScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _pagesNavigation.AddChild(pageScroll);
        pageScroll.AddChild(_pageList);
        pageColumn.AddChild(_pagesNavigation);

        _explorerNavigation = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _explorerNavigation.AddThemeConstantOverride("separation", 6);
        _explorerNavigation.AddChild(StudioStyle.SectionLabel("HIÉRARCHIE DU PROJET"));
        _explorerGrouping = new OptionButton();
        StudioStyle.ApplyOptionButton(_explorerGrouping);
        foreach (var grouping in Enum.GetValues<EditorExplorerGrouping>())
        {
            _explorerGrouping.AddItem(ExplorerGroupingName(grouping));
        }
        _explorerGrouping.ItemSelected += ChangeExplorerGrouping;
        _explorerNavigation.AddChild(_explorerGrouping);
        _explorerTree = new Tree
        {
            Columns = 1,
            HideRoot = false,
            SelectMode = Tree.SelectModeEnum.Single,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 280),
        };
        _explorerTree.ItemSelected += SelectExplorerNode;
        _explorerNavigation.AddChild(_explorerTree);
        pageColumn.AddChild(_explorerNavigation);
        ShowNavigationMode(explorer: true);
    }

    private void InitializeUx0Overlays()
    {
        _globalSearchOverlay = new StudioGlobalSearchOverlay();
        _globalSearchOverlay.QueryChanged += RebuildGlobalSearch;
        _globalSearchOverlay.EntrySelected += ShowGlobalSearchDetails;
        _globalSearchOverlay.EntryActivated += ActivateGlobalSearchEntry;
        _globalSearchOverlay.RenameRequested += OpenSafeRenameFromSearch;
        AddChild(_globalSearchOverlay);

        _displayFilterOverlay = new StudioDisplayFilterOverlay();
        _displayFilterOverlay.ApplyRequested += filter => ApplyDisplayFilter(filter, persist: true);
        _displayFilterOverlay.ResetRequested += _ => ResetDisplayFilter();
        _displayFilterOverlay.SavePresetRequested += SaveDisplayFilterPreset;
        _displayFilterOverlay.ProfileSelected += SelectDisplayFilterProfile;
        AddChild(_displayFilterOverlay);

        WorldViewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);
        RebuildExplorer();
        RefreshFilterSummary();
    }

    private void ShowNavigationMode(bool explorer)
    {
        if (_explorerNavigation is null || _pagesNavigation is null)
        {
            return;
        }
        _explorerNavigation.Visible = explorer;
        _pagesNavigation.Visible = !explorer;
        _explorerModeButton.AddThemeStyleboxOverride("normal", StudioStyle.Box(
            explorer ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
            explorer ? StudioStyle.Success : StudioStyle.Border,
            5,
            explorer ? 2 : 1));
        _pagesModeButton.AddThemeStyleboxOverride("normal", StudioStyle.Box(
            explorer ? StudioStyle.SurfaceRaised : StudioStyle.Selection,
            explorer ? StudioStyle.Border : StudioStyle.Success,
            5,
            explorer ? 1 : 2));
        if (explorer)
        {
            RebuildExplorer();
        }
    }

    private void RebuildExplorer()
    {
        if (_explorerTree is null || !_session.IsOpen)
        {
            return;
        }

        _syncingExplorer = true;
        _loadingExplorerGrouping = true;
        _explorerGrouping.Selected = (int)_session.WorkspaceProfile.Shell.ExplorerGrouping;
        _loadingExplorerGrouping = false;
        _explorerNodes.Clear();
        _explorerSelectionItems.Clear();
        _explorerTree.Clear();
        _explorerNodeCount = 0;
        AddExplorerNode(_session.BuildExplorer(), null, 0);
        _syncingExplorer = false;
        SyncExplorerSelection();
    }

    private void AddExplorerNode(CreatorExplorerNode node, TreeItem? parent, int depth)
    {
        var item = _explorerTree.CreateItem(parent);
        item.SetText(0, node.DisplayName);
        item.SetTooltipText(0, $"Ouvrir « {node.DisplayName} »");
        item.Collapsed = depth > 1;
        _explorerNodes[item] = node;
        _explorerNodeCount++;
        if (node.ToSelection() is { } selection)
        {
            _explorerSelectionItems.TryAdd(SelectionKey(selection.Kind, selection.Id), item);
            if (selection.Kind == CreatorSelectionKind.Area && selection.Id == _session.ActiveAreaId?.ToString())
            {
                item.Collapsed = false;
            }
        }
        foreach (var child in node.Children)
        {
            AddExplorerNode(child, item, depth + 1);
        }
    }

    private void SelectExplorerNode()
    {
        if (_syncingExplorer || _explorerTree.GetSelected() is not { } item ||
            !_explorerNodes.TryGetValue(item, out var node) || node.ToSelection() is not { } selection)
        {
            return;
        }
        RouteSelection(selection, "Explorateur");
    }

    private void SyncExplorerSelection()
    {
        if (_explorerTree is null)
        {
            return;
        }
        var kind = _session.SelectedEntityId is null ? CreatorSelectionKind.Area : CreatorSelectionKind.Entity;
        var id = _session.SelectedEntityId?.ToString() ?? _session.ActiveAreaId?.ToString();
        if (id is null || !_explorerSelectionItems.TryGetValue(SelectionKey(kind, id), out var item))
        {
            return;
        }
        _syncingExplorer = true;
        for (var parent = item.GetParent(); parent is not null; parent = parent.GetParent())
        {
            parent.Collapsed = false;
        }
        item.Select(0);
        _explorerTree.ScrollToItem(item);
        _syncingExplorer = false;
    }

    private void ChangeExplorerGrouping(long index)
    {
        if (_loadingExplorerGrouping || index < 0 || index >= Enum.GetValues<EditorExplorerGrouping>().Length)
        {
            return;
        }
        var result = _session.ConfigureExplorerGrouping((EditorExplorerGrouping)index, persist: true);
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RebuildExplorer();
        }
    }

    private void OpenGlobalSearch(string query = "")
    {
        _displayFilterOverlay.Close();
        _globalSearchOverlay.Open(query);
    }

    private void RebuildGlobalSearch(string query)
    {
        IReadOnlyList<StudioGlobalSearchEntry> entries;
        try
        {
            entries = CreateGlobalSearchEntries(query);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidDataException or ArgumentException)
        {
            SetStatus($"Recherche incomplète · {exception.Message}", error: true);
            entries = [];
        }
        _globalSearchEntries.Clear();
        _globalSearchEntries.AddRange(entries);
        _globalSearchOverlay.SetResults(entries);
    }

    private IReadOnlyList<StudioGlobalSearchEntry> CreateGlobalSearchEntries(string query)
    {
        var entries = new List<StudioGlobalSearchEntry>();
        foreach (var page in StudioPageCatalog.Pages)
        {
            var section = StudioPageCatalog.SectionForPage(page.Id);
            var score = MatchScore(query, $"{page.Title} {page.Subtitle} {section.Title} {page.PrimaryAction}");
            if (score > 0)
            {
                entries.Add(new StudioGlobalSearchEntry(
                    $"page:{page.Id}",
                    "PAGE",
                    page.Title,
                    $"{section.Title} · {page.Subtitle}",
                    score + 24,
                    PageId: page.Id));
            }
        }

        foreach (var action in GlobalActions)
        {
            var score = MatchScore(query, $"{action.Title} {action.Description}");
            if (score > 0)
            {
                entries.Add(new StudioGlobalSearchEntry(
                    $"action:{action.Id}",
                    "ACTION",
                    action.Title,
                    action.Description,
                    score + 18,
                    ActionId: action.Id));
            }
        }

        foreach (var result in _session.Search(query, 160))
        {
            var document = result.Document;
            entries.Add(new StudioGlobalSearchEntry(
                $"content:{document.Kind}:{document.Id}",
                ContentGroupName(document.Kind),
                document.DisplayName,
                $"{ContentKindName(document.Kind)} du module",
                result.Score + 8,
                Document: document));
        }

        return entries
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Group, StringComparer.Ordinal)
            .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Take(160)
            .ToArray();
    }

    private void ShowGlobalSearchDetails(int index)
    {
        var entry = _globalSearchOverlay.EntryAt(index);
        if (entry is null)
        {
            return;
        }

        if (entry.Document is not { } document)
        {
            _globalSearchOverlay.SetRenameAvailable(false);
            _globalSearchOverlay.ShowDetails(
                entry.Title,
                entry.PageId is null ? "Commande du shell" : "Navigation Studio",
                entry.Subtitle,
                []);
            return;
        }

        _globalSearchOverlay.SetRenameAvailable(_session.CreateSafeRenameDraft(document) is not null);
        var references = _session.FindReferences(document.Kind, document.Id);
        _globalSearchOverlay.ShowDetails(
            $"{ContentKindName(document.Kind)} · {document.DisplayName}",
            "Contenu du module courant",
            $"Type : {ContentKindName(document.Kind)} · résultat ouvrable",
            references.Select(reference =>
                $"{ReferenceUsageName(reference.Usage)} · {reference.DisplayName}").ToArray());
    }

    private void ActivateGlobalSearchEntry(int index)
    {
        var entry = _globalSearchOverlay.EntryAt(index);
        if (entry is null)
        {
            return;
        }
        _globalSearchOverlay.Close();
        if (entry.PageId is not null)
        {
            SwitchPage(entry.PageId);
            SetStatus($"Page ouverte depuis Ctrl+K · {entry.Title}");
        }
        else if (entry.ActionId is not null)
        {
            ExecuteGlobalAction(entry.ActionId);
        }
        else if (entry.Document is not null)
        {
            RouteDocument(entry.Document);
        }
    }

    private void ExecuteGlobalAction(string actionId)
    {
        switch (actionId)
        {
            case "save": Save(); break;
            case "undo": Undo(); break;
            case "redo": Redo(); break;
            case "filters": OpenDisplayFilters(); break;
            case "workspace": OpenWorkspaceSettings(); break;
            case "explorer": ShowNavigationMode(explorer: true); break;
            case "rename":
                OpenGlobalSearch();
                SetStatus("Choisissez un contenu puis utilisez « Renommer en sécurité ».");
                break;
            case "diagnostics": SwitchPage("validation"); break;
            case "new-area": OpenAreaCreation(); break;
            case "resize-area": OpenAreaDocumentResize(); break;
            case "duplicate-area": OpenAreaDocumentClone(); break;
            case "loading-screen": OpenAreaLoading(); break;
            case "area-layers": OpenAreaLayers(); break;
            case "trigger-foundation": OpenTriggerFoundationUx1Y(); break;
            case "placeable-blueprint": OpenPlaceableBlueprint(); break;
            case "static-decoration": OpenStaticDecoration(); break;
            case "usable-placeable": OpenInteractablePlaceable(); break;
            case "placeable-container": OpenContainerPlaceable(); break;
            case "placeable-identity": OpenIdentityAppearancePlaceable(); break;
            case "placeable-transform": OpenPreciseTransformPlaceable(); break;
            case "placeable-initial-animation": OpenInitialAnimationPlaceable(); break;
            case "placeable-destructibility": OpenDestructibilityPlaceable(); break;
            case "placeable-plot-protection": OpenPlotProtectionPlaceable(); break;
            case "placeable-faction": OpenFactionPlaceable(); break;
            case "placeable-conversation": OpenConversationPlaceable(); break;
            case "placeable-lock": OpenLockPlaceable(); break;
            case "placeable-credential": OpenCredentialPlaceable(); break;
            case "placeable-trap": OpenTrapPlaceable(); break;
            case "placeable-loot": OpenInitialTreasurePlaceable(); break;
            case "placeable-container-events": OpenContainerEventsPlaceable(); break;
            case "placeable-lock-events": OpenLockEventsPlaceable(); break;
            case "help": ShowContextualHelp(); break;
            case "add":
                SwitchPage("zones");
                BeginAddEntity(WorldViewport.SuggestedPlacementPosition);
                break;
        }
    }

    private void RouteDocument(CreatorSearchDocument document)
    {
        if (document.Selection is not null)
        {
            RouteSelection(document.Selection, "Recherche globale");
            return;
        }
        switch (document.Kind)
        {
            case CreatorContentKind.Script: SwitchPage("scripts"); break;
            case CreatorContentKind.Asset: SwitchPage("assets"); break;
            case CreatorContentKind.Module:
            case CreatorContentKind.ProjectMetadata:
            case CreatorContentKind.ProjectVariable:
                SwitchPage("module");
                break;
            default:
                SetStatus($"Contenu localisé · {document.DisplayName}");
                return;
        }
        SetStatus($"Contenu ciblé · {document.DisplayName}");
    }

    private void RouteSelection(CreatorSelection selection, string source)
    {
        _referenceTargetKind = null;
        _referenceTargetId = null;
        _contextualHelpVisible = false;
        if (selection.Kind == CreatorSelectionKind.Entity)
        {
            var entityId = EntityId.Parse(selection.Id);
            var area = _session.Project!.Areas.FirstOrDefault(candidate =>
                candidate.Entities.Any(entity => entity.Id == entityId));
            if (area is null)
            {
                SetStatus("La cible n’existe plus dans le projet courant.", error: true);
                return;
            }
            if (_session.ActiveAreaId != area.Id)
            {
                _session.SelectArea(area.Id);
                PopulateAreas();
                WorldViewport.Rebuild(_session);
            }
            SwitchPage("zones");
            SelectEntity(entityId);
        }
        else if (selection.Kind == CreatorSelectionKind.Area)
        {
            var areaId = AreaId.Parse(selection.Id);
            if (_session.SelectArea(areaId))
            {
                PopulateAreas();
                WorldViewport.Rebuild(_session);
                SwitchPage("zones");
                RebuildExplorer();
            }
        }
        else
        {
            SwitchPage(selection.Kind switch
            {
                CreatorSelectionKind.Blueprint => "blueprints",
                CreatorSelectionKind.Dialogue => "dialogues",
                CreatorSelectionKind.Quest => "quests",
                CreatorSelectionKind.Diagnostic => "validation",
                _ => "module",
            });
        }
        SetStatus($"{source} · {selection.DisplayName}");
        SyncExplorerSelection();
    }

    private void OpenDisplayFilters()
    {
        _globalSearchOverlay.Close();
        _displayFilterOverlay.Open(
            _session.WorkspaceProfile,
            _session.WorkspaceProfiles,
            _session.Project!.Areas);
    }

    private void ApplyDisplayFilter(EditorDisplayFilter filter, bool persist)
    {
        var result = _session.ConfigureDisplayFilter(filter, persist);
        if (!result.Succeeded)
        {
            _displayFilterOverlay.SetStatus(result.Message, error: true);
            SetStatus(result.Message, error: true);
            return;
        }
        var visible = WorldViewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);
        _displayFilterOverlay.Load(
            _session.WorkspaceProfile,
            _session.WorkspaceProfiles,
            _session.Project!.Areas);
        _displayFilterOverlay.SetStatus(
            $"Filtre appliqué · {visible} objet(s) visibles · profil atomique validé",
            error: false);
        RefreshFilterSummary();
        SetStatus($"Filtre d’affichage mémorisé · {visible} objet(s) visibles");
    }

    private void ResetDisplayFilter()
    {
        var current = _session.WorkspaceProfile.DisplayFilter;
        ApplyDisplayFilter(new EditorDisplayFilter(
            id: current.Id,
            schemaVersion: EditorDisplayFilter.CurrentSchemaVersion,
            provenance: "studio:ux0-a-reset"), persist: true);
    }

    private void SaveDisplayFilterPreset(string name, EditorDisplayFilter filter)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _displayFilterOverlay.SetStatus("Donnez un nom à la vue avant de l’enregistrer.", error: true);
            return;
        }
        var result = _session.SaveDisplayFilterPreset(name, filter);
        if (result.Succeeded)
        {
            WorldViewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);
            RebuildExplorer();
            RefreshFilterSummary();
            _displayFilterOverlay.Load(
                _session.WorkspaceProfile,
                _session.WorkspaceProfiles,
                _session.Project!.Areas);
        }
        _displayFilterOverlay.SetStatus(result.Message, !result.Succeeded);
        SetStatus(result.Message, !result.Succeeded);
    }

    private void SelectDisplayFilterProfile(Guid profileId)
    {
        var result = _session.SelectWorkspaceProfile(profileId);
        if (result.Succeeded)
        {
            WorldViewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);
            RebuildExplorer();
            RefreshFilterSummary();
            _displayFilterOverlay.Load(
                _session.WorkspaceProfile,
                _session.WorkspaceProfiles,
                _session.Project!.Areas);
        }
        _displayFilterOverlay.SetStatus(result.Message, !result.Succeeded);
        SetStatus(result.Message, !result.Succeeded);
    }

    private void RefreshFilterSummary()
    {
        if (_filterButton is null)
        {
            return;
        }
        var filter = _session.WorkspaceProfile.DisplayFilter;
        var facets = filter.RequiredTags.Count + filter.VisibleLayerIds.Count +
            (filter.IsolateSelection ? 1 : 0) + (filter.GhostHidden ? 1 : 0) +
            (filter.VisibleKinds.Count == Enum.GetValues<BlueprintKind>().Length ? 0 : 1) +
            (string.IsNullOrWhiteSpace(filter.Author) ? 0 : 1) +
            (string.IsNullOrWhiteSpace(filter.State) ? 0 : 1);
        _filterButton.Text = facets == 0 ? "Filtres" : $"Filtres · {facets}";
        _filterButton.TooltipText = facets == 0
            ? "Tous les objets sont visibles."
            : $"{facets} facette(s) active(s) · vue {_session.WorkspaceProfile.Name}";
    }

    private void ShowReferences(CreatorContentKind kind, string id, string title)
    {
        _contextualHelpVisible = false;
        _referenceTargetKind = kind;
        _referenceTargetId = id;
        _referenceTargetTitle = title;
        _compactInspectorRequested = true;
        UpdateResponsiveLayout();
        RefreshInspector();
        SetStatus($"Utilisations inverses · {title} · éléments liés");
    }

    private void BuildReferenceInspector()
    {
        var references = _session.FindReferences(_referenceTargetKind!.Value, _referenceTargetId!);
        _inspectorPanel.CustomMinimumSize = new Vector2(360, 0);
        _inspectorTitle.Text = "Références · " + _referenceTargetTitle;
        AddInspectorValue("CIBLE", $"{ContentKindName(_referenceTargetKind.Value)} · {_referenceTargetTitle}");
        AddInspectorValue("UTILISATIONS", references.Count == 0
            ? "Aucune référence inverse"
            : $"{references.Count} élément(s) lié(s)");
        foreach (var reference in references)
        {
            var button = StudioStyle.Button(
                $"{ReferenceUsageName(reference.Usage)}\n{reference.DisplayName}", compact: true);
            button.Alignment = HorizontalAlignment.Left;
            button.TooltipText = $"Ouvrir « {reference.DisplayName} »";
            button.Disabled = reference.SourceSelection is null;
            if (reference.SourceSelection is { } selection)
            {
                button.Pressed += () => RouteSelection(selection, "Référence inverse");
            }
            _inspectorContent.AddChild(button);
        }
        var close = StudioStyle.Button("Revenir à l’inspecteur", compact: true);
        close.Pressed += () =>
        {
            _referenceTargetKind = null;
            _referenceTargetId = null;
            RefreshInspector();
        };
        _inspectorContent.AddChild(close);
    }

    private void ShowContextualHelp(string? query = null)
    {
        _referenceTargetKind = null;
        _referenceTargetId = null;
        _contextualHelpVisible = true;
        _helpQueryText = query ?? HelpTopicForPage(PageId);
        _compactInspectorRequested = true;
        UpdateResponsiveLayout();
        RefreshInspector();
        SetStatus("Aide contextuelle locale · tâche et prochaine action affichées");
    }

    private void BuildContextualHelpInspector(StudioPageDefinition page, EntityInstance? selected)
    {
        _inspectorPanel.CustomMinimumSize = new Vector2(360, 0);
        _inspectorTitle.Text = "Aide · " + page.Title;
        AddInspectorValue("TÂCHE ACTUELLE", page.Subtitle);
        AddInspectorValue("PROCHAINE ACTION", selected is null
            ? page.Layout == StudioPageLayout.Specialized
                ? page.PrimaryAction
                : "Sélectionner un objet dans la vue 3D ou l’explorateur"
            : "Clic droit sur la sélection pour modifier ou trouver ses références");

        var search = new LineEdit
        {
            Text = _helpQueryText,
            PlaceholderText = "Propriété, dépendance, exemple…",
            ClearButtonEnabled = true,
        };
        StudioStyle.ApplyLineEdit(search);
        search.TextSubmitted += value =>
        {
            _helpQueryText = value;
            RefreshInspector();
        };
        _inspectorContent.AddChild(search);

        var source = _session.WorkspaceProfile.Help;
        var enabled = source.Enabled
            ? source
            : new EditorContextualHelpProfile(
                source.Id,
                EditorContextualHelpProfile.CurrentSchemaVersion,
                "studio:ux0-a-contextual-help",
                enabled: true,
                source.SourcedAssistantEnabled,
                source.Locale,
                source.ActiveTopicKey);
        var result = EditorContextualHelpService.Query(enabled, _helpQueryText, assisted: false);
        if (result.Topics.Count == 0)
        {
            AddInspectorValue("DOCUMENTATION", "Aucun sujet local ne correspond. Essayez « filtres », « placement » ou « zones ».");
        }
        foreach (var topic in result.Topics.Take(3))
        {
            AddInspectorValue(topic.Title.ToUpperInvariant(), topic.Description);
            if (topic.PropertyDescriptions.Count > 0)
            {
                AddInspectorValue("PROPRIÉTÉS", string.Join("\n", topic.PropertyDescriptions.Select(item => "• " + item)));
            }
            if (topic.ForgeScriptExamples.Count > 0)
            {
                AddInspectorValue("EXEMPLE FORGESCRIPT", string.Join("\n", topic.ForgeScriptExamples));
            }
            if (topic.Dependencies.Count > 0)
            {
                AddInspectorValue("DÉPENDANCES", string.Join(" · ", topic.Dependencies));
            }
        }
        AddInspectorValue("SOURCES LOCALES", result.Citations.Count == 0
            ? "Aucune source requise pour ce résumé local."
            : string.Join("\n", result.Citations));
        var close = StudioStyle.Button("Fermer l’aide", compact: true);
        close.Pressed += () =>
        {
            _contextualHelpVisible = false;
            RefreshInspector();
        };
        _inspectorContent.AddChild(close);
    }

    internal bool RunUx0ASmoke()
    {
        RebuildExplorer();
        var project = _session.Project!;
        var entity = _session.ActiveArea!.Entities.First();
        var blueprint = project.Blueprints.First(item => item.Id == entity.BlueprintId);
        _session.SelectEntity(entity.Id);
        WorldViewport.SetSelection(entity.Id);
        SyncExplorerSelection();

        var pageSearch = CreateGlobalSearchEntries("zones").Any(entry => entry.PageId == "zones");
        var actionSearch = CreateGlobalSearchEntries("afficher filtres").Any(entry => entry.ActionId == "filters");
        var objectSearch = CreateGlobalSearchEntries(blueprint.DisplayName).Any(entry =>
            entry.Document?.Kind is CreatorContentKind.Blueprint or CreatorContentKind.Entity);
        var resourceSearch = _session.Search(string.Empty, 1000).Any(result =>
            result.Document.Kind is CreatorContentKind.Script or CreatorContentKind.Asset);
        var references = _session.FindReferences(CreatorContentKind.Blueprint, blueprint.Id.ToString());
        var helpProfile = _session.WorkspaceProfile.Help;
        var smokeHelp = new EditorContextualHelpProfile(
            helpProfile.Id,
            EditorContextualHelpProfile.CurrentSchemaVersion,
            "studio:ux0-a-smoke",
            enabled: true,
            sourcedAssistantEnabled: true,
            helpProfile.Locale,
            "display.filters");
        var help = EditorContextualHelpService.Query(smokeHelp, "display.filters", assisted: true);

        var original = _session.WorkspaceProfile.DisplayFilter;
        var isolated = new EditorDisplayFilter(
            [blueprint.Kind],
            entity.Tags.Take(1).ToArray(),
            isolateSelection: true,
            ghostHidden: true,
            id: original.Id,
            provenance: "studio:ux0-a-smoke");
        var filterApply = _session.ConfigureDisplayFilter(isolated, persist: false);
        var visible = filterApply.Succeeded ? WorldViewport.SetDisplayFilter(isolated) : -1;
        var filterUndo = _session.UndoWorkspaceProfile(persist: false);
        WorldViewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);

        var valid = _explorerNodeCount > project.Areas.Count &&
            _explorerSelectionItems.ContainsKey(SelectionKey(CreatorSelectionKind.Entity, entity.Id.ToString())) &&
            pageSearch && actionSearch && objectSearch && resourceSearch &&
            references.Count > 0 && help.Diagnostics.Count == 0 && help.Citations.Count > 0 &&
            filterApply.Succeeded && visible == 1 && filterUndo.Succeeded &&
            _session.WorkspaceProfile.DisplayFilter.Id == original.Id;
        GD.Print(
            $"OPENRPG_FORGE_STUDIO_UX0A_{(valid ? "PASS" : "FAILED")} " +
            $"explorer_nodes={_explorerNodeCount} synced=true page_search={pageSearch.ToString().ToLowerInvariant()} " +
            $"object_search={objectSearch.ToString().ToLowerInvariant()} resource_search={resourceSearch.ToString().ToLowerInvariant()} " +
            $"action_search={actionSearch.ToString().ToLowerInvariant()} references={references.Count} " +
            $"help_sources={help.Citations.Count} filter_visible={visible} windows=1 persisted=false");
        return valid;
    }

    internal bool PrepareUx0AEvidence(string mode)
    {
        ShowNavigationMode(explorer: true);
        if (string.Equals(mode, "ux0-a-filter", StringComparison.OrdinalIgnoreCase))
        {
            OpenDisplayFilters();
            return _displayFilterOverlay.IsOpen;
        }
        if (string.Equals(mode, "ux0-a-help", StringComparison.OrdinalIgnoreCase))
        {
            ShowContextualHelp("display.filters");
            return _contextualHelpVisible;
        }
        if (string.Equals(mode, "ux0-a-references", StringComparison.OrdinalIgnoreCase))
        {
            var entity = _session.ActiveArea!.Entities.First();
            var blueprint = _session.Project!.Blueprints.First(item => item.Id == entity.BlueprintId);
            SelectEntity(entity.Id);
            ShowReferences(CreatorContentKind.Blueprint, blueprint.Id.ToString(), blueprint.DisplayName);
            return _referenceTargetKind is not null;
        }
        var query = _session.Project!.Blueprints.First().DisplayName;
        OpenGlobalSearch(query);
        return _globalSearchOverlay.IsOpen && _globalSearchOverlay.ResultCount > 0;
    }

    private static int MatchScore(string query, string candidate)
    {
        var normalizedQuery = NormalizeSearchText(query);
        if (normalizedQuery.Length == 0)
        {
            return 1;
        }
        var normalizedCandidate = NormalizeSearchText(candidate);
        var tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Any(token => !normalizedCandidate.Contains(token, StringComparison.Ordinal)))
        {
            return 0;
        }
        return normalizedCandidate.StartsWith(normalizedQuery, StringComparison.Ordinal) ? 80 :
            normalizedCandidate.Contains(normalizedQuery, StringComparison.Ordinal) ? 50 : 20;
    }

    private static string NormalizeSearchText(string value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    private static string SelectionKey(CreatorSelectionKind kind, string id) => $"{kind}:{id}";

    private static string ExplorerGroupingName(EditorExplorerGrouping grouping) => grouping switch
    {
        EditorExplorerGrouping.Type => "Par type",
        EditorExplorerGrouping.Folder => "Par dossier",
        EditorExplorerGrouping.Area => "Par zone",
        EditorExplorerGrouping.Tag => "Par tag",
        EditorExplorerGrouping.Dependency => "Par dépendance",
        EditorExplorerGrouping.ValidationStatus => "Par validation",
        _ => grouping.ToString(),
    };

    private static string ContentGroupName(CreatorContentKind kind) => kind switch
    {
        CreatorContentKind.Script or CreatorContentKind.Asset => "RESSOURCE",
        CreatorContentKind.Entity or CreatorContentKind.Blueprint or CreatorContentKind.Area => "OBJET",
        CreatorContentKind.Diagnostic => "DIAGNOSTIC",
        _ => "CONTENU",
    };

    private static string ContentKindName(CreatorContentKind kind) => kind switch
    {
        CreatorContentKind.Project => "Projet",
        CreatorContentKind.Module => "Module",
        CreatorContentKind.Area => "Zone",
        CreatorContentKind.Entity => "Objet placé",
        CreatorContentKind.Blueprint => "Blueprint",
        CreatorContentKind.Dialogue => "Dialogue",
        CreatorContentKind.Quest => "Quête",
        CreatorContentKind.Script => "ForgeScript",
        CreatorContentKind.Asset => "Asset",
        CreatorContentKind.Diagnostic => "Diagnostic",
        _ => kind.ToString(),
    };

    private static string ReferenceUsageName(CreatorReferenceUsageKind usage) => usage switch
    {
        CreatorReferenceUsageKind.ModuleEntryArea => "Zone d’entrée",
        CreatorReferenceUsageKind.EntityBlueprint => "Instance du blueprint",
        CreatorReferenceUsageKind.PortalSourceEntity => "Source de transition",
        CreatorReferenceUsageKind.PortalDestinationArea => "Destination de transition",
        CreatorReferenceUsageKind.PortalDestinationEntity => "Ancre de destination",
        CreatorReferenceUsageKind.DialogueEntryNode => "Entrée de dialogue",
        CreatorReferenceUsageKind.DialogueSpeakerBlueprint => "Locuteur",
        CreatorReferenceUsageKind.DialogueChoiceTargetNode => "Cible de choix",
        CreatorReferenceUsageKind.QuestInitialState => "État initial",
        CreatorReferenceUsageKind.QuestTransitionFromState => "Départ de transition",
        CreatorReferenceUsageKind.QuestTransitionToState => "Arrivée de transition",
        CreatorReferenceUsageKind.LocalizedTextKey => "Texte localisé",
        CreatorReferenceUsageKind.ProjectVariableKey => "Variable de projet",
        CreatorReferenceUsageKind.QuestStateValue => "Valeur d’état",
        CreatorReferenceUsageKind.ScriptPath => "Chemin de script",
        CreatorReferenceUsageKind.AssetPath => "Chemin d’asset",
        _ => usage.ToString(),
    };

    private static string HelpTopicForPage(string pageId) => pageId switch
    {
        "zones" or "workspace" => "workspace.overview",
        "placement" => "placement.repeated",
        "contextual" or "plans" => "assistant.sourced",
        _ => string.Empty,
    };
}
