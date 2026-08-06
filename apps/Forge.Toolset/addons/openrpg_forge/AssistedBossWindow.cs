#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Bosses;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedBossWindow : Window
{
    private Func<ProjectSession?>? _getSession;
    private Func<ProjectState?>? _getProject;
    private Func<LoreIndexState?>? _getLore;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<AssistedBossProposal, Guid, Guid, bool, bool>? _apply;
    private Action<Window>? _brandWindow;
    private readonly System.Net.Http.HttpClient _httpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    { Timeout = Timeout.InfiniteTimeSpan };
    private AssistedBossProposal? _proposal;
    private Guid _transactionId;
    private Guid _executionPlanId;
    private AreaId[] _areaIds = [];
    private string[] _scripts = [];

    private OptionButton? _area;
    private LineEdit? _slug;
    private LineEdit? _displayName;
    private TextEdit? _synopsis;
    private TextEdit? _abilities;
    private TextEdit? _phaseScripts;
    private TextEdit? _summons;
    private TextEdit? _rewards;
    private LineEdit? _deathScript;
    private TextEdit? _style;
    private TextEdit? _citations;
    private SpinBox? _health;
    private SpinBox? _offense;
    private SpinBox? _defense;
    private SpinBox? _threat;
    private SpinBox? _seed;
    private CheckButton? _approve;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _generateButton;
    private Button? _applyButton;

    public int LastAbilityCount { get; private set; }
    public int LastPhaseCount { get; private set; }
    public int LastBalanceRuns { get; private set; }
    public bool LastBalancePassed { get; private set; }
    public int LastDiagnosticCount { get; private set; }
    public decimal LastEstimatedCostUsd { get; private set; }

    public void Initialize(
        Func<ProjectSession?> getSession,
        Func<ProjectState?> getProject,
        Func<LoreIndexState?> getLore,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<AssistedBossProposal, Guid, Guid, bool, bool> apply,
        Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLore = getLore ?? throw new ArgumentNullException(nameof(getLore));
        _getAssistant = getAssistant ?? throw new ArgumentNullException(nameof(getAssistant));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Boss assisté · OpenRPG Forge";
        MinSize = new Vector2I(1420, 880);
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
        var session = _getSession?.Invoke();
        var project = _getProject?.Invoke();
        if (session is null || project is null)
        {
            SetStatus("Aucun projet Forge ouvert.", true);
            return;
        }
        _area!.Clear();
        _areaIds = project.Project.Areas.OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .Select(item => item.Id).ToArray();
        foreach (var id in _areaIds)
            _area.AddItem(project.Project.Areas.Single(item => item.Id == id).DisplayName);
        var scriptsRoot = Path.Combine(session.RootDirectory, "scripts");
        _scripts = Directory.Exists(scriptsRoot)
            ? Directory.GetFiles(scriptsRoot, "*.forge", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal).ToArray()
            : [];
        var phase = _scripts.Take(2).ToArray();
        _slug!.Text = "souveraine-des-cendres";
        _displayName!.Text = "Souveraine des Cendres";
        _synopsis!.Text = "La souveraine protège le cœur ardent de la crypte et change de tactique lorsque sa vitalité baisse.";
        _abilities!.Text = "Trait de braise\nCercle de feu\nCouronne ardente";
        _phaseScripts!.Text = string.Join('\n', phase);
        _summons!.Text = project.Project.Blueprints.Where(item => item.Kind == Forge.Domain.Blueprints.BlueprintKind.Creature)
            .Select(item => item.Slug).Take(1).FirstOrDefault() ?? string.Empty;
        _rewards!.Text = "crown-ember\nroyal-ash";
        _deathScript!.Text = _scripts.LastOrDefault() ?? string.Empty;
        _style!.Text = "style:forge-clair\ncombat:telegraphed";
        _citations!.Text = $"project:{project.Project.Id}\nlore:world-bible";
        _health!.Value = 480;
        _offense!.Value = 24;
        _defense!.Value = 12;
        _threat!.Value = 18;
        _seed!.Value = 20260806;
        _approve!.ButtonPressed = false;
        Invalidate();
        _preview!.Text = "[color=#aa94c9]Le lot composera créature, rencontre, capacités, phases, dialogue et loot, puis exécutera 32 simulations avant toute mutation.[/color]";
        SetStatus($"{_areaIds.Length} zone(s) · {_scripts.Length} ForgeScript(s) · révision {project.Revision}.", _scripts.Length < 2);
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        Refresh();
        _approve!.ButtonPressed = true;
        return _areaIds.Length > 0 && _scripts.Length >= 2;
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
        var header = new Label { Text = "CRÉATION DE BOSS ASSISTÉE", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label { Text = "Composition multi-domaines · simulation d'équilibrage · lot canonique atomique et réversible." });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var identity = AddCard(body, 390);
        identity.AddChild(Section("IDENTITÉ & COMBAT"));
        _area = new OptionButton();
        identity.AddChild(Field("Zone", _area));
        _slug = AddLine(identity, "Slug", "souveraine-des-cendres");
        _displayName = AddLine(identity, "Nom", "Souveraine des Cendres");
        identity.AddChild(FieldLabel("SYNOPSIS"));
        _synopsis = AddText(identity, 80);
        var stats = new GridContainer { Columns = 2 };
        _health = Spin(50, 1_000_000, 480); _offense = Spin(1, 100_000, 24);
        _defense = Spin(0, 100_000, 12); _threat = Spin(1, 100_000, 18);
        stats.AddChild(Field("PV", _health)); stats.AddChild(Field("Attaque", _offense));
        stats.AddChild(Field("Défense", _defense)); stats.AddChild(Field("Menace", _threat));
        identity.AddChild(stats);
        identity.AddChild(FieldLabel("CAPACITÉS · UNE PAR LIGNE"));
        _abilities = AddText(identity, 105);

        var orchestration = AddCard(body, 400);
        orchestration.AddChild(Section("PHASES & DÉPENDANCES"));
        orchestration.AddChild(FieldLabel("FORGESCRIPT DE PHASE · 2 OU 3"));
        _phaseScripts = AddText(orchestration, 100);
        orchestration.AddChild(FieldLabel("INVOCATIONS · SLUGS OPTIONNELS"));
        _summons = AddText(orchestration, 70);
        orchestration.AddChild(FieldLabel("RÉCOMPENSES · SLUGS"));
        _rewards = AddText(orchestration, 80);
        _deathScript = AddLine(orchestration, "ForgeScript de mort", "scripts/boss_death.forge");
        _seed = Spin(0, int.MaxValue, 20260806);
        orchestration.AddChild(Field("Seed simulation", _seed));
        orchestration.AddChild(FieldLabel("RÈGLES DE STYLE"));
        _style = AddText(orchestration, 75);
        orchestration.AddChild(FieldLabel("CITATIONS"));
        _citations = AddText(orchestration, 75);
        _approve = new CheckButton { Text = "Approuver ce lot pour application" };
        _approve.Toggled += _ => Invalidate();
        orchestration.AddChild(_approve);

        var result = AddCard(body, 560, true);
        result.AddChild(Section("COMPOSITION & ÉQUILIBRAGE"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 620)
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8));
        result.AddChild(_preview);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        _generateButton = new Button { Text = "Prévisualiser le boss" };
        _generateButton.Pressed += async () => await GenerateAsync();
        footer.AddChild(_generateButton);
        _applyButton = new Button { Text = "Appliquer le lot", Disabled = true };
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" }; close.Pressed += Hide; footer.AddChild(close);
        foreach (var line in new[] { _slug, _displayName, _deathScript }) line.TextChanged += _ => Invalidate();
        foreach (var text in new[] { _synopsis, _abilities, _phaseScripts, _summons, _rewards, _style, _citations })
            text.TextChanged += Invalidate;
        foreach (var spin in new[] { _health, _offense, _defense, _threat, _seed }) spin.ValueChanged += _ => Invalidate();
        _area.ItemSelected += _ => Invalidate();
    }

    private async Task<bool> GenerateAsync()
    {
        var project = _getProject?.Invoke();
        var lore = _getLore?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (project is null || lore is null || assistant is null || _areaIds.Length == 0 || _scripts.Length < 2)
        {
            SetStatus("Projet, lore, provider, zone ou au moins deux ForgeScripts indisponibles.", true);
            return false;
        }
        _generateButton!.Disabled = true;
        _applyButton!.Disabled = true;
        try
        {
            var brief = new AssistedBossBrief(Guid.NewGuid(), Guid.NewGuid(), _slug!.Text, _displayName!.Text,
                _areaIds[Math.Clamp(_area!.Selected, 0, _areaIds.Length - 1)].Value, _synopsis!.Text,
                checked((int)_health!.Value), checked((int)_offense!.Value), checked((int)_defense!.Value),
                _threat!.Value, Lines(_abilities!.Text), Lines(_phaseScripts!.Text), Lines(_summons!.Text),
                Lines(_rewards!.Text), _deathScript!.Text, Lines(_citations!.Text), Lines(_style!.Text),
                "@toolset-author", checked((int)_seed!.Value));
            var providerDefinition = assistant.Profile.ActiveProvider;
            var registry = providerDefinition.Kind == AssistantProviderKind.DeterministicOffline
                ? new AssistantProviderRegistry()
                : AssistantProviderRegistry.CreateWithLocalTransports(_httpClient, requestTimeout: TimeSpan.FromSeconds(30));
            var proposal = await AssistedBossCreationService.ProposeAsync(brief, project.Project, lore.Index,
                _scripts.ToHashSet(StringComparer.Ordinal), registry.Resolve(providerDefinition), providerDefinition,
                assistant.Profile.NetworkAllowed, assistant.Profile.ShareProjectContent);
            _proposal = proposal.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-h" : proposal.Provenance);
            _transactionId = Guid.NewGuid();
            _executionPlanId = Guid.NewGuid();
            if (_proposal.ApprovedForApplication && _apply?.Invoke(_proposal, _transactionId, _executionPlanId, true) != true)
            {
                _proposal = null;
                SetStatus("Le dry-run transactionnel du boss a été refusé.", true);
                return false;
            }
            LastAbilityCount = proposal.Boss.Blueprint.Combat!.Abilities.Count;
            LastPhaseCount = proposal.Boss.Phases.Count;
            LastBalanceRuns = proposal.Balance.Runs;
            LastBalancePassed = proposal.Balance.Passed;
            LastDiagnosticCount = proposal.Diagnostics.Count;
            LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            _preview!.Text = Render(_proposal);
            _applyButton.Disabled = !_proposal.ApprovedForApplication || !_proposal.CanApply;
            SetStatus(_proposal.CanApply
                ? $"Boss prêt · {LastAbilityCount} capacités · {LastPhaseCount} phases · {LastBalanceRuns} simulations · coût {LastEstimatedCostUsd:0.0000} USD."
                : "Composition refusée : corrigez les diagnostics avant application.", !_proposal.CanApply);
            return _proposal.CanApply;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or AssistantProviderTransportException)
        {
            SetStatus(exception.Message, true);
            return false;
        }
        finally { _generateButton.Disabled = false; }
    }

    private void Apply()
    {
        if (_proposal is null || !_proposal.ApprovedForApplication || !_proposal.CanApply) return;
        if (_apply?.Invoke(_proposal, _transactionId, _executionPlanId, false) == true)
        {
            _proposal = null;
            _applyButton!.Disabled = true;
            SetStatus("Boss assisté appliqué · lot canonique disponible dans l'historique Creator.", false);
        }
    }

    private void Invalidate()
    {
        _proposal = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        LastAbilityCount = LastPhaseCount = LastBalanceRuns = LastDiagnosticCount = 0;
        LastBalancePassed = false;
        LastEstimatedCostUsd = 0;
    }

    private static string Render(AssistedBossProposal proposal)
    {
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{proposal.Boss.DisplayName}[/b][/color] · {proposal.Boss.Slug}",
            $"Provider {proposal.ProviderKey} · coût {proposal.EstimatedCostUsd:0.0000} USD · prompt {proposal.PromptSha256[..Math.Min(12, proposal.PromptSha256.Length)]}…",
            $"[color=#9ee6b3]Approbation : {(proposal.ApprovedForApplication ? "OUI" : "NON")} · application : NON[/color]", "",
            $"[b]Créature[/b] · {proposal.Boss.Blueprint.Combat!.Stats.MaximumHealth} PV · attaque {proposal.Boss.Blueprint.Combat.Stats.Offense} · défense {proposal.Boss.Blueprint.Combat.Stats.Defense}",
            $"[b]Rencontre[/b] · {proposal.Boss.Encounter.DisplayName} · budget {proposal.Boss.Encounter.DifficultyProfile.EffectiveBudget:0.##}",
            "[b]Capacités[/b]",
        };
        lines.AddRange(proposal.Boss.Blueprint.Combat.Abilities.Select(item => $"• {item.DisplayName} · {item.Targeting} · recharge {item.CooldownSeconds:0.#} s"));
        lines.Add("[b]Phases[/b]");
        lines.AddRange(proposal.Boss.Phases.Select(item => $"• {item.Id} · PV ≤ {item.HealthThreshold:P0} · {item.AbilityIds.Count} capacité(s) · {item.TransitionScript}"));
        lines.Add($"[b]Dialogue[/b] · {proposal.Boss.IntroductionDialogue.Nodes.Count} nœuds typés");
        lines.Add($"[b]Loot[/b] · {string.Join(" · ", proposal.Boss.Rewards.Select(item => item.ItemSlug))}");
        lines.Add($"[b]Simulation[/b] · {proposal.Balance.Summary} · {(proposal.Balance.Passed ? "PASS" : "ÉCHEC")}");
        lines.Add($"[b]Citations[/b] · {string.Join(" · ", proposal.Citations)}");
        lines.Add($"[b]Diagnostics[/b] · {(proposal.Diagnostics.Count == 0 ? "aucun" : string.Join(" · ", proposal.Diagnostics.Select(item => item.Code)))}");
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
        parent.AddChild(text); return text;
    }
    private static Label Section(string text)
    {
        var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new Color("ffb276")); return label;
    }
    private static Label FieldLabel(string text)
    {
        var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("9f8dbd")); return label;
    }
    private static Control Field(string label, Control control)
    {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 3);
        column.AddChild(FieldLabel(label.ToUpperInvariant())); column.AddChild(control); return column;
    }
    private static LineEdit AddLine(Container parent, string label, string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder }; parent.AddChild(Field(label, line)); return line;
    }
    private static VBoxContainer AddCard(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10)); parent.AddChild(panel);
        var margin = new MarginContainer(); foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 13);
        panel.AddChild(margin); var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 8); margin.AddChild(content); return content;
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
