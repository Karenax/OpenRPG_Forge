#if TOOLS
using Forge.Domain.Build;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class BuildPipelineWindow : Window
{
    private Func<ProjectSession?>? _getSession;
    private Action<Window>? _brandWindow;
    private Label? _status;
    private Label? _graphSummary;
    private Label? _impactSummary;
    private Label? _cacheSummary;
    private ItemList? _resources;
    private ItemList? _dependencies;

    public int ResourceCount { get; private set; }

    public int ScriptCount { get; private set; }

    public int AutomaticDependencyCount { get; private set; }

    public void Initialize(Func<ProjectSession?> getSession, Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Scripts et build incrémental · OpenRPG Forge";
        MinSize = new Vector2I(1120, 720);
        Size = new Vector2I(1120, 720);
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
        PopupCentered(new Vector2I(1120, 720));
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var session = _getSession?.Invoke();
        _resources?.Clear();
        _dependencies?.Clear();
        ResourceCount = 0;
        ScriptCount = 0;
        AutomaticDependencyCount = 0;
        if (session is null)
        {
            SetStatus("Aucun projet ouvert.", true);
            return;
        }

        try
        {
            var build = WorkspaceBuildGraphBuilder.Build(
                session.RootDirectory,
                session.Project,
                session.ProjectFilePath);
            var previous = WorkspaceBuildStateStore.TryLoad(session.RootDirectory);
            var plan = WorkspaceIncrementalBuildPlanner.Plan(previous, build.Graph);
            ResourceCount = build.Graph.Nodes.Count;
            ScriptCount = build.Graph.Nodes.Count(node => node.Kind == BuildResourceKind.Script);
            AutomaticDependencyCount = build.AutomaticDependencies.Count;
            foreach (var node in build.Graph.Nodes)
            {
                var state = plan.RebuiltResources.Any(candidate => candidate.Id == node.Id)
                    ? "REBUILD"
                    : node.Kind == BuildResourceKind.ExternalPackage ? "EXTERNE" : "CACHE";
                _resources!.AddItem($"{state,-7}  {FormatKind(node.Kind),-10}  {node.Path}\n{node.ContentSha256[..12]} · {node.LicenseSpdx}");
                _resources.SetItemTooltip(
                    _resources.ItemCount - 1,
                    $"UUID {node.Id}\nProvenance {node.Provenance}\n{node.Dependencies.Count} dépendance(s)");
            }

            foreach (var dependency in build.AutomaticDependencies)
            {
                _dependencies!.AddItem($"PACK  {dependency}");
            }

            if (build.AutomaticDependencies.Count == 0)
            {
                _dependencies!.AddItem("Aucun pack externe requis par le contenu utilisé.");
                _dependencies.SetItemSelectable(0, false);
            }

            _graphSummary!.Text =
                $"Graphe v{build.Graph.SchemaVersion} · {build.Graph.Nodes.Count} ressources · " +
                $"{build.Graph.Nodes.Sum(node => node.Dependencies.Count)} liens";
            _impactSummary!.Text =
                $"Prochain export · {plan.RebuiltResources.Count} à recalculer · " +
                $"{plan.ReusedResources.Count} réutilisées · {plan.RemovedPaths.Count} retirées";
            _cacheSummary!.Text =
                $"ForgeScript · {ScriptCount} source(s) · clé SHA-256(source + API + compilateur) · " +
                "bytecode contrôlé avant réutilisation";
            SetStatus("✓ Analyse déterministe prête · le cache reste jetable et hors du module", false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            SetStatus($"Analyse impossible : {exception.Message}", true);
        }
    }

    public bool AnalyzeForSmokeTest()
    {
        Refresh();
        return ResourceCount >= 5 && ScriptCount == 2 && _status?.Text.StartsWith("✓", StringComparison.Ordinal) == true;
    }

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
        var title = new Label { Text = "SCRIPTS & BUILD INCRÉMENTAL", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Comprendre l’impact avant l’export, sans éditer les fichiers générés.",
        };
        subtitle.AddThemeColorOverride("font_color", new Color("c3b8d8"));
        root.AddChild(subtitle);

        var metrics = new HBoxContainer();
        metrics.AddThemeConstantOverride("separation", 12);
        root.AddChild(metrics);
        _graphSummary = AddMetric(metrics, "GRAPHE", "Analyse en attente");
        _impactSummary = AddMetric(metrics, "IMPACT", "Analyse en attente");
        _cacheSummary = AddMetric(metrics, "CACHE FORGESCRIPT", "Analyse en attente");

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);
        var resourcesCard = AddCard(body, expand: true);
        resourcesCard.AddChild(Section("RESSOURCES ET IMPACT"));
        _resources = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        resourcesCard.AddChild(_resources);
        var dependenciesCard = AddCard(body, new Vector2(330, 0));
        dependenciesCard.AddChild(Section("DÉPENDANCES AUTOMATIQUES"));
        var dependencyHelp = new Label
        {
            Text = "Seuls les packs réellement référencés rejoignent le manifeste `.orpmod`.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        dependencyHelp.AddThemeColorOverride("font_color", new Color("aa9bc7"));
        dependenciesCard.AddChild(dependencyHelp);
        _dependencies = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        dependenciesCard.AddChild(_dependencies);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        _status = new Label
        {
            Text = "Analyse en attente",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        footer.AddChild(_status);
        var refresh = new Button { Text = "Actualiser", CustomMinimumSize = new Vector2(130, 38) };
        refresh.Pressed += Refresh;
        footer.AddChild(refresh);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 38) };
        close.Pressed += Hide;
        footer.AddChild(close);
        CloseRequested += Hide;
    }

    private static Label AddMetric(Container parent, string title, string value)
    {
        var card = AddCard(parent, expand: true);
        card.AddChild(Section(title));
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        card.AddChild(label);
        return label;
    }

    private static VBoxContainer AddCard(Container parent, Vector2? minimum = null, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimum ?? new Vector2(0, 0),
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

    private static StyleBoxFlat Card(string background, string border, int width, int radius)
    {
        var style = new StyleBoxFlat
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
        return style;
    }

    private static string FormatKind(BuildResourceKind kind) => kind switch
    {
        BuildResourceKind.ProjectDocument => "PROJET",
        BuildResourceKind.AreaDocument => "ZONE",
        BuildResourceKind.Script => "SCRIPT",
        BuildResourceKind.Asset => "ASSET",
        BuildResourceKind.ExternalPackage => "PACK",
        BuildResourceKind.ModulePackage => "MODULE",
        _ => kind.ToString().ToUpperInvariant(),
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
