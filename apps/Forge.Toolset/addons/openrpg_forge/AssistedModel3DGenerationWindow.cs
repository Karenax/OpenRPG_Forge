#if TOOLS
using System.Globalization;
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedModel3DGenerationWindow : Window
{
    private Func<ProjectSession?>? _getSession;
    private Func<AssetExternalEditState?>? _getExternalEdits;
    private Func<AssistedModel3DGenerationProposal, Guid, Guid, bool, bool>? _apply;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Action<Window>? _brandWindow;
    private readonly System.Net.Http.HttpClient _httpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private AssistedModel3DGenerationProposal? _proposal;
    private Guid _transactionId;
    private Guid _executionPlanId;

    private OptionButton? _providerKind;
    private LineEdit? _endpoint;
    private LineEdit? _credentialEnvironment;
    private CheckButton? _networkAuthorized;
    private OptionButton? _inputKind;
    private LineEdit? _imagePath;
    private LineEdit? _slug;
    private LineEdit? _displayName;
    private TextEdit? _prompt;
    private SpinBox? _maximumTriangles;
    private LineEdit? _lods;
    private CheckButton? _collision;
    private TextEdit? _style;
    private TextEdit? _citations;
    private SpinBox? _seed;
    private CheckButton? _approve;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _previewButton;
    private Button? _applyButton;

    public int LastTestCount { get; private set; }
    public int LastPassedTestCount { get; private set; }
    public int LastDiagnosticCount { get; private set; }
    public decimal LastEstimatedCostUsd { get; private set; }
    public string LastSourceRelativePath { get; private set; } = string.Empty;
    public string LastReceiptRelativePath { get; private set; } = string.Empty;
    public string LastWorkerRequestRelativePath { get; private set; } = string.Empty;

    public void Initialize(
        Func<ProjectSession?> getSession,
        Func<AssetExternalEditState?> getExternalEdits,
        Func<AssistedModel3DGenerationProposal, Guid, Guid, bool, bool> apply,
        Func<bool> undo,
        Func<bool> redo,
        Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _getExternalEdits = getExternalEdits ?? throw new ArgumentNullException(nameof(getExternalEdits));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Génération de modèle 3D · OpenRPG Forge";
        MinSize = new Vector2I(1380, 860);
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
        if (_getSession?.Invoke() is null || _getExternalEdits?.Invoke() is null)
        {
            SetStatus("Projet ou état des assets indisponible.", true);
            return;
        }
        _providerKind!.Selected = 0;
        _endpoint!.Text = "http://127.0.0.1:8091/generate";
        _credentialEnvironment!.Text = string.Empty;
        _networkAuthorized!.ButtonPressed = false;
        _inputKind!.Selected = 0;
        _imagePath!.Text = string.Empty;
        _slug!.Text = "idole-forge";
        _displayName!.Text = "Idole Forge";
        _prompt!.Text = "Une idole de pierre compacte, lisible en vue trois-quarts, aux volumes robustes.";
        _maximumTriangles!.Value = 10_000;
        _lods!.Text = "0.5, 0.25";
        _collision!.ButtonPressed = true;
        _style!.Text = "style:lisible\nsilhouette:forte\nperformance:temps-reel";
        _citations!.Text = "lore:identite-du-module\nart-direction:openrpg-forge";
        _seed!.Value = 20260806;
        _approve!.ButtonPressed = false;
        Invalidate();
        SetStatus("Choisissez un fournisseur, prévisualisez, puis acceptez explicitement le résultat.", false);
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        Refresh();
        _slug!.Text = "g12k-provider-idol";
        _displayName!.Text = "G12K Provider Idol";
        _approve!.ButtonPressed = true;
        return _getSession?.Invoke() is not null && _getExternalEdits?.Invoke() is not null;
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
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);
        var header = new Label { Text = "GÉNÉRATION DE MODÈLE 3D", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "Texte ou image vers OBJ en staging · licence, provenance, topologie et acceptation avant Blender."
        });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); root.AddChild(body);

        var source = AddCard(body, 390);
        source.AddChild(Section("FOURNISSEUR & SOURCE"));
        _providerKind = new OptionButton();
        foreach (var value in Enum.GetValues<Model3DProviderKind>()) _providerKind.AddItem(value.ToString());
        source.AddChild(Field("Fournisseur", _providerKind));
        _endpoint = AddLine(source, "Endpoint loopback", "http://127.0.0.1:8091/generate");
        _credentialEnvironment = AddLine(source, "Variable d’environnement du jeton", "OPENRPG_TRELLIS_TOKEN");
        _networkAuthorized = new CheckButton { Text = "Autoriser ce provider local pour cette prévisualisation" };
        source.AddChild(_networkAuthorized);
        _inputKind = new OptionButton();
        foreach (var value in Enum.GetValues<Model3DInputKind>()) _inputKind.AddItem(value.ToString());
        source.AddChild(Field("Entrée", _inputKind));
        _imagePath = AddLine(source, "Image relative au projet", "assets/references/idole.png");
        _slug = AddLine(source, "Slug", "idole-forge");
        _displayName = AddLine(source, "Nom", "Idole Forge");
        source.AddChild(FieldLabel("PROMPT")); _prompt = AddText(source, 125);

        var policy = AddCard(body, 390);
        policy.AddChild(Section("GOUVERNANCE & TOPOLOGIE"));
        _maximumTriangles = Spin(12, 500_000, 10_000, 100); policy.AddChild(Field("Triangles maximum", _maximumTriangles));
        _lods = AddLine(policy, "Ratios LOD", "0.5, 0.25");
        _collision = new CheckButton { Text = "Générer une collision" }; policy.AddChild(_collision);
        policy.AddChild(FieldLabel("RÈGLES DE STYLE")); _style = AddText(policy, 100);
        policy.AddChild(FieldLabel("CITATIONS")); _citations = AddText(policy, 85);
        _seed = Spin(0, int.MaxValue, 20260806); policy.AddChild(Field("Seed déterministe", _seed));
        _approve = new CheckButton { Text = "J’accepte la source CC0 et sa provenance" }; policy.AddChild(_approve);
        policy.AddChild(new Label
        {
            Text = "Les fournisseurs distants sont refusés. TRELLIS et TripoSR doivent exposer un endpoint HTTP loopback explicite.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var result = AddCard(body, 550, true); result.AddChild(Section("PLAN, TESTS, PROVENANCE & DIFF"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 630),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8)); result.AddChild(_preview);

        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 8); root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        var undo = new Button { Text = "Annuler" }; undo.Pressed += () => { if (_undo?.Invoke() == true) Refresh(); }; footer.AddChild(undo);
        var redo = new Button { Text = "Rétablir" }; redo.Pressed += () => { if (_redo?.Invoke() == true) Refresh(); }; footer.AddChild(redo);
        _previewButton = new Button { Text = "Prévisualiser sans écrire" }; _previewButton.Pressed += async () => await GenerateAsync(); footer.AddChild(_previewButton);
        _applyButton = new Button { Text = "Accepter et préparer Blender", Disabled = true }; _applyButton.Pressed += Apply; footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" }; close.Pressed += Hide; footer.AddChild(close);

        foreach (var line in new[] { _endpoint, _credentialEnvironment, _imagePath, _slug, _displayName, _lods })
            line.TextChanged += _ => Invalidate();
        foreach (var text in new[] { _prompt, _style, _citations }) text.TextChanged += Invalidate;
        foreach (var option in new[] { _providerKind, _inputKind }) option.ItemSelected += _ => Invalidate();
        foreach (var spin in new[] { _maximumTriangles, _seed }) spin.ValueChanged += _ => Invalidate();
        foreach (var toggle in new[] { _networkAuthorized, _collision, _approve }) toggle.Toggled += _ => Invalidate();
    }

    private async Task<bool> GenerateAsync()
    {
        var session = _getSession?.Invoke();
        if (session is null || _getExternalEdits?.Invoke() is null)
        {
            SetStatus("Contexte Forge incomplet.", true); return false;
        }
        _previewButton!.Disabled = true; _applyButton!.Disabled = true;
        try
        {
            var providerKind = Enum.GetValues<Model3DProviderKind>()[Math.Clamp(_providerKind!.Selected, 0,
                Enum.GetValues<Model3DProviderKind>().Length - 1)];
            var definition = ProviderDefinition(providerKind);
            IModel3DGenerationProvider provider = providerKind == Model3DProviderKind.DeterministicOffline
                ? new DeterministicModel3DGenerationProvider()
                : new LoopbackModel3DGenerationProvider(definition, _httpClient, System.Environment.GetEnvironmentVariable);
            var inputKind = Enum.GetValues<Model3DInputKind>()[Math.Clamp(_inputKind!.Selected, 0,
                Enum.GetValues<Model3DInputKind>().Length - 1)];
            var brief = new AssistedModel3DGenerationBrief(
                Guid.NewGuid(), _slug!.Text, _displayName!.Text, inputKind, _prompt!.Text,
                inputKind == Model3DInputKind.Image ? _imagePath!.Text : null, definition,
                new Model3DTopologyPolicy(checked((int)_maximumTriangles!.Value), ParseLods(_lods!.Text),
                    _collision!.ButtonPressed), Lines(_citations!.Text), Lines(_style!.Text), "@toolset-author",
                checked((int)_seed!.Value));
            var proposal = await AssistedModel3DGenerationService.ProposeAsync(
                brief, session.RootDirectory, provider, _networkAuthorized!.ButtonPressed);
            _proposal = proposal.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-k" : string.Empty);
            _transactionId = Guid.NewGuid(); _executionPlanId = Guid.NewGuid();
            if (_proposal.ApprovedForApplication &&
                _apply?.Invoke(_proposal, _transactionId, _executionPlanId, true) != true)
            {
                _proposal = null; SetStatus("Le dry-run transactionnel a été refusé.", true); return false;
            }
            LastTestCount = proposal.Tests.Count;
            LastPassedTestCount = proposal.Tests.Count(item => item.Passed);
            LastDiagnosticCount = _proposal.Diagnostics.Count;
            LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            LastSourceRelativePath = proposal.Receipt.SourceRelativePath;
            LastReceiptRelativePath = proposal.ReceiptRelativePath;
            LastWorkerRequestRelativePath = $"assets/roundtrip/{proposal.Configuration.AssetId}.worker-request.json";
            _preview!.Text = Render(_proposal);
            _applyButton.Disabled = !_proposal.CanApply;
            SetStatus(_proposal.CanApply
                ? $"Source prête · {LastPassedTestCount}/{LastTestCount} tests · coût {LastEstimatedCostUsd:0.0000} USD."
                : "Prévisualisation prête, mais l’acceptation humaine reste requise.", false);
            return _proposal.CanApply;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Model3DProviderException)
        {
            SetStatus(exception.Message, true); return false;
        }
        finally { _previewButton.Disabled = false; }
    }

    private Model3DProviderDefinition ProviderDefinition(Model3DProviderKind kind)
    {
        if (kind == Model3DProviderKind.DeterministicOffline) return Model3DProviderDefinition.CreateOffline();
        var key = kind == Model3DProviderKind.TrellisLocal ? "trellis-local" : "triposr-local";
        return new(1, Guid.NewGuid(), key, kind, _endpoint!.Text.Trim(),
            string.IsNullOrWhiteSpace(_credentialEnvironment!.Text) ? null : _credentialEnvironment.Text.Trim(),
            true, 120, 64L * 1024 * 1024,
            new HashSet<string>(["CC0-1.0"], StringComparer.Ordinal), $"toolset:model3d-provider:{key}");
    }

    private void Apply()
    {
        if (_proposal is null || !_proposal.CanApply) return;
        if (_apply?.Invoke(_proposal, _transactionId, _executionPlanId, false) == true)
        {
            _proposal = null; _applyButton!.Disabled = true;
            SetStatus("OBJ, reçu, configuration et requête Blender enregistrés atomiquement.", false);
        }
    }

    private void Invalidate()
    {
        _proposal = null; if (_applyButton is not null) _applyButton.Disabled = true;
        LastTestCount = LastPassedTestCount = LastDiagnosticCount = 0; LastEstimatedCostUsd = 0;
    }

    private static string Render(AssistedModel3DGenerationProposal proposal)
    {
        var receipt = proposal.Receipt;
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{receipt.DisplayName}[/b][/color] · {receipt.Slug}",
            $"Provider {receipt.Provider.Key} ({receipt.Provider.Kind}) · coût {proposal.EstimatedCostUsd:0.0000} USD",
            $"[color=#9ee6b3]Acceptation : {(receipt.HumanAccepted ? "OUI" : "NON")} · mutation : NON[/color]", "",
            $"[b]Entrée[/b] · {receipt.InputKind} · prompt SHA-256 {receipt.PromptSha256[..12]}…",
            $"[b]Staging[/b] · {receipt.SourceRelativePath} · {receipt.SourceBytes} octets",
            $"[b]Licence[/b] · {receipt.LicenseSpdx} · provenance {receipt.ProviderProvenance}",
            $"[b]Topologie[/b] · {receipt.SourceTriangleCount}/{receipt.Topology.MaximumSourceTriangles} triangles · LOD {string.Join(", ", receipt.Topology.LodRatios)}",
            $"[b]Sortie[/b] · {receipt.OutputRelativePath}", "", "[b]Tests[/b]",
        };
        lines.AddRange(proposal.Tests.Select(item => $"• {item.Id} · {(item.Passed ? "PASS" : "ÉCHEC")} · {item.Detail}"));
        lines.Add($"[b]Diagnostics[/b] · {(proposal.Diagnostics.Count == 0 ? "aucun" : string.Join(" · ", proposal.Diagnostics))}");
        lines.Add($"[b]Diff[/b] · {proposal.SemanticDiff}");
        lines.Add("[b]Sécurité[/b] · endpoint loopback · secret par environnement · CC0 · Blender typé · aucun Python libre");
        return string.Join('\n', lines);
    }

    private void SetStatus(string text, bool error)
    {
        _status!.Text = text; _status.AddThemeColorOverride("font_color", new Color(error ? "ff8e9d" : "9ee6b3"));
    }

    private static IReadOnlyList<decimal> ParseLods(string value) => value
        .Split([',', ';', ' '], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(item => decimal.Parse(item, NumberStyles.Number, CultureInfo.InvariantCulture)).ToArray();
    private static IReadOnlyList<string> Lines(string value) =>
        value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static SpinBox Spin(double min, double max, double value, double step = 1) =>
        new() { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(100, 0) };
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
        column.AddChild(FieldLabel(label)); column.AddChild(control); return column;
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
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
        };
        panel.AddThemeStyleboxOverride("panel", Card("171123", "4b3764", 1, 10)); parent.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12);
        panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 7); margin.AddChild(column); return column;
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
