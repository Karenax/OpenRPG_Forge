#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Abilities;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedAbilityWindow : Window
{
    private Func<ProjectSession?>? _getSession;
    private Func<ProjectState?>? _getProject;
    private Func<LoreIndexState?>? _getLore;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<AssistedAbilityProposal, Guid, Guid, bool, bool>? _apply;
    private Action<Window>? _brandWindow;
    private readonly System.Net.Http.HttpClient _httpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private AssistedAbilityProposal? _proposal;
    private Guid _transactionId;
    private Guid _executionPlanId;
    private BlueprintId[] _ownerIds = [];
    private string[] _itemSlugs = [];
    private string[] _scripts = [];

    private OptionButton? _owner;
    private OptionButton? _item;
    private OptionButton? _targeting;
    private OptionButton? _effect;
    private LineEdit? _slug;
    private LineEdit? _displayName;
    private LineEdit? _vfx;
    private LineEdit? _audio;
    private LineEdit? _script;
    private TextEdit? _aiTags;
    private TextEdit? _balanceIntent;
    private TextEdit? _style;
    private TextEdit? _citations;
    private SpinBox? _cost;
    private SpinBox? _cooldown;
    private SpinBox? _magnitude;
    private SpinBox? _seed;
    private CheckButton? _approve;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _generateButton;
    private Button? _applyButton;

    public int LastTestCount { get; private set; }
    public int LastPassedTestCount { get; private set; }
    public bool LastBalancePassed { get; private set; }
    public int LastDiagnosticCount { get; private set; }
    public decimal LastEstimatedCostUsd { get; private set; }

    public void Initialize(
        Func<ProjectSession?> getSession,
        Func<ProjectState?> getProject,
        Func<LoreIndexState?> getLore,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<AssistedAbilityProposal, Guid, Guid, bool, bool> apply,
        Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLore = getLore ?? throw new ArgumentNullException(nameof(getLore));
        _getAssistant = getAssistant ?? throw new ArgumentNullException(nameof(getAssistant));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Capacité assistée · OpenRPG Forge";
        MinSize = new Vector2I(1360, 840);
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
        _owner!.Clear();
        _ownerIds = project.Project.Blueprints
            .Where(item => item.Combat is not null && item.Kind is (BlueprintKind.Creature or BlueprintKind.PlayerSpawn))
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal).Select(item => item.Id).ToArray();
        foreach (var id in _ownerIds)
            _owner.AddItem(project.Project.Blueprints.Single(item => item.Id == id).DisplayName);
        _item!.Clear();
        _item.AddItem("Aucun item requis");
        _itemSlugs = project.Project.Blueprints.Where(candidate => candidate.Kind == BlueprintKind.Item)
            .OrderBy(candidate => candidate.DisplayName, StringComparer.Ordinal).Select(candidate => candidate.Slug)
            .ToArray();
        foreach (var slug in _itemSlugs)
            _item.AddItem(project.Project.Blueprints.Single(candidate => candidate.Slug == slug).DisplayName);
        var scriptsRoot = Path.Combine(session.RootDirectory, "scripts");
        _scripts = Directory.Exists(scriptsRoot)
            ? Directory.GetFiles(scriptsRoot, "*.forge", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetRelativePath(session.RootDirectory, path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal).ToArray()
            : [];
        _slug!.Text = "lance-de-braise";
        _displayName!.Text = "Lance de braise";
        _cost!.Value = _ownerIds.Length == 0
            ? 0
            : Math.Min(15, project.Project.Blueprints.Single(item => item.Id == _ownerIds[0]).Combat!.Stats.MaximumResource);
        _cooldown!.Value = 6;
        _magnitude!.Value = 18;
        _targeting!.Selected = (int)AbilityTargeting.SingleEnemy;
        _effect!.Selected = (int)AbilityEffectKind.Damage;
        _vfx!.Text = "vfx.ability.ember-lance";
        _audio!.Text = "audio.ability.ember-lance";
        _script!.Text = _scripts.FirstOrDefault() ?? string.Empty;
        _aiTags!.Text = "ranged\nfinisher\ntelegraphed";
        _balanceIntent!.Text = "Dégâts concentrés lisibles avec un coût et une recharge significatifs.";
        _style!.Text = "style:forge-clair\ncombat:telegraphed";
        _citations!.Text = $"project:{project.Project.Id}\nlore:world-bible";
        _seed!.Value = 20260806;
        _approve!.ButtonPressed = false;
        Invalidate();
        _preview!.Text = "[color=#aa94c9]La capacité sera testée par le vrai résolveur de combat avant d'être ajoutée au blueprint et au document runtime.[/color]";
        SetStatus($"{_ownerIds.Length} porteur(s) · {_itemSlugs.Length} item(s) · {_scripts.Length} ForgeScript(s).",
            _ownerIds.Length == 0 || _scripts.Length == 0);
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        Refresh();
        _approve!.ButtonPressed = true;
        return _ownerIds.Length > 0 && _scripts.Length > 0;
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
        var header = new Label { Text = "CRÉATION DE CAPACITÉ ASSISTÉE", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label { Text = "Ciblage, coûts, effets, cues, IA et tests · application canonique atomique." });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var design = AddCard(body, 390);
        design.AddChild(Section("DESIGN DE COMBAT"));
        _owner = new OptionButton(); design.AddChild(Field("Porteur", _owner));
        _slug = AddLine(design, "Slug", "lance-de-braise");
        _displayName = AddLine(design, "Nom", "Lance de braise");
        _targeting = new OptionButton();
        foreach (var value in Enum.GetValues<AbilityTargeting>()) _targeting.AddItem(value.ToString());
        design.AddChild(Field("Ciblage", _targeting));
        _effect = new OptionButton();
        foreach (var value in Enum.GetValues<AbilityEffectKind>()) _effect.AddItem(value.ToString());
        design.AddChild(Field("Effet", _effect));
        var numbers = new GridContainer { Columns = 2 };
        _cost = Spin(0, 1_000_000, 15); _cooldown = Spin(0, 300, 6, .5); _magnitude = Spin(1, 100_000, 18);
        _seed = Spin(0, int.MaxValue, 20260806);
        numbers.AddChild(Field("Coût", _cost)); numbers.AddChild(Field("Recharge", _cooldown));
        numbers.AddChild(Field("Magnitude", _magnitude)); numbers.AddChild(Field("Seed", _seed));
        design.AddChild(numbers);
        _vfx = AddLine(design, "Cue VFX", "vfx.ability.ember-lance");
        _audio = AddLine(design, "Cue audio", "audio.ability.ember-lance");

        var dependencies = AddCard(body, 360);
        dependencies.AddChild(Section("DÉPENDANCES & GOUVERNANCE"));
        _script = AddLine(dependencies, "ForgeScript", "scripts/ability.forge");
        _item = new OptionButton(); dependencies.AddChild(Field("Item requis", _item));
        dependencies.AddChild(FieldLabel("TAGS IA · UN PAR LIGNE"));
        _aiTags = AddText(dependencies, 90);
        dependencies.AddChild(FieldLabel("INTENTION D'ÉQUILIBRAGE"));
        _balanceIntent = AddText(dependencies, 95);
        dependencies.AddChild(FieldLabel("RÈGLES DE STYLE"));
        _style = AddText(dependencies, 80);
        dependencies.AddChild(FieldLabel("CITATIONS"));
        _citations = AddText(dependencies, 80);
        _approve = new CheckButton { Text = "Approuver cette capacité" };
        _approve.Toggled += _ => Invalidate();
        dependencies.AddChild(_approve);

        var result = AddCard(body, 550, true);
        result.AddChild(Section("TESTS & ÉQUILIBRAGE"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 600),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8));
        result.AddChild(_preview);

        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 8); root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        _generateButton = new Button { Text = "Prévisualiser la capacité" };
        _generateButton.Pressed += async () => await GenerateAsync(); footer.AddChild(_generateButton);
        _applyButton = new Button { Text = "Appliquer la capacité", Disabled = true };
        _applyButton.Pressed += Apply; footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" }; close.Pressed += Hide; footer.AddChild(close);
        foreach (var line in new[] { _slug, _displayName, _vfx, _audio, _script }) line.TextChanged += _ => Invalidate();
        foreach (var text in new[] { _aiTags, _balanceIntent, _style, _citations }) text.TextChanged += Invalidate;
        foreach (var spin in new[] { _cost, _cooldown, _magnitude, _seed }) spin.ValueChanged += _ => Invalidate();
        foreach (var option in new[] { _owner, _item, _targeting, _effect }) option.ItemSelected += _ => Invalidate();
    }

    private async Task<bool> GenerateAsync()
    {
        var project = _getProject?.Invoke();
        var lore = _getLore?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (project is null || lore is null || assistant is null || _ownerIds.Length == 0 || _scripts.Length == 0)
        {
            SetStatus("Projet, lore, provider, porteur ou ForgeScript indisponible.", true);
            return false;
        }
        _generateButton!.Disabled = true;
        _applyButton!.Disabled = true;
        try
        {
            var itemIndex = _item!.Selected - 1;
            var brief = new AssistedAbilityBrief(Guid.NewGuid(),
                _ownerIds[Math.Clamp(_owner!.Selected, 0, _ownerIds.Length - 1)].Value,
                _slug!.Text, _displayName!.Text, checked((int)_cost!.Value), _cooldown!.Value,
                Enum.GetValues<AbilityTargeting>()[Math.Clamp(_targeting!.Selected, 0, Enum.GetValues<AbilityTargeting>().Length - 1)],
                [new AbilityEffect(Enum.GetValues<AbilityEffectKind>()[Math.Clamp(_effect!.Selected, 0, Enum.GetValues<AbilityEffectKind>().Length - 1)], checked((int)_magnitude!.Value))],
                _vfx!.Text, _audio!.Text, _script!.Text,
                itemIndex >= 0 && itemIndex < _itemSlugs.Length ? _itemSlugs[itemIndex] : null,
                Lines(_aiTags!.Text).ToHashSet(StringComparer.Ordinal), _balanceIntent!.Text,
                Lines(_citations!.Text), Lines(_style!.Text), "@toolset-author", checked((int)_seed!.Value));
            var providerDefinition = assistant.Profile.ActiveProvider;
            var registry = providerDefinition.Kind == AssistantProviderKind.DeterministicOffline
                ? new AssistantProviderRegistry()
                : AssistantProviderRegistry.CreateWithLocalTransports(_httpClient, requestTimeout: TimeSpan.FromSeconds(30));
            var proposal = await AssistedAbilityCreationService.ProposeAsync(brief, project.Project, lore.Index,
                _scripts.ToHashSet(StringComparer.Ordinal), registry.Resolve(providerDefinition), providerDefinition,
                assistant.Profile.NetworkAllowed, assistant.Profile.ShareProjectContent);
            _proposal = proposal.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-i" : proposal.Provenance);
            _transactionId = Guid.NewGuid();
            _executionPlanId = Guid.NewGuid();
            if (_proposal.ApprovedForApplication && _apply?.Invoke(_proposal, _transactionId, _executionPlanId, true) != true)
            {
                _proposal = null;
                SetStatus("Le dry-run transactionnel de la capacité a été refusé.", true);
                return false;
            }
            LastTestCount = proposal.Tests.Count;
            LastPassedTestCount = proposal.Tests.Count(item => item.Passed);
            LastBalancePassed = proposal.Balance.Passed;
            LastDiagnosticCount = proposal.Diagnostics.Count;
            LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            _preview!.Text = Render(_proposal);
            _applyButton.Disabled = !_proposal.ApprovedForApplication || !_proposal.CanApply;
            SetStatus(_proposal.CanApply
                ? $"Capacité prête · {LastPassedTestCount}/{LastTestCount} tests · score {proposal.Balance.PowerScore:0.###} · coût {LastEstimatedCostUsd:0.0000} USD."
                : "Capacité refusée : corrigez les diagnostics avant application.", !_proposal.CanApply);
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
            SetStatus("Capacité assistée appliquée · undo/redo disponibles dans l'historique Creator.", false);
        }
    }

    private void Invalidate()
    {
        _proposal = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        LastTestCount = LastPassedTestCount = LastDiagnosticCount = 0;
        LastBalancePassed = false;
        LastEstimatedCostUsd = 0;
    }

    private static string Render(AssistedAbilityProposal proposal)
    {
        var ability = proposal.Orchestration.Ability;
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{ability.DisplayName}[/b][/color] · {ability.Slug}",
            $"Provider {proposal.ProviderKey} · coût {proposal.EstimatedCostUsd:0.0000} USD · prompt {proposal.PromptSha256[..Math.Min(12, proposal.PromptSha256.Length)]}…",
            $"[color=#9ee6b3]Approbation : {(proposal.ApprovedForApplication ? "OUI" : "NON")} · application : NON[/color]", "",
            $"[b]Ciblage[/b] · {ability.Targeting}",
            $"[b]Coût / recharge[/b] · {ability.ResourceCost} · {ability.CooldownSeconds:0.##} s",
            $"[b]Effets[/b] · {string.Join(" · ", ability.Effects.Select(item => $"{item.Kind}:{item.Magnitude}"))}",
            $"[b]VFX / audio[/b] · {ability.VisualCue.CueKey} · {proposal.Orchestration.AudioCueKey}",
            $"[b]ForgeScript[/b] · {proposal.Orchestration.ScriptPath}",
            $"[b]Item[/b] · {proposal.Orchestration.RequiredItemSlug ?? "aucun"}",
            $"[b]Tags IA[/b] · {string.Join(" · ", proposal.Orchestration.AiTags)}",
            "[b]Tests[/b]",
        };
        lines.AddRange(proposal.Tests.Select(item => $"• {item.Kind} · {(item.Passed ? "PASS" : "ÉCHEC")} · {item.Evidence}"));
        lines.Add($"[b]Équilibrage[/b] · {proposal.Balance.Notes} · {(proposal.Balance.Passed ? "PASS" : "ÉCHEC")}");
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
    private static SpinBox Spin(double min, double max, double value, double step = 1) =>
        new() { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(120, 0) };
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
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10)); parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 13);
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
