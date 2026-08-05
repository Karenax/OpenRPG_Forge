#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Areas;
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

    internal string Summary => _module is null || _area is null
        ? "unavailable"
        : $"{_module.Heartbeats.Count}:{_area.Heartbeats.Count}:{_module.Rest.Enabled}:" +
          $"{_area.Rest.Mode}:{_area.Rest.Volumes.Count}:{_module.SequenceAbort.Restore}";

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

        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(tabs);
        BuildHeartbeatTab(tabs);
        BuildRestTab(tabs);
        BuildSequenceTab(tabs);

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
        _status!.Text = $"Prêt · {_module.Heartbeats.Count} timer(s) globaux · {_area.Heartbeats.Count} locaux · {_area.Rest.Volumes.Count} volume(s).";
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
            var module = new ModuleRuntimePolicyProfile(_module.Id, _module.SchemaVersion, [global], rest, abort,
                "forge-ui", _module.Cinematics, _module.Gameplay, _module.Rules);
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
