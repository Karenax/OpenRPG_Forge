#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Assistant;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedAreaPlanWindow : Window
{
    private Func<ProjectState?>? _getProject;
    private Func<AssistedAreaPlanState?>? _getPlans;
    private Func<LoreIndexState?>? _getLore;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<AssistedAreaPlanCatalog, Guid, Guid, AssistantPermissionLevel, bool, bool>? _apply;
    private Func<Guid, Guid, Guid, bool, bool>? _build;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Action<Window>? _brandWindow;
    private readonly System.Net.Http.HttpClient _httpClient = new(new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private AssistedAreaPlanDefinition? _proposal;
    private AssistedAreaPlanCatalog? _approvedCatalog;
    private Guid _approvedTransactionId;
    private Guid _approvedPreviewPlanId;
    private Guid _selectedPlanId;
    private Guid _buildTransactionId;
    private Guid _buildPreviewPlanId;
    private Guid[] _areaIds = [];
    private Guid[] _blueprintIds = [];

    private OptionButton? _area;
    private OptionButton? _archetype;
    private OptionButton? _kit;
    private LineEdit? _title;
    private SpinBox? _seed;
    private SpinBox? _width;
    private SpinBox? _depth;
    private SpinBox? _nodes;
    private TextEdit? _constraints;
    private TextEdit? _objectives;
    private CheckButton? _approve;
    private ItemList? _savedPlans;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _generateButton;
    private Button? _applyButton;
    private Button? _previewBuildButton;
    private Button? _buildButton;

    public int PlanCount => _getPlans?.Invoke()?.Catalog.Plans.Count ?? 0;
    public bool LastGeometryApplied { get; private set; }
    public decimal LastEstimatedCostUsd { get; private set; }
    public int LastBuildEntityCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> getProject,
        Func<AssistedAreaPlanState?> getPlans,
        Func<LoreIndexState?> getLore,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<AssistedAreaPlanCatalog, Guid, Guid, AssistantPermissionLevel, bool, bool> apply,
        Func<Guid, Guid, Guid, bool, bool> build,
        Func<bool> undo,
        Func<bool> redo,
        Action<Window> brandWindow)
    {
        _getProject = getProject;
        _getPlans = getPlans;
        _getLore = getLore;
        _getAssistant = getAssistant;
        _apply = apply;
        _build = build;
        _undo = undo;
        _redo = redo;
        _brandWindow = brandWindow;
        Title = "Plans de zone assistés · OpenRPG Forge";
        MinSize = new Vector2I(1260, 800);
        Size = MinSize;
        Transient = true;
        Exclusive = false;
        Unresizable = true;
        Visible = false;
        BuildUi();
        CloseRequested += Hide;
    }

    public void Open()
    {
        Refresh();
        Exclusive = true;
        PopupCentered(Size);
        _brandWindow?.Invoke(this);
    }

    public void Refresh()
    {
        var state = _getProject?.Invoke();
        var plans = _getPlans?.Invoke();
        if (state is null || plans is null)
        {
            SetStatus("Aucun projet Forge ouvert.", true);
            return;
        }
        _area!.Clear();
        _areaIds = state.Project.Areas.Select(item => item.Id.Value).ToArray();
        foreach (var item in state.Project.Areas) _area.AddItem($"{item.DisplayName} · {item.Dimensions.Width} × {item.Dimensions.Depth} m");
        _kit!.Clear();
        _blueprintIds = state.Project.Blueprints.Select(item => item.Id.Value).ToArray();
        foreach (var item in state.Project.Blueprints) _kit.AddItem($"{item.DisplayName} · {item.Kind}");
        _savedPlans!.Clear();
        foreach (var item in plans.Catalog.Plans)
            _savedPlans.AddItem($"{(item.ApprovedForConstruction ? "✓" : "○")} {item.Title} · {item.Archetype} · {item.Nodes.Count} nœuds");
        _selectedPlanId = plans.Catalog.Plans.FirstOrDefault()?.Id ?? Guid.Empty;
        if (_savedPlans.ItemCount > 0) _savedPlans.Select(0);
        _proposal = null;
        _approvedCatalog = null;
        _applyButton!.Disabled = true;
        LastGeometryApplied = false;
        LastEstimatedCostUsd = 0;
        LastBuildEntityCount = 0;
        if (_areaIds.Length > 0)
        {
            var selected = state.Project.Areas[0];
            _width!.Value = Math.Min(96, selected.Dimensions.Width);
            _depth!.Value = Math.Min(96, selected.Dimensions.Depth);
        }
        _title!.Text = "Crypte assistée";
        _seed!.Value = 20260805;
        _nodes!.Value = 8;
        _constraints!.Text = "Conserver un chemin navigable entre entrée et sortie.\nPréserver une boucle optionnelle sans bloquer l’objectif.";
        _objectives!.Text = "Découvrir le lieu central.\nAtteindre l’objectif puis revenir vers la sortie.";
        _approve!.ButtonPressed = false;
        _preview!.Text = "[color=#aa94c9]Aucune géométrie ne sera créée. Le plan topologique doit être prévisualisé puis validé.[/color]";
        SetStatus($"{plans.Catalog.Plans.Count} plan(s) canonique(s) · révision {plans.Revision}.", false);
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        _archetype!.Selected = 0;
        _title!.Text = "Crypte G12-C";
        _approve!.ButtonPressed = false;
        return _areaIds.Length > 0 && _blueprintIds.Length > 0;
    }

    public bool PreviewForSmokeTest() => GenerateAsync().GetAwaiter().GetResult();

    public bool ApplyForSmokeTest()
    {
        Apply();
        return _approvedCatalog is null;
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        if (!ConfigureForSmokeTest()) return false;
        _approve!.ButtonPressed = true;
        return true;
    }

    public bool PreviewBuildForSmokeTest() => PreviewBuild();

    public bool SelectApprovedPlanForSmokeTest()
    {
        var plans = _getPlans?.Invoke()?.Catalog.Plans;
        if (plans is null) return false;
        var index = plans.ToList().FindLastIndex(item => item.ApprovedForConstruction);
        if (index < 0) return false;
        _selectedPlanId = plans[index].Id;
        _savedPlans?.Select(index);
        _buildButton!.Disabled = true;
        return true;
    }

    public bool ApplyBuildForSmokeTest()
    {
        ApplyBuild();
        return _buildPreviewPlanId == Guid.Empty;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var header = new Label { Text = "PLAN DE ZONE ASSISTÉ", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label { Text = "Crypte ou village · topologie, kits, contraintes et objectifs avant toute géométrie." });

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);
        var brief = AddCard(body, 350);
        brief.AddChild(Section("BRIEF & PÉRIMÈTRE"));
        _area = new OptionButton();
        brief.AddChild(Field("Zone cible", _area));
        _archetype = new OptionButton();
        _archetype.AddItem("Crypte");
        _archetype.AddItem("Village");
        brief.AddChild(Field("Archétype", _archetype));
        _title = AddLine(brief, "Titre du plan", "Crypte du Serment");
        _kit = new OptionButton();
        brief.AddChild(Field("Kit / blueprint", _kit));
        var dimensions = new HBoxContainer();
        _width = Spin(8, 2048, 64);
        _depth = Spin(8, 2048, 64);
        _nodes = Spin(4, 24, 8);
        dimensions.AddChild(Field("Largeur", _width));
        dimensions.AddChild(Field("Profondeur", _depth));
        dimensions.AddChild(Field("Nœuds", _nodes));
        brief.AddChild(dimensions);
        _seed = Spin(0, int.MaxValue, 20260805);
        brief.AddChild(Field("Seed déterministe", _seed));
        brief.AddChild(FieldLabel("CONTRAINTES · UNE PAR LIGNE"));
        _constraints = new TextEdit { CustomMinimumSize = new Vector2(0, 105), WrapMode = TextEdit.LineWrappingMode.Boundary };
        brief.AddChild(_constraints);
        brief.AddChild(FieldLabel("OBJECTIFS · UN PAR LIGNE"));
        _objectives = new TextEdit { CustomMinimumSize = new Vector2(0, 105), WrapMode = TextEdit.LineWrappingMode.Boundary };
        brief.AddChild(_objectives);

        var topology = AddCard(body, 510, true);
        topology.AddChild(Section("TOPOLOGIE & DIFF SÉMANTIQUE"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 500),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8));
        topology.AddChild(_preview);

        var governance = AddCard(body, 300);
        governance.AddChild(Section("VALIDATION HUMAINE"));
        governance.AddChild(new Label
        {
            Text = "Le provider ne modifie jamais la carte. Enregistrer conserve seulement le document de plan.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _approve = new CheckButton { Text = "Approuver pour la future construction" };
        governance.AddChild(_approve);
        governance.AddChild(new HSeparator());
        governance.AddChild(Section("PLANS ENREGISTRÉS"));
        _savedPlans = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 330) };
        _savedPlans.ItemSelected += index =>
        {
            var plans = _getPlans?.Invoke()?.Catalog.Plans;
            if (plans is not null && index >= 0 && index < plans.Count) _selectedPlanId = plans[(int)index].Id;
            _buildButton!.Disabled = true;
        };
        governance.AddChild(_savedPlans);
        _previewBuildButton = new Button { Text = "Prévisualiser construction" };
        _previewBuildButton.Pressed += () => PreviewBuild();
        governance.AddChild(_previewBuildButton);
        _buildButton = new Button { Text = "Construire le plan", Disabled = true };
        _buildButton.Pressed += ApplyBuild;
        governance.AddChild(_buildButton);
        var history = new HBoxContainer();
        var undo = new Button { Text = "Annuler" };
        undo.Pressed += () => { if (_undo?.Invoke() == true) Refresh(); };
        history.AddChild(undo);
        var redo = new Button { Text = "Rétablir" };
        redo.Pressed += () => { if (_redo?.Invoke() == true) Refresh(); };
        history.AddChild(redo);
        governance.AddChild(history);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        _generateButton = new Button { Text = "Prévisualiser le plan" };
        _generateButton.Pressed += async () => await GenerateAsync();
        footer.AddChild(_generateButton);
        _applyButton = new Button { Text = "Enregistrer le plan", Disabled = true };
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
    }

    private async Task<bool> GenerateAsync()
    {
        var project = _getProject?.Invoke();
        var plans = _getPlans?.Invoke();
        var lore = _getLore?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (project is null || plans is null || lore is null || assistant is null ||
            _areaIds.Length == 0 || _blueprintIds.Length == 0)
        {
            SetStatus("Zone, kit ou configuration Assistant indisponible.", true);
            return false;
        }
        _generateButton!.Disabled = true;
        _applyButton!.Disabled = true;
        try
        {
            var planId = Guid.NewGuid();
            var brief = new AssistedAreaPlanBrief(
                planId,
                _areaIds[Math.Clamp(_area!.Selected, 0, _areaIds.Length - 1)],
                _title!.Text,
                _archetype!.Selected == 0 ? AssistedAreaArchetype.Crypt : AssistedAreaArchetype.Village,
                checked((int)_seed!.Value),
                (float)_width!.Value,
                (float)_depth!.Value,
                checked((int)_nodes!.Value),
                [_blueprintIds[Math.Clamp(_kit!.Selected, 0, _blueprintIds.Length - 1)]],
                Lines(_constraints!.Text),
                Lines(_objectives!.Text),
                [$"project:{project.Project.Id}"],
                "toolset:g12-c");
            var registry = AssistantProviderRegistry.CreateWithLocalTransports(_httpClient, requestTimeout: TimeSpan.FromSeconds(30));
            var providerDefinition = assistant.Profile.ActiveProvider;
            var proposal = await AssistedAreaPlanProposalService.ProposeAsync(
                brief, project.Project, lore.Index, registry.Resolve(providerDefinition), providerDefinition,
                assistant.Profile.NetworkAllowed, assistant.Profile.ShareProjectContent);
            if (!proposal.Succeeded || proposal.Plan is null)
            {
                SetStatus(proposal.Diagnostics.FirstOrDefault()?.Message ?? "Plan invalide.", true);
                return false;
            }
            _proposal = proposal.Plan.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-c" : proposal.Plan.Provenance);
            var catalogPlans = plans.Catalog.Plans.Where(item => item.Id != _proposal.Id).Append(_proposal).ToArray();
            _approvedCatalog = new AssistedAreaPlanCatalog(plans.Catalog.Id, 1, catalogPlans, "toolset:g12-c");
            _approvedTransactionId = Guid.NewGuid();
            _approvedPreviewPlanId = Guid.NewGuid();
            var permission = _proposal.ApprovedForConstruction
                ? AssistantPermissionLevel.ApplyApprovedPlan
                : AssistantPermissionLevel.Configure;
            if (_apply?.Invoke(_approvedCatalog, _approvedTransactionId, _approvedPreviewPlanId, permission, true) != true)
            {
                _approvedCatalog = null;
                SetStatus("Le dry-run du plan a été refusé.", true);
                return false;
            }
            LastGeometryApplied = false;
            LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            _preview!.Text = Render(_proposal, proposal);
            _applyButton.Disabled = false;
            SetStatus($"Plan prêt · {proposal.InputTokens + proposal.OutputTokens} tokens · coût estimé {proposal.EstimatedCostUsd:0.0000} USD · géométrie inchangée.", false);
            return true;
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, true);
            return false;
        }
        finally
        {
            _generateButton.Disabled = false;
        }
    }

    private void Apply()
    {
        if (_approvedCatalog is null || _proposal is null) return;
        var permission = _proposal.ApprovedForConstruction
            ? AssistantPermissionLevel.ApplyApprovedPlan
            : AssistantPermissionLevel.Configure;
        if (_apply?.Invoke(_approvedCatalog, _approvedTransactionId, _approvedPreviewPlanId, permission, false) == true)
        {
            _approvedCatalog = null;
            _applyButton!.Disabled = true;
            Refresh();
        }
    }

    private bool PreviewBuild()
    {
        var plan = _getPlans?.Invoke()?.Catalog.Plans.SingleOrDefault(item => item.Id == _selectedPlanId);
        if (plan is null || !plan.ApprovedForConstruction)
        {
            SetStatus("Sélectionnez un plan explicitement approuvé pour la construction.", true);
            return false;
        }
        _buildTransactionId = Guid.NewGuid();
        _buildPreviewPlanId = Guid.NewGuid();
        if (_build?.Invoke(plan.Id, _buildTransactionId, _buildPreviewPlanId, true) != true)
        {
            _buildPreviewPlanId = Guid.Empty;
            SetStatus("Le dry-run de construction a été refusé.", true);
            return false;
        }
        LastBuildEntityCount = plan.Nodes.Sum(item => item.ContentBlueprintIds.Count);
        _buildButton!.Disabled = false;
        SetStatus($"Construction prête · {LastBuildEntityCount} placement(s) · navigation et éclairage validés · undo atomique.", false);
        return true;
    }

    private void ApplyBuild()
    {
        if (_selectedPlanId == Guid.Empty || _buildPreviewPlanId == Guid.Empty) return;
        if (_build?.Invoke(_selectedPlanId, _buildTransactionId, _buildPreviewPlanId, false) == true)
        {
            _buildPreviewPlanId = Guid.Empty;
            _buildButton!.Disabled = true;
            SetStatus($"Construction appliquée · {LastBuildEntityCount} placement(s) atomiques.", false);
        }
    }

    private static string Render(AssistedAreaPlanDefinition plan, AssistedAreaPlanProposal proposal)
    {
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{plan.Title}[/b][/color] · {plan.Archetype}",
            $"Zone {plan.AreaId:D} · seed {plan.Seed} · {plan.WidthMeters:0} × {plan.DepthMeters:0} m",
            $"Provider {plan.ProviderKey} · coût {plan.EstimatedCostUsd:0.0000} USD · prompt {proposal.PromptSha256[..12]}…",
            $"[color=#9ee6b3]Géométrie appliquée : NON · approbation construction : {(plan.ApprovedForConstruction ? "OUI" : "NON")}[/color]",
            "",
            "[b]Topologie[/b]",
        };
        lines.AddRange(plan.Nodes.Select((node, index) =>
            $"{index + 1}. {node.Kind} · {node.Label} · X {node.X:0.0} / Z {node.Z:0.0} · {node.ContentBlueprintIds.Count} contenu(s)"));
        lines.Add("");
        lines.Add($"[b]Connexions[/b] · {plan.Links.Count}");
        lines.AddRange(plan.Links.Select(link => $"{Short(link.FromNodeId)} → {Short(link.ToNodeId)} · {link.Traversal} · {(link.Required ? "requise" : "optionnelle")}"));
        lines.Add("");
        lines.Add($"[b]Diff[/b] · {proposal.SemanticDiff}");
        return string.Join('\n', lines);
    }

    private static string Short(Guid id) => id.ToString("N")[..8];
    private static IReadOnlyList<string> Lines(string text) => text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private void SetStatus(string text, bool error)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8e9d" : "9ee6b3"));
    }

    private static SpinBox Spin(double min, double max, double value) => new() { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new Vector2(95, 0) };
    private static Label Section(string text) { var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new Color("ffb276")); return label; }
    private static Label FieldLabel(string text) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", 11); label.AddThemeColorOverride("font_color", new Color("9f8dbd")); return label; }
    private static Control Field(string label, Control control) { var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 3); column.AddChild(FieldLabel(label.ToUpperInvariant())); column.AddChild(control); return column; }
    private static LineEdit AddLine(Container parent, string label, string placeholder) { var line = new LineEdit { PlaceholderText = placeholder }; parent.AddChild(Field(label, line)); return line; }
    private static VBoxContainer AddCard(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0), SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 13);
        panel.AddChild(margin);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        return content;
    }
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
}
#endif
