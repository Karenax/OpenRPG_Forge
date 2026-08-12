#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Lifecycle;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

public partial class RuntimePoliciesWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _active;
    private Func<ModuleRuntimePolicyProfile, AreaId, AreaRuntimePolicyProfile, bool>? _apply;
    private Action<Window>? _brand;
    private ModuleRuntimePolicyProfile? _module;
    private AreaRuntimePolicyProfile? _area;

    private CheckBox? _globalEnabled;
    private SpinBox? _globalInterval;
    private SpinBox? _globalBudget;
    private LineEdit? _globalHandler;
    private CheckBox? _areaEnabled;
    private SpinBox? _areaInterval;
    private SpinBox? _areaBudget;
    private LineEdit? _areaHandler;
    private CheckBox? _suspendInactive;
    private CheckBox? _restEnabled;
    private OptionButton? _partyPolicy;
    private SpinBox? _restDuration;
    private OptionButton? _restMode;
    private LineEdit? _denialReason;
    private LineEdit? _condition;
    private SpinBox? _cooldown;
    private SpinBox? _secureRadius;
    private CheckBox? _denialVolume;
    private SpinBox? _volumeSize;
    private CheckBox? _abortEnabled;
    private CheckBox? _restoreCamera;
    private CheckBox? _restoreControl;
    private CheckBox? _restoreInvulnerability;
    private CheckBox? _restoreLocks;
    private CheckBox? _restoreNarrative;
    private SpinBox? _rollbackSteps;
    private Label? _status;
    private TabContainer? _tabs;
    private CheckBox? _projectEventsEnabled;
    private CheckBox? _projectEventsRequired;
    private CheckBox? _projectEventsDowned;
    private CheckBox? _projectEventsRespawn;
    private CheckBox? _projectEventsReplicated;
    private readonly Dictionary<string, LineEdit> _projectEventHandlers = new(StringComparer.Ordinal);
    private readonly List<CustomEventSchema> _customEvents = [];
    private CheckBox? _worldEnabled;
    private CheckBox? _worldRequired;
    private SpinBox? _clockSpeed;
    private CheckBox? _clockPaused;
    private SpinBox? _daysPerMonth;
    private SpinBox? _monthsPerYear;
    private SpinBox? _hoursPerDay;
    private SpinBox? _minutesPerHour;
    private SpinBox? _initialMinute;
    private TextEdit? _areaOffsets;
    private ItemList? _customEventList;
    private LineEdit? _customNamespace;
    private LineEdit? _customName;
    private SpinBox? _customPayloadVersion;
    private LineEdit? _customFields;
    private LineEdit? _customHandler;
    private CheckBox? _customRequired;
    private string? _capturePath;
    private string _captureMarker = "R051_PROJECT_EVENTS";
    private int _captureTab = 3;
    private double _captureDelay;
    private bool _captureOpened;

    internal string Summary => _module is null || _area is null
        ? "unavailable"
        : $"{_module.Heartbeats.Count}:{_area.Heartbeats.Count}:{_module.Rest.Enabled}:" +
          $"{_area.Rest.Mode}:{_area.Rest.Volumes.Count}:{_module.SequenceAbort.Restore}:" +
          $"{_module.GameplayEvents.Bindings.Count}:{_module.World.CustomEvents.Count}";

    public void Initialize(Func<ProjectState?> state, Func<AreaId?> active,
        Func<ModuleRuntimePolicyProfile, AreaId, AreaRuntimePolicyProfile, bool> apply, Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _active = active ?? throw new ArgumentNullException(nameof(active));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Politiques d’exécution · OpenRPG Forge";
        MinSize = new Vector2I(1120, 720);
        Size = new Vector2I(1180, 780);
        Transient = true;
        Visible = false;
        Build();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r051-project-events-ui-capture=", StringComparison.Ordinal));
        var capturePrefix = "--r051-project-events-ui-capture=";
        capture ??= OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r052-project-world-ui-capture=", StringComparison.Ordinal));
        if (capture is not null && capture.StartsWith("--r052-project-world-ui-capture=", StringComparison.Ordinal))
        {
            capturePrefix = "--r052-project-world-ui-capture=";
            _captureMarker = "R052_PROJECT_WORLD";
            _captureTab = 4;
        }
        if (capture is not null)
        {
            var path = capture[capturePrefix.Length..].Trim();
            if (path.Length == 0)
                GD.PrintErr($"OPENRPG_FORGE_{_captureMarker}_UI_CAPTURE_FAILED reason=path-empty");
            else
            {
                _capturePath = path;
                SetProcess(true);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1.5)
        {
            Open();
            if (_tabs is not null) _tabs.CurrentTab = _captureTab;
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 4) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_{_captureMarker}_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination} size={image.GetWidth()}x{image.GetHeight()}");
        _capturePath = null;
        SetProcess(false);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered(Size);
        _brand?.Invoke(this);
    }

    internal bool ConfigureForSmokeTest(ModuleRuntimePolicyProfile module, AreaRuntimePolicyProfile area)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _area = area ?? throw new ArgumentNullException(nameof(area));
        Populate();
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 20);
        }

        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);
        var heading = new Label { Text = "RYTHME, REPOS & SÉQUENCES", ThemeTypeVariation = "HeaderSmall" };
        heading.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(heading);
        root.AddChild(new Label
        {
            Text = "Configurez des tâches bornées, le cycle de repos et la restauration sûre des cinématiques. Chaque application est une transaction annulable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(_tabs);
        BuildHeartbeatTab(_tabs);
        BuildRestTab(_tabs);
        BuildSequenceTab(_tabs);
        BuildProjectEventsTab(_tabs);
        BuildProjectWorldTab(_tabs);

        _status = new Label { Text = "Prêt · aucune mutation avant validation." };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        root.AddChild(_status);
        var actions = new HBoxContainer();
        actions.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var apply = new Button { Text = "Valider et appliquer", CustomMinimumSize = new Vector2(190, 40) };
        apply.Pressed += () => Save();
        actions.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide;
        actions.AddChild(close);
        root.AddChild(actions);
    }

    private void BuildHeartbeatTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Tâches planifiées");
        var global = Card(columns, "RYTHME GLOBAL DU MODULE");
        _globalEnabled = Check("Activer le timer global", false);
        global.AddChild(_globalEnabled);
        _globalInterval = Spin(100, 86_400_000, 2000, 100);
        _globalBudget = Spin(1, 1000, 12, 1);
        _globalHandler = Field("OnGlobalHeartbeat");
        global.AddChild(FieldRow("Intervalle (ms)", _globalInterval));
        global.AddChild(FieldRow("Budget (ms)", _globalBudget));
        global.AddChild(FieldRow("Handler ForgeScript", _globalHandler));
        global.AddChild(Info("Réservez ce rythme aux règles réellement globales. Le runtime plafonne le rattrapage après une pause."));

        var local = Card(columns, "RYTHME LOCAL DE LA ZONE");
        _areaEnabled = Check("Activer le timer de la zone", false);
        local.AddChild(_areaEnabled);
        _areaInterval = Spin(100, 86_400_000, 1000, 100);
        _areaBudget = Spin(1, 1000, 8, 1);
        _areaHandler = Field("OnAreaHeartbeat");
        _suspendInactive = Check("Suspendre lorsque la zone est inactive", true);
        local.AddChild(FieldRow("Intervalle (ms)", _areaInterval));
        local.AddChild(FieldRow("Budget (ms)", _areaBudget));
        local.AddChild(FieldRow("Handler ForgeScript", _areaHandler));
        local.AddChild(_suspendInactive);
        local.AddChild(Info("Un intervalle très court ou un dépassement répété produit un diagnostic de performance inspectable."));
    }

    private void BuildRestTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Repos");
        var lifecycle = Card(columns, "CYCLE DE REPOS");
        _restEnabled = Check("Autoriser le système de repos", false);
        lifecycle.AddChild(_restEnabled);
        _partyPolicy = EnumOption<PartyRestPolicy>();
        _restDuration = Spin(1, 86_400, 8, 1);
        lifecycle.AddChild(FieldRow("Groupe", _partyPolicy));
        lifecycle.AddChild(FieldRow("Durée (s)", _restDuration));
        lifecycle.AddChild(Info("Forge émet requested, started, interrupted et completed vers les bindings ForgeScript versionnés."));

        var policy = Card(columns, "RÈGLE DE LA ZONE");
        _restMode = EnumOption<RestPermissionMode>();
        _denialReason = Field("Repos impossible ici.");
        _condition = Field("camp.secure == true");
        _cooldown = Spin(0, 86_400, 30, 1);
        _secureRadius = Spin(0, 512, 8, .5);
        policy.AddChild(FieldRow("Permission", _restMode));
        policy.AddChild(FieldRow("Explication joueur", _denialReason));
        policy.AddChild(FieldRow("Condition", _condition));
        policy.AddChild(FieldRow("Recharge (s)", _cooldown));
        policy.AddChild(FieldRow("Rayon sécurisé", _secureRadius));
        _denialVolume = Check("Interdire un volume dangereux au centre", false);
        _volumeSize = Spin(1, 512, 8, 1);
        policy.AddChild(_denialVolume);
        policy.AddChild(FieldRow("Taille du volume", _volumeSize));
    }

    private void BuildSequenceTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Séquences");
        var restore = Card(columns, "ABANDON SÛR D’UNE CINÉMATIQUE");
        _abortEnabled = Check("Autoriser l’abandon", true);
        _restoreCamera = Check("Restaurer la caméra", true);
        _restoreControl = Check("Rendre le contrôle joueur", true);
        _restoreInvulnerability = Check("Restaurer l’invulnérabilité temporaire", true);
        _restoreLocks = Check("Libérer les verrous", true);
        _restoreNarrative = Check("Annuler la transaction narrative", true);
        restore.AddChild(_abortEnabled);
        restore.AddChild(_restoreCamera);
        restore.AddChild(_restoreControl);
        restore.AddChild(_restoreInvulnerability);
        restore.AddChild(_restoreLocks);
        restore.AddChild(_restoreNarrative);
        _rollbackSteps = Spin(0, 10_000, 64, 1);
        restore.AddChild(FieldRow("Étapes de rollback max.", _rollbackSteps));
        restore.AddChild(Info("Le runtime restaure l’instantané d’entrée et émet sequence.aborted après une restauration réussie."));
    }

    private void BuildProjectEventsTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Événements projet");
        var connection = Card(columns, "CONNEXION, INVENTAIRE & UTILISATION");
        _projectEventsEnabled = Check("Activer le bus d’événements projet", true);
        _projectEventsRequired = Check("Configuration requise au runtime", false);
        connection.AddChild(_projectEventsEnabled);
        connection.AddChild(_projectEventsRequired);
        AddProjectEventHandlers(connection,
        [
            "player.joining", "player.ready", "player.leaving", "player.disconnected",
            "inventory.changed", "item.used", "ability.activated",
        ]);
        connection.AddChild(Info(
            "Les connexions sont authentifiées. Inventaire et utilisation restent transactionnels et peuvent être refusés avant journalisation."));

        var lifecycle = Card(columns, "MORT, RESPAWN, ÉQUIPEMENT & NIVEAU");
        _projectEventsDowned = Check("Inclure l’état Downed", true);
        _projectEventsRespawn = Check("Demander puis confirmer le respawn", true);
        _projectEventsReplicated = Check("Répliquer équipement et progression", true);
        lifecycle.AddChild(_projectEventsDowned);
        lifecycle.AddChild(_projectEventsRespawn);
        lifecycle.AddChild(_projectEventsReplicated);
        AddProjectEventHandlers(lifecycle,
        [
            "life.downed", "life.dying", "life.dead", "life.respawnrequested", "life.respawned",
            "equipment.changed", "progression.changed",
        ]);
        lifecycle.AddChild(Info(
            "Tous les bindings utilisent l’autorité serveur, une API ForgeScript versionnée et un UUID stable."));
    }

    private void AddProjectEventHandlers(Container parent, IReadOnlyList<string> eventNames)
    {
        foreach (var eventName in eventNames)
        {
            var handler = Field("On" + eventName);
            _projectEventHandlers[eventName] = handler;
            parent.AddChild(FieldRow(eventName, handler));
        }
    }

    private void BuildProjectWorldTab(TabContainer tabs)
    {
        var columns = Tab(tabs, "Monde & calendrier");
        var events = Card(columns, "ÉVÉNEMENTS UTILISATEUR GLOBAUX");
        _worldEnabled = Check("Activer la configuration du monde", true);
        _worldRequired = Check("Configuration requise au runtime", false);
        events.AddChild(_worldEnabled);
        events.AddChild(_worldRequired);
        _customEventList = new ItemList { CustomMinimumSize = new Vector2(0, 120) };
        _customEventList.ItemSelected += index => LoadCustomEvent((int)index);
        events.AddChild(_customEventList);
        _customNamespace = Field("module");
        _customName = Field("ritual_completed");
        _customPayloadVersion = Spin(1, 1_000, 1, 1);
        _customFields = Field("result:string,actor:guid");
        _customHandler = Field("OnRitualCompleted");
        _customRequired = Check("Extension requise", false);
        events.AddChild(FieldRow("Namespace", _customNamespace));
        events.AddChild(FieldRow("Nom", _customName));
        events.AddChild(FieldRow("Version payload", _customPayloadVersion));
        events.AddChild(FieldRow("Champs typés", _customFields));
        events.AddChild(FieldRow("Handler ForgeScript", _customHandler));
        events.AddChild(_customRequired);
        var eventActions = new HBoxContainer();
        var upsert = new Button { Text = "Ajouter / mettre à jour", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        upsert.Pressed += UpsertCustomEvent;
        eventActions.AddChild(upsert);
        var remove = new Button { Text = "Retirer" };
        remove.Pressed += RemoveCustomEvent;
        eventActions.AddChild(remove);
        events.AddChild(eventActions);
        events.AddChild(Info("Chaque événement est namespacé, versionné, validé par schéma et dispatché avec autorité serveur."));

        var clock = Card(columns, "TEMPS & CALENDRIER DU MONDE");
        _clockSpeed = Spin(0, 10_000, 1, .25);
        _clockPaused = Check("Horloge en pause au démarrage", false);
        _daysPerMonth = Spin(1, 366, 28, 1);
        _monthsPerYear = Spin(1, 100, 12, 1);
        _hoursPerDay = Spin(1, 100, 24, 1);
        _minutesPerHour = Spin(1, 1_000, 60, 1);
        _initialMinute = Spin(0, 10_000_000, 0, 1);
        clock.AddChild(FieldRow("Vitesse", _clockSpeed));
        clock.AddChild(_clockPaused);
        clock.AddChild(FieldRow("Jours / mois", _daysPerMonth));
        clock.AddChild(FieldRow("Mois / an", _monthsPerYear));
        clock.AddChild(FieldRow("Heures / jour", _hoursPerDay));
        clock.AddChild(FieldRow("Minutes / heure", _minutesPerHour));
        clock.AddChild(FieldRow("Minute initiale", _initialMinute));
        clock.AddChild(new Label { Text = "Fuseaux des zones · UUID=minutes, une ligne par zone" });
        _areaOffsets = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 92),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        clock.AddChild(_areaOffsets);
        clock.AddChild(Info("Aube, lever, coucher, crépuscule et minuit sont persistants. La sauvegarde conserve minute, pause et révision."));
    }

    private void RefreshCustomEventList()
    {
        _customEventList!.Clear();
        foreach (var schema in _customEvents)
            _customEventList.AddItem($"{schema.Namespace}.{schema.Name} · payload v{schema.PayloadVersion}");
    }

    private void LoadCustomEvent(int index)
    {
        if (index < 0 || index >= _customEvents.Count) return;
        var schema = _customEvents[index];
        _customNamespace!.Text = schema.Namespace;
        _customName!.Text = schema.Name;
        _customPayloadVersion!.Value = schema.PayloadVersion;
        _customFields!.Text = string.Join(",", schema.Fields.Select(item => $"{item.Key}:{item.Value}"));
        _customHandler!.Text = schema.Handler;
        _customRequired!.ButtonPressed = schema.RequiredExtension;
    }

    private void UpsertCustomEvent()
    {
        try
        {
            var fields = ParsePairs(_customFields!.Text, ':');
            var existing = _customEvents.FirstOrDefault(item =>
                string.Equals(item.Namespace, _customNamespace!.Text.Trim(), StringComparison.Ordinal) &&
                string.Equals(item.Name, _customName!.Text.Trim(), StringComparison.Ordinal));
            var schema = new CustomEventSchema(existing?.Id ?? Guid.NewGuid(), CustomEventSchema.CurrentSchemaVersion,
                "forge-ui:r052", _customNamespace!.Text.Trim(), _customName!.Text.Trim(),
                (int)_customPayloadVersion!.Value, fields, _customRequired!.ButtonPressed, "1.0",
                _customHandler!.Text.Trim(), EventAuthorityScope.Server);
            var index = _customEvents.FindIndex(item => item.Id == schema.Id);
            if (index >= 0) _customEvents[index] = schema;
            else _customEvents.Add(schema);
            _customEvents.Sort((left, right) => string.Compare(
                $"{left.Namespace}.{left.Name}", $"{right.Namespace}.{right.Name}", StringComparison.Ordinal));
            RefreshCustomEventList();
            _status!.Text = $"Événement {schema.Namespace}.{schema.Name} prêt pour validation.";
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"✕ {exception.Message}";
        }
    }

    private void RemoveCustomEvent()
    {
        var selected = _customEventList!.GetSelectedItems();
        if (selected.Length == 0) return;
        _customEvents.RemoveAt(selected[0]);
        RefreshCustomEventList();
    }

    private void Refresh()
    {
        var state = _state?.Invoke();
        var areaId = _active?.Invoke();
        var area = areaId is null ? null : state?.Project.Areas.SingleOrDefault(item => item.Id == areaId);
        if (state is null || area is null)
        {
            _status!.Text = "Aucun projet ou aucune zone active.";
            return;
        }

        _module = state.Project.Module.RuntimePolicies;
        _area = area.RuntimePolicies;
        Populate();
    }

    private void Populate()
    {
        if (_module is null || _area is null) return;
        var global = _module.Heartbeats.FirstOrDefault();
        _globalEnabled!.ButtonPressed = global?.Enabled ?? false;
        _globalInterval!.Value = global?.IntervalMilliseconds ?? 2000;
        _globalBudget!.Value = global?.BudgetMilliseconds ?? 12;
        _globalHandler!.Text = global?.Handler ?? "OnGlobalHeartbeat";
        var local = _area.Heartbeats.FirstOrDefault();
        _areaEnabled!.ButtonPressed = local?.Enabled ?? false;
        _areaInterval!.Value = local?.IntervalMilliseconds ?? 1000;
        _areaBudget!.Value = local?.BudgetMilliseconds ?? 8;
        _areaHandler!.Text = local?.Handler ?? "OnAreaHeartbeat";
        _suspendInactive!.ButtonPressed = local?.SuspendWhenInactive ?? true;
        _restEnabled!.ButtonPressed = _module.Rest.Enabled;
        _partyPolicy!.Selected = (int)_module.Rest.PartyPolicy;
        _restDuration!.Value = _module.Rest.DefaultDurationSeconds;
        _restMode!.Selected = (int)_area.Rest.Mode;
        _denialReason!.Text = _area.Rest.DenialReason;
        _condition!.Text = _area.Rest.Condition;
        _cooldown!.Value = _area.Rest.CooldownSeconds;
        _secureRadius!.Value = _area.Rest.RequiredSecureRadius;
        _denialVolume!.ButtonPressed = _area.Rest.Volumes.Count > 0;
        _volumeSize!.Value = _area.Rest.Volumes.FirstOrDefault()?.Size.X ?? 8;
        var restore = _module.SequenceAbort.Restore;
        _abortEnabled!.ButtonPressed = _module.SequenceAbort.Enabled;
        _restoreCamera!.ButtonPressed = restore.HasFlag(SequenceAbortRestoreFlags.Camera);
        _restoreControl!.ButtonPressed = restore.HasFlag(SequenceAbortRestoreFlags.PlayerControl);
        _restoreInvulnerability!.ButtonPressed = restore.HasFlag(SequenceAbortRestoreFlags.TemporaryInvulnerability);
        _restoreLocks!.ButtonPressed = restore.HasFlag(SequenceAbortRestoreFlags.Locks);
        _restoreNarrative!.ButtonPressed = restore.HasFlag(SequenceAbortRestoreFlags.NarrativeTransaction);
        _rollbackSteps!.Value = _module.SequenceAbort.MaximumNarrativeRollbackSteps;
        var events = _module.GameplayEvents;
        _projectEventsEnabled!.ButtonPressed = events.Enabled;
        _projectEventsRequired!.ButtonPressed = events.RequiredAtRuntime;
        _projectEventsDowned!.ButtonPressed = events.UseDownedState;
        _projectEventsRespawn!.ButtonPressed = events.AutoRespawn;
        _projectEventsReplicated!.ButtonPressed = events.ReplicateEquipmentAndProgression;
        foreach (var binding in events.Bindings)
            if (_projectEventHandlers.TryGetValue(binding.EventName, out var handler)) handler.Text = binding.Handler;
        var world = _module.World;
        _worldEnabled!.ButtonPressed = world.Enabled;
        _worldRequired!.ButtonPressed = world.RequiredAtRuntime;
        _customEvents.Clear();
        _customEvents.AddRange(world.CustomEvents);
        RefreshCustomEventList();
        var clock = world.Clock;
        _clockSpeed!.Value = clock.Speed;
        _clockPaused!.ButtonPressed = clock.Paused;
        _daysPerMonth!.Value = clock.DaysPerMonth;
        _monthsPerYear!.Value = clock.MonthsPerYear;
        _hoursPerDay!.Value = clock.HoursPerDay;
        _minutesPerHour!.Value = clock.MinutesPerHour;
        _initialMinute!.Value = clock.InitialMinute;
        _areaOffsets!.Text = string.Join("\n", clock.AreaTimeZoneOffsets.Select(item => $"{item.Key}={item.Value}"));
        _status!.Text = $"Prêt · {_module.Heartbeats.Count} timer(s) globaux · {_area.Heartbeats.Count} locaux · " +
                        $"{events.Bindings.Count} événement(s) projet · {world.CustomEvents.Count} custom.";
    }

    private bool Save()
    {
        var areaId = _active?.Invoke();
        if (_module is null || _area is null || areaId is null) return false;
        try
        {
            var global = BuildHeartbeat(_module.Heartbeats.FirstOrDefault(), HeartbeatScope.Module, "Global",
                _globalEnabled!, _globalInterval!, _globalBudget!, false, _globalHandler!);
            var local = BuildHeartbeat(_area.Heartbeats.FirstOrDefault(), HeartbeatScope.Area, "Zone active",
                _areaEnabled!, _areaInterval!, _areaBudget!, _suspendInactive!.ButtonPressed, _areaHandler!);
            var rest = new RestLifecycleProfile(_module.Rest.Id, _module.Rest.SchemaVersion,
                _restEnabled!.ButtonPressed, (PartyRestPolicy)_partyPolicy!.Selected, (int)_restDuration!.Value,
                _module.Rest.Bindings, "forge-ui");
            var flags = SequenceAbortRestoreFlags.None;
            if (_restoreCamera!.ButtonPressed) flags |= SequenceAbortRestoreFlags.Camera;
            if (_restoreControl!.ButtonPressed) flags |= SequenceAbortRestoreFlags.PlayerControl;
            if (_restoreInvulnerability!.ButtonPressed) flags |= SequenceAbortRestoreFlags.TemporaryInvulnerability;
            if (_restoreLocks!.ButtonPressed) flags |= SequenceAbortRestoreFlags.Locks;
            if (_restoreNarrative!.ButtonPressed) flags |= SequenceAbortRestoreFlags.NarrativeTransaction;
            if (flags == SequenceAbortRestoreFlags.None) flags = SequenceAbortRestoreFlags.PlayerControl;
            var abort = new SequenceAbortPolicy(_module.SequenceAbort.Id, _module.SequenceAbort.SchemaVersion,
                _abortEnabled!.ButtonPressed, _module.SequenceAbort.Binding, flags, (int)_rollbackSteps!.Value,
                "forge-ui");
            var projectEvents = new ProjectGameplayEventProfile(
                _module.GameplayEvents.Id,
                ProjectGameplayEventProfile.CurrentSchemaVersion,
                "forge-ui:r051",
                _projectEventsEnabled!.ButtonPressed,
                _projectEventsRequired!.ButtonPressed,
                _projectEventsDowned!.ButtonPressed,
                _projectEventsRespawn!.ButtonPressed,
                _projectEventsReplicated!.ButtonPressed,
                _module.GameplayEvents.Bindings.Select(binding => binding with
                {
                    Provenance = "forge-ui:r051",
                    Handler = NonEmpty(_projectEventHandlers[binding.EventName].Text, binding.Handler),
                    Authority = EventAuthorityScope.Server,
                }).ToArray());
            var currentClock = _module.World.Clock;
            var clock = new WorldClockProfile(currentClock.Id, WorldClockProfile.CurrentSchemaVersion,
                "forge-ui:r052", (int)_daysPerMonth!.Value, (int)_monthsPerYear!.Value,
                (int)_hoursPerDay!.Value, (int)_minutesPerHour!.Value, _clockSpeed!.Value,
                _clockPaused!.ButtonPressed, (long)_initialMinute!.Value, ParseOffsets(_areaOffsets!.Text),
                currentClock.AstronomicalMinutes);
            var world = new ProjectWorldProfile(_module.Id, ProjectWorldProfile.CurrentSchemaVersion,
                "forge-ui:r052", _worldEnabled!.ButtonPressed, _worldRequired!.ButtonPressed,
                _customEvents.ToArray(), clock);
            var module = new ModuleRuntimePolicyProfile(_module.Id, _module.SchemaVersion, [global], rest, abort,
                "forge-ui", _module.Cinematics, _module.Gameplay, _module.Rules, _module.Extensions, projectEvents,
                world);
            var mode = (RestPermissionMode)_restMode!.Selected;
            var reason = mode == RestPermissionMode.Allowed ? string.Empty : NonEmpty(_denialReason!.Text, "Repos impossible ici.");
            var condition = mode == RestPermissionMode.Conditional ? NonEmpty(_condition!.Text, "camp.secure == true") : string.Empty;
            var volumes = _denialVolume!.ButtonPressed
                ? new[] { BuildDenialVolume(_area.Rest.Volumes.FirstOrDefault(), (float)_volumeSize!.Value) }
                : [];
            var areaRest = new AreaRestPolicy(_area.Rest.Id, _area.Rest.SchemaVersion, mode, reason, condition,
                (int)_cooldown!.Value, (float)_secureRadius!.Value, volumes, "forge-ui");
            var area = new AreaRuntimePolicyProfile(_area.Id, _area.SchemaVersion, [local], areaRest, "forge-ui",
                _area.Cinematics, _area.Rules, _area.Experience);
            var succeeded = _apply?.Invoke(module, areaId, area) == true;
            _status!.Text = succeeded
                ? "✓ Politiques validées et appliquées par une transaction atomique."
                : "✕ Application refusée · consultez les diagnostics du projet.";
            if (succeeded)
            {
                _module = module;
                _area = area;
            }

            return succeeded;
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    private static HeartbeatScheduleDefinition BuildHeartbeat(HeartbeatScheduleDefinition? source,
        HeartbeatScope scope, string name, CheckBox enabled, SpinBox interval, SpinBox budget, bool suspend,
        LineEdit handler) => new(source?.Id ?? Guid.NewGuid(), HeartbeatScheduleDefinition.CurrentSchemaVersion,
        source?.Name ?? name, scope, enabled.ButtonPressed, (int)interval.Value, (int)budget.Value, suspend,
        source?.ForgeScriptApiVersion ?? "1.0", NonEmpty(handler.Text, scope == HeartbeatScope.Module
            ? "OnGlobalHeartbeat" : "OnAreaHeartbeat"), "forge-ui");

    private static RestPolicyVolume BuildDenialVolume(RestPolicyVolume? source, float size) => new(
        source?.Id ?? Guid.NewGuid(), source?.Name ?? "Zone dangereuse", Vector3Value.Zero,
        new Vector3Value(size, Math.Max(2, size), size), RestPermissionMode.Forbidden,
        "Repos impossible : la zone n’est pas sécurisée.", string.Empty, source?.CooldownSeconds ?? 0,
        source?.RequiredSecureRadius ?? 0, source?.Priority ?? 100, "forge-ui");

    private static string NonEmpty(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static IReadOnlyDictionary<string, string> ParsePairs(string value, char separator)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(separator, 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0 || !result.TryAdd(parts[0], parts[1]))
                throw new ArgumentException($"Paire typée invalide ou dupliquée : '{token}'.");
        }
        if (result.Count == 0) throw new ArgumentException("Un événement custom requiert au moins un champ typé.");
        return result;
    }

    private static IReadOnlyDictionary<string, int> ParseOffsets(string value)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var offset) || !result.TryAdd(parts[0], offset))
                throw new ArgumentException($"Fuseau de zone invalide ou dupliqué : '{line}'.");
        }
        return result;
    }

    private static HBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tabs.AddChild(scroll);
        var columns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(columns);
        return columns;
    }

    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat { BgColor = new Color("171221"), BorderColor = new Color("513a6d") };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 16;
        style.ContentMarginTop = style.ContentMarginBottom = 14;
        panel.AddThemeStyleboxOverride("panel", style);
        parent.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        panel.AddChild(box);
        var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new Color("ffb06f"));
        box.AddChild(label);
        return box;
    }

    private static HBoxContainer FieldRow(string label, Control field)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(170, 0) });
        field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(field);
        return row;
    }

    private static Label Info(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        Modulate = new Color("bcaacb"),
    };

    private static CheckBox Check(string text, bool value) => new() { Text = text, ButtonPressed = value };

    private static LineEdit Field(string placeholder) => new() { PlaceholderText = placeholder };

    private static SpinBox Spin(double min, double max, double value, double step) => new()
    {
        MinValue = min,
        MaxValue = max,
        Value = value,
        Step = step,
        AllowGreater = false,
        AllowLesser = false,
    };

    private static OptionButton EnumOption<T>() where T : struct, Enum
    {
        var option = new OptionButton();
        foreach (var value in Enum.GetValues<T>()) option.AddItem(value.ToString());
        return option;
    }
}
#endif
