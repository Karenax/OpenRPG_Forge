#if TOOLS
using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Forge.Network.Sessions;
using Godot;

namespace Forge.Toolset.Editor;

public partial class NetworkBetaWindow : Window
{
    private readonly NetworkAuthoringService _authoring = new();
    private DedicatedServerConfiguration _configuration = new(Guid.Parse("10000000-0000-4000-8000-000000000001"), 1, "Serveur OpenRPG Forge", "127.0.0.1", 5121, 8, DiscoveryMode.Lan, "module-build-fingerprint", "1");
    private LineEdit? _name;
    private SpinBox? _port;
    private SpinBox? _capacity;
    private OptionButton? _discovery;
    private Label? _configurationStatus;
    private Label? _scenarioStatus;
    private RichTextLabel? _details;
    private Action<Window>? _applyIcon;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;

    public void Initialize(Action<Window> applyIcon) => _applyIcon = applyIcon;

    public override void _Ready()
    {
        Title = "Network Beta · OpenRPG Forge";
        Size = new(1180, 760);
        MinSize = new(920, 620);
        CloseRequested += Hide;
        BuildUi();
        Refresh();
        _applyIcon?.Invoke(this);
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--network-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            _capturePath = captureArgument["--network-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_capturePrepared && _captureDelay >= 1.0)
        {
            Refresh();
            RunScenario();
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2.0) return;
        var error = GetViewport().GetTexture().GetImage().SavePng(_capturePath);
        GD.Print($"OPENRPG_FORGE_NETWORK_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={_capturePath}");
        _capturePath = null;
        SetProcess(false);
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
    }

    private void BuildUi()
    {
        var background = new ColorRect { Color = new("0b0915") };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 24); AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 14); margin.AddChild(root);
        var title = new Label { Text = "NETWORK BETA", TooltipText = "Configuration et validation du serveur autoritaire" }; title.AddThemeFontSizeOverride("font_size", 26); title.AddThemeColorOverride("font_color", new("ff9858")); root.AddChild(title);
        var subtitle = new Label { Text = "Configurez l’hôte, vérifiez la sécurité et testez deux joueurs avant de publier le module." }; subtitle.AddThemeColorOverride("font_color", new("c9bddc")); root.AddChild(subtitle);
        var summary = new HBoxContainer(); summary.AddThemeConstantOverride("separation", 12); root.AddChild(summary);
        AddSummary(summary, "SERVEUR", "Autoritaire · headless"); AddSummary(summary, "CONTENU", "Fingerprint verrouillé"); AddSummary(summary, "RÉSILIENCE", "Reconnexion · rollback"); AddSummary(summary, "OBSERVABILITÉ", "Health · logs · métriques");
        var body = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(body);
        var formCard = Card(); body.AddChild(formCard); var form = new VBoxContainer(); form.AddThemeConstantOverride("separation", 10); formCard.AddChild(form);
        form.AddChild(Heading("CONFIGURATION DU SERVEUR"));
        _name = AddField(form, "Nom visible");
        form.AddChild(new Label { Text = "Port" }); _port = new SpinBox { MinValue = 1024, MaxValue = 65535, Step = 1, CustomMinimumSize = new(0, 38) }; form.AddChild(_port);
        form.AddChild(new Label { Text = "Capacité" }); _capacity = new SpinBox { MinValue = 1, MaxValue = 64, Step = 1, CustomMinimumSize = new(0, 38) }; form.AddChild(_capacity);
        form.AddChild(new Label { Text = "Découverte" }); _discovery = new OptionButton { CustomMinimumSize = new(0, 38) }; _discovery.AddItem("Réseau local"); _discovery.AddItem("Connexion directe"); form.AddChild(_discovery);
        _configurationStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; form.AddChild(_configurationStatus);
        var buttons = new HBoxContainer(); form.AddChild(buttons); AddButton(buttons, "Prévisualiser", () => Save(false)); AddButton(buttons, "Appliquer", () => Save(true), true);
        var validationCard = Card(); body.AddChild(validationCard); var validation = new VBoxContainer(); validation.AddThemeConstantOverride("separation", 12); validationCard.AddChild(validation);
        validation.AddChild(Heading("VALIDATION DE LA SESSION"));
        validation.AddChild(new Label { Text = "Le test lance deux clients, partage une progression, reconnecte le second puis tente une mutation forgée.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var run = new Button { Text = "▶ Tester avec deux clients", CustomMinimumSize = new(0, 46) }; run.AddThemeColorOverride("font_color", Colors.White); run.AddThemeStyleboxOverride("normal", CardStyle("d84f32", "ff8b63")); run.Pressed += RunScenario; validation.AddChild(run);
        _scenarioStatus = new Label { Text = "Pas encore testé" }; _scenarioStatus.AddThemeColorOverride("font_color", new("aa9bc7")); validation.AddChild(_scenarioStatus);
        _details = new RichTextLabel { BbcodeEnabled = true, FitContent = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(420, 260) }; validation.AddChild(_details);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new(110, 38) }; close.Pressed += Hide; root.AddChild(close);
    }

    private void Save(bool apply)
    {
        var after = _configuration with { Name = _name!.Text.Trim(), Port = (int)_port!.Value, Capacity = (int)_capacity!.Value, Discovery = _discovery!.Selected == 0 ? DiscoveryMode.Lan : DiscoveryMode.Direct };
        var result = _authoring.Execute(new(_configuration, after, "toolset:local-author", apply));
        if (result.Applied) _configuration = result.Configuration;
        _configurationStatus!.Text = result.Diagnostics.Count == 0 ? $"✓ {(apply ? "Configuration appliquée" : "Prévisualisation valide")} · {result.SemanticDiff}" : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
        _configurationStatus.AddThemeColorOverride("font_color", result.Diagnostics.Count == 0 ? new("70d6a3") : new("ff806d"));
    }

    private void RunScenario()
    {
        var result = NetworkBetaScenario.Run();
        _scenarioStatus!.Text = result.Passed ? "✓ Network Beta prête · 2 clients synchronisés" : "✕ Échec du scénario réseau";
        _scenarioStatus.AddThemeColorOverride("font_color", result.Passed ? new("70d6a3") : new("ff806d"));
        _details!.Text = $"[color=#ff9858]Révision serveur[/color]  {result.ServerRevision}\n[color=#c9bddc]Client A / Client B[/color]  {result.ClientARevision} / {result.ClientBRevision}\n[color=#70d6a3]Reconnexion[/color]  {result.ReconnectionCode}\n[color=#70d6a3]Anti-triche[/color]  {result.ForgedMutationCode}\n\n[color=#9f92b8]Digest partagé[/color]\n{result.SharedDigest}";
    }

    private void Refresh() { if (_name is null) return; _name.Text = _configuration.Name; _port!.Value = _configuration.Port; _capacity!.Value = _configuration.Capacity; _discovery!.Select(_configuration.Discovery == DiscoveryMode.Lan ? 0 : 1); }
    private static PanelContainer Card() { var panel = new PanelContainer { CustomMinimumSize = new(420, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; panel.AddThemeStyleboxOverride("panel", CardStyle("151022", "47365f")); return panel; }
    private static StyleBoxFlat CardStyle(string background, string border) { var style = new StyleBoxFlat { BgColor = new(background), BorderColor = new(border), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 18, ContentMarginTop = 16, ContentMarginRight = 18, ContentMarginBottom = 16 }; style.SetBorderWidthAll(1); return style; }
    private static Label Heading(string text) { var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new("ff9858")); return label; }
    private static LineEdit AddField(Container owner, string label) { owner.AddChild(new Label { Text = label }); var field = new LineEdit { CustomMinimumSize = new(0, 38) }; owner.AddChild(field); return field; }
    private static void AddSummary(Container owner, string title, string value) { var card = Card(); card.CustomMinimumSize = new(0, 82); owner.AddChild(card); var box = new VBoxContainer(); card.AddChild(box); box.AddChild(Heading(title)); box.AddChild(new Label { Text = value }); }
    private static void AddButton(Container owner, string text, Action action, bool primary = false) { var button = new Button { Text = text, CustomMinimumSize = new(130, 38) }; if (primary) button.AddThemeStyleboxOverride("normal", CardStyle("38264f", "8060aa")); button.Pressed += action; owner.AddChild(button); }
}
#endif
