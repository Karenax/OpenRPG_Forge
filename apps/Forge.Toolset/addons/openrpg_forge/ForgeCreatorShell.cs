#if TOOLS
using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Godot;

namespace Forge.Toolset.Editor;

internal sealed class ForgeCreatorShell
{
    private static readonly string[] WorkspaceNames =
    [
        "Monde",
        "Blueprints",
        "Dialogues",
        "Quêtes",
        "Scripts",
        "Assets",
        "Validation",
        "Playtests",
        "Assistant",
    ];

    private static readonly string[] AdvancedToolNames = ["Données", "Réseau", "Live Director"];

    private readonly Tree _projectTree;
    private readonly Label _explorerSummary;
    private readonly Label _selectionTitle;
    private readonly Label _selectionKind;
    private readonly Label _selectionSummary;
    private readonly Label _selectionPath;
    private readonly VBoxContainer _technicalDetails;
    private readonly CheckButton _technicalDetailsToggle;
    private readonly Button _selectionLock;
    private readonly ItemList _diagnosticList;
    private readonly Label _diagnosticSummary;
    private readonly LineEdit _diagnosticSearch;
    private readonly LineEdit _diagnosticCorrelation;
    private readonly OptionButton _diagnosticCategory;
    private readonly OptionButton _diagnosticSeverity;
    private readonly Button _diagnosticExport;
    private readonly OptionButton _quickFixPicker;
    private readonly Button _quickFixPreview;
    private readonly Button _quickFixApply;
    private readonly Dictionary<string, CreatorExplorerNode> _nodesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TreeItem> _treeItemsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _workspaceButtons = new(StringComparer.Ordinal);
    private MenuButton? _advancedToolsButton;
    private readonly HBoxContainer _areaTabBar;
    private readonly HSplitContainer _shellBody;
    private readonly PanelContainer _explorerPanel;
    private readonly TabContainer _rightTabs;
    private readonly PanelContainer _inspectorPanel;
    private readonly PanelContainer _diagnosticsPanel;
    private readonly Control _playtestConsole;
    private CreatorDiagnostic[] _diagnostics = [];
    private CreatorDiagnostic[] _visibleDiagnostics = [];
    private bool _suppressTreeSelection;

    internal int AreaTabCount => _areaTabBar.GetChildCount();

    internal event Action<CreatorExplorerNode>? ExplorerSelectionRequested;

    internal event Action<CreatorDiagnostic>? DiagnosticSelectionRequested;

    internal event Action<CreatorDiagnostic, DiagnosticQuickFixDefinition, bool>? DiagnosticQuickFixRequested;

    internal event Action<IReadOnlyList<CreatorDiagnostic>>? DiagnosticExportRequested;

    internal event Action<string>? WorkspaceRequested;

    internal event Action<bool>? SelectionLockRequested;

    internal event Action<AreaId>? AreaTabRequested;

    internal event Action<AreaId>? AreaTabCloseRequested;

    internal ForgeCreatorShell(Control mainPanel)
    {
        ArgumentNullException.ThrowIfNull(mainPanel);
        var content = mainPanel.GetNode<VBoxContainer>("Margin/Content");
        content.AddThemeConstantOverride("separation", 8);
        var margin = mainPanel.GetNode<MarginContainer>("Margin");
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 18);
        }

        content.GetNode<Label>("Subtitle").Visible = false;
        var workspaceBar = CreateWorkspaceBar();
        content.AddChild(workspaceBar);
        content.MoveChild(workspaceBar, 1);

        _areaTabBar = new HBoxContainer { Name = "AreaTabs" };
        _areaTabBar.AddThemeConstantOverride("separation", 4);
        content.AddChild(_areaTabBar);
        content.MoveChild(_areaTabBar, 2);

        _shellBody = new HSplitContainer
        {
            Name = "CreatorShell",
            CustomMinimumSize = new Vector2(0, 380),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddChild(_shellBody);

        _explorerPanel = CreatePanel("ProjectExplorerPanel", new Color("100d1b"), new Color("47365f"));
        _explorerPanel.CustomMinimumSize = new Vector2(228, 0);
        _shellBody.AddChild(_explorerPanel);
        var explorerContent = CreatePanelContent(_explorerPanel, 12);
        explorerContent.AddChild(CreateSectionTitle("PROJET FORGE"));
        _explorerSummary = new Label
        {
            Text = "Aucun projet ouvert",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _explorerSummary.AddThemeColorOverride("font_color", new Color("aa9bc7"));
        explorerContent.AddChild(_explorerSummary);
        _projectTree = new Tree
        {
            Name = "ProjectTree",
            Columns = 1,
            HideRoot = false,
            AllowReselect = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = Tree.SelectModeEnum.Single,
        };
        _projectTree.ItemSelected += HandleTreeSelection;
        explorerContent.AddChild(_projectTree);

        var worldWorkspace = new VBoxContainer
        {
            Name = "WorldWorkspace",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        worldWorkspace.AddThemeConstantOverride("separation", 8);
        _shellBody.AddChild(worldWorkspace);

        var editing = content.GetNode<VBoxContainer>("Editing");
        var playtestBar = content.GetNode<Control>("PlaytestBar");
        var preview = content.GetNode<Control>("PreviewHost");
        _playtestConsole = content.GetNode<Control>("PlaytestConsole");
        preview.CustomMinimumSize = new Vector2(520, 180);
        editing.Owner = null;
        preview.Owner = null;
        playtestBar.Owner = null;
        editing.Reparent(worldWorkspace, keepGlobalTransform: false);
        preview.Reparent(worldWorkspace, keepGlobalTransform: false);
        playtestBar.Reparent(worldWorkspace, keepGlobalTransform: false);
        PlaceableQuickEditorPanel.Attach(worldWorkspace, editing, preview);

        _rightTabs = new TabContainer
        {
            Name = "ContextTabs",
            CustomMinimumSize = new Vector2(258, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _shellBody.AddChild(_rightTabs);

        _inspectorPanel = CreatePanel("Inspecteur", new Color("100d1b"), new Color("47365f"));
        _rightTabs.AddChild(_inspectorPanel);
        var inspectorContent = CreatePanelContent(_inspectorPanel, 14);
        var inspectorHeader = new HBoxContainer();
        inspectorHeader.AddThemeConstantOverride("separation", 8);
        inspectorContent.AddChild(inspectorHeader);
        var inspectorTitle = CreateSectionTitle("SÉLECTION");
        inspectorTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inspectorHeader.AddChild(inspectorTitle);
        _selectionLock = new Button
        {
            CustomMinimumSize = new Vector2(90, 32),
            Text = "Déverrouillée",
            ToggleMode = true,
            TooltipText = "Verrouiller la sélection partagée",
        };
        _selectionLock.Pressed += () => SelectionLockRequested?.Invoke(_selectionLock.ButtonPressed);
        inspectorHeader.AddChild(_selectionLock);
        _selectionTitle = new Label
        {
            Text = "Aucune sélection",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _selectionTitle.AddThemeFontSizeOverride("font_size", 18);
        _selectionTitle.AddThemeColorOverride("font_color", new Color("ff9858"));
        inspectorContent.AddChild(_selectionTitle);
        _selectionKind = new Label { Text = "Choisissez un élément dans le projet" };
        _selectionKind.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        inspectorContent.AddChild(_selectionKind);
        _selectionSummary = new Label
        {
            Text = "La sélection est partagée entre les espaces.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _selectionSummary.AddThemeColorOverride("font_color", new Color("b8adc8"));
        inspectorContent.AddChild(_selectionSummary);
        _technicalDetailsToggle = new CheckButton
        {
            Text = "Détails techniques",
            TooltipText = "Afficher le chemin du document, son identifiant stable et sa version de schéma.",
        };
        inspectorContent.AddChild(_technicalDetailsToggle);
        _technicalDetails = new VBoxContainer { Visible = false };
        _technicalDetails.AddThemeConstantOverride("separation", 4);
        inspectorContent.AddChild(_technicalDetails);
        _technicalDetailsToggle.Toggled += visible => _technicalDetails.Visible = visible;
        _selectionPath = new Label
        {
            Text = "Le même contexte sera utilisé par tous les workspaces.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _selectionPath.AddThemeColorOverride("font_color", new Color("9f92b8"));
        _technicalDetails.AddChild(_selectionPath);

        _diagnosticsPanel = CreatePanel("Diagnostics", new Color("0d0b17"), new Color("47365f"));
        _rightTabs.AddChild(_diagnosticsPanel);
        var diagnosticsContent = CreatePanelContent(_diagnosticsPanel, 10);
        diagnosticsContent.AddChild(CreateSectionTitle("DIAGNOSTICS"));
        _diagnosticSummary = new Label
        {
            Text = "Validation en attente",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _diagnosticSummary.AddThemeColorOverride("font_color", new Color("aa9bc7"));
        diagnosticsContent.AddChild(_diagnosticSummary);
        _diagnosticSearch = new LineEdit
        {
            PlaceholderText = "Code, message, chemin…",
            TooltipText = "Filtrer les diagnostics affichés sans modifier le projet.",
        };
        _diagnosticSearch.TextChanged += _ => RefreshDiagnosticList();
        diagnosticsContent.AddChild(_diagnosticSearch);
        var diagnosticFilters = new HBoxContainer();
        diagnosticFilters.AddThemeConstantOverride("separation", 6);
        diagnosticsContent.AddChild(diagnosticFilters);
        _diagnosticCategory = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "Catégorie structurée du diagnostic.",
        };
        _diagnosticCategory.AddItem("Toutes catégories");
        foreach (var category in Enum.GetValues<CreatorDiagnosticCategory>())
            _diagnosticCategory.AddItem(FormatDiagnosticCategory(category));
        _diagnosticCategory.ItemSelected += _ => RefreshDiagnosticList();
        diagnosticFilters.AddChild(_diagnosticCategory);
        _diagnosticSeverity = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "Sévérité minimale affichée.",
        };
        _diagnosticSeverity.AddItem("Info+");
        _diagnosticSeverity.AddItem("Alerte+");
        _diagnosticSeverity.AddItem("Erreur");
        _diagnosticSeverity.ItemSelected += _ => RefreshDiagnosticList();
        diagnosticFilters.AddChild(_diagnosticSeverity);
        _diagnosticCorrelation = new LineEdit
        {
            PlaceholderText = "Corrélation (ex. revision:42)",
            TooltipText = "Limiter l’affichage à une transaction ou révision exacte.",
        };
        _diagnosticCorrelation.TextChanged += _ => RefreshDiagnosticList();
        diagnosticsContent.AddChild(_diagnosticCorrelation);
        _diagnosticList = new ItemList
        {
            Name = "DiagnosticList",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _diagnosticList.ItemSelected += HandleDiagnosticSelection;
        diagnosticsContent.AddChild(_diagnosticList);
        _diagnosticExport = new Button
        {
            Text = "Exporter diagnostics + logs",
            TooltipText = "Écrire un bundle ZIP déterministe dans .forge/diagnostics.",
        };
        _diagnosticExport.Pressed += () => DiagnosticExportRequested?.Invoke(_visibleDiagnostics);
        diagnosticsContent.AddChild(_diagnosticExport);
        diagnosticsContent.AddChild(CreateSectionTitle("CORRECTIF GUIDÉ"));
        _quickFixPicker = new OptionButton
        {
            Disabled = true,
            TooltipText = "Sélectionnez un diagnostic proposant un correctif.",
        };
        diagnosticsContent.AddChild(_quickFixPicker);
        var quickFixActions = new HBoxContainer();
        quickFixActions.AddThemeConstantOverride("separation", 6);
        diagnosticsContent.AddChild(quickFixActions);
        _quickFixPreview = new Button
        {
            Text = "Prévisualiser",
            Disabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _quickFixPreview.Pressed += () => InvokeSelectedQuickFix(dryRun: true);
        quickFixActions.AddChild(_quickFixPreview);
        _quickFixApply = new Button
        {
            Text = "Appliquer",
            Disabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _quickFixApply.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("d84f32"),
            BorderColor = new Color("ff9a73"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        });
        _quickFixApply.Pressed += () => InvokeSelectedQuickFix(dryRun: false);
        quickFixActions.AddChild(_quickFixApply);

        _playtestConsole.Name = "Playtests";
        _playtestConsole.CustomMinimumSize = new Vector2(0, 120);
        _playtestConsole.Owner = null;
        _playtestConsole.Reparent(_rightTabs, keepGlobalTransform: false);

        SetWorkspace("Monde");
        SetDiagnostics([]);
    }

    internal void ApplyWorkspaceProfile(EditorWorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var configuration = profile.Shell;
        ArgumentNullException.ThrowIfNull(configuration);
        var layout = configuration.Layout;
        _explorerPanel.Visible = layout.ExplorerVisible;
        _explorerPanel.CustomMinimumSize = new Vector2(layout.ExplorerWidth, 0);
        _inspectorPanel.Visible = layout.InspectorVisible;
        _diagnosticsPanel.Visible = layout.DiagnosticsVisible;
        _rightTabs.Visible = layout.InspectorVisible || layout.DiagnosticsVisible || _playtestConsole.Visible;
        _rightTabs.CustomMinimumSize = new Vector2(layout.ContextWidth, 0);
        _playtestConsole.CustomMinimumSize = new Vector2(0, layout.BottomHeight);
        if (_workspaceButtons.TryGetValue("Blueprints", out var palette)) palette.Visible = layout.PaletteVisible;
        if (_workspaceButtons.TryGetValue("Assistant", out var assistant)) assistant.Visible = layout.AssistantVisible;
        var visiblePanels = profile.RolePolicy.VisiblePanels;
        SetWorkspaceVisibility("Monde", visiblePanels.Contains(EditorPanel.Viewport));
        SetWorkspaceVisibility("Blueprints", visiblePanels.Contains(EditorPanel.Palette));
        SetWorkspaceVisibility("Dialogues", visiblePanels.Contains(EditorPanel.Dialogue));
        SetWorkspaceVisibility("Quêtes", visiblePanels.Contains(EditorPanel.Quest));
        SetWorkspaceVisibility("Scripts", visiblePanels.Contains(EditorPanel.Script));
        SetWorkspaceVisibility("Assets", visiblePanels.Contains(EditorPanel.Assets));
        SetWorkspaceVisibility("Validation",
            profile.Role != EditorRole.Beginner && visiblePanels.Contains(EditorPanel.Diagnostics));
        SetWorkspaceVisibility("Playtests", profile.RolePolicy.Allows(EditorPermission.RunPlaytests));
        SetWorkspaceVisibility("Assistant", visiblePanels.Contains(EditorPanel.Assistant));
        if (_advancedToolsButton is not null)
        {
            _advancedToolsButton.Visible = profile.Role != EditorRole.Beginner;
        }
        _projectTree.TooltipText = $"Explorateur virtuel · {configuration.ExplorerGrouping}";
    }

    internal void SetAreaTabs(ProjectDefinition project, EditorWorkspaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profile);
        foreach (var child in _areaTabBar.GetChildren())
        {
            _areaTabBar.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var areaId in profile.OpenAreaTabs)
        {
            var area = project.Areas.FirstOrDefault(candidate => candidate.Id == areaId);
            if (area is null) continue;
            var tab = new HBoxContainer();
            var activate = new Button
            {
                Text = $"{(profile.ActiveAreaId == area.Id ? "◆" : "◇")} {area.DisplayName}",
                ToggleMode = true,
                ButtonPressed = profile.ActiveAreaId == area.Id,
                TooltipText = $"Zone {area.Id} · {area.Entities.Count} entité(s)",
                CustomMinimumSize = new Vector2(150, 32),
            };
            activate.Pressed += () => AreaTabRequested?.Invoke(area.Id);
            tab.AddChild(activate);
            var close = new Button
            {
                Text = "×",
                TooltipText = $"Fermer l'onglet {area.DisplayName}",
                Disabled = profile.OpenAreaTabs.Count <= 1,
                CustomMinimumSize = new Vector2(32, 32),
            };
            close.Pressed += () => AreaTabCloseRequested?.Invoke(area.Id);
            tab.AddChild(close);
            _areaTabBar.AddChild(tab);
        }

        if (_areaTabBar.GetChildCount() == 0)
        {
            var empty = new Label { Text = "Aucun onglet de zone · ouvrez Zones & transitions" };
            empty.AddThemeColorOverride("font_color", new Color("9f92b8"));
            _areaTabBar.AddChild(empty);
        }
    }

    internal int WorkspaceCount => _workspaceButtons.Count;

    internal int AdvancedToolCount => AdvancedToolNames.Length;

    internal int VisibleWorkspaceCount => _workspaceButtons.Values.Count(button => button.Visible);

    internal bool TechnicalDetailsVisible => _technicalDetails.Visible;

    internal int ExplorerNodeCount => _nodesByKey.Count;

    internal int DiagnosticCount => _visibleDiagnostics.Length;

    internal void SetProject(CreatorExplorerNode? root)
    {
        _projectTree.Clear();
        _nodesByKey.Clear();
        _treeItemsByKey.Clear();
        if (root is null)
        {
            _explorerSummary.Text = "Aucun projet ouvert";
            return;
        }

        AddTreeNode(root, parent: null, depth: 0);
        _explorerSummary.Text = $"{CountByKind(root, CreatorExplorerNodeKind.Area)} zone(s) · " +
            $"{CountByKind(root, CreatorExplorerNodeKind.Entity)} entité(s)";
    }

    internal void SetSelection(IReadOnlyList<CreatorSelection> selections, bool locked)
    {
        ArgumentNullException.ThrowIfNull(selections);
        _selectionLock.ButtonPressed = locked;
        _selectionLock.Text = locked ? "Verrouillée" : "Déverrouillée";
        _selectionLock.TooltipText = locked ? "Déverrouiller la sélection partagée" : "Verrouiller la sélection partagée";
        if (selections.Count == 0)
        {
            _selectionTitle.Text = "Aucune sélection";
            _selectionKind.Text = "Choisissez un élément dans le projet";
            _selectionSummary.Text = "La sélection est partagée entre les espaces.";
            _selectionPath.Text = "Le même contexte sera utilisé par tous les workspaces.";
            return;
        }

        if (selections.Count > 1)
        {
            _selectionTitle.Text = $"{selections.Count} éléments sélectionnés";
            _selectionKind.Text = "SÉLECTION MULTIPLE";
            _selectionSummary.Text = "Les propriétés communes apparaîtront ici.";
            _selectionPath.Text = "Calcul des propriétés communes…";
            return;
        }

        var selection = selections[0];
        _selectionTitle.Text = selection.DisplayName;
        _selectionKind.Text = FormatSelectionKind(selection.Kind).ToUpperInvariant();
        _selectionSummary.Text = "Prêt à être modifié dans l’espace actif.";
        _selectionPath.Text = selection.DocumentPath;
        SelectTreeItem($"{selection.Kind.ToString().ToLowerInvariant()}:{selection.Id}");
    }

    internal void SetSelectionInspection(CreatorSelectionInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (inspection.Selection.Count < 2) return;
        var properties = inspection.CommonProperties.Select(property =>
            $"{property.DisplayName}: {(property.Mixed ? "valeurs mixtes" : property.Value ?? "—")}" +
            (property.Editable ? " · modifiable" : string.Empty));
        _selectionSummary.Text = string.Join("\n", properties);
        _selectionPath.Text = $"UUID {inspection.Id:D} · schéma v{inspection.SchemaVersion}";
    }

    internal void SetDiagnostics(IReadOnlyList<CreatorDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics.ToArray();
        RefreshDiagnosticList();
    }

    internal void SetDiagnosticExportStatus(string message, bool failed)
    {
        _diagnosticSummary.Text = message;
        _diagnosticSummary.AddThemeColorOverride(
            "font_color",
            failed ? new Color("ff8c8c") : new Color("83d6ae"));
    }

    private void RefreshDiagnosticList()
    {
        var category = _diagnosticCategory.Selected <= 0
            ? null
            : (CreatorDiagnosticCategory?)(_diagnosticCategory.Selected - 1);
        var severity = (CreatorDiagnosticSeverity)Math.Clamp(_diagnosticSeverity.Selected, 0, 2);
        _visibleDiagnostics = CreatorDiagnosticConsole.Filter(_diagnostics, new(
            _diagnosticSearch.Text,
            category,
            severity,
            _diagnosticCorrelation.Text)).ToArray();
        _diagnosticList.Clear();
        ResetQuickFixControls();
        if (_visibleDiagnostics.Length == 0)
        {
            _diagnosticSummary.Text = _diagnostics.Length == 0
                ? "✓ Projet prêt · aucun diagnostic"
                : $"Aucun résultat · {_diagnostics.Length} diagnostic(s) masqué(s)";
            _diagnosticSummary.AddThemeColorOverride("font_color", new Color("83d6ae"));
            _diagnosticList.AddItem(_diagnostics.Length == 0
                ? "Aucune erreur ou alerte pour la révision courante."
                : "Modifiez les filtres pour afficher les diagnostics disponibles.");
            _diagnosticList.SetItemSelectable(0, false);
            return;
        }

        var errorCount = _visibleDiagnostics.Count(diagnostic => diagnostic.Severity == CreatorDiagnosticSeverity.Error);
        var warningCount = _visibleDiagnostics.Count(diagnostic => diagnostic.Severity == CreatorDiagnosticSeverity.Warning);
        _diagnosticSummary.Text =
            $"{_visibleDiagnostics.Length}/{_diagnostics.Length} · {errorCount} erreur(s) · {warningCount} avertissement(s)";
        _diagnosticSummary.AddThemeColorOverride("font_color", errorCount > 0 ? new Color("ff8c8c") : new Color("ffc66d"));
        foreach (var diagnostic in _visibleDiagnostics)
        {
            var prefix = diagnostic.Severity switch
            {
                CreatorDiagnosticSeverity.Error => "✕",
                CreatorDiagnosticSeverity.Warning => "▲",
                _ => "●",
            };
            _diagnosticList.AddItem($"{prefix} {diagnostic.Code}\n{diagnostic.Message}");
            var index = _diagnosticList.ItemCount - 1;
            var subject = diagnostic.SubjectUuid?.ToString("D") ?? "document";
            var fixes = diagnostic.QuickFixes.Count == 0 ? "aucun correctif automatique" :
                $"{diagnostic.QuickFixes.Count} correctif(s) disponible(s)";
            _diagnosticList.SetItemTooltip(
                index,
                $"UUID · {diagnostic.Id}\nCatégorie · {diagnostic.Category}\nSévérité · {diagnostic.Severity}\nChemin · {diagnostic.DocumentPath}\n" +
                $"Sujet · {subject}\nPropriété · {diagnostic.Property}\nSource · {diagnostic.Source}\n{fixes}");
        }
    }

    internal bool SelectNodeForSmokeTest(string key)
    {
        if (!_treeItemsByKey.TryGetValue(key, out var item) || !_nodesByKey.TryGetValue(key, out var node))
        {
            return false;
        }

        item.Select(0);
        ExplorerSelectionRequested?.Invoke(node);
        return true;
    }

    internal bool SelectDiagnosticForSmokeTest(int index)
    {
        if (index < 0 || index >= _visibleDiagnostics.Length)
        {
            return false;
        }

        _diagnosticList.Select(index);
        RefreshQuickFixControls(index);
        DiagnosticSelectionRequested?.Invoke(_visibleDiagnostics[index]);
        return true;
    }

    internal bool InvokeSelectedQuickFixForSmokeTest(bool dryRun) => InvokeSelectedQuickFix(dryRun);

    internal bool FilterDiagnosticsForSmokeTest(
        CreatorDiagnosticCategory? category,
        CreatorDiagnosticSeverity severity,
        string text,
        string correlationId,
        int expectedCount)
    {
        _diagnosticCategory.Select(category is null ? 0 : (int)category + 1);
        _diagnosticSeverity.Select((int)severity);
        _diagnosticSearch.Text = text;
        _diagnosticCorrelation.Text = correlationId;
        RefreshDiagnosticList();
        return _visibleDiagnostics.Length == expectedCount;
    }

    private HBoxContainer CreateWorkspaceBar()
    {
        var bar = new HBoxContainer { Name = "WorkspaceBar" };
        bar.AddThemeConstantOverride("separation", 5);
        foreach (var name in WorkspaceNames)
        {
            var button = new Button
            {
                CustomMinimumSize = new Vector2(name == "Blueprints" ? 104 : 92, 34),
                Text = name,
                ToggleMode = true,
                TooltipText = $"Ouvrir l’espace {name}",
            };
            button.Pressed += () => WorkspaceRequested?.Invoke(name);
            bar.AddChild(button);
            _workspaceButtons.Add(name, button);
        }

        _advancedToolsButton = new MenuButton
        {
            Text = "Outils avancés",
            TooltipText = "Données, réseau et administration en direct",
            CustomMinimumSize = new Vector2(126, 34),
        };
        var advancedMenu = _advancedToolsButton.GetPopup();
        for (var index = 0; index < AdvancedToolNames.Length; index++)
        {
            advancedMenu.AddItem(AdvancedToolNames[index], index);
        }
        advancedMenu.IdPressed += id =>
        {
            if (id >= 0 && id < AdvancedToolNames.Length)
            {
                WorkspaceRequested?.Invoke(AdvancedToolNames[id]);
            }
        };
        bar.AddChild(_advancedToolsButton);

        return bar;
    }

    private void SetWorkspaceVisibility(string name, bool visible)
    {
        if (_workspaceButtons.TryGetValue(name, out var button))
        {
            button.Visible = visible;
        }
    }

    internal void SetWorkspace(string name)
    {
        foreach (var (workspace, button) in _workspaceButtons)
        {
            button.SetPressedNoSignal(string.Equals(workspace, name, StringComparison.Ordinal));
            button.AddThemeColorOverride(
                "font_color",
                string.Equals(workspace, name, StringComparison.Ordinal) ? new Color("ff9858") : new Color("c3b8d8"));
        }
    }

    private void AddTreeNode(CreatorExplorerNode node, TreeItem? parent, int depth)
    {
        var item = _projectTree.CreateItem(parent);
        item.SetText(0, $"{NodePrefix(node.Kind)} {node.DisplayName}");
        item.SetTooltipText(0, node.DocumentPath);
        item.SetMetadata(0, node.Key);
        item.Collapsed = depth > 1;
        _nodesByKey.Add(node.Key, node);
        _treeItemsByKey.Add(node.Key, item);
        foreach (var child in node.Children)
        {
            AddTreeNode(child, item, depth + 1);
        }
    }

    private void HandleTreeSelection()
    {
        if (_suppressTreeSelection)
        {
            return;
        }

        var key = _projectTree.GetSelected()?.GetMetadata(0).AsString();
        if (key is not null && _nodesByKey.TryGetValue(key, out var node))
        {
            ExplorerSelectionRequested?.Invoke(node);
        }
    }

    private void HandleDiagnosticSelection(long index)
    {
        if (index >= 0 && index < _visibleDiagnostics.Length)
        {
            RefreshQuickFixControls((int)index);
            DiagnosticSelectionRequested?.Invoke(_visibleDiagnostics[index]);
        }
    }

    private void RefreshQuickFixControls(int diagnosticIndex)
    {
        ResetQuickFixControls();
        if (diagnosticIndex < 0 || diagnosticIndex >= _visibleDiagnostics.Length)
        {
            return;
        }

        foreach (var fix in _visibleDiagnostics[diagnosticIndex].QuickFixes)
        {
            _quickFixPicker.AddItem(fix.Label);
            _quickFixPicker.SetItemTooltip(_quickFixPicker.ItemCount - 1, fix.Description);
        }

        var available = _quickFixPicker.ItemCount > 0;
        _quickFixPicker.Disabled = !available;
        _quickFixPreview.Disabled = !available;
        _quickFixApply.Disabled = !available;
        if (available)
        {
            _quickFixPicker.Selected = 0;
        }
    }

    private void ResetQuickFixControls()
    {
        _quickFixPicker.Clear();
        _quickFixPicker.Disabled = true;
        _quickFixPreview.Disabled = true;
        _quickFixApply.Disabled = true;
    }

    private bool InvokeSelectedQuickFix(bool dryRun)
    {
        var diagnosticIndex = _diagnosticList.GetSelectedItems().FirstOrDefault(-1);
        if (diagnosticIndex < 0 || diagnosticIndex >= _visibleDiagnostics.Length ||
            _quickFixPicker.Selected < 0 ||
            _quickFixPicker.Selected >= _visibleDiagnostics[diagnosticIndex].QuickFixes.Count)
        {
            return false;
        }

        DiagnosticQuickFixRequested?.Invoke(
            _visibleDiagnostics[diagnosticIndex],
            _visibleDiagnostics[diagnosticIndex].QuickFixes[_quickFixPicker.Selected],
            dryRun);
        return true;
    }

    private static string FormatDiagnosticCategory(CreatorDiagnosticCategory category) => category switch
    {
        CreatorDiagnosticCategory.Schema => "Schéma",
        CreatorDiagnosticCategory.Reference => "Références",
        CreatorDiagnosticCategory.Graph => "Graphes",
        CreatorDiagnosticCategory.Geometry => "Géométrie",
        CreatorDiagnosticCategory.Navigation => "Navigation",
        CreatorDiagnosticCategory.License => "Licences",
        _ => "Runtime",
    };

    private void SelectTreeItem(string key)
    {
        if (!_treeItemsByKey.TryGetValue(key, out var item))
        {
            return;
        }

        _suppressTreeSelection = true;
        item.Select(0);
        _projectTree.ScrollToItem(item, centerOnItem: true);
        _suppressTreeSelection = false;
    }

    private static PanelContainer CreatePanel(string name, Color background, Color border)
    {
        var panel = new PanelContainer { Name = name };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        return panel;
    }

    private static VBoxContainer CreatePanelContent(PanelContainer panel, int marginSize)
    {
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, marginSize);
        }
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        return content;
    }

    private static Label CreateSectionTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        return label;
    }

    private static int CountByKind(CreatorExplorerNode root, CreatorExplorerNodeKind kind) =>
        (root.Kind == kind ? 1 : 0) + root.Children.Sum(child => CountByKind(child, kind));

    private static string ShortId(string id) => id.Length > 8 ? id[..8] : id;

    private static string NodePrefix(CreatorExplorerNodeKind kind) => kind switch
    {
        CreatorExplorerNodeKind.Project => "◆",
        CreatorExplorerNodeKind.Folder => "▸",
        CreatorExplorerNodeKind.Area => "◇",
        CreatorExplorerNodeKind.Entity => "•",
        CreatorExplorerNodeKind.Blueprint => "⬡",
        CreatorExplorerNodeKind.Dialogue => "◌",
        CreatorExplorerNodeKind.Quest => "✓",
        _ => "•",
    };

    private static string FormatSelectionKind(CreatorSelectionKind kind) => kind switch
    {
        CreatorSelectionKind.Project => "Projet",
        CreatorSelectionKind.Area => "Zone",
        CreatorSelectionKind.Entity => "Entité",
        CreatorSelectionKind.Blueprint => "Blueprint",
        CreatorSelectionKind.Dialogue => "Dialogue",
        CreatorSelectionKind.Quest => "Quête",
        CreatorSelectionKind.Diagnostic => "Diagnostic",
        _ => kind.ToString(),
    };
}
#endif
