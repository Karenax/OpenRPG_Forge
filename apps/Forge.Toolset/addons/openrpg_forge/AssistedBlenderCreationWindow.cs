#if TOOLS
using System.Globalization;
using Forge.Application.Assets;
using Forge.Application.Assistant;
using Forge.Application.Projects;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Workspaces;
using Godot;

namespace Forge.Toolset.Editor;

public partial class AssistedBlenderCreationWindow : Window
{
    private Func<ProjectSession?>? _getSession;
    private Func<ProjectState?>? _getProject;
    private Func<LoreIndexState?>? _getLore;
    private Func<AssistantWorkspaceProfileState?>? _getAssistant;
    private Func<AssetExternalEditState?>? _getExternalEdits;
    private Func<AssistedBlenderCreationProposal, Guid, Guid, bool, bool>? _apply;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Action<Window>? _brandWindow;
    private readonly System.Net.Http.HttpClient _httpClient = new(
        new System.Net.Http.SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };
    private AssistedBlenderCreationProposal? _proposal;
    private Guid _transactionId;
    private Guid _executionPlanId;

    private LineEdit? _slug;
    private LineEdit? _displayName;
    private LineEdit? _primitiveName;
    private OptionButton? _primitiveKind;
    private SpinBox? _positionX;
    private SpinBox? _positionY;
    private SpinBox? _positionZ;
    private SpinBox? _rotationX;
    private SpinBox? _rotationY;
    private SpinBox? _rotationZ;
    private SpinBox? _scaleX;
    private SpinBox? _scaleY;
    private SpinBox? _scaleZ;
    private LineEdit? _materialName;
    private SpinBox? _red;
    private SpinBox? _green;
    private SpinBox? _blue;
    private SpinBox? _metallic;
    private SpinBox? _roughness;
    private OptionButton? _uv;
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
    public string LastRecipeRelativePath { get; private set; } = string.Empty;
    public string LastWorkerRequestRelativePath { get; private set; } = string.Empty;

    public void Initialize(
        Func<ProjectSession?> getSession,
        Func<ProjectState?> getProject,
        Func<LoreIndexState?> getLore,
        Func<AssistantWorkspaceProfileState?> getAssistant,
        Func<AssetExternalEditState?> getExternalEdits,
        Func<AssistedBlenderCreationProposal, Guid, Guid, bool, bool> apply,
        Func<bool> undo,
        Func<bool> redo,
        Action<Window> brandWindow)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getLore = getLore ?? throw new ArgumentNullException(nameof(getLore));
        _getAssistant = getAssistant ?? throw new ArgumentNullException(nameof(getAssistant));
        _getExternalEdits = getExternalEdits ?? throw new ArgumentNullException(nameof(getExternalEdits));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Création 3D assistée · OpenRPG Forge";
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
        var session = _getSession?.Invoke();
        if (session is null || _getProject?.Invoke() is null || _getLore?.Invoke() is null ||
            _getAssistant?.Invoke() is null || _getExternalEdits?.Invoke() is null)
        {
            SetStatus("Projet, lore, profil assistant ou état des assets indisponible.", true);
            return;
        }
        _slug!.Text = "pilier-forge";
        _displayName!.Text = "Pilier Forge";
        _primitiveName!.Text = "pillar";
        _primitiveKind!.Selected = (int)BlenderPrimitiveKind.Cylinder;
        _positionX!.Value = 0; _positionY!.Value = 0; _positionZ!.Value = 1;
        _rotationX!.Value = 0; _rotationY!.Value = 0; _rotationZ!.Value = 0;
        _scaleX!.Value = 1; _scaleY!.Value = 1; _scaleZ!.Value = 2;
        _materialName!.Text = "forge_stone";
        _red!.Value = .32; _green!.Value = .24; _blue!.Value = .18;
        _metallic!.Value = .05; _roughness!.Value = .72;
        _uv!.Selected = (int)BlenderUvProjection.Cube;
        _lods!.Text = "0.5, 0.25";
        _collision!.ButtonPressed = true;
        _style!.Text = "style:forge-clair\nforme:lisible\nperformance:jeu-temps-réel";
        _citations!.Text = $"project:{_getProject()!.Project.Id}\nlore:world-bible";
        _seed!.Value = 20260806;
        _approve!.ButtonPressed = false;
        Invalidate();
        _preview!.Text = "[color=#aa94c9]Décrivez une forme bornée. Forge produira une recette canonique, puis une requête Blender typée sans Python libre.[/color]";
        SetStatus($"Prêt · {session.RootDirectory}", false);
    }

    public bool ConfigureApprovedForSmokeTest()
    {
        Refresh();
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
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);
        var header = new Label { Text = "CRÉATION 3D ASSISTÉE", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(header);
        root.AddChild(new Label { Text = "Primitives, transforms, matériau PBR, UV, LOD et collision · recette canonique avant Blender." });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); root.AddChild(body);

        var shape = AddCard(body, 390);
        shape.AddChild(Section("FORME & TRANSFORMS"));
        _slug = AddLine(shape, "Slug", "pilier-forge");
        _displayName = AddLine(shape, "Nom", "Pilier Forge");
        _primitiveName = AddLine(shape, "Primitive", "pillar");
        _primitiveKind = new OptionButton();
        foreach (var value in Enum.GetValues<BlenderPrimitiveKind>()) _primitiveKind.AddItem(value.ToString());
        shape.AddChild(Field("Type", _primitiveKind));
        var transforms = new GridContainer { Columns = 3 };
        _positionX = Spin(-1000, 1000, 0); _positionY = Spin(-1000, 1000, 0); _positionZ = Spin(-1000, 1000, 1);
        _rotationX = Spin(-360, 360, 0); _rotationY = Spin(-360, 360, 0); _rotationZ = Spin(-360, 360, 0);
        _scaleX = Spin(.001, 1000, 1, .05); _scaleY = Spin(.001, 1000, 1, .05); _scaleZ = Spin(.001, 1000, 2, .05);
        foreach (var (label, control) in new[]
        {
            ("Position X", _positionX), ("Y", _positionY), ("Z", _positionZ),
            ("Rotation X", _rotationX), ("Y", _rotationY), ("Z", _rotationZ),
            ("Échelle X", _scaleX), ("Y", _scaleY), ("Z", _scaleZ),
        }) transforms.AddChild(Field(label, control));
        shape.AddChild(transforms);

        var surface = AddCard(body, 390);
        surface.AddChild(Section("MATÉRIAU & PRODUCTION"));
        _materialName = AddLine(surface, "Matériau", "forge_stone");
        var color = new GridContainer { Columns = 3 };
        _red = Spin(0, 1, .32, .01); _green = Spin(0, 1, .24, .01); _blue = Spin(0, 1, .18, .01);
        color.AddChild(Field("Rouge", _red)); color.AddChild(Field("Vert", _green)); color.AddChild(Field("Bleu", _blue));
        surface.AddChild(color);
        var pbr = new GridContainer { Columns = 2 };
        _metallic = Spin(0, 1, .05, .01); _roughness = Spin(0, 1, .72, .01);
        pbr.AddChild(Field("Métallique", _metallic)); pbr.AddChild(Field("Rugosité", _roughness)); surface.AddChild(pbr);
        _uv = new OptionButton(); foreach (var value in Enum.GetValues<BlenderUvProjection>()) _uv.AddItem(value.ToString());
        surface.AddChild(Field("Projection UV", _uv));
        _lods = AddLine(surface, "Ratios LOD", "0.5, 0.25");
        _collision = new CheckButton { Text = "Générer une collision" }; surface.AddChild(_collision);
        surface.AddChild(FieldLabel("RÈGLES DE STYLE")); _style = AddText(surface, 92);
        surface.AddChild(FieldLabel("CITATIONS")); _citations = AddText(surface, 76);
        _seed = Spin(0, int.MaxValue, 20260806); surface.AddChild(Field("Seed", _seed));
        _approve = new CheckButton { Text = "Approuver cette création 3D" }; surface.AddChild(_approve);

        var result = AddCard(body, 550, true); result.AddChild(Section("PLAN, TESTS & DIFF"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 630)
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8)); result.AddChild(_preview);

        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 8); root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        var undo = new Button { Text = "Annuler" }; undo.Pressed += () => { if (_undo?.Invoke() == true) Refresh(); }; footer.AddChild(undo);
        var redo = new Button { Text = "Rétablir" }; redo.Pressed += () => { if (_redo?.Invoke() == true) Refresh(); }; footer.AddChild(redo);
        _previewButton = new Button { Text = "Prévisualiser la recette" }; _previewButton.Pressed += async () => await GenerateAsync(); footer.AddChild(_previewButton);
        _applyButton = new Button { Text = "Appliquer et préparer Blender", Disabled = true }; _applyButton.Pressed += Apply; footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" }; close.Pressed += Hide; footer.AddChild(close);
        foreach (var line in new[] { _slug, _displayName, _primitiveName, _materialName, _lods }) line.TextChanged += _ => Invalidate();
        foreach (var text in new[] { _style, _citations }) text.TextChanged += Invalidate;
        foreach (var spin in new[] { _positionX, _positionY, _positionZ, _rotationX, _rotationY, _rotationZ,
                     _scaleX, _scaleY, _scaleZ, _red, _green, _blue, _metallic, _roughness, _seed })
            spin.ValueChanged += _ => Invalidate();
        foreach (var option in new[] { _primitiveKind, _uv }) option.ItemSelected += _ => Invalidate();
        _collision.Toggled += _ => Invalidate(); _approve.Toggled += _ => Invalidate();
    }

    private async Task<bool> GenerateAsync()
    {
        var session = _getSession?.Invoke(); var project = _getProject?.Invoke(); var lore = _getLore?.Invoke();
        var assistant = _getAssistant?.Invoke();
        if (session is null || project is null || lore is null || assistant is null)
        {
            SetStatus("Contexte Forge incomplet.", true); return false;
        }
        _previewButton!.Disabled = true; _applyButton!.Disabled = true;
        try
        {
            var brief = new AssistedBlenderCreationBrief(Guid.NewGuid(), _slug!.Text, _displayName!.Text,
                [new BlenderPrimitiveDefinition(Guid.NewGuid(), _primitiveName!.Text,
                    Enum.GetValues<BlenderPrimitiveKind>()[Math.Clamp(_primitiveKind!.Selected, 0, Enum.GetValues<BlenderPrimitiveKind>().Length - 1)],
                    Vector(_positionX!, _positionY!, _positionZ!), Vector(_rotationX!, _rotationY!, _rotationZ!),
                    Vector(_scaleX!, _scaleY!, _scaleZ!))],
                new BlenderMaterialDefinition(_materialName!.Text,
                    new BlenderColor(Decimal(_red!), Decimal(_green!), Decimal(_blue!), 1m),
                    Decimal(_metallic!), Decimal(_roughness!)),
                Enum.GetValues<BlenderUvProjection>()[Math.Clamp(_uv!.Selected, 0, Enum.GetValues<BlenderUvProjection>().Length - 1)],
                ParseLods(_lods!.Text), _collision!.ButtonPressed, Lines(_citations!.Text), Lines(_style!.Text),
                "@toolset-author", checked((int)_seed!.Value));
            var providerDefinition = assistant.Profile.ActiveProvider;
            var registry = providerDefinition.Kind == AssistantProviderKind.DeterministicOffline
                ? new AssistantProviderRegistry()
                : AssistantProviderRegistry.CreateWithLocalTransports(_httpClient, requestTimeout: TimeSpan.FromSeconds(30));
            var proposal = await AssistedBlenderCreationService.ProposeAsync(brief, session.RootDirectory,
                project.Project, lore.Index, registry.Resolve(providerDefinition), providerDefinition,
                assistant.Profile.NetworkAllowed, assistant.Profile.ShareProjectContent);
            _proposal = proposal.WithApproval(_approve!.ButtonPressed,
                _approve.ButtonPressed ? "author-approved:toolset:g12-j" : proposal.Provenance);
            _transactionId = Guid.NewGuid(); _executionPlanId = Guid.NewGuid();
            if (_proposal.ApprovedForApplication && _apply?.Invoke(_proposal, _transactionId, _executionPlanId, true) != true)
            {
                _proposal = null; SetStatus("Le dry-run transactionnel a été refusé.", true); return false;
            }
            LastTestCount = proposal.Tests.Count; LastPassedTestCount = proposal.Tests.Count(item => item.Passed);
            LastDiagnosticCount = proposal.Diagnostics.Count; LastEstimatedCostUsd = proposal.EstimatedCostUsd;
            LastRecipeRelativePath = proposal.RecipeRelativePath;
            LastWorkerRequestRelativePath = $"assets/roundtrip/{proposal.Configuration.AssetId}.worker-request.json";
            _preview!.Text = Render(_proposal); _applyButton.Disabled = !_proposal.ApprovedForApplication || !_proposal.CanApply;
            SetStatus(_proposal.CanApply
                ? $"Recette prête · {LastPassedTestCount}/{LastTestCount} tests · coût {LastEstimatedCostUsd:0.0000} USD."
                : "Création refusée : corrigez les diagnostics.", !_proposal.CanApply);
            return _proposal.CanApply;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or AssistantProviderTransportException)
        {
            SetStatus(exception.Message, true); return false;
        }
        finally { _previewButton.Disabled = false; }
    }

    private void Apply()
    {
        if (_proposal is null || !_proposal.ApprovedForApplication || !_proposal.CanApply) return;
        if (_apply?.Invoke(_proposal, _transactionId, _executionPlanId, false) == true)
        {
            _proposal = null; _applyButton!.Disabled = true;
            SetStatus("Recette et requête Blender enregistrées · annulation/rétablissement disponibles.", false);
        }
    }

    private void Invalidate()
    {
        _proposal = null; if (_applyButton is not null) _applyButton.Disabled = true;
        LastTestCount = LastPassedTestCount = LastDiagnosticCount = 0; LastEstimatedCostUsd = 0;
    }

    private static string Render(AssistedBlenderCreationProposal proposal)
    {
        var definition = proposal.Definition;
        var lines = new List<string>
        {
            $"[color=#ffb276][b]{definition.DisplayName}[/b][/color] · {definition.Slug}",
            $"Provider {proposal.ProviderKey} · coût {proposal.EstimatedCostUsd:0.0000} USD",
            $"[color=#9ee6b3]Approbation : {(proposal.ApprovedForApplication ? "OUI" : "NON")} · mutation : NON[/color]", "",
            $"[b]Recette[/b] · {proposal.RecipeRelativePath} · SHA-256 {proposal.RecipeSha256[..12]}…",
            $"[b]Primitive[/b] · {string.Join(" · ", definition.Primitives.Select(item => $"{item.Name}:{item.Kind}"))}",
            $"[b]Transform[/b] · position/rotation/échelle bornées en mètres",
            $"[b]Matériau PBR[/b] · {definition.Material.Name} · métallique {definition.Material.Metallic:0.##} · rugosité {definition.Material.Roughness:0.##}",
            $"[b]UV / LOD / collision[/b] · {definition.UvProjection} · {string.Join(", ", definition.LodRatios)} · {definition.GenerateCollision}",
            $"[b]Sortie[/b] · {definition.OutputRelativePath}", "", "[b]Tests[/b]",
        };
        lines.AddRange(proposal.Tests.Select(item => $"• {item.Id} · {(item.Passed ? "PASS" : "ÉCHEC")} · {item.Detail}"));
        lines.Add($"[b]Diagnostics[/b] · {(proposal.Diagnostics.Count == 0 ? "aucun" : string.Join(" · ", proposal.Diagnostics))}");
        lines.Add($"[b]Diff[/b] · {proposal.SemanticDiff}");
        lines.Add("[b]Sécurité[/b] · opérations Blender typées uniquement · Python libre interdit");
        return string.Join('\n', lines);
    }

    private void SetStatus(string text, bool error)
    {
        _status!.Text = text; _status.AddThemeColorOverride("font_color", new Color(error ? "ff8e9d" : "9ee6b3"));
    }

    private static decimal Decimal(SpinBox value) => Convert.ToDecimal(value.Value, CultureInfo.InvariantCulture);
    private static BlenderVector3 Vector(SpinBox x, SpinBox y, SpinBox z) => new(Decimal(x), Decimal(y), Decimal(z));
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
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin
        };
        panel.AddThemeStyleboxOverride("panel", Card("171123", "4b3764", 1, 10)); parent.AddChild(panel);
        var margin = new MarginContainer(); foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 12); panel.AddChild(margin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 7); margin.AddChild(column); return column;
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
            CornerRadiusBottomRight = radius
        }; return style;
    }
}
#endif
