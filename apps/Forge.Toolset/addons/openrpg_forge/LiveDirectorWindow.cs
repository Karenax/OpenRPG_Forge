#if TOOLS
using Forge.LiveDirector.Scenarios;
using Godot;

namespace Forge.Toolset.Editor;

public partial class LiveDirectorWindow : Window
{
    private Label? _session;
    private Label? _authority;
    private Label? _audit;
    private RichTextLabel? _timeline;
    private string? _capturePath;
    private double _captureDelay;
    private bool _prepared;
    private Action<Window>? _applyIcon;

    public void Initialize(Action<Window> applyIcon) => _applyIcon = applyIcon;

    public override void _Ready()
    {
        Title = "Live Director · OpenRPG Forge";
        Size = new(1320, 820);
        MinSize = new(1020, 680);
        CloseRequested += Hide;
        BuildUi();
        _applyIcon?.Invoke(this);
        var argument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--live-director-ui-capture=", StringComparison.Ordinal));
        if (argument is not null) { _capturePath = argument["--live-director-ui-capture=".Length..]; SetProcess(true); }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_prepared && _captureDelay >= 1) { RunScenario(); _prepared = true; return; }
        if (!_prepared || _captureDelay < 2) return;
        var error = GetViewport().GetTexture().GetImage().SavePng(_capturePath);
        GD.Print($"OPENRPG_FORGE_LIVE_DIRECTOR_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={_capturePath}");
        _capturePath = null; SetProcess(false);
    }

    public void Open() => PopupCentered();

    private void BuildUi()
    {
        var background = new ColorRect { Color = new("0b0915") }; background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 24); AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 14); margin.AddChild(root);
        root.AddChild(Heading("LIVE DIRECTOR", 28));
        var subtitle = new Label { Text = "Dirigez la session en direct avec autorité, prévisualisation et audit immuable." }; subtitle.AddThemeColorOverride("font_color", new("c9bddc")); root.AddChild(subtitle);
        var summaries = new HBoxContainer(); summaries.AddThemeConstantOverride("separation", 12); root.AddChild(summaries);
        AddSummary(summaries, "SESSION", "2 joueurs · Crypte"); AddSummary(summaries, "AUTORITÉ", "Rôle GM · moindre privilège"); AddSummary(summaries, "MONDE LIVE", "32 capacités disponibles"); AddSummary(summaries, "AUDIT", "Chaîne SHA-256 vérifiable");
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 12); root.AddChild(body);

        var players = Card(270); body.AddChild(players); var playerBox = Box(players, "JOUEURS & CIBLES");
        AddItem(playerBox, "● Elyra", "Crypte · 42 ms · Groupe A", "70d6a3"); AddItem(playerBox, "● Cael", "Crypte · 48 ms · Groupe A", "70d6a3"); AddItem(playerBox, "◆ Gardien des braises", "Créature · active", "ff9858"); AddItem(playerBox, "◇ Porte du sanctuaire", "Verrouillée · portail", "c9bddc");

        var actions = Card(430); body.AddChild(actions); var actionBox = Box(actions, "ACTIONS LIVE");
        actionBox.AddChild(new LineEdit { PlaceholderText = "Rechercher joueur, objet, quête...", CustomMinimumSize = new(0, 40) });
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); actionBox.AddChild(grid);
        foreach (var text in new[] { "Téléporter", "Examiner", "Soigner", "Posséder", "Lancer une scène", "Modifier une quête", "Contrôler rencontre", "Envoyer message" }) AddAction(grid, text);
        actionBox.AddChild(Heading("SCÈNE PRÉPARÉE", 14));
        var scene = new Label { Text = "Tempête sur la Crypte\n2 spawns · dialogue · musique · pluie\nRollback : 2 étapes", AutowrapMode = TextServer.AutowrapMode.WordSmart }; scene.AddThemeColorOverride("font_color", new("c9bddc")); actionBox.AddChild(scene);
        var run = new Button { Text = "▶ Exécuter le scénario GM", CustomMinimumSize = new(0, 48) }; run.AddThemeStyleboxOverride("normal", Style("d84f32", "ff8b63")); run.Pressed += RunScenario; actionBox.AddChild(run);

        var auditCard = Card(360); body.AddChild(auditCard); var auditBox = Box(auditCard, "SESSION & AUDIT");
        _session = Status("En attente du test réseau"); auditBox.AddChild(_session);
        _authority = Status("Autorité non vérifiée"); auditBox.AddChild(_authority);
        _audit = Status("Journal non vérifié"); auditBox.AddChild(_audit);
        _timeline = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(0, 300), Text = "[color=#9f92b8]La chronologie des actions apparaîtra ici.[/color]" }; auditBox.AddChild(_timeline);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new(120, 38) }; close.Pressed += Hide; root.AddChild(close);
    }

    private void RunScenario()
    {
        var result = LiveDirectorScenario.Run();
        SetStatus(_session!, result.Passed ? "✓ Session réseau synchronisée" : "✕ Session indisponible", result.Passed);
        SetStatus(_authority!, result.Passed ? "✓ Action GM acceptée · joueur refusé" : "✕ Autorité invalide", result.Passed);
        SetStatus(_audit!, result.Passed ? $"✓ Audit immuable · {result.AuditEntries} entrée" : "✕ Audit invalide", result.Passed);
        _timeline!.Text = $"[color=#ff9858]15:00[/color]  GM connecté à la session\n[color=#70d6a3]15:00[/color]  Gardien live créé\n[color=#70d6a3]15:00[/color]  Quête Cendres → terminée\n[color=#ff806d]15:00[/color]  Création joueur refusée\n\n[color=#9f92b8]Digest réseau[/color]\n{result.NetworkDigest}\n\n[color=#9f92b8]Hash audit[/color]\n{result.AuditHash}";
    }

    private static PanelContainer Card(float width) { var card = new PanelContainer { CustomMinimumSize = new(width, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; card.AddThemeStyleboxOverride("panel", Style("151022", "47365f")); return card; }
    private static VBoxContainer Box(Container card, string title) { var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10); card.AddChild(box); box.AddChild(Heading(title, 14)); return box; }
    private static Label Heading(string text, int size) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new("ff9858")); return label; }
    private static Label Status(string text) { var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new("aa9bc7")); return label; }
    private static void SetStatus(Label label, string text, bool ok) { label.Text = text; label.AddThemeColorOverride("font_color", new(ok ? "70d6a3" : "ff806d")); }
    private static void AddSummary(Container owner, string title, string value) { var card = Card(0); card.CustomMinimumSize = new(0, 82); owner.AddChild(card); var box = new VBoxContainer(); card.AddChild(box); box.AddChild(Heading(title, 12)); box.AddChild(new Label { Text = value }); }
    private static void AddItem(Container owner, string title, string detail, string color) { var label = new Label { Text = $"{title}\n   {detail}" }; label.AddThemeColorOverride("font_color", new(color)); owner.AddChild(label); }
    private static void AddAction(Container owner, string text) { var button = new Button { Text = text, CustomMinimumSize = new(190, 40) }; button.AddThemeStyleboxOverride("normal", Style("251a35", "5d4777")); owner.AddChild(button); }
    private static StyleBoxFlat Style(string bg, string border) { var style = new StyleBoxFlat { BgColor = new(bg), BorderColor = new(border), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 16, ContentMarginTop = 14, ContentMarginRight = 16, ContentMarginBottom = 14 }; style.SetBorderWidthAll(1); return style; }
}
#endif
