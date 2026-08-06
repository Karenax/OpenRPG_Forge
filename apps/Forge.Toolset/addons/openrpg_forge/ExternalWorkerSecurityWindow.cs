#if TOOLS
using Forge.Application.Assets;
using Forge.Domain.Assets;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ExternalWorkerSecurityWindow : Window
{
    private Func<ExternalWorkerSecurityPolicyState?>? _getState;
    private Func<ExternalWorkerSecurityPolicy, Guid, Guid, bool, bool>? _apply;
    private Func<bool>? _undo;
    private Func<bool>? _redo;
    private Action<Window>? _brandWindow;
    private SpinBox? _timeout;
    private SpinBox? _outputMiB;
    private LineEdit? _provenance;
    private RichTextLabel? _preview;
    private Label? _status;
    private Button? _applyButton;
    private readonly Dictionary<AssetExternalOperation, CheckButton> _operations = [];
    private ExternalWorkerSecurityPolicy? _approved;
    private Guid _transactionId;
    private Guid _planId;
    private bool _suppressSignals;

    public Guid LastTransactionId { get; private set; }
    public Guid LastPlanId { get; private set; }
    public decimal LastEstimatedCostUsd { get; private set; }
    public string LastSemanticDiff { get; private set; } = string.Empty;

    public void Initialize(
        Func<ExternalWorkerSecurityPolicyState?> getState,
        Func<ExternalWorkerSecurityPolicy, Guid, Guid, bool, bool> apply,
        Func<bool> undo,
        Func<bool> redo,
        Action<Window> brandWindow)
    {
        _getState = getState ?? throw new ArgumentNullException(nameof(getState));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
        _redo = redo ?? throw new ArgumentNullException(nameof(redo));
        _brandWindow = brandWindow ?? throw new ArgumentNullException(nameof(brandWindow));
        Title = "Securite des workers externes - OpenRPG Forge";
        MinSize = new Vector2I(1080, 720);
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
        var state = _getState?.Invoke();
        if (state is null)
        {
            SetStatus("Aucun projet ouvert : la politique ne peut pas etre configuree.", true);
            return;
        }
        _suppressSignals = true;
        _timeout!.Value = state.Policy.MaximumJobSeconds;
        _outputMiB!.Value = state.Policy.MaximumOutputBytes / (1024d * 1024d);
        _provenance!.Text = state.Policy.Provenance;
        foreach (var operation in _operations)
            operation.Value.ButtonPressed = state.Policy.AllowedBlenderOperations.Contains(operation.Key);
        _suppressSignals = false;
        Invalidate();
        _preview!.Text = RenderState(state);
        SetStatus("Politique canonique chargee. Previsualisez avant toute application.", false);
    }

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        if (_getState?.Invoke() is null) return false;
        _timeout!.Value = 180;
        _outputMiB!.Value = 256;
        _provenance!.Text = "toolset:g12-l:smoke";
        return true;
    }

    public bool PreviewForSmokeTest() => Preview();

    public bool ApplyForSmokeTest()
    {
        Apply();
        return _approved is null;
    }

    private void BuildUi()
    {
        var background = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        background.AddThemeStyleboxOverride("panel", Card("100d1b", "62458a", 1, 0));
        AddChild(background);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22);
        background.AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var header = new Label { Text = "SECURITE DES WORKERS EXTERNES", ThemeTypeVariation = "HeaderSmall" };
        header.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(header);
        root.AddChild(new Label
        {
            Text = "Configuration explicite du worker Blender : operations typees, chemins confines et budgets bornes.",
        });
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); root.AddChild(body);

        var rules = AddCard(body, 360);
        rules.AddChild(Section("GARANTIES NON DESACTIVABLES"));
        foreach (var text in new[]
        {
            "Python libre interdit", "Shell interdit", "eval interdit", "Chemins confines obligatoires", "Reseau interdit",
        })
            rules.AddChild(new CheckButton { Text = text, ButtonPressed = true, Disabled = true });
        rules.AddChild(Section("BUDGETS"));
        _timeout = Spin(5, 900, 300); rules.AddChild(Field("Duree maximum (secondes)", _timeout));
        _outputMiB = Spin(1, 2048, 512); rules.AddChild(Field("Sortie GLB maximum (MiB)", _outputMiB));
        _provenance = new LineEdit { PlaceholderText = "author:security-policy" };
        rules.AddChild(Field("Provenance de modification", _provenance));
        rules.AddChild(new Label
        {
            Text = "Cout fournisseur : 0 USD. Cette politique locale n'ouvre aucun acces reseau.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var allowlist = AddCard(body, 300);
        allowlist.AddChild(Section("ALLOWLIST BLENDER TYPEE"));
        foreach (var operation in ExternalWorkerSecurityPolicyValidator.SupportedBlenderOperations)
        {
            var toggle = new CheckButton { Text = LabelFor(operation) };
            if (operation is AssetExternalOperation.ValidateModel or AssetExternalOperation.ExportGlb)
            {
                toggle.ButtonPressed = true;
                toggle.Disabled = true;
            }
            toggle.Toggled += _ => Invalidate();
            _operations[operation] = toggle;
            allowlist.AddChild(toggle);
        }
        allowlist.AddChild(new Label
        {
            Text = "Aucun champ de code, commande, script ou argument de processus n'est expose.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var plan = AddCard(body, 360, true);
        plan.AddChild(Section("PLAN, DIFF, PERMISSION & AUDIT"));
        _preview = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 500),
        };
        _preview.AddThemeStyleboxOverride("normal", Card("0b0911", "3d3150", 1, 8));
        plan.AddChild(_preview);

        var footer = new HBoxContainer(); footer.AddThemeConstantOverride("separation", 8); root.AddChild(footer);
        _status = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        footer.AddChild(_status);
        var undo = new Button { Text = "Annuler" }; undo.Pressed += () => { if (_undo?.Invoke() == true) Refresh(); }; footer.AddChild(undo);
        var redo = new Button { Text = "Retablir" }; redo.Pressed += () => { if (_redo?.Invoke() == true) Refresh(); }; footer.AddChild(redo);
        var preview = new Button { Text = "Previsualiser sans ecrire" }; preview.Pressed += () => Preview(); footer.AddChild(preview);
        _applyButton = new Button { Text = "Appliquer la politique", Disabled = true };
        _applyButton.Pressed += Apply; footer.AddChild(_applyButton);
        var close = new Button { Text = "Fermer" }; close.Pressed += Hide; footer.AddChild(close);

        _timeout.ValueChanged += _ => Invalidate();
        _outputMiB.ValueChanged += _ => Invalidate();
        _provenance.TextChanged += _ => Invalidate();
    }

    private bool Preview()
    {
        var state = _getState?.Invoke();
        if (state is null) { SetStatus("Politique indisponible.", true); return false; }
        var draft = BuildDraft(state.Policy);
        var diagnostics = ExternalWorkerSecurityPolicyValidator.Validate(draft);
        if (diagnostics.Count > 0)
        {
            SetStatus($"{diagnostics[0].Code} - {diagnostics[0].Path} - {diagnostics[0].Message}", true);
            return false;
        }
        _transactionId = Guid.NewGuid();
        _planId = Guid.NewGuid();
        if (_apply?.Invoke(draft, _transactionId, _planId, true) != true)
        {
            SetStatus("Le dry-run transactionnel a ete refuse.", true);
            return false;
        }
        _approved = draft;
        LastTransactionId = _transactionId;
        LastPlanId = _planId;
        LastEstimatedCostUsd = 0;
        LastSemanticDiff =
            $"execution arbitraire=interdite; operations={draft.AllowedBlenderOperations.Count}; " +
            $"timeout={draft.MaximumJobSeconds}s; sortie={draft.MaximumOutputBytes} octets";
        _preview!.Text = RenderPlan(state, draft);
        _applyButton!.Disabled = false;
        SetStatus("Plan valide. Aucun fichier n'a ete ecrit.", false);
        return true;
    }

    private void Apply()
    {
        if (_approved is null || _planId == Guid.Empty || _transactionId == Guid.Empty) return;
        if (_apply?.Invoke(_approved, _transactionId, _planId, false) != true)
        {
            SetStatus("Application refusee : plan, permission, revision ou persistance invalide.", true);
            return;
        }
        _approved = null;
        _applyButton!.Disabled = true;
        Refresh();
        SetStatus("Politique appliquee et auditee. Annulation et retablissement disponibles.", false);
    }

    private ExternalWorkerSecurityPolicy BuildDraft(ExternalWorkerSecurityPolicy source) =>
        new(
            source.Id,
            ExternalWorkerSecurityPolicy.CurrentSchemaVersion,
            true,
            true,
            true,
            true,
            false,
            checked((int)_timeout!.Value),
            checked((long)_outputMiB!.Value * 1024 * 1024),
            _operations.Where(item => item.Value.ButtonPressed).Select(item => item.Key).ToArray(),
            source.RequiredPermission,
            _provenance!.Text);

    private string RenderState(ExternalWorkerSecurityPolicyState state) =>
        $"[color=#9ee6b3][b]POLITIQUE ACTIVE[/b][/color]\n" +
        $"UUID : {state.Policy.Id:D}\nSchema : v{state.Policy.SchemaVersion} - revision {state.Revision.Value}\n" +
        $"Python libre : INTERDIT - shell : INTERDIT - eval : INTERDIT\n" +
        $"Chemins : confines - reseau : interdit\nPermission : {state.Policy.RequiredPermission}\n" +
        $"Provenance : {state.Policy.Provenance}\nCout : 0 USD";

    private string RenderPlan(ExternalWorkerSecurityPolicyState state, ExternalWorkerSecurityPolicy draft) =>
        $"[color=#ffb276][b]PLAN {_planId:D}[/b][/color]\nTransaction : {_transactionId:D}\n" +
        $"Revision de base : {state.Revision.Value}\nPermission : {draft.RequiredPermission}\nCout : 0 USD\n" +
        $"Provenance : {draft.Provenance}\n\n[b]DIFF SEMANTIQUE[/b]\n{LastSemanticDiff}\n\n" +
        $"[b]OPERATIONS AUTORISEES[/b]\n{string.Join("\n", draft.AllowedBlenderOperations.Select(LabelFor))}\n\n" +
        "[color=#9ee6b3]Dry-run valide - mutation : NON[/color]";

    private void Invalidate()
    {
        if (_suppressSignals) return;
        _approved = null;
        _transactionId = Guid.Empty;
        _planId = Guid.Empty;
        LastTransactionId = Guid.Empty;
        LastPlanId = Guid.Empty;
        LastEstimatedCostUsd = 0;
        LastSemanticDiff = string.Empty;
        if (_applyButton is not null) _applyButton.Disabled = true;
    }

    private void SetStatus(string text, bool error)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", new Color(error ? "ff8e9d" : "9ee6b3"));
    }

    private static string LabelFor(AssetExternalOperation operation) => operation switch
    {
        AssetExternalOperation.ValidateModel => "Valider le modele (obligatoire)",
        AssetExternalOperation.NormalizeModel => "Normaliser le modele",
        AssetExternalOperation.GenerateCollision => "Generer les collisions",
        AssetExternalOperation.GenerateLods => "Generer les LOD",
        AssetExternalOperation.ExportGlb => "Exporter en GLB (obligatoire)",
        AssetExternalOperation.GenerateThumbnail => "Generer une miniature",
        AssetExternalOperation.AuditQuality => "Auditer la qualite",
        AssetExternalOperation.CreatePrimitives => "Creer des primitives typees",
        _ => operation.ToString(),
    };

    private static SpinBox Spin(double min, double max, double value) =>
        new() { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new Vector2(120, 0) };
    private static Control Field(string label, Control control)
    {
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 3);
        var caption = new Label { Text = label }; caption.AddThemeFontSizeOverride("font_size", 11);
        caption.AddThemeColorOverride("font_color", new Color("9f8dbd"));
        column.AddChild(caption); column.AddChild(control); return column;
    }
    private static Label Section(string text)
    {
        var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new Color("ffb276")); return label;
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
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 8); margin.AddChild(column); return column;
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
