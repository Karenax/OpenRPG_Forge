#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.RpgKit.Application.Quests;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedQuestWindow : Window
{
    private Func<ProjectState?>? _getProject;
    private Func<LoreIndexState?>? _getLore;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<AssistedQuestProposal, Guid, Guid, bool, bool>? _apply;
    private Action<Window>? _brandWindow;
    private readonly System.Net.Http.HttpClient _httpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private AssistedQuestProposal? _proposal;
    private Guid _transactionId;
    private Guid _executionPlanId;
    private AreaId[] _areaIds = [];
    private BlueprintId[] _participantIds = [];

    private OptionButton? _area;
    private OptionButton? _participant;
    private LineEdit? _slug;
    private LineEdit? _displayName;
    private TextEdit? _synopsis;
    private TextEdit? _objectives;
    private TextEdit? _style;
    private TextEdit? _citations;
    private LineEdit? _localizationPrefix;
    private SpinBox? _seed;
    private SpinBox? _reward;
    private CheckButton? _approve;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _generateButton;
    private Button? _applyButton;

    public int LastStateCount { get; private set; }
    public int LastObjectiveCount { get; private set; }
    public int LastScenarioCount { get; private set; }
    public int LastLintCount { get; private set; }
    public decimal LastEstimatedCostUsd { get; private set; }

    public void Initialize(
        Func<ProjectState?> getProject,
        Func<LoreIndexState?> getLore,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<AssistedQuestProposal, Guid, Guid, bool, bool> apply,
        Action<Window> brandWindow)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLore = getLore ?? throw new ArgumentNullException(nameof(getLore));
        _getAssistant = getAssistant ?? throw new ArgumentNullException(nameof(getAssistant));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Quête assistée · OpenRPG Forge";
        MinSize = new Vector2I(1320, 820);
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
        _area!.Clear();
        _areaIds = project.Project.Areas.OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .Select(item => item.Id).ToArray();
        foreach (var id in _areaIds)
            _area.AddItem(project.Project.Areas.Single(item => item.Id == id).DisplayName);
        _participant!.Clear();
        _participantIds = project.Project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .Select(item => item.Id).ToArray();
        foreach (var id in _participantIds)
            _participant.AddItem(project.Project.Blueprints.Single(item => item.Id == id).DisplayName);
        _slug!.Text = "braises-perdues";
        _displayName!.Text = "Les braises perdues";
        _synopsis!.Text = "Un guide demande de retrouver les braises avant l'effondrement de la crypte.";
        _objectives!.Text = "Trouver les braises\nRapporter les braises au guide";
        _style!.Text = "style:forge-clair\nprogression lisible";
        _citations!.Text = $"project:{project.Project.Id}\nlore:world-bible";
        _localizationPrefix!.Text = "quest.braises-perdues";
        _seed!.Value = 20260806;
        _reward!.Value = 250;
        _approve!.ButtonPressed = false;
        Invalidate();
        _preview!.Text = "[color=#aa94c9]Décrivez la quête. États, objectifs, transitions, récompenses, échec et cinq scénarios seront vérifiés avant toute mutation.[/color]";
        SetStatus($"{_areaIds.Length} zone(s) · {_participantIds.Length} participant(s) · révision {project.Revision}.", false);
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        Refresh();
        _approve!.ButtonPressed = true;
        return _areaIds.Length > 0 && _participantIds.Length > 0;
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
        var header = new Label { Text = "CRÉATION DE QUÊTE ASSISTÉE", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "États, objectifs, récompenses, échec et tests · aucune application sans approbation et dry-run exact.",
        });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var brief = AddCard(body, 400);
        brief.AddChild(Section("BRIEF & MONDE"));
        _area = new OptionButton();
        brief.AddChild(Field("Zone", _area));
        _participant = new OptionButton();
        brief.AddChild(Field("Participant", _participant));
        _slug = AddLine(brief, "Slug", "braises-perdues");
        _displayName = AddLine(brief, "Nom", "Les braises perdues");
        brief.AddChild(FieldLabel("SYNOPSIS"));
        _synopsis = AddText(brief, 90);
        brief.AddChild(FieldLabel("OBJECTIFS · UN PAR LIGNE"));
        _objectives = AddText(brief, 105);
        _localizationPrefix = AddLine(brief, "Préfixe localisation", "quest.braises-perdues");
        var numbers = new HBoxContainer();
        _seed = Spin(0, int.MaxValue, 20260806);
        _reward = Spin(0, 1_000_000, 250);
        numbers.AddChild(Field("Seed", _seed));
        numbers.AddChild(Field("Progression", _reward));
        brief.AddChild(numbers);

        var governance = AddCard(body, 310);
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
            Text = "L'approbation autorise uniquement cette quête, ce plan et cette transaction.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var result = AddCard(body, 550, true);
        result.AddChild(Section("GRAPHE · RÉCOMPENSES · TESTS"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 580),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8));
        result.AddChild(_preview);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        _generateButton = new Button { Text = "Prévisualiser la quête" };
        _generateButton.Pressed += async () => await GenerateAsync();
        footer.AddChild(_generateButton);
        _applyButton = new Button { Text = "Appliquer la quête", Disabled = true };
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        foreach (var line in new[] { _slug, _displayName, _localizationPrefix })
            line.TextChanged += _ => Invalidate();
        foreach (var text in new[] { _synopsis, _objectives, _style, _citations })
            text.TextChanged += Invalidate;
        _area.ItemSelected += _ => Invalidate();
        _participant.ItemSelected += _ => Invalidate();
        _seed.ValueChanged += _ => Invalidate();
        _reward.ValueChanged += _ => Invalidate();
    }

    private async Task<bool> GenerateAsync()
    {
        var project = _getProject?.Invoke();
        var lore = _getLore?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (project is null || lore is null || assistant is null || _areaIds.Length == 0 || _participantIds.Length == 0)
        {
            SetStatus("Projet, lore, provider, zone ou participant indisponible.", true);
            return false;
        }
        _generateButton!.Disabled = true;
        _applyButton!.Disabled = true;
        try
        {
            var brief = new AssistedQuestBrief(
                Guid.NewGuid(),
                _slug!.Text,
                _displayName!.Text,
                _areaIds[Math.Clamp(_area!.Selected, 0, _areaIds.Length - 1)].Value,
                _participantIds[Math.Clamp(_participant!.Selected, 0, _participantIds.Length - 1)].Value,
                _synopsis!.Text,
                Lines(_objectives!.Text),
                Lines(_citations!.Text),
                Lines(_style!.Text),
                _localizationPrefix!.Text,
                "@toolset-author",
                checked((int)_seed!.Value),
                _reward!.Value);
            var providerDefinition = assistant.Profile.ActiveProvider;
            var registry = providerDefinition.Kind == AssistantProviderKind.DeterministicOffline
                ? new AssistantProviderRegistry()
                : AssistantProviderRegistry.CreateWithLocalTransports(_httpClient, requestTimeout: TimeSpan.FromSeconds(30));
            var proposal = await AssistedQuestCreationService.ProposeAsync(
                brief,
                project.Project,
                lore.Index,
                registry.Resolve(providerDefinition),
                providerDefinition,
                assistant.Profile.NetworkAllowed,
                assistant.Profile.ShareProjectContent);
            _proposal = proposal.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-f" : proposal.Provenance);
            _transactionId = Guid.NewGuid();
            _executionPlanId = Guid.NewGuid();
            if (_proposal.ApprovedForApplication &&
                _apply?.Invoke(_proposal, _transactionId, _executionPlanId, true) != true)
            {
                _proposal = null;
                SetStatus("Le dry-run transactionnel de la quête a été refusé.", true);
                return false;
            }
            LastStateCount = proposal.Quest.States.Count;
            LastObjectiveCount = proposal.AdvancedQuest.Objectives.Count;
            LastScenarioCount = proposal.TestScenarios.Count;
            LastLintCount = proposal.Lint.Count;
            LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            _preview!.Text = Render(_proposal);
            _applyButton.Disabled = !_proposal.ApprovedForApplication;
            SetStatus(_proposal.ApprovedForApplication
                ? $"Quête prête · {LastObjectiveCount} objectifs · {LastScenarioCount} tests · lint {LastLintCount} · coût {LastEstimatedCostUsd:0.0000} USD."
                : "Proposition en lecture seule · cochez l'approbation puis prévisualisez à nouveau pour appliquer.", false);
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
            SetStatus("Quête assistée appliquée · undo/redo disponibles dans l'historique Creator.", false);
        }
    }

    private void Invalidate()
    {
        _proposal = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        LastStateCount = 0;
        LastObjectiveCount = 0;
        LastScenarioCount = 0;
        LastLintCount = 0;
        LastEstimatedCostUsd = 0;
    }

    private static string Render(AssistedQuestProposal proposal)
    {
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{proposal.Quest.DisplayName}[/b][/color] · {proposal.Quest.Slug}",
            $"Provider {proposal.ProviderKey} · coût {proposal.EstimatedCostUsd:0.0000} USD · prompt {proposal.PromptSha256[..Math.Min(12, proposal.PromptSha256.Length)]}…",
            $"[color=#9ee6b3]Approbation : {(proposal.ApprovedForApplication ? "OUI" : "NON")} · application : NON[/color]",
            "",
            "[b]États et transitions[/b]",
        };
        lines.AddRange(proposal.Quest.States.Select(state =>
            $"• {state.DisplayName}{(state.IsTerminal ? " · terminal" : string.Empty)}"));
        lines.AddRange(proposal.Quest.Transitions.Select(transition =>
            $"  {transition.FromStateId} → {transition.ToStateId} · {transition.Conditions.Count} condition(s) · {transition.Actions.Count} action(s)"));
        lines.Add("");
        lines.Add($"[b]Objectifs[/b] · {proposal.AdvancedQuest.Objectives.Count}");
        lines.AddRange(proposal.AdvancedQuest.Objectives.Select(item => $"• {item.Label}"));
        lines.Add($"[b]Récompense[/b] · {proposal.AdvancedQuest.Rewards.Progression:0} progression");
        lines.Add($"[b]Chemin d'échec[/b] · {(proposal.SemanticDiff.Contains("failure-path:1", StringComparison.Ordinal) ? "explicite" : "absent")}");
        lines.Add($"[b]Scénarios[/b] · {string.Join(" · ", proposal.TestScenarios.Select(item => item.Kind))}");
        lines.Add($"[b]Citations[/b] · {string.Join(" · ", proposal.Citations)}");
        lines.Add($"[b]Lint[/b] · {(proposal.Lint.Count == 0 ? "aucun diagnostic" : string.Join(" · ", proposal.Lint.Select(item => item.Code)))}");
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
        new() { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new Vector2(120, 0) };
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
