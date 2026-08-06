#if TOOLS
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedForgeScriptWindow : Window
{
    private Func<ProjectSession?>? _getSession;
    private Func<ProjectState?>? _getProject;
    private Func<LoreIndexState?>? _getLore;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<AssistedForgeScriptProposal, Guid, Guid, bool, bool>? _apply;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Action<Window>? _brandWindow;
    private readonly NativeForgeScriptRuntime _runtime = new();
    private readonly System.Net.Http.HttpClient _httpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private AssistedForgeScriptProposal? _proposal;
    private Guid _transactionId;
    private Guid _executionPlanId;
    private bool _suppressSourceChange;

    private LineEdit? _path;
    private LineEdit? _binding;
    private TextEdit? _intent;
    private LineEdit? _message;
    private LineEdit? _variableKey;
    private LineEdit? _variableValue;
    private TextEdit? _style;
    private TextEdit? _citations;
    private TextEdit? _source;
    private CheckButton? _approve;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _generateButton;
    private Button? _applyButton;

    public bool LastCompiled { get; private set; }
    public int LastTestCount { get; private set; }
    public int LastErrorCount { get; private set; }
    public string LastApiVersion { get; private set; } = string.Empty;
    public decimal LastEstimatedCostUsd { get; private set; }
    public string SmokeRelativePath => "scripts/assisted-g12g-smoke.forge";

    public void Initialize(
        Func<ProjectSession?> getSession,
        Func<ProjectState?> getProject,
        Func<LoreIndexState?> getLore,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<AssistedForgeScriptProposal, Guid, Guid, bool, bool> apply,
        Func<bool> undo,
        Func<bool> redo,
        Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLore = getLore ?? throw new ArgumentNullException(nameof(getLore));
        _getAssistant = getAssistant ?? throw new ArgumentNullException(nameof(getAssistant));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "ForgeScript assisté · OpenRPG Forge";
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
        _path!.Text = "scripts/interaction-assistee.forge";
        _binding!.Text = "OnUse";
        _intent!.Text = "Informer le joueur de l'interaction et enregistrer l'événement dans une variable autorisée.";
        _message!.Text = "La braise répond à votre présence.";
        _variableKey!.Text = "assistant.script.used";
        _variableValue!.Text = "true";
        _style!.Text = "style:forge-clair\nmessage bref";
        _citations!.Text = $"project:{project.Project.Id}\napi:forgescript-1.0";
        _suppressSourceChange = true;
        _source!.Text = string.Empty;
        _suppressSourceChange = false;
        _approve!.ButtonPressed = false;
        Invalidate();
        _preview!.Text = "[color=#aa94c9]Décrivez le comportement. La source, l'API, la compilation, la sandbox et les tests seront visibles avant toute écriture.[/color]";
        SetStatus($"API ForgeScript {_runtime.ApiVersion} · VM {_runtime.VirtualMachineVersion} · révision {project.Revision}.", false);
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        Refresh();
        _path!.Text = SmokeRelativePath;
        _message!.Text = "ForgeScript G12-G prêt.";
        _approve!.ButtonPressed = true;
        return true;
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
        var header = new Label { Text = "CRÉATION & RÉPARATION FORGESCRIPT", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "API autorisée, source éditable, compilation native et sandbox · seuls les changements approuvés et verts sont écrits.",
        });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);

        var brief = AddCard(body, 390);
        brief.AddChild(Section("BRIEF & BINDING"));
        _path = AddLine(brief, "Chemin", "scripts/interaction.forge");
        _binding = AddLine(brief, "Événement", "OnUse");
        brief.AddChild(FieldLabel("INTENTION"));
        _intent = AddText(brief, 100);
        _message = AddLine(brief, "Message", "Message localisable");
        _variableKey = AddLine(brief, "Variable", "assistant.script.used");
        _variableValue = AddLine(brief, "Valeur", "true");
        brief.AddChild(FieldLabel("RÈGLES DE STYLE"));
        _style = AddText(brief, 90);
        brief.AddChild(FieldLabel("CITATIONS"));
        _citations = AddText(brief, 90);
        _approve = new CheckButton { Text = "Approuver pour application" };
        _approve.Toggled += _ => Invalidate();
        brief.AddChild(_approve);

        var sourceCard = AddCard(body, 500, true);
        sourceCard.AddChild(Section("SOURCE PROPOSÉE OU RÉPARÉE"));
        sourceCard.AddChild(new Label
        {
            Text = "Vous pouvez corriger la source ici puis relancer la validation. Le fichier du module reste intact jusqu'à l'application.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _source = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 580),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        _source.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8));
        _source.TextChanged += () => { if (!_suppressSourceChange) Invalidate(); };
        sourceCard.AddChild(_source);

        var result = AddCard(body, 390);
        result.AddChild(Section("COMPILATION & TESTS"));
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
        var undo = new Button { Text = "Annuler script" };
        undo.Pressed += () => { if (_undo?.Invoke() == true) SetStatus("Source précédente restaurée.", false); };
        footer.AddChild(undo);
        var redo = new Button { Text = "Rétablir script" };
        redo.Pressed += () => { if (_redo?.Invoke() == true) SetStatus("Source validée rétablie.", false); };
        footer.AddChild(redo);
        _generateButton = new Button { Text = "Compiler & tester" };
        _generateButton.Pressed += async () => await GenerateAsync();
        footer.AddChild(_generateButton);
        _applyButton = new Button { Text = "Appliquer la source", Disabled = true };
        _applyButton.Pressed += Apply;
        footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" };
        close.Pressed += Hide;
        footer.AddChild(close);
        foreach (var line in new[] { _path, _binding, _message, _variableKey, _variableValue })
            line.TextChanged += _ => Invalidate();
        foreach (var text in new[] { _intent, _style, _citations })
            text.TextChanged += Invalidate;
    }

    private async Task<bool> GenerateAsync()
    {
        var session = _getSession?.Invoke();
        var project = _getProject?.Invoke();
        var lore = _getLore?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (session is null || project is null || lore is null || assistant is null)
        {
            SetStatus("Projet, lore ou provider indisponible.", true);
            return false;
        }
        _generateButton!.Disabled = true;
        _applyButton!.Disabled = true;
        try
        {
            var sourceOverride = string.IsNullOrWhiteSpace(_source!.Text) ? null : _source.Text;
            var brief = new AssistedForgeScriptBrief(
                Guid.NewGuid(),
                _path!.Text,
                _binding!.Text,
                _intent!.Text,
                _message!.Text,
                _variableKey!.Text,
                _variableValue!.Text,
                Lines(_citations!.Text),
                Lines(_style!.Text),
                "@toolset-author",
                20260806,
                sourceOverride);
            var providerDefinition = assistant.Profile.ActiveProvider;
            var registry = providerDefinition.Kind == AssistantProviderKind.DeterministicOffline
                ? new AssistantProviderRegistry()
                : AssistantProviderRegistry.CreateWithLocalTransports(_httpClient, requestTimeout: TimeSpan.FromSeconds(30));
            var proposal = await AssistedForgeScriptCreationService.ProposeAsync(
                brief,
                session.RootDirectory,
                project.Project,
                lore.Index,
                registry.Resolve(providerDefinition),
                providerDefinition,
                _runtime,
                assistant.Profile.NetworkAllowed,
                assistant.Profile.ShareProjectContent);
            _proposal = proposal.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-g" : proposal.Provenance);
            _transactionId = Guid.NewGuid();
            _executionPlanId = Guid.NewGuid();
            _suppressSourceChange = true;
            _source.Text = proposal.Source;
            _suppressSourceChange = false;
            if (_proposal.ApprovedForApplication &&
                _apply?.Invoke(_proposal, _transactionId, _executionPlanId, true) != true)
            {
                _proposal = null;
                SetStatus("Le dry-run transactionnel ForgeScript a été refusé.", true);
                return false;
            }
            LastCompiled = proposal.Tests.Any(item => item.Id == "compile" && item.Passed);
            LastTestCount = proposal.Tests.Count;
            LastErrorCount = proposal.Diagnostics.Count(item => item.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
            LastApiVersion = proposal.ForgeScriptApiVersion;
            LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            _preview!.Text = Render(_proposal);
            _applyButton.Disabled = !_proposal.ApprovedForApplication || !_proposal.CanApply;
            SetStatus(_proposal.CanApply
                ? $"Source prête · API {LastApiVersion} · {LastTestCount} preuves vertes · coût {LastEstimatedCostUsd:0.0000} USD."
                : $"Source refusée · {LastErrorCount} erreur(s) · corrigez puis relancez.", !_proposal.CanApply);
            return _proposal.CanApply;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or AssistantProviderTransportException)
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
        if (_proposal is null || !_proposal.ApprovedForApplication || !_proposal.CanApply) return;
        if (_apply?.Invoke(_proposal, _transactionId, _executionPlanId, false) == true)
        {
            _proposal = null;
            _applyButton!.Disabled = true;
            SetStatus("ForgeScript appliqué · annuler/rétablir restent disponibles dans cette fenêtre.", false);
        }
    }

    private void Invalidate()
    {
        _proposal = null;
        if (_applyButton is not null) _applyButton.Disabled = true;
        LastCompiled = false;
        LastTestCount = 0;
        LastErrorCount = 0;
        LastApiVersion = string.Empty;
        LastEstimatedCostUsd = 0;
    }

    private static string Render(AssistedForgeScriptProposal proposal)
    {
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{proposal.RelativePath}[/b][/color] · {proposal.EventBinding}",
            $"API {proposal.ForgeScriptApiVersion} · VM {proposal.VirtualMachineVersion}",
            $"Provider {proposal.ProviderKey} · coût {proposal.EstimatedCostUsd:0.0000} USD",
            $"[color=#9ee6b3]Approbation : {(proposal.ApprovedForApplication ? "OUI" : "NON")} · application : NON[/color]",
            "",
            "[b]Tests[/b]",
        };
        lines.AddRange(proposal.Tests.Select(test => $"{(test.Passed ? "✓" : "✗")} {test.Id} · {test.Detail}"));
        lines.Add("");
        lines.Add($"[b]Diagnostics[/b] · {(proposal.Diagnostics.Count == 0 ? "aucun" : proposal.Diagnostics.Count)}");
        lines.AddRange(proposal.Diagnostics.Select(item =>
            $"• {item.Severity} · {item.Source}:{item.Line}:{item.Column} · {item.Message}"));
        lines.Add($"[b]Citations[/b] · {string.Join(" · ", proposal.Citations)}");
        lines.Add($"[b]Provenance[/b] · {proposal.Provenance}");
        lines.Add($"[b]Hash source[/b] · {proposal.SourceSha256}");
        lines.Add($"[b]Diff[/b] · {proposal.SemanticDiff}");
        return string.Join('\n', lines);
    }

    private void SetStatus(string text, bool error)
    {
        _status!.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8e9d" : "9ee6b3"));
    }
    private static IReadOnlyList<string> Lines(string value) => value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static TextEdit AddText(Container parent, float height) { var text = new TextEdit { CustomMinimumSize = new Vector2(0, height), WrapMode = TextEdit.LineWrappingMode.Boundary }; parent.AddChild(text); return text; }
    private static Label Section(string text) { var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new Color("ffb276")); return label; }
    private static Label FieldLabel(string text) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", 11); label.AddThemeColorOverride("font_color", new Color("9f8dbd")); return label; }
    private static LineEdit AddLine(Container parent, string label, string placeholder) { var line = new LineEdit { PlaceholderText = placeholder }; var column = new VBoxContainer(); column.AddChild(FieldLabel(label.ToUpperInvariant())); column.AddChild(line); parent.AddChild(column); return line; }
    private static VBoxContainer AddCard(Container parent, float width, bool expand = false)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0), SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Card("171221", "47365f", 1, 10)); parent.AddChild(panel);
        var margin = new MarginContainer(); foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 13); panel.AddChild(margin);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 8); margin.AddChild(content); return content;
    }
    private static StyleBoxFlat Card(string background, string border, int width, int radius) => new() { BgColor = new Color(background), BorderColor = new Color(border), BorderWidthLeft = width, BorderWidthTop = width, BorderWidthRight = width, BorderWidthBottom = width, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
}
#endif
