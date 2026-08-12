#if TOOLS
using Forge.Domain.Quality;
using Forge.Network.Authoring;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.Scenarios;
using Godot;

namespace Forge.Toolset.Editor;

public partial class NetworkAssuranceWindow : Window
{
    private NetworkAssuranceProfile _profile = NetworkAssuranceProfile.CreateDefault("toolset:r104");
    private readonly NetworkAssuranceHistory _history;
    private OptionButton? _severity;
    private OptionButton? _privacy;
    private SpinBox? _rotationKilobytes;
    private SpinBox? _rotationFiles;
    private LineEdit? _sinkEndpoint;
    private SpinBox? _maximumSpeed;
    private SpinBox? _intentRate;
    private SpinBox? _anomalyThreshold;
    private CheckBox? _signedContent;
    private CheckBox? _authoritativeRandom;
    private CheckBox? _trustClientSaves;
    private SpinBox? _clients;
    private SpinBox? _latency;
    private SpinBox? _dropEvery;
    private SpinBox? _replayPasses;
    private SpinBox? _soakCycles;
    private Label? _status;
    private Label? _audit;
    private RichTextLabel? _results;
    private Action<Window>? _applyIcon;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;

    public NetworkAssuranceWindow() => _history = new(_profile);

    public void Initialize(Action<Window> applyIcon) => _applyIcon = applyIcon;

    public override void _Ready()
    {
        Title = "Network Assurance · OpenRPG Forge";
        Size = new(1500, 920);
        MinSize = new(1180, 760);
        CloseRequested += Hide;
        BuildUi();
        Refresh();
        _applyIcon?.Invoke(this);
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r104-network-assurance-ui-capture=", StringComparison.Ordinal));
        if (capture is null) return;
        _capturePath = capture[(capture.IndexOf('=') + 1)..];
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_capturePrepared && _captureDelay >= 1.0)
        {
            Open();
            RunScenario();
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2.0) return;
        var path = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(path);
        GD.Print($"OPENRPG_FORGE_R104_NETWORK_ASSURANCE_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={path} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
    }

    public bool RunR104SmokeTest()
    {
        var scenario = NetworkAssuranceAcceptanceScenario.Run();
        var store = new NetworkAssuranceStore();
        var loaded = store.Deserialize(store.Serialize(_profile));
        var optional = NetworkAssuranceRuntimeAdapter.Resolve(null, false);
        var required = NetworkAssuranceRuntimeAdapter.Resolve(null, true);
        var invalid = NetworkAssuranceValidator.Validate(_profile with
        {
            AntiCheat = _profile.AntiCheat with { TrustClientSaves = true }
        });
        var runtime = NetworkAssuranceRuntimeAdapter.Build(loaded, sinks: [new MemoryRemoteLogSink("audit")]);
        return scenario.Passed && store.Serialize(loaded) == store.Serialize(_profile) &&
            !optional.Available && optional.Diagnostics.Count == 0 &&
            !required.Available && required.Diagnostics.Single().Code == "NET_ASSURANCE_REQUIRED_MISSING" &&
            invalid.Any(item => item.Code == "NET_ASSURANCE_ANTICHEAT_POLICY_INVALID") &&
            runtime.Scripts.Read(runtime.Scripts.Handle).SimulatedClients == 3;
    }

    private void BuildUi()
    {
        var background = new ColorRect { Color = new("0b0915") };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(background);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 20);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var title = Heading("ASSURANCE RÉSEAU · LOGS, AUTORITÉ & TESTS", 26);
        title.TooltipText = "Configuration canonique des garde-fous serveur et campagnes reproductibles";
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Configurez les preuves d'exploitation, la confiance zéro côté client et une campagne réseau rejouable — sans modifier de fichier généré.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeColorOverride("font_color", new("c9bddc"));
        root.AddChild(subtitle);

        var summary = new HBoxContainer();
        summary.AddThemeConstantOverride("separation", 12);
        root.AddChild(summary);
        AddSummary(summary, "JOURNAUX", "JSON + texte · rotation · sinks · vie privée");
        AddSummary(summary, "ANTI-CHEAT", "Intents · signatures · RNG · anomalies");
        AddSummary(summary, "DÉTERMINISME", "Clients · défauts · replay · soak · état");

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);
        BuildLogsCard(body);
        BuildAntiCheatCard(body);
        BuildTestsCard(body);

        var resultsCard = Card();
        resultsCard.CustomMinimumSize = new(0, 150);
        root.AddChild(resultsCard);
        var resultsBox = new VBoxContainer();
        resultsBox.AddThemeConstantOverride("separation", 6);
        resultsCard.AddChild(resultsBox);
        var resultsHeader = new HBoxContainer();
        resultsBox.AddChild(resultsHeader);
        resultsHeader.AddChild(Heading("PREUVE SERVEUR INTÉGRÉE"));
        _status = new Label
        {
            Text = "État vide · aucune campagne exécutée",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _status.AddThemeColorOverride("font_color", new("aa9bc7"));
        resultsHeader.AddChild(_status);
        _results = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(0, 62)
        };
        resultsBox.AddChild(_results);
        _audit = new Label
        {
            Text = "Permission Administrator · scope Server · audit prêt",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _audit.AddThemeColorOverride("font_color", new("9f92b8"));
        resultsBox.AddChild(_audit);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        AddButton(footer, "Prévisualiser", () => Save(false));
        AddButton(footer, "Appliquer", () => Save(true), true);
        AddButton(footer, "Annuler", Undo);
        AddButton(footer, "Rétablir", Redo);
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        footer.AddChild(spacer);
        AddButton(footer, "▶ Exécuter la campagne", RunScenario, true, 220);
        AddButton(footer, "Fermer", Hide);
    }

    private void BuildLogsCard(Container body)
    {
        var card = Card();
        body.AddChild(card);
        var box = CardBox(card, "LOGS STRUCTURÉS", "Corrélation, confidentialité, rotation et livraison distante");
        var grid = FormGrid();
        box.AddChild(grid);
        grid.AddChild(new Label { Text = "Sévérité minimale" });
        _severity = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var item in Enum.GetValues<StructuredLogSeverity>()) _severity.AddItem(item.ToString());
        grid.AddChild(_severity);
        grid.AddChild(new Label { Text = "Identités joueur" });
        _privacy = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var item in Enum.GetValues<PlayerPrivacyMode>()) _privacy.AddItem(item.ToString());
        grid.AddChild(_privacy);
        _rotationKilobytes = AddNumber(grid, "Rotation", 1, 1_048_576, " Kio");
        _rotationFiles = AddNumber(grid, "Segments conservés", 1, 100);
        grid.AddChild(new Label { Text = "Sink distant requis" });
        _sinkEndpoint = new LineEdit { PlaceholderText = "udp://127.0.0.1:5514", CustomMinimumSize = new(0, 34), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddChild(_sinkEndpoint);
        box.AddChild(Note("Catégories réseau, sécurité et runtime · UUID objet/source/transaction · secrets masqués"));
    }

    private void BuildAntiCheatCard(Container body)
    {
        var card = Card();
        body.AddChild(card);
        var box = CardBox(card, "ANTI-CHEAT PAR CONCEPTION", "Le serveur valide les intentions et ne fait confiance ni au hasard ni aux sauvegardes client");
        var grid = FormGrid();
        box.AddChild(grid);
        _maximumSpeed = AddNumber(grid, "Vitesse maximale", 1, 1000, " u/s");
        _intentRate = AddNumber(grid, "Intentions / seconde", 1, 10000);
        _anomalyThreshold = AddNumber(grid, "Seuil d'anomalie", 1, 10000);
        _signedContent = AddCheckRow(grid, "Contenu signé obligatoire");
        _authoritativeRandom = AddCheckRow(grid, "RNG autoritaire serveur");
        _trustClientSaves = AddCheckRow(grid, "Faire confiance aux sauvegardes client");
        _trustClientSaves!.TooltipText = "Doit rester désactivé : une sauvegarde client ne peut pas muter l'état autoritaire.";
        box.AddChild(Note("Signer approuvé visible · rejets audités · quarantaine au seuil · aucune mutation client directe"));
    }

    private void BuildTestsCard(Container body)
    {
        var card = Card();
        body.AddChild(card);
        var box = CardBox(card, "TESTS RÉSEAU DÉTERMINISTES", "Simulation reproductible des clients, défauts de transport et assertions serveur");
        var grid = FormGrid();
        box.AddChild(grid);
        _clients = AddNumber(grid, "Clients simulés", 2, 128);
        _latency = AddNumber(grid, "Latence", 0, 10000, " ticks");
        _dropEvery = AddNumber(grid, "Perte déterministe", 0, 10000, " paquets");
        _replayPasses = AddNumber(grid, "Passes de replay", 2, 100);
        _soakCycles = AddNumber(grid, "Cycles de soak", 1, 100000);
        box.AddChild(Note("Duplication + réordonnancement activés · révision autoritaire 42 · empreinte stable exigée"));
    }

    private void Save(bool apply)
    {
        var sink = _profile.Logs.RemoteSinks.Single() with { Endpoint = _sinkEndpoint!.Text.Trim() };
        var candidate = _profile with
        {
            ServerRevision = _profile.ServerRevision + 1,
            Provenance = "toolset:r104",
            Logs = _profile.Logs with
            {
                MinimumSeverity = Enum.GetValues<StructuredLogSeverity>()[_severity!.Selected],
                PlayerPrivacy = Enum.GetValues<PlayerPrivacyMode>()[_privacy!.Selected],
                RotationBytes = (int)_rotationKilobytes!.Value * 1024,
                RotationFiles = (int)_rotationFiles!.Value,
                RemoteSinks = [sink]
            },
            AntiCheat = _profile.AntiCheat with
            {
                MaximumUnitsPerSecond = (float)_maximumSpeed!.Value,
                MaximumIntentsPerSecond = (int)_intentRate!.Value,
                AnomalyThreshold = (int)_anomalyThreshold!.Value,
                RequireSignedContent = _signedContent!.ButtonPressed,
                AuthoritativeRandom = _authoritativeRandom!.ButtonPressed,
                TrustClientSaves = _trustClientSaves!.ButtonPressed
            },
            DeterministicTests = _profile.DeterministicTests with
            {
                ClientCount = (int)_clients!.Value,
                LatencyTicks = (int)_latency!.Value,
                DropEvery = (int)_dropEvery!.Value,
                ReplayPasses = (int)_replayPasses!.Value,
                SoakCycles = (int)_soakCycles!.Value
            }
        };
        var result = _history.Execute(new(Guid.NewGuid(), 1, "toolset:r104", _profile.ServerRevision,
            "toolset:local-author", NetworkRole.Administrator, apply, candidate));
        if (result.Applied) _profile = result.Profile;
        _status!.Text = result.Diagnostics.Count == 0
            ? $"✓ {(apply ? "Profil appliqué" : "Aperçu valide")} · {result.SemanticDiff}"
            : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Path}";
        _status.AddThemeColorOverride("font_color", result.Diagnostics.Count == 0 ? new("70d6a3") : new("ff806d"));
        _audit!.Text = result.AuditEntry;
    }

    private void Undo()
    {
        var result = _history.Undo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _profile = result.Profile;
        _audit!.Text = result.AuditEntry;
        Refresh();
    }

    private void Redo()
    {
        var result = _history.Redo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _profile = result.Profile;
        _audit!.Text = result.AuditEntry;
        Refresh();
    }

    private void RunScenario()
    {
        var result = NetworkAssuranceAcceptanceScenario.Run();
        _status!.Text = result.Passed ? "✓ Assurance serveur validée" : "✕ Échec · consultez les diagnostics";
        _status.AddThemeColorOverride("font_color", result.Passed ? new("70d6a3") : new("ff806d"));
        _results!.Text =
            $"[color=#ff9858]Logs[/color]  {result.LogCode} · {result.LogSegments} segments · {result.RemoteDeliveries} livraisons distantes\n" +
            $"[color=#70d6a3]Autorité[/color]  {result.JoinCode} · sauvegarde {result.ClientSaveCode}\n" +
            $"[color=#70d6a3]Anomalie / RNG[/color]  {result.AnomalyCode} · quarantaine {result.Quarantined} · tirage {result.RandomValue}\n" +
            $"[color=#c9bddc]Campagne[/color]  {result.TestCode} · {result.StateAssertions} assertions · empreinte {result.Fingerprint[..12]}…";
        _audit!.Text = result.Audit.Last();
    }

    private void Refresh()
    {
        if (_severity is null) return;
        _severity.Select((int)_profile.Logs.MinimumSeverity);
        _privacy!.Select((int)_profile.Logs.PlayerPrivacy);
        _rotationKilobytes!.Value = _profile.Logs.RotationBytes / 1024;
        _rotationFiles!.Value = _profile.Logs.RotationFiles;
        _sinkEndpoint!.Text = _profile.Logs.RemoteSinks.Single().Endpoint;
        _maximumSpeed!.Value = _profile.AntiCheat.MaximumUnitsPerSecond;
        _intentRate!.Value = _profile.AntiCheat.MaximumIntentsPerSecond;
        _anomalyThreshold!.Value = _profile.AntiCheat.AnomalyThreshold;
        _signedContent!.ButtonPressed = _profile.AntiCheat.RequireSignedContent;
        _authoritativeRandom!.ButtonPressed = _profile.AntiCheat.AuthoritativeRandom;
        _trustClientSaves!.ButtonPressed = _profile.AntiCheat.TrustClientSaves;
        _clients!.Value = _profile.DeterministicTests.ClientCount;
        _latency!.Value = _profile.DeterministicTests.LatencyTicks;
        _dropEvery!.Value = _profile.DeterministicTests.DropEvery;
        _replayPasses!.Value = _profile.DeterministicTests.ReplayPasses;
        _soakCycles!.Value = _profile.DeterministicTests.SoakCycles;
        _audit!.Text = $"actor={_profile.AuditIdentity} · scope={_profile.AuthorityScope} · revision={_profile.ServerRevision}";
    }

    private static VBoxContainer CardBox(PanelContainer card, string title, string subtitle)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        card.AddChild(box);
        box.AddChild(Heading(title));
        var label = new Label { Text = subtitle, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new("b9accd"));
        box.AddChild(label);
        return box;
    }

    private static PanelContainer Card()
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new(390, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel", CardStyle("151022", "47365f"));
        return card;
    }

    private static StyleBoxFlat CardStyle(string background, string border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new(background),
            BorderColor = new(border),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16,
            ContentMarginTop = 14,
            ContentMarginRight = 16,
            ContentMarginBottom = 14
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static Label Heading(string text, int size = 15)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new("ff9858"));
        return label;
    }

    private static Label Note(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new("9f92b8"));
        return label;
    }

    private static SpinBox AddNumber(Container owner, string label, double minimum, double maximum,
        string suffix = "")
    {
        owner.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 1,
            Suffix = suffix,
            CustomMinimumSize = new(0, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        owner.AddChild(input);
        return input;
    }

    private static GridContainer FormGrid()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static CheckBox AddCheckRow(GridContainer owner, string text)
    {
        owner.AddChild(new Control());
        var check = new CheckBox { Text = text };
        owner.AddChild(check);
        return check;
    }

    private static void AddSummary(Container owner, string title, string value)
    {
        var card = Card();
        card.CustomMinimumSize = new(0, 68);
        owner.AddChild(card);
        var box = new VBoxContainer();
        card.AddChild(box);
        box.AddChild(Heading(title));
        box.AddChild(new Label { Text = value });
    }

    private static void AddButton(Container owner, string text, Action action, bool primary = false,
        int width = 130)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(width, 36) };
        if (primary) button.AddThemeStyleboxOverride("normal", CardStyle("38264f", "8060aa"));
        button.Pressed += action;
        owner.AddChild(button);
    }
}
#endif
