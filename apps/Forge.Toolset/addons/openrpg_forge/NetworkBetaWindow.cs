#if TOOLS
using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Forge.Network.Sessions;
using Godot;
using NetworkRole = Forge.Network.Operations.NetworkRole;

namespace Forge.Toolset.Editor;

public partial class NetworkBetaWindow : Window
{
    private NetworkFoundationProfile _profile = NetworkFoundationProfile.CreateDefault("toolset:r102");
    private readonly NetworkFoundationHistory _history;
    private NetworkOperationsProfile _operations = NetworkOperationsProfile.CreateDefault("toolset:r103");
    private readonly NetworkOperationsHistory _operationsHistory;
    private NetworkPlayerExperienceProfile _playerExperience = NetworkPlayerExperienceProfile.CreateDefault("toolset:r121");
    private readonly NetworkPlayerExperienceHistory _playerExperienceHistory;
    private NetworkWorldAdministrationProfile _worldAdministration = NetworkWorldAdministrationProfile.CreateDefault("toolset:r122");
    private readonly NetworkWorldAdministrationHistory _worldAdministrationHistory;
    private NetworkPersistenceObservabilityProfile _persistenceObservability = NetworkPersistenceObservabilityProfile.CreateDefault("toolset:r123");
    private readonly NetworkPersistenceObservabilityHistory _persistenceObservabilityHistory;
    private NetworkResilienceCapacityProfile _resilienceCapacity = NetworkResilienceCapacityProfile.CreateDefault("toolset:r124");
    private readonly NetworkResilienceCapacityHistory _resilienceCapacityHistory;
    private LineEdit? _name;
    private SpinBox? _port;
    private SpinBox? _capacity;
    private OptionButton? _discovery;
    private Label? _configurationStatus;
    private Label? _scenarioStatus;
    private Label? _auditStatus;
    private RichTextLabel? _details;
    private LineEdit? _moduleVersion;
    private SpinBox? _chunkKilobytes;
    private CheckBox? _builderZoneScoped;
    private SpinBox? _chatRadius;
    private SpinBox? _eventCapacity;
    private Label? _operationsStatus;
    private Label? _operationsAudit;
    private SpinBox? _lobbySlots;
    private LineEdit? _characterTemplates;
    private CheckBox? _characterImport;
    private Label? _playerExperienceStatus;
    private Label? _playerExperienceAudit;
    private SpinBox? _maximumAreaInstances;
    private SpinBox? _privacyRetentionDays;
    private Label? _worldAdministrationStatus;
    private Label? _worldAdministrationAudit;
    private SpinBox? _backupRetentionDays;
    private SpinBox? _reconnectionGraceSeconds;
    private Label? _persistenceObservabilityStatus;
    private Label? _persistenceObservabilityAudit;
    private SpinBox? _snapshotIntervalMinutes;
    private SpinBox? _maximumPlayers;
    private Label? _resilienceCapacityStatus;
    private Label? _resilienceCapacityAudit;
    private Action<Window>? _applyIcon;
    private Action? _openAssurance;
    private string? _capturePath;
    private double _captureDelay;
    private bool _capturePrepared;
    private bool _captureR103;

    public NetworkBetaWindow()
    {
        _history = new(_profile);
        _operationsHistory = new(_operations);
        _playerExperienceHistory = new(_playerExperience);
        _worldAdministrationHistory = new(_worldAdministration);
        _persistenceObservabilityHistory = new(_persistenceObservability);
        _resilienceCapacityHistory = new(_resilienceCapacity);
    }

    public void Initialize(Action<Window> applyIcon, Action openAssurance)
    {
        _applyIcon = applyIcon;
        _openAssurance = openAssurance;
    }

    public override void _Ready()
    {
        Title = "Network Foundation · OpenRPG Forge";
        Size = new(1500, 920);
        MinSize = new(1100, 720);
        CloseRequested += Hide;
        BuildUi();
        Refresh();
        _applyIcon?.Invoke(this);
        var captureArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r103-network-operations-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--r102-network-foundation-ui-capture=", StringComparison.Ordinal) ||
            value.StartsWith("--network-ui-capture=", StringComparison.Ordinal));
        if (captureArgument is not null)
        {
            _captureR103 = captureArgument.StartsWith("--r103", StringComparison.Ordinal);
            _capturePath = captureArgument[(captureArgument.IndexOf('=') + 1)..];
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
        var path = _capturePath;
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        var marker = _captureR103
            ? "OPENRPG_FORGE_R103_NETWORK_OPERATIONS_UI_CAPTURE"
            : "OPENRPG_FORGE_R102_NETWORK_FOUNDATION_UI_CAPTURE";
        GD.Print($"{marker}_{(error == Error.Ok ? "PASS" : "FAILED")} path={path} size={Size.X}x{Size.Y}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
    }

    public bool RunR102SmokeTest()
    {
        var foundation = NetworkFoundationAcceptanceScenario.Run();
        var store = new NetworkFoundationStore();
        var roundTrip = store.Deserialize(store.Serialize(_profile));
        var optional = NetworkFoundationRuntimeAdapter.Resolve(null, false);
        var required = NetworkFoundationRuntimeAdapter.Resolve(null, true);
        var invalid = NetworkFoundationValidator.Validate(_profile with
        {
            Interest = _profile.Interest with { MaximumDistance = 0 }
        });
        return foundation.Passed && store.Serialize(roundTrip) == store.Serialize(_profile) &&
            !optional.Available && optional.Diagnostics.Count == 0 &&
            !required.Available && required.Diagnostics.Single().Code == "NET_FOUNDATION_REQUIRED_MISSING" &&
            invalid.Any(item => item.Code == "NET_INTEREST_DISTANCE_INVALID");
    }

    public bool RunR103SmokeTest()
    {
        var scenario = NetworkOperationsAcceptanceScenario.Run();
        var store = new NetworkOperationsStore();
        var loaded = store.Deserialize(store.Serialize(_operations));
        var optional = NetworkOperationsRuntimeAdapter.Resolve(null, false);
        var required = NetworkOperationsRuntimeAdapter.Resolve(null, true);
        var invalid = NetworkOperationsValidator.Validate(_operations with
        {
            Chat = _operations.Chat with { LocalRadius = 0 }
        });
        var runtime = NetworkOperationsRuntimeAdapter.Build(loaded);
        return scenario.Passed && store.Serialize(loaded) == store.Serialize(_operations) &&
            !optional.Available && optional.Diagnostics.Count == 0 &&
            !required.Available && required.Diagnostics.Single().Code == "NET_OPERATIONS_REQUIRED_MISSING" &&
            invalid.Any(item => item.Code == "NET_CHAT_POLICY_INVALID") &&
            runtime.Scripts.Read(runtime.Scripts.Handle).RoleCount == 6;
    }

    public bool RunR121SmokeTest()
    {
        var store = new NetworkPlayerExperienceStore();
        var loaded = store.Deserialize(store.Serialize(_playerExperience));
        var invalid = NetworkPlayerExperienceValidator.Validate(_playerExperience with
        {
            Discovery = _playerExperience.Discovery with
            {
                DirectAddress = false,
                LanDiscovery = false,
                CuratedDirectory = false
            }
        });
        var optional = NetworkPlayerExperienceRuntimeAdapter.Resolve(null, false);
        var required = NetworkPlayerExperienceRuntimeAdapter.Resolve(null, true);
        var script = new NetworkPlayerExperienceScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_playerExperience) &&
            invalid.Any(item => item.Code == "NET_DISCOVERY_ROUTE_REQUIRED") &&
            !optional.Available && optional.Diagnostics.Count == 0 &&
            !required.Available && required.Diagnostics.Single().Code == "NET_PLAYER_EXPERIENCE_REQUIRED_MISSING" &&
            script.Read(script.Handle) is { ConnectionRoutes: 3, AuthorityModes: 4, ConsentRequired: true };
    }

    public bool RunR122SmokeTest()
    {
        var store = new NetworkWorldAdministrationStore();
        var loaded = store.Deserialize(store.Serialize(_worldAdministration));
        var invalid = NetworkWorldAdministrationValidator.Validate(_worldAdministration with
        {
            Administration = _worldAdministration.Administration with { RequestsPerMinute = 0 }
        });
        var optional = NetworkWorldAdministrationRuntimeAdapter.Resolve(null, false);
        var required = NetworkWorldAdministrationRuntimeAdapter.Resolve(null, true);
        var script = new NetworkWorldAdministrationScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_worldAdministration) &&
            invalid.Any(item => item.Code == "NET_REMOTE_ADMIN_POLICY_INVALID") &&
            !optional.Available && optional.Diagnostics.Count == 0 &&
            !required.Available && required.Diagnostics.Single().Code == "NET_WORLD_ADMIN_REQUIRED_MISSING" &&
            script.Read(script.Handle) is { ProviderCount: 5, SafeCommandCount: 4, RetentionDays: 90 };
    }

    public bool RunR123SmokeTest()
    {
        var store = new NetworkPersistenceObservabilityStore();
        var loaded = store.Deserialize(store.Serialize(_persistenceObservability));
        var invalid = NetworkPersistenceObservabilityValidator.Validate(_persistenceObservability with
        {
            Sql = _persistenceObservability.Sql with { RawSqlAllowed = true }
        });
        var optional = NetworkPersistenceObservabilityRuntimeAdapter.Resolve(null, false);
        var required = NetworkPersistenceObservabilityRuntimeAdapter.Resolve(null, true);
        var script = new NetworkPersistenceObservabilityScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_persistenceObservability) &&
            invalid.Any(item => item.Code == "NET_SECURE_SQL_POLICY_INVALID") &&
            !optional.Available && optional.Diagnostics.Count == 0 &&
            !required.Available && required.Diagnostics.Single().Code == "NET_PERSIST_OBSERVE_REQUIRED_MISSING" &&
            script.Read(script.Handle) is { ProviderCount: 2, MetricCount: 6, ReconnectionGraceSeconds: 120 };
    }

    public bool RunR124SmokeTest()
    {
        var store = new NetworkResilienceCapacityStore();
        var loaded = store.Deserialize(store.Serialize(_resilienceCapacity));
        var invalid = NetworkResilienceCapacityValidator.Validate(_resilienceCapacity with
        {
            Capacity = _resilienceCapacity.Capacity with { ReservedAdministratorSlots = 64 }
        });
        var script = new NetworkResilienceCapacityScriptService(loaded);
        return store.Serialize(loaded) == store.Serialize(_resilienceCapacity) &&
            invalid.Any(item => item.Code == "NET_CAPACITY_POLICY_INVALID") &&
            NetworkResilienceCapacityRuntimeAdapter.Resolve(null, false).Diagnostics.Count == 0 &&
            NetworkResilienceCapacityRuntimeAdapter.Resolve(null, true).Diagnostics.Single().Code == "NET_RESILIENCE_REQUIRED_MISSING" &&
            script.Read(script.Handle) is { SnapshotMinutes: 15, MaximumPlayers: 64, ReservedSlots: 4 };
    }

    private void BuildUi()
    {
        var background = new ColorRect { Color = new("0b0915") };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(background);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label
        {
            Text = "NETWORK CONTROL CENTER · FONDATION & OPÉRATIONS",
            TooltipText = "Configuration et validation du serveur autoritaire et de ses opérations"
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new("ff9858"));
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "Configurez serveur, contenu, accès, chat et événements sans fichier généré, puis validez le parcours intégré."
        };
        subtitle.AddThemeColorOverride("font_color", new("c9bddc"));
        root.AddChild(subtitle);

        var summary = new HBoxContainer();
        summary.AddThemeConstantOverride("separation", 12);
        root.AddChild(summary);
        AddSummary(summary, "CONTENU", "Chunks · reprise · cache · confiance");
        AddSummary(summary, "VERSION", "Lockfile · fingerprint · rapport");
        AddSummary(summary, "ACCÈS", "6 rôles · ABAC · capacités custom");
        AddSummary(summary, "CHAT", "7 canaux · langue · accessibilité");
        AddSummary(summary, "ÉVÉNEMENTS", "Priorités · trace · backpressure");

        var body = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(body);
        var formCard = Card();
        body.AddChild(formCard);
        var form = new VBoxContainer();
        form.AddThemeConstantOverride("separation", 8);
        formCard.AddChild(form);
        form.AddChild(Heading("CONFIGURATION CANONIQUE DU SERVEUR"));
        _name = AddField(form, "Nom visible");
        form.AddChild(new Label { Text = "Port" });
        _port = new SpinBox { MinValue = 1024, MaxValue = 65535, Step = 1, CustomMinimumSize = new(0, 34) };
        form.AddChild(_port);
        form.AddChild(new Label { Text = "Capacité" });
        _capacity = new SpinBox { MinValue = 1, MaxValue = 64, Step = 1, CustomMinimumSize = new(0, 34) };
        form.AddChild(_capacity);
        form.AddChild(new Label { Text = "Découverte" });
        _discovery = new OptionButton { CustomMinimumSize = new(0, 34) };
        _discovery.AddItem("Réseau local");
        _discovery.AddItem("Connexion directe");
        form.AddChild(_discovery);
        _configurationStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        form.AddChild(_configurationStatus);
        var buttons = new HBoxContainer();
        form.AddChild(buttons);
        AddButton(buttons, "Prévisualiser", () => Save(false));
        AddButton(buttons, "Appliquer", () => Save(true), true);
        var historyButtons = new HBoxContainer();
        form.AddChild(historyButtons);
        AddButton(historyButtons, "Annuler", Undo);
        AddButton(historyButtons, "Rétablir", Redo);
        form.AddChild(Heading("IDENTITÉ, PORTÉE & AUDIT"));
        _auditStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        form.AddChild(_auditStatus);
        form.AddChild(new Label
        {
            Text = "UUID stables · schéma v1 · provenance toolset:r102\nPortée Server · permission Administrator · révision optimiste",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var validationCard = Card();
        body.AddChild(validationCard);
        var validationScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        validationCard.AddChild(validationScroll);
        var validation = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        validation.AddThemeConstantOverride("separation", 8);
        validationScroll.AddChild(validation);
        validation.AddChild(Heading("OPÉRATIONS CANONIQUES R103"));
        validation.AddChild(new Label
        {
            Text = "Synchronisation adressée par hash · verrou avant join · RBAC/ABAC · chat accessible · bus typé",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        var operationGrid = new GridContainer { Columns = 2 };
        operationGrid.AddThemeConstantOverride("h_separation", 12);
        operationGrid.AddThemeConstantOverride("v_separation", 6);
        validation.AddChild(operationGrid);
        operationGrid.AddChild(new Label { Text = "Version module" });
        _moduleVersion = new LineEdit { CustomMinimumSize = new(250, 32) };
        operationGrid.AddChild(_moduleVersion);
        operationGrid.AddChild(new Label { Text = "Chunk (Kio)" });
        _chunkKilobytes = new SpinBox { MinValue = 16, MaxValue = 4096, Step = 16, CustomMinimumSize = new(250, 32) };
        operationGrid.AddChild(_chunkKilobytes);
        operationGrid.AddChild(new Label { Text = "Builder borné par zone" });
        _builderZoneScoped = new CheckBox { Text = "Portée ABAC visible" };
        operationGrid.AddChild(_builderZoneScoped);
        operationGrid.AddChild(new Label { Text = "Proximité chat" });
        _chatRadius = new SpinBox { MinValue = 1, MaxValue = 10000, Step = 1, Suffix = " m", CustomMinimumSize = new(250, 32) };
        operationGrid.AddChild(_chatRadius);
        operationGrid.AddChild(new Label { Text = "File événements" });
        _eventCapacity = new SpinBox { MinValue = 1, MaxValue = 65536, Step = 1, CustomMinimumSize = new(250, 32) };
        operationGrid.AddChild(_eventCapacity);
        var operationButtons = new HBoxContainer();
        validation.AddChild(operationButtons);
        AddButton(operationButtons, "Prévisualiser", () => SaveOperations(false));
        AddButton(operationButtons, "Appliquer", () => SaveOperations(true), true);
        AddButton(operationButtons, "Annuler", UndoOperations);
        AddButton(operationButtons, "Rétablir", RedoOperations);
        _operationsStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_operationsStatus);
        _operationsAudit = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_operationsAudit);
        validation.AddChild(Heading("PARCOURS JOUEUR R121"));
        var playerGrid = new GridContainer { Columns = 2 };
        playerGrid.AddThemeConstantOverride("h_separation", 12);
        playerGrid.AddThemeConstantOverride("v_separation", 6);
        validation.AddChild(playerGrid);
        playerGrid.AddChild(new Label { Text = "Places du lobby" });
        _lobbySlots = new SpinBox { MinValue = 1, MaxValue = 64, Step = 1, CustomMinimumSize = new(250, 32) };
        playerGrid.AddChild(_lobbySlots);
        playerGrid.AddChild(new Label { Text = "Templates personnage" });
        _characterTemplates = new LineEdit { PlaceholderText = "template:adventurer", CustomMinimumSize = new(250, 32) };
        playerGrid.AddChild(_characterTemplates);
        playerGrid.AddChild(new Label { Text = "Import personnage" });
        _characterImport = new CheckBox { Text = "Autorisé avec signature et approbation serveur" };
        playerGrid.AddChild(_characterImport);
        var playerButtons = new HBoxContainer();
        validation.AddChild(playerButtons);
        AddButton(playerButtons, "Prévisualiser", () => SavePlayerExperience(false));
        AddButton(playerButtons, "Appliquer", () => SavePlayerExperience(true), true);
        AddButton(playerButtons, "Annuler", UndoPlayerExperience);
        AddButton(playerButtons, "Rétablir", RedoPlayerExperience);
        _playerExperienceStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_playerExperienceStatus);
        _playerExperienceAudit = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_playerExperienceAudit);
        validation.AddChild(Heading("EXPLOITATION SERVEUR R122"));
        var worldGrid = new GridContainer { Columns = 2 };
        worldGrid.AddThemeConstantOverride("h_separation", 12);
        worldGrid.AddThemeConstantOverride("v_separation", 6);
        validation.AddChild(worldGrid);
        worldGrid.AddChild(new Label { Text = "Instances de zones" });
        _maximumAreaInstances = new SpinBox { MinValue = 1, MaxValue = 1024, Step = 1, CustomMinimumSize = new(250, 32) };
        worldGrid.AddChild(_maximumAreaInstances);
        worldGrid.AddChild(new Label { Text = "Rétention modération" });
        _privacyRetentionDays = new SpinBox { MinValue = 1, MaxValue = 3650, Step = 1, Suffix = " jours", CustomMinimumSize = new(250, 32) };
        worldGrid.AddChild(_privacyRetentionDays);
        var worldButtons = new HBoxContainer();
        validation.AddChild(worldButtons);
        AddButton(worldButtons, "Prévisualiser", () => SaveWorldAdministration(false));
        AddButton(worldButtons, "Appliquer", () => SaveWorldAdministration(true), true);
        AddButton(worldButtons, "Annuler", UndoWorldAdministration);
        AddButton(worldButtons, "Rétablir", RedoWorldAdministration);
        _worldAdministrationStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_worldAdministrationStatus);
        _worldAdministrationAudit = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_worldAdministrationAudit);
        validation.AddChild(Heading("PERSISTANCE & OBSERVABILITÉ R123"));
        var persistenceGrid = new GridContainer { Columns = 2 };
        persistenceGrid.AddThemeConstantOverride("h_separation", 12);
        persistenceGrid.AddThemeConstantOverride("v_separation", 6);
        validation.AddChild(persistenceGrid);
        persistenceGrid.AddChild(new Label { Text = "Rétention sauvegardes" });
        _backupRetentionDays = new SpinBox { MinValue = 1, MaxValue = 3650, Step = 1, Suffix = " jours", CustomMinimumSize = new(250, 32) };
        persistenceGrid.AddChild(_backupRetentionDays);
        persistenceGrid.AddChild(new Label { Text = "Grâce de reconnexion" });
        _reconnectionGraceSeconds = new SpinBox { MinValue = 5, MaxValue = 3600, Step = 5, Suffix = " s", CustomMinimumSize = new(250, 32) };
        persistenceGrid.AddChild(_reconnectionGraceSeconds);
        var persistenceButtons = new HBoxContainer();
        validation.AddChild(persistenceButtons);
        AddButton(persistenceButtons, "Prévisualiser", () => SavePersistenceObservability(false));
        AddButton(persistenceButtons, "Appliquer", () => SavePersistenceObservability(true), true);
        AddButton(persistenceButtons, "Annuler", UndoPersistenceObservability);
        AddButton(persistenceButtons, "Rétablir", RedoPersistenceObservability);
        _persistenceObservabilityStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_persistenceObservabilityStatus);
        _persistenceObservabilityAudit = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_persistenceObservabilityAudit);
        validation.AddChild(Heading("SAUVEGARDES & CAPACITÉ R124"));
        var resilienceGrid = new GridContainer { Columns = 2 };
        validation.AddChild(resilienceGrid);
        resilienceGrid.AddChild(new Label { Text = "Intervalle snapshots" });
        _snapshotIntervalMinutes = new SpinBox { MinValue = 1, MaxValue = 10080, Step = 1, Suffix = " min", CustomMinimumSize = new(250, 32) };
        resilienceGrid.AddChild(_snapshotIntervalMinutes);
        resilienceGrid.AddChild(new Label { Text = "Capacité joueurs" });
        _maximumPlayers = new SpinBox { MinValue = 1, MaxValue = 100000, Step = 1, CustomMinimumSize = new(250, 32) };
        resilienceGrid.AddChild(_maximumPlayers);
        var resilienceButtons = new HBoxContainer();
        validation.AddChild(resilienceButtons);
        AddButton(resilienceButtons, "Prévisualiser", () => SaveResilienceCapacity(false));
        AddButton(resilienceButtons, "Appliquer", () => SaveResilienceCapacity(true), true);
        AddButton(resilienceButtons, "Annuler", UndoResilienceCapacity);
        AddButton(resilienceButtons, "Rétablir", RedoResilienceCapacity);
        _resilienceCapacityStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_resilienceCapacityStatus);
        _resilienceCapacityAudit = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        validation.AddChild(_resilienceCapacityAudit);
        validation.AddChild(Heading("VALIDATION SERVEUR INTÉGRÉE"));
        validation.AddChild(new Label
        {
            Text = "Le test contrôle cache repris, mismatch lisible, permissions, proximité, bridge, priorité et backpressure.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        var run = new Button { Text = "▶ Tester avec deux clients", CustomMinimumSize = new(0, 42) };
        run.AddThemeColorOverride("font_color", Colors.White);
        run.AddThemeStyleboxOverride("normal", CardStyle("d84f32", "ff8b63"));
        run.Pressed += RunScenario;
        validation.AddChild(run);
        _scenarioStatus = new Label { Text = "Pas encore testé" };
        _scenarioStatus.AddThemeColorOverride("font_color", new("aa9bc7"));
        validation.AddChild(_scenarioStatus);
        _details = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(560, 120)
        };
        validation.AddChild(_details);
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        root.AddChild(footer);
        var assurance = new Button { Text = "Assurance réseau R104", CustomMinimumSize = new(210, 38) };
        assurance.Pressed += () => _openAssurance?.Invoke();
        footer.AddChild(assurance);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new(110, 38) };
        close.Pressed += Hide;
        footer.AddChild(close);
    }

    private void Save(bool apply)
    {
        var server = _profile.Server with
        {
            Name = _name!.Text.Trim(),
            Port = (int)_port!.Value,
            Capacity = (int)_capacity!.Value,
            Discovery = _discovery!.Selected == 0 ? DiscoveryMode.Lan : DiscoveryMode.Direct
        };
        var candidate = _profile with
        {
            Server = server,
            ServerRevision = _profile.ServerRevision + 1,
            Provenance = "toolset:r102"
        };
        var result = _history.Execute(new(Guid.NewGuid(), 1, "toolset:r102", _profile.ServerRevision,
            "toolset:local-author", NetworkRole.Administrator, apply, candidate));
        if (result.Applied) _profile = result.Profile;
        _configurationStatus!.Text = result.Diagnostics.Count == 0
            ? $"✓ {(apply ? "Configuration appliquée" : "Prévisualisation valide")} · {result.SemanticDiff}"
            : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
        _configurationStatus.AddThemeColorOverride("font_color",
            result.Diagnostics.Count == 0 ? new("70d6a3") : new("ff806d"));
        _auditStatus!.Text = result.AuditEntry;
    }

    private void Undo()
    {
        var result = _history.Undo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _profile = result.Profile;
        _auditStatus!.Text = result.AuditEntry;
        Refresh();
    }

    private void Redo()
    {
        var result = _history.Redo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _profile = result.Profile;
        _auditStatus!.Text = result.AuditEntry;
        Refresh();
    }

    private void SaveOperations(bool apply)
    {
        var grants = _operations.Roles.Grants.Select(item => item.Role == NetworkRole.Builder
            ? item with { ZoneScoped = _builderZoneScoped!.ButtonPressed }
            : item).ToArray();
        var candidate = _operations with
        {
            ServerRevision = _operations.ServerRevision + 1,
            Provenance = "toolset:r103",
            Content = _operations.Content with { ChunkBytes = (int)_chunkKilobytes!.Value * 1024 },
            VersionLock = _operations.VersionLock with { ModuleVersion = _moduleVersion!.Text.Trim() },
            Roles = _operations.Roles with { Grants = grants },
            Chat = _operations.Chat with { LocalRadius = (float)_chatRadius!.Value },
            Events = _operations.Events with { MaximumPendingEvents = (int)_eventCapacity!.Value }
        };
        var result = _operationsHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r103",
            _operations.ServerRevision, "toolset:local-author", NetworkRole.Administrator, apply, candidate));
        if (result.Applied) _operations = result.Profile;
        _operationsStatus!.Text = result.Diagnostics.Count == 0
            ? $"✓ {(apply ? "Opérations appliquées" : "Prévisualisation valide")} · {result.SemanticDiff}"
            : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
        _operationsStatus.AddThemeColorOverride("font_color",
            result.Diagnostics.Count == 0 ? new("70d6a3") : new("ff806d"));
        _operationsAudit!.Text = result.AuditEntry;
    }

    private void UndoOperations()
    {
        var result = _operationsHistory.Undo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _operations = result.Profile;
        _operationsAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void SavePlayerExperience(bool apply)
    {
        var templates = _characterTemplates!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidate = _playerExperience with
        {
            ServerRevision = _playerExperience.ServerRevision + 1,
            Provenance = "toolset:r121",
            Lobby = _playerExperience.Lobby with { MaximumSlots = (int)_lobbySlots!.Value },
            CharacterBuilder = _playerExperience.CharacterBuilder with
            {
                TemplateIds = templates,
                ImportAllowed = _characterImport!.ButtonPressed
            }
        };
        var result = _playerExperienceHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r121",
            _playerExperience.ServerRevision, "toolset:local-author", NetworkRole.Administrator, apply, candidate));
        if (result.Applied) _playerExperience = result.Profile;
        _playerExperienceStatus!.Text = result.Diagnostics.Count == 0
            ? $"✓ {(apply ? "Parcours appliqué" : "Prévisualisation valide")} · {result.SemanticDiff}"
            : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
        _playerExperienceStatus.AddThemeColorOverride("font_color",
            result.Diagnostics.Count == 0 ? new("70d6a3") : new("ff806d"));
        _playerExperienceAudit!.Text = result.AuditEntry;
    }

    private void UndoPlayerExperience()
    {
        var result = _playerExperienceHistory.Undo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _playerExperience = result.Profile;
        _playerExperienceAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void RedoPlayerExperience()
    {
        var result = _playerExperienceHistory.Redo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _playerExperience = result.Profile;
        _playerExperienceAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void SaveWorldAdministration(bool apply)
    {
        var candidate = _worldAdministration with
        {
            ServerRevision = _worldAdministration.ServerRevision + 1,
            Provenance = "toolset:r122",
            Instances = _worldAdministration.Instances with { MaximumInstances = (int)_maximumAreaInstances!.Value },
            Moderation = _worldAdministration.Moderation with { PrivacyRetentionDays = (int)_privacyRetentionDays!.Value }
        };
        var result = _worldAdministrationHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r122",
            _worldAdministration.ServerRevision, "toolset:server-operator", NetworkRole.Administrator, apply, candidate));
        if (result.Applied) _worldAdministration = result.Profile;
        _worldAdministrationStatus!.Text = result.Diagnostics.Count == 0
            ? $"✓ {(apply ? "Exploitation appliquée" : "Prévisualisation valide")} · {result.SemanticDiff}"
            : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
        _worldAdministrationStatus.AddThemeColorOverride("font_color",
            result.Diagnostics.Count == 0 ? new("70d6a3") : new("ff806d"));
        _worldAdministrationAudit!.Text = result.AuditEntry;
    }

    private void UndoWorldAdministration()
    {
        var result = _worldAdministrationHistory.Undo("toolset:server-operator", NetworkRole.Administrator);
        if (result.Succeeded) _worldAdministration = result.Profile;
        _worldAdministrationAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void RedoWorldAdministration()
    {
        var result = _worldAdministrationHistory.Redo("toolset:server-operator", NetworkRole.Administrator);
        if (result.Succeeded) _worldAdministration = result.Profile;
        _worldAdministrationAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void SavePersistenceObservability(bool apply)
    {
        var candidate = _persistenceObservability with
        {
            ServerRevision = _persistenceObservability.ServerRevision + 1,
            Provenance = "toolset:r123",
            Persistence = _persistenceObservability.Persistence with
            {
                BackupRetentionDays = (int)_backupRetentionDays!.Value
            },
            Reconnection = _persistenceObservability.Reconnection with
            {
                GracePeriodSeconds = (int)_reconnectionGraceSeconds!.Value
            }
        };
        var result = _persistenceObservabilityHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r123",
            _persistenceObservability.ServerRevision, "toolset:server-operator", NetworkRole.Administrator,
            apply, candidate));
        if (result.Applied) _persistenceObservability = result.Profile;
        _persistenceObservabilityStatus!.Text = result.Diagnostics.Count == 0
            ? $"✓ {(apply ? "Politique appliquée" : "Prévisualisation valide")} · {result.SemanticDiff}"
            : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
        _persistenceObservabilityStatus.AddThemeColorOverride("font_color",
            result.Diagnostics.Count == 0 ? new("70d6a3") : new("ff806d"));
        _persistenceObservabilityAudit!.Text = result.AuditEntry;
    }

    private void UndoPersistenceObservability()
    {
        var result = _persistenceObservabilityHistory.Undo("toolset:server-operator", NetworkRole.Administrator);
        if (result.Succeeded) _persistenceObservability = result.Profile;
        _persistenceObservabilityAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void RedoPersistenceObservability()
    {
        var result = _persistenceObservabilityHistory.Redo("toolset:server-operator", NetworkRole.Administrator);
        if (result.Succeeded) _persistenceObservability = result.Profile;
        _persistenceObservabilityAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void SaveResilienceCapacity(bool apply)
    {
        var candidate = _resilienceCapacity with
        {
            ServerRevision = _resilienceCapacity.ServerRevision + 1,
            Provenance = "toolset:r124",
            Backup = _resilienceCapacity.Backup with { SnapshotIntervalMinutes = (int)_snapshotIntervalMinutes!.Value },
            Capacity = _resilienceCapacity.Capacity with { MaximumPlayers = (int)_maximumPlayers!.Value }
        };
        var result = _resilienceCapacityHistory.Execute(new(Guid.NewGuid(), 1, "toolset:r124",
            _resilienceCapacity.ServerRevision, "toolset:server-operator", NetworkRole.Administrator, apply, candidate));
        if (result.Applied) _resilienceCapacity = result.Profile;
        _resilienceCapacityStatus!.Text = result.Diagnostics.Count == 0
            ? $"✓ {(apply ? "Politique appliquée" : "Prévisualisation valide")} · {result.SemanticDiff}"
            : $"⚠ {result.Diagnostics[0].Code} · {result.Diagnostics[0].Message}";
        _resilienceCapacityAudit!.Text = result.AuditEntry;
    }

    private void UndoResilienceCapacity()
    {
        var result = _resilienceCapacityHistory.Undo("toolset:server-operator", NetworkRole.Administrator);
        if (result.Succeeded) _resilienceCapacity = result.Profile;
        _resilienceCapacityAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void RedoResilienceCapacity()
    {
        var result = _resilienceCapacityHistory.Redo("toolset:server-operator", NetworkRole.Administrator);
        if (result.Succeeded) _resilienceCapacity = result.Profile;
        _resilienceCapacityAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void RedoOperations()
    {
        var result = _operationsHistory.Redo("toolset:local-author", NetworkRole.Administrator);
        if (result.Succeeded) _operations = result.Profile;
        _operationsAudit!.Text = result.AuditEntry;
        Refresh();
    }

    private void RunScenario()
    {
        var result = NetworkBetaScenario.Run();
        var foundation = NetworkFoundationAcceptanceScenario.Run();
        var operations = NetworkOperationsAcceptanceScenario.Run();
        _scenarioStatus!.Text = result.Passed && foundation.Passed && operations.Passed
            ? "✓ Réseau prêt · fondation et opérations validées"
            : "✕ Échec du scénario réseau";
        _scenarioStatus.AddThemeColorOverride("font_color",
            result.Passed && foundation.Passed && operations.Passed ? new("70d6a3") : new("ff806d"));
        _details!.Text =
            $"[color=#ff9858]Contenu[/color]  {operations.ContentCode} · {operations.ContentChunks} chunks\n" +
            $"[color=#70d6a3]Version[/color]  {operations.VersionCode} · {operations.VersionDetails} écarts lisibles\n" +
            $"[color=#70d6a3]Permissions / Chat[/color]  {operations.PermissionCode} · {operations.ChatCode}\n" +
            $"[color=#c9bddc]Messages / bridges[/color]  {operations.ChatMessages} / {operations.BridgeReceipts}\n" +
            $"[color=#c9bddc]Événements / traces[/color]  {operations.EventHandlers} / {operations.EventTraces}\n" +
            $"[color=#70d6a3]Backpressure[/color]  {operations.BackpressureCode}\n" +
            $"[color=#9f92b8]Fondation[/color]  permission {foundation.PermissionCode} · réconciliation {foundation.PredictionCode}";
    }

    private void Refresh()
    {
        if (_name is null) return;
        _name.Text = _profile.Server.Name;
        _port!.Value = _profile.Server.Port;
        _capacity!.Value = _profile.Server.Capacity;
        _discovery!.Select(_profile.Server.Discovery == DiscoveryMode.Lan ? 0 : 1);
        _configurationStatus!.Text = "✓ Configuration v1 valide · aucun diagnostic";
        _configurationStatus.AddThemeColorOverride("font_color", new("70d6a3"));
        _auditStatus!.Text = $"actor={_profile.AuditIdentity} · scope={_profile.AuthorityScope} · revision={_profile.ServerRevision}";
        if (_moduleVersion is null) return;
        _moduleVersion.Text = _operations.VersionLock.ModuleVersion;
        _chunkKilobytes!.Value = _operations.Content.ChunkBytes / 1024;
        _builderZoneScoped!.ButtonPressed = _operations.Roles.Grants.Single(item => item.Role == NetworkRole.Builder).ZoneScoped;
        _chatRadius!.Value = _operations.Chat.LocalRadius;
        _eventCapacity!.Value = _operations.Events.MaximumPendingEvents;
        _operationsStatus!.Text = "✓ Profil opérations v1 valide · aucun diagnostic";
        _operationsStatus.AddThemeColorOverride("font_color", new("70d6a3"));
        _operationsAudit!.Text = $"actor={_operations.AuditIdentity} · scope={_operations.AuthorityScope} · revision={_operations.ServerRevision}";
        if (_lobbySlots is null) return;
        _lobbySlots.Value = _playerExperience.Lobby.MaximumSlots;
        _characterTemplates!.Text = string.Join(", ", _playerExperience.CharacterBuilder.TemplateIds);
        _characterImport!.ButtonPressed = _playerExperience.CharacterBuilder.ImportAllowed;
        _playerExperienceStatus!.Text = "✓ Connexion, lobby, personnages et PvP prêts · aucun diagnostic";
        _playerExperienceStatus.AddThemeColorOverride("font_color", new("70d6a3"));
        _playerExperienceAudit!.Text = $"actor={_playerExperience.AuditIdentity} · scope={_playerExperience.AuthorityScope} · revision={_playerExperience.ServerRevision}";
        if (_maximumAreaInstances is null) return;
        _maximumAreaInstances.Value = _worldAdministration.Instances.MaximumInstances;
        _privacyRetentionDays!.Value = _worldAdministration.Moderation.PrivacyRetentionDays;
        _worldAdministrationStatus!.Text = "✓ Instances, comptes, administration et modération prêts · aucun diagnostic";
        _worldAdministrationStatus.AddThemeColorOverride("font_color", new("70d6a3"));
        _worldAdministrationAudit!.Text = $"actor={_worldAdministration.AuditIdentity} · scope={_worldAdministration.AuthorityScope} · revision={_worldAdministration.ServerRevision}";
        if (_backupRetentionDays is null) return;
        _backupRetentionDays.Value = _persistenceObservability.Persistence.BackupRetentionDays;
        _reconnectionGraceSeconds!.Value = _persistenceObservability.Reconnection.GracePeriodSeconds;
        _persistenceObservabilityStatus!.Text = "✓ Persistance, SQL borné, métriques, profilage et reconnexion prêts · aucun diagnostic";
        _persistenceObservabilityStatus.AddThemeColorOverride("font_color", new("70d6a3"));
        _persistenceObservabilityAudit!.Text = $"actor={_persistenceObservability.AuditIdentity} · scope={_persistenceObservability.AuthorityScope} · revision={_persistenceObservability.ServerRevision}";
        if (_snapshotIntervalMinutes is null) return;
        _snapshotIntervalMinutes.Value = _resilienceCapacity.Backup.SnapshotIntervalMinutes;
        _maximumPlayers!.Value = _resilienceCapacity.Capacity.MaximumPlayers;
        _resilienceCapacityStatus!.Text = "✓ Snapshots, rollback, file, réservations et délestage prêts · aucun diagnostic";
        _resilienceCapacityStatus.AddThemeColorOverride("font_color", new("70d6a3"));
        _resilienceCapacityAudit!.Text = $"actor={_resilienceCapacity.AuditIdentity} · scope={_resilienceCapacity.AuthorityScope} · revision={_resilienceCapacity.ServerRevision}";
    }

    private static PanelContainer Card()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new(500, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", CardStyle("151022", "47365f"));
        return panel;
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
            ContentMarginLeft = 18,
            ContentMarginTop = 16,
            ContentMarginRight = 18,
            ContentMarginBottom = 16
        };
        style.SetBorderWidthAll(1);
        return style;
    }

    private static Label Heading(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new("ff9858"));
        return label;
    }

    private static LineEdit AddField(Container owner, string label)
    {
        owner.AddChild(new Label { Text = label });
        var field = new LineEdit { CustomMinimumSize = new(0, 34) };
        owner.AddChild(field);
        return field;
    }

    private static void AddSummary(Container owner, string title, string value)
    {
        var card = Card();
        card.CustomMinimumSize = new(0, 72);
        owner.AddChild(card);
        var box = new VBoxContainer();
        card.AddChild(box);
        box.AddChild(Heading(title));
        box.AddChild(new Label { Text = value });
    }

    private static void AddButton(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(130, 34) };
        if (primary) button.AddThemeStyleboxOverride("normal", CardStyle("38264f", "8060aa"));
        button.Pressed += action;
        owner.AddChild(button);
    }
}
#endif
