#if TOOLS
using Forge.Application.Creator;
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

    private readonly Tree _projectTree;
    private readonly Label _explorerSummary;
    private readonly Label _selectionTitle;
    private readonly Label _selectionKind;
    private readonly Label _selectionPath;
    private readonly Button _selectionLock;
    private readonly ItemList _diagnosticList;
    private readonly Label _diagnosticSummary;
    private readonly OptionButton _quickFixPicker;
    private readonly Button _quickFixPreview;
    private readonly Button _quickFixApply;
    private readonly Dictionary<string, CreatorExplorerNode> _nodesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TreeItem> _treeItemsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _workspaceButtons = new(StringComparer.Ordinal);
    private CreatorDiagnostic[] _diagnostics = [];
    private bool _suppressTreeSelection;

    internal event Action<CreatorExplorerNode>? ExplorerSelectionRequested;

    internal event Action<CreatorDiagnostic>? DiagnosticSelectionRequested;

    internal event Action<CreatorDiagnostic, DiagnosticQuickFixDefinition, bool>? DiagnosticQuickFixRequested;

    internal event Action<string>? WorkspaceRequested;

    internal event Action<bool>? SelectionLockRequested;

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

        var shellBody = new HSplitContainer
        {
            Name = "CreatorShell",
            CustomMinimumSize = new Vector2(0, 380),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddChild(shellBody);

        var explorerPanel = CreatePanel("ProjectExplorerPanel", new Color("100d1b"), new Color("47365f"));
        explorerPanel.CustomMinimumSize = new Vector2(228, 0);
        shellBody.AddChild(explorerPanel);
        var explorerContent = CreatePanelContent(explorerPanel, 12);
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
        shellBody.AddChild(worldWorkspace);

        var editing = content.GetNode<Control>("Editing");
        var playtestBar = content.GetNode<Control>("PlaytestBar");
        var preview = content.GetNode<Control>("PreviewHost");
        var playtestConsole = content.GetNode<Control>("PlaytestConsole");
        preview.CustomMinimumSize = new Vector2(520, 180);
        editing.Reparent(worldWorkspace, keepGlobalTransform: false);
        preview.Reparent(worldWorkspace, keepGlobalTransform: false);
        playtestBar.Reparent(worldWorkspace, keepGlobalTransform: false);

        var rightTabs = new TabContainer
        {
            Name = "ContextTabs",
            CustomMinimumSize = new Vector2(258, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        shellBody.AddChild(rightTabs);

        var inspectorPanel = CreatePanel("Inspecteur", new Color("100d1b"), new Color("47365f"));
        rightTabs.AddChild(inspectorPanel);
        var inspectorContent = CreatePanelContent(inspectorPanel, 14);
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
        _selectionPath = new Label
        {
            Text = "Le même contexte sera utilisé par tous les workspaces.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _selectionPath.AddThemeColorOverride("font_color", new Color("9f92b8"));
        inspectorContent.AddChild(_selectionPath);

        var diagnosticsPanel = CreatePanel("Diagnostics", new Color("0d0b17"), new Color("47365f"));
        rightTabs.AddChild(diagnosticsPanel);
        var diagnosticsContent = CreatePanelContent(diagnosticsPanel, 10);
        diagnosticsContent.AddChild(CreateSectionTitle("DIAGNOSTICS"));
        _diagnosticSummary = new Label
        {
            Text = "Validation en attente",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _diagnosticSummary.AddThemeColorOverride("font_color", new Color("aa9bc7"));
        diagnosticsContent.AddChild(_diagnosticSummary);
        _diagnosticList = new ItemList
        {
            Name = "DiagnosticList",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _diagnosticList.ItemSelected += HandleDiagnosticSelection;
        diagnosticsContent.AddChild(_diagnosticList);
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

        playtestConsole.Name = "Playtests";
        playtestConsole.CustomMinimumSize = new Vector2(0, 120);
        playtestConsole.Reparent(rightTabs, keepGlobalTransform: false);

        CompactEditingControls(mainPanel);
        SetWorkspace("Monde");
        SetDiagnostics([]);
    }

    internal int WorkspaceCount => _workspaceButtons.Count;

    internal int ExplorerNodeCount => _nodesByKey.Count;

    internal int DiagnosticCount => _diagnostics.Length;

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
            _selectionPath.Text = "Le même contexte sera utilisé par tous les workspaces.";
            return;
        }

        if (selections.Count > 1)
        {
            _selectionTitle.Text = $"{selections.Count} éléments sélectionnés";
            _selectionKind.Text = "SÉLECTION MULTIPLE";
            _selectionPath.Text = "L’inspection commune sera enrichie dans un prochain incrément.";
            return;
        }

        var selection = selections[0];
        _selectionTitle.Text = selection.DisplayName;
        _selectionKind.Text = $"{FormatSelectionKind(selection.Kind).ToUpperInvariant()} · {ShortId(selection.Id)}";
        _selectionPath.Text = selection.DocumentPath;
        SelectTreeItem($"{selection.Kind.ToString().ToLowerInvariant()}:{selection.Id}");
    }

    internal void SetDiagnostics(IReadOnlyList<CreatorDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics.ToArray();
        _diagnosticList.Clear();
        ResetQuickFixControls();
        if (_diagnostics.Length == 0)
        {
            _diagnosticSummary.Text = "✓ Projet prêt · aucun diagnostic";
            _diagnosticSummary.AddThemeColorOverride("font_color", new Color("83d6ae"));
            _diagnosticList.AddItem("Aucune erreur ou alerte pour la révision courante.");
            _diagnosticList.SetItemSelectable(0, false);
            return;
        }

        var errorCount = _diagnostics.Count(diagnostic => diagnostic.Severity == CreatorDiagnosticSeverity.Error);
        var warningCount = _diagnostics.Count(diagnostic => diagnostic.Severity == CreatorDiagnosticSeverity.Warning);
        _diagnosticSummary.Text = $"{errorCount} erreur(s) · {warningCount} avertissement(s)";
        _diagnosticSummary.AddThemeColorOverride("font_color", errorCount > 0 ? new Color("ff8c8c") : new Color("ffc66d"));
        foreach (var diagnostic in _diagnostics)
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
                $"UUID · {diagnostic.Id}\nSévérité · {diagnostic.Severity}\nChemin · {diagnostic.DocumentPath}\n" +
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
        if (index < 0 || index >= _diagnostics.Length)
        {
            return false;
        }

        _diagnosticList.Select(index);
        RefreshQuickFixControls(index);
        DiagnosticSelectionRequested?.Invoke(_diagnostics[index]);
        return true;
    }

    internal bool InvokeSelectedQuickFixForSmokeTest(bool dryRun) => InvokeSelectedQuickFix(dryRun);

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

        return bar;
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
        if (index >= 0 && index < _diagnostics.Length)
        {
            RefreshQuickFixControls((int)index);
            DiagnosticSelectionRequested?.Invoke(_diagnostics[index]);
        }
    }

    private void RefreshQuickFixControls(int diagnosticIndex)
    {
        ResetQuickFixControls();
        if (diagnosticIndex < 0 || diagnosticIndex >= _diagnostics.Length)
        {
            return;
        }

        foreach (var fix in _diagnostics[diagnosticIndex].QuickFixes)
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
        if (diagnosticIndex < 0 || diagnosticIndex >= _diagnostics.Length ||
            _quickFixPicker.Selected < 0 ||
            _quickFixPicker.Selected >= _diagnostics[diagnosticIndex].QuickFixes.Count)
        {
            return false;
        }

        DiagnosticQuickFixRequested?.Invoke(
            _diagnostics[diagnosticIndex],
            _diagnostics[diagnosticIndex].QuickFixes[_quickFixPicker.Selected],
            dryRun);
        return true;
    }

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

    private static void CompactEditingControls(Control mainPanel)
    {
        mainPanel.GetNode<OptionButton>("Margin/Content/CreatorShell/WorldWorkspace/Editing/PaletteRow/BlueprintPalette")
            .CustomMinimumSize = new Vector2(180, 38);
        mainPanel.GetNode<OptionButton>("Margin/Content/CreatorShell/WorldWorkspace/Editing/EntityRow/EntityPicker")
            .CustomMinimumSize = new Vector2(190, 38);
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
