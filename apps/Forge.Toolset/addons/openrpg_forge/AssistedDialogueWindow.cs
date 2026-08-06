#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Domain.Dialogues;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedDialogueWindow : Window
{
    private Func<ProjectState?>? _getProject;
    private Func<LoreIndexState?>? _getLore;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<AssistedDialogueProposal, Guid, Guid, bool, bool>? _apply;
    private Action<Window>? _brandWindow;
    private readonly System.Net.Http.HttpClient _httpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private AssistedDialogueProposal? _proposal;
    private Guid _transactionId;
    private Guid _executionPlanId;
    private BlueprintId[] _speakerIds = [];

    private OptionButton? _speaker;
    private LineEdit? _slug;
    private LineEdit? _displayName;
    private TextEdit? _synopsis;
    private TextEdit? _beats;
    private TextEdit? _style;
    private TextEdit? _citations;
    private LineEdit? _locale;
    private LineEdit? _localizationPrefix;
    private SpinBox? _seed;
    private SpinBox? _nodeCount;
    private CheckButton? _approve;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _generateButton;
    private Button? _applyButton;

    public int LastNodeCount { get; private set; }
    public int LastLintCount { get; private set; }
    public int LastCitationCount { get; private set; }
    public decimal LastEstimatedCostUsd { get; private set; }

    public void Initialize(
        Func<ProjectState?> getProject,
        Func<LoreIndexState?> getLore,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<AssistedDialogueProposal, Guid, Guid, bool, bool> apply,
        Action<Window> brandWindow)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLore = getLore ?? throw new ArgumentNullException(nameof(getLore));
        _getAssistant = getAssistant ?? throw new ArgumentNullException(nameof(getAssistant));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Dialogue assisté · OpenRPG Forge";
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
        var project = _getProject?.Invoke();
        if (project is null)
        {
            SetStatus("Aucun projet Forge ouvert.", true);
            return;
        }
        _speaker!.Clear();
        _speakerIds = project.Project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .Select(item => item.Id)
            .ToArray();
        foreach (var id in _speakerIds)
        {
            var blueprint = project.Project.Blueprints.Single(item => item.Id == id);
            _speaker.AddItem(blueprint.DisplayName);
        }
        _slug!.Text = "dialogue-assiste";
        _displayName!.Text = "Dialogue assisté";
        _synopsis!.Text = "Un personnage révèle un secret du monde puis demande une décision au joueur.";
        _beats!.Text = "Présenter le contexte\nRévéler le secret\nDemander un choix\nConclure";
        _style!.Text = "style:forge-clair\nrépliques brèves";
        _citations!.Text = $"project:{project.Project.Id}\nlore:world-bible";
        _locale!.Text = "fr";
        _localizationPrefix!.Text = "dialogue.assiste";
        _seed!.Value = 20260805;
        _nodeCount!.Value = 4;
        _approve!.ButtonPressed = false;
        Invalidate();
        _preview!.Text = "[color=#aa94c9]Décrivez le dialogue. La proposition affichera graphe, mini-carte, citations, localisation et lint avant toute mutation.[/color]";
        SetStatus($"{_speakerIds.Length} locuteur(s) disponible(s) · révision {project.Revision}.", false);
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        Refresh();
        _approve!.ButtonPressed = true;
        return _speakerIds.Length > 0;
    }

    public bool PreviewForSmokeTest() => GenerateAsync().GetAwaiter().GetResult();

    public bool ApplyForSmokeTest()
    {
        Apply();
        return _proposal is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        background.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var header = new Label { Text = "CRÉATION DE DIALOGUE ASSISTÉE", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "Graphe canonique, style sourcé, localisation et lint · aucune application sans approbation et dry-run.",
        });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);
        var brief = AddCard(body, 390);
        brief.AddChild(Section("BRIEF & PERSONNAGE"));
        _speaker = new OptionButton();
        brief.AddChild(Field("Locuteur", _speaker));
        _slug = AddLine(brief, "Slug", "dialogue-assiste");
        _displayName = AddLine(brief, "Nom", "Dialogue assisté");
        brief.AddChild(FieldLabel("SYNOPSIS"));
        _synopsis = AddText(brief, 90);
        brief.AddChild(FieldLabel("BEATS · UN PAR LIGNE"));
        _beats = AddText(brief, 100);
        var settings = new HBoxContainer();
        _locale = new LineEdit { PlaceholderText = "fr", CustomMinimumSize = new Vector2(70, 0) };
        _localizationPrefix = new LineEdit { PlaceholderText = "dialogue.cle", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        settings.AddChild(Field("Locale", _locale));
        settings.AddChild(Field("Préfixe localisation", _localizationPrefix));
        brief.AddChild(settings);
        var numbers = new HBoxContainer();
        _seed = Spin(0, int.MaxValue, 20260805);
        _nodeCount = Spin(3, 12, 4);
        numbers.AddChild(Field("Seed", _seed));
        numbers.AddChild(Field("Nœuds", _nodeCount));
        brief.AddChild(numbers);

        var governance = AddCard(body, 300);
        governance.AddChild(Section("STYLE & SOURCES"));
        governance.AddChild(FieldLabel("RÈGLES DE STYLE"));
        _style = AddText(governance, 150);
        governance.AddChild(FieldLabel("CITATIONS DE CONTEXTE"));
        _citations = AddText(governance, 150);
        _approve = new CheckButton { Text = "Approuver pour application" };
        _approve.Toggled += _ => Invalidate();
        governance.AddChild(_approve);
        governance.AddChild(new Label
        {
            Text = "L’approbation autorise uniquement le dialogue affiché par ce plan et cette transaction.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var result = AddCard(body, 500, true);
        result.AddChild(Section("GRAPHE · MINI-CARTE · VALIDATION"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 560),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8));
        result.AddChild(_preview);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        _generateButton = new Button { Text = "Prévisualiser le dialogue" };
        _generateButton.Pressed += async () => await GenerateAsync();
        footer.AddChild(_generateButton);
        _applyButton = new Button { Text = "Appliquer le dialogue", Disabled = true };
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        foreach (var line in new[] { _slug, _displayName, _locale, _localizationPrefix })
            line.TextChanged += _ => Invalidate();
        foreach (var text in new[] { _synopsis, _beats, _style, _citations })
            text.TextChanged += Invalidate;
    }

    private async Task<bool> GenerateAsync()
    {
        var project = _getProject?.Invoke();
        var lore = _getLore?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (project is null || lore is null || assistant is null || _speakerIds.Length == 0)
        {
            SetStatus("Projet, lore, provider ou locuteur indisponible.", true);
            return false;
        }
        _generateButton!.Disabled = true;
        _applyButton!.Disabled = true;
        try
        {
            var dialogueId = Guid.NewGuid();
            var brief = new AssistedDialogueBrief(
                dialogueId,
                _slug!.Text,
                _displayName!.Text,
                _speakerIds[Math.Clamp(_speaker!.Selected, 0, _speakerIds.Length - 1)].Value,
                _synopsis!.Text,
                Lines(_beats!.Text),
                Lines(_style!.Text),
                Lines(_citations!.Text),
                _locale!.Text,
                _localizationPrefix!.Text,
                "@toolset-author",
                checked((int)_seed!.Value),
                checked((int)_nodeCount!.Value));
            var providerDefinition = assistant.Profile.ActiveProvider;
            var registry = providerDefinition.Kind == AssistantProviderKind.DeterministicOffline
                ? new AssistantProviderRegistry()
                : AssistantProviderRegistry.CreateWithLocalTransports(
                    _httpClient,
                    requestTimeout: TimeSpan.FromSeconds(30));
            var proposal = await AssistedDialogueCreationService.ProposeAsync(
                brief,
                project.Project,
                lore.Index,
                registry.Resolve(providerDefinition),
                providerDefinition,
                assistant.Profile.NetworkAllowed,
                assistant.Profile.ShareProjectContent);
            _proposal = proposal.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-e" : proposal.Provenance);
            _transactionId = Guid.NewGuid();
            _executionPlanId = Guid.NewGuid();
            if (_proposal.ApprovedForApplication &&
                _apply?.Invoke(_proposal, _transactionId, _executionPlanId, true) != true)
            {
                _proposal = null;
                SetStatus("Le dry-run transactionnel du dialogue a été refusé.", true);
                return false;
            }
            LastNodeCount = proposal.Dialogue.Nodes.Count;
            LastLintCount = proposal.Lint.Count;
            LastCitationCount = proposal.Citations.Count;
            LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            _preview!.Text = Render(_proposal);
            _applyButton.Disabled = !_proposal.ApprovedForApplication;
            SetStatus(_proposal.ApprovedForApplication
                ? $"Dialogue prêt · {LastNodeCount} nœuds · lint {LastLintCount} · coût {LastEstimatedCostUsd:0.0000} USD."
                : "Proposition prête en lecture seule · cochez l’approbation puis prévisualisez à nouveau pour appliquer.", false);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or AssistantProviderTransportException)
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
        if (_proposal is null || !_proposal.ApprovedForApplication) return;
        if (_apply?.Invoke(_proposal, _transactionId, _executionPlanId, false) == true)
        {
            _proposal = null;
            _applyButton!.Disabled = true;
            SetStatus("Dialogue assisté appliqué · undo/redo disponibles dans l’historique Creator.", false);
        }
    }

    private void Invalidate()
    {
        _proposal = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        LastNodeCount = 0;
        LastLintCount = 0;
        LastCitationCount = 0;
        LastEstimatedCostUsd = 0;
    }

    private static string Render(AssistedDialogueProposal proposal)
    {
        var participant = proposal.Graph.Participants[0];
        var simulation = DialogueGraphService.Render(
            proposal.Graph,
            proposal.Graph.EntryNodeId,
            new DialogueContext(
                new Dictionary<string, object>
                {
                    ["player_name"] = "Voyageur",
                    ["assistant.dialogue.ready"] = true,
                },
                new Dictionary<string, string> { [participant.Role] = participant.Value },
                20260805));
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{proposal.Dialogue.DisplayName}[/b][/color] · {proposal.Dialogue.Slug}",
            $"Provider {proposal.ProviderKey} · coût {proposal.EstimatedCostUsd:0.0000} USD · prompt {proposal.PromptSha256[..Math.Min(12, proposal.PromptSha256.Length)]}…",
            $"[color=#9ee6b3]Approbation : {(proposal.ApprovedForApplication ? "OUI" : "NON")} · application : NON[/color]",
            "",
            "[b]Mini-carte du graphe[/b]",
        };
        for (var index = 0; index < proposal.Dialogue.Nodes.Count; index++)
        {
            var node = proposal.Dialogue.Nodes[index];
            var targets = node.Choices.Select(item => item.TargetNodeId?.ToString() ?? "FIN");
            lines.Add($"{index + 1}. {node.Text.Fallback} → {(node.Choices.Count == 0 ? "FIN" : string.Join(", ", targets))}");
        }
        lines.Add("");
        lines.Add($"[b]Citations[/b] · {string.Join(" · ", proposal.Citations)}");
        lines.Add($"[b]Localisation[/b] · {proposal.LocalizationPlaceholders.Count} placeholder(s)");
        lines.AddRange(proposal.LocalizationPlaceholders.Select(item => $"  {item}"));
        lines.Add($"[b]Lint[/b] · {(proposal.Lint.Count == 0 ? "aucun diagnostic" : string.Join(" · ", proposal.Lint.Select(item => item.Code)))}");
        lines.Add($"[b]Simulation entrée[/b] · {simulation.Text} · {simulation.AvailableEdges.Count} transition(s) · {simulation.Diagnostics.Count} diagnostic(s)");
        lines.Add($"[b]Diff[/b] · {proposal.SemanticDiff}");
        return string.Join('\n', lines);
    }

    private void SetStatus(string text, bool error)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8e9d" : "9ee6b3"));
    }

    private static IReadOnlyList<string> Lines(string value) =>
        value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static SpinBox Spin(double min, double max, double value) =>
        new() { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new Vector2(100, 0) };
    private static TextEdit AddText(Container parent, float height)
    {
        var text = new TextEdit { CustomMinimumSize = new Vector2(0, height), WrapMode = TextEdit.LineWrappingMode.Boundary };
        parent.AddChild(text);
        return text;
    }
    private static Label Section(string text)
    {
        var label = new Label { Text = text };
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
    private static Control Field(string label, Control control)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        column.AddChild(FieldLabel(label.ToUpperInvariant()));
        column.AddChild(control);
        return column;
    }
    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder };
        parent.AddChild(Field(label, line));
        return line;
    }
    private static VBoxContainer AddCard(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10));
        parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 13);
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
