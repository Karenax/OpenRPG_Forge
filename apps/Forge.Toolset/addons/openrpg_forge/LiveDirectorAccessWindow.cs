#if TOOLS
using Forge.Domain.Quality;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Godot;

namespace Forge.Toolset.Editor;

public partial class LiveDirectorAccessWindow : Window
{
    private LiveDirectorAccessProfile _profile = LiveDirectorAccessProfile.CreateDefault("toolset:r105");
    private readonly LiveDirectorAccessHistory _history;
    private CheckBox? _mfa;
    private LineEdit? _mfaAdapter;
    private SpinBox? _sessionMinutes;
    private CheckBox? _inspectionReadOnly;
    private SpinBox? _historyLimit;
    private SpinBox? _refreshSeconds;
    private LineEdit? _latencyAdapter;
    private CheckBox? _voiceStatus;
    private CheckBox? _moderationFlags;
    private SpinBox? _retentionDays;
    private SpinBox? _maximumEntries;
    private LineEdit? _auditSink;
    private CheckBox? _immutableAudit;
    private SpinBox? _messageLength;
    private CheckBox? _moderation;
    private CheckBox? _immutableChat;
    private LineEdit? _voiceAdapter;
    private Label? _status;
    private Label? _audit;
    private RichTextLabel? _results;
    private Action<Window>? _applyIcon;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;

    public LiveDirectorAccessWindow() => _history = new(_profile);

    public void Initialize(Action<Window> applyIcon) => _applyIcon = applyIcon;

    public override void _Ready()
    {
        Title = "Accès & supervision Live Director · OpenRPG Forge";
        Size = new(1500, 920);
        MinSize = new(1180, 760);
        CloseRequested += Hide;
        BuildUi();
        Refresh();
        _applyIcon?.Invoke(this);
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r105-live-director-access-ui-capture=", StringComparison.Ordinal));
        if (capture is null) return;
        _capturePath = capture[(capture.IndexOf('=') + 1)..];
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_capturePrepared && _captureDelay >= 1)
        {
            Open();
            RunScenario();
            _capturePrepared = true;
            return;
        }
        if (!_capturePrepared || _captureDelay < 2) return;
        var path = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(path);
        GD.Print($"OPENRPG_FORGE_R105_LIVE_DIRECTOR_ACCESS_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
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

    public bool RunR105SmokeTest()
    {
        var scenario = LiveDirectorAccessAcceptanceScenario.Run();
        var store = new LiveDirectorAccessStore();
        var loaded = store.Deserialize(store.Serialize(_profile));
        var optional = LiveDirectorAccessRuntimeAdapter.Resolve(null, false);
        var required = LiveDirectorAccessRuntimeAdapter.Resolve(null, true);
        var invalid = LiveDirectorAccessValidator.Validate(_profile with
        {
            Authentication = _profile.Authentication with { RequireSecondFactor = false }
        });
        var runtime = LiveDirectorAccessRuntimeAdapter.Build(loaded);
        return scenario.Passed && store.Serialize(loaded) == store.Serialize(_profile) &&
            !optional.Available && optional.Diagnostics.Count == 0 &&
            !required.Available && required.Diagnostics.Single().Code == "LIVE_ACCESS_REQUIRED_MISSING" &&
            invalid.Any(item => item.Code == "LIVE_ACCESS_AUTH_POLICY_INVALID") &&
            runtime.Scripts.Read(runtime.Scripts.Handle).ChatChannels == 7;
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

        root.AddChild(Heading("LIVE DIRECTOR · ACCÈS & SUPERVISION", 26));
        var subtitle = new Label
        {
            Text = "Configurez une console GM sûre : authentification forte, inspection en lecture seule, joueurs en direct, audit immuable et canaux modérés.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeColorOverride("font_color", new("c9bddc"));
        root.AddChild(subtitle);

        var summaries = new HBoxContainer();
        summaries.AddThemeConstantOverride("separation", 12);
        root.AddChild(summaries);
        AddSummary(summaries, "AUTORITÉ", "GM · MFA · expiration · révocation");
        AddSummary(summaries, "OBSERVATION", "6 sections · 8 colonnes live");
        AddSummary(summaries, "GOUVERNANCE", "SHA-256 · 7 canaux · modération");

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);
        var left = Column(body);
        BuildAuthentication(left);
        BuildInspection(left);
        var center = Column(body);
        BuildPlayers(center);
        BuildAudit(center);
        var right = Column(body);
        BuildChat(right);
        BuildProof(right);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        AddButton(footer, "Prévisualiser", () => Save(false));
        AddButton(footer, "Appliquer", () => Save(true), true);
        AddButton(footer, "Annuler", Undo);
        AddButton(footer, "Rétablir", Redo);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        AddButton(footer, "▶ Tester les 5 fonctions", RunScenario, true, 220);
        AddButton(footer, "Fermer", Hide);
    }

    private void BuildAuthentication(Container owner)
    {
        var box = CardBox(owner, "AUTHENTIFICATION GM", "Moindre privilège, second facteur et session révocable");
        var grid = FormGrid();
        box.AddChild(grid);
        _mfa = AddCheckRow(grid, "Second facteur obligatoire");
        _mfaAdapter = AddText(grid, "Adaptateur MFA", "totp:local");
        _sessionMinutes = AddNumber(grid, "Durée de session", 1, 1440, " min");
        box.AddChild(Note("Rôle autorisé : Administrator · toute décision est auditée · révocation immédiate"));
    }

    private void BuildInspection(Container owner)
    {
        var box = CardBox(owner, "EXAMINER", "Composants, statistiques, variables, scripts, propriétaire et historique");
        var grid = FormGrid();
        box.AddChild(grid);
        _inspectionReadOnly = AddCheckRow(grid, "Lecture seule par défaut");
        _historyLimit = AddNumber(grid, "Historique visible", 1, 10_000, " entrées");
        box.AddChild(Note("Éditions désactivées · permissions objet/zone vérifiées · aucune mutation parallèle"));
    }

    private void BuildPlayers(Container owner)
    {
        var box = CardBox(owner, "LISTE DES JOUEURS", "Identité, personnage, zone, latence, groupe, droits et modération");
        var grid = FormGrid();
        box.AddChild(grid);
        _refreshSeconds = AddNumber(grid, "Rafraîchissement", 1, 300, " s");
        _latencyAdapter = AddText(grid, "Source de latence", "network:authoritative-latency");
        _voiceStatus = AddCheckRow(grid, "Afficher l’état vocal");
        _moderationFlags = AddCheckRow(grid, "Afficher les alertes");
        box.AddChild(Note("Valeurs serveur · rafraîchissement borné · lignes stables par UUID compte/personnage"));
    }

    private void BuildAudit(Container owner)
    {
        var box = CardBox(owner, "JOURNAL D’AUDIT GM", "Acteur, commande, cibles, avant/après, raison et corrélation");
        var grid = FormGrid();
        box.AddChild(grid);
        _immutableAudit = AddCheckRow(grid, "Chaîne de hash immuable");
        _retentionDays = AddNumber(grid, "Rétention", 1, 3650, " jours");
        _maximumEntries = AddNumber(grid, "Capacité", 1, 10_000_000, " entrées");
        _auditSink = AddText(grid, "Archive externe", "optional:none");
    }

    private void BuildChat(Container owner)
    {
        var box = CardBox(owner, "CHAT & CANAUX GM", "Local, groupe, tell, serveur, GM, faction et incarnation");
        var grid = FormGrid();
        box.AddChild(grid);
        _messageLength = AddNumber(grid, "Taille maximale", 1, 500, " caractères");
        _moderation = AddCheckRow(grid, "Modération active");
        _immutableChat = AddCheckRow(grid, "Messages journalisés");
        _voiceAdapter = AddText(grid, "Adaptateur vocal", "optional:none");
        box.AddChild(Note("L’incarnation exige une cible et une raison · texte normalisé · canaux scopés"));
    }

    private void BuildProof(Container owner)
    {
        var box = CardBox(owner, "PREUVE INTÉGRÉE", "Exécution serveur reproductible des cinq contrats");
        _status = new Label { Text = "État vide · aucun test exécuté", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.AddThemeColorOverride("font_color", new("aa9bc7"));
        box.AddChild(_status);
        _results = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(0, 145),
            Text = "[color=#9f92b8]Les résultats d’authentification, d’inspection, de joueurs, d’audit et de chat apparaîtront ici.[/color]"
        };
        box.AddChild(_results);
        _audit = new Label { Text = "Permission Administrator · scope Server · audit prêt", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _audit.AddThemeColorOverride("font_color", new("9f92b8"));
        box.AddChild(_audit);
    }

    private void Save(bool apply)
    {
        var candidate = _profile with
        {
            ServerRevision = _profile.ServerRevision + 1,
            Provenance = "toolset:r105",
            Authentication = _profile.Authentication with
            {
                RequireSecondFactor = _mfa!.ButtonPressed,
                SecondFactorAdapter = _mfaAdapter!.Text.Trim(),
                SessionLifetimeMinutes = (int)_sessionMinutes!.Value
            },
            Inspection = _profile.Inspection with
            {
                ReadOnlyByDefault = _inspectionReadOnly!.ButtonPressed,
                HistoryEntryLimit = (int)_historyLimit!.Value
            },
            Players = _profile.Players with
            {
                RefreshSeconds = (int)_refreshSeconds!.Value,
                LatencyAdapter = _latencyAdapter!.Text.Trim(),
                ShowVoiceStatus = _voiceStatus!.ButtonPressed,
                ShowModerationFlags = _moderationFlags!.ButtonPressed
            },
            Audit = _profile.Audit with
            {
                ImmutableHashChain = _immutableAudit!.ButtonPressed,
                RetentionDays = (int)_retentionDays!.Value,
                MaximumEntries = (int)_maximumEntries!.Value,
                ExternalSinkAdapter = _auditSink!.Text.Trim()
            },
            Chat = _profile.Chat with
            {
                MaximumMessageLength = (int)_messageLength!.Value,
                ModerationEnabled = _moderation!.ButtonPressed,
                ImmutableLogs = _immutableChat!.ButtonPressed,
                VoiceAdapter = _voiceAdapter!.Text.Trim()
            }
        };
        var result = _history.Execute(new(Guid.NewGuid(), 1, "toolset:r105", _profile.ServerRevision,
            "toolset:local-author", NetworkRole.Administrator, NetworkAuthorityScope.Server, apply, candidate));
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
        var result = LiveDirectorAccessAcceptanceScenario.Run();
        _status!.Text = result.Passed ? "✓ Les 5 fonctions Live Director sont validées" : "✕ Échec · consultez les diagnostics";
        _status.AddThemeColorOverride("font_color", result.Passed ? new("70d6a3") : new("ff806d"));
        _results!.Text =
            $"[color=#ff9858]Authentification[/color]  {result.AuthenticationCode} · session active {result.SessionActive}\n" +
            $"[color=#70d6a3]Examiner[/color]  {result.InspectionCode} · {result.InspectionSections} sections en lecture seule\n" +
            $"[color=#70d6a3]Joueurs[/color]  {result.Players} connecté · latence {result.LatencyMs} ms · {result.RefreshedLatencies} rafraîchi\n" +
            $"[color=#c9bddc]Audit[/color]  {result.AuditEntries} entrées · chaîne vérifiée {result.AuditVerified}\n" +
            $"[color=#c9bddc]Chat[/color]  {result.ChatMessages} messages · dernier canal {result.LastChannel}";
        _audit!.Text = $"{result.PermissionCode} · diff={result.ProfileDiff} · {result.AuditTrail.Last()}";
    }

    private void Refresh()
    {
        if (_mfa is null) return;
        _mfa.ButtonPressed = _profile.Authentication.RequireSecondFactor;
        _mfaAdapter!.Text = _profile.Authentication.SecondFactorAdapter;
        _sessionMinutes!.Value = _profile.Authentication.SessionLifetimeMinutes;
        _inspectionReadOnly!.ButtonPressed = _profile.Inspection.ReadOnlyByDefault;
        _historyLimit!.Value = _profile.Inspection.HistoryEntryLimit;
        _refreshSeconds!.Value = _profile.Players.RefreshSeconds;
        _latencyAdapter!.Text = _profile.Players.LatencyAdapter;
        _voiceStatus!.ButtonPressed = _profile.Players.ShowVoiceStatus;
        _moderationFlags!.ButtonPressed = _profile.Players.ShowModerationFlags;
        _immutableAudit!.ButtonPressed = _profile.Audit.ImmutableHashChain;
        _retentionDays!.Value = _profile.Audit.RetentionDays;
        _maximumEntries!.Value = _profile.Audit.MaximumEntries;
        _auditSink!.Text = _profile.Audit.ExternalSinkAdapter;
        _messageLength!.Value = _profile.Chat.MaximumMessageLength;
        _moderation!.ButtonPressed = _profile.Chat.ModerationEnabled;
        _immutableChat!.ButtonPressed = _profile.Chat.ImmutableLogs;
        _voiceAdapter!.Text = _profile.Chat.VoiceAdapter;
        _audit!.Text = $"actor={_profile.AuditIdentity} · scope={_profile.AuthorityScope} · revision={_profile.ServerRevision}";
    }

    private static VBoxContainer Column(Container owner)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12);
        owner.AddChild(column);
        return column;
    }

    private static VBoxContainer CardBox(Container owner, string title, string subtitle)
    {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", Style("151022", "47365f"));
        owner.AddChild(card);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        card.AddChild(box);
        box.AddChild(Heading(title));
        var label = new Label { Text = subtitle, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new("b9accd"));
        box.AddChild(label);
        return box;
    }

    private static GridContainer FormGrid()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 5);
        return grid;
    }

    private static SpinBox AddNumber(Container owner, string label, double minimum, double maximum, string suffix = "")
    {
        owner.AddChild(new Label { Text = label });
        var input = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 1,
            Suffix = suffix,
            CustomMinimumSize = new(0, 31),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        owner.AddChild(input);
        return input;
    }

    private static LineEdit AddText(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label });
        var input = new LineEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new(0, 31),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        owner.AddChild(input);
        return input;
    }

    private static CheckBox AddCheckRow(Container owner, string text)
    {
        owner.AddChild(new Control());
        var check = new CheckBox { Text = text };
        owner.AddChild(check);
        return check;
    }

    private static void AddSummary(Container owner, string title, string value)
    {
        var card = new PanelContainer { CustomMinimumSize = new(0, 68), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", Style("151022", "47365f"));
        owner.AddChild(card);
        var box = new VBoxContainer();
        card.AddChild(box);
        box.AddChild(Heading(title));
        box.AddChild(new Label { Text = value });
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

    private static StyleBoxFlat Style(string background, string border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new(background),
            BorderColor = new(border),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14,
            ContentMarginTop = 12,
            ContentMarginRight = 14,
            ContentMarginBottom = 12
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static void AddButton(Container owner, string text, Action action, bool primary = false, int width = 130)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(width, 36) };
        if (primary) button.AddThemeStyleboxOverride("normal", Style("38264f", "8060aa"));
        button.Pressed += action;
        owner.AddChild(button);
    }
}
#endif
