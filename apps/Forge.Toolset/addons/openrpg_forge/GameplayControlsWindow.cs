#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Gameplay;
using Godot;

namespace Forge.Toolset.Editor;

public partial class GameplayControlsWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<ModuleGameplayControlProfile, bool>? _apply;
    private Action<Window>? _brand;
    private ModuleGameplayControlProfile? _profile;
    private CheckBox? _swimEnabled;
    private CheckBox? _swimRequired;
    private SpinBox? _buoyancy;
    private SpinBox? _swimSpeed;
    private SpinBox? _diveSpeed;
    private SpinBox? _breath;
    private CheckBox? _climbEnabled;
    private CheckBox? _climbRequired;
    private CheckBox? _climbIk;
    private CheckBox? _authoredLinks;
    private SpinBox? _climbSpeed;
    private SpinBox? _stamina;
    private SpinBox? _staminaDrain;
    private SpinBox? _ledgeReach;
    private CheckBox? _radialEnabled;
    private CheckBox? _gamepad;
    private CheckBox? _keyboard;
    private CheckBox? _listFallback;
    private SpinBox? _radialDepth;
    private TextEdit? _radialActions;
    private CheckBox? _pause;
    private CheckBox? _slowMotion;
    private SpinBox? _slowScale;
    private CheckBox? _planning;
    private SpinBox? _plannedActions;
    private CheckBox? _turnMode;
    private OptionButton? _multiplayer;
    private SpinBox? _vote;
    private OptionButton? _combatProvider;
    private Label? _summary;
    private Label? _status;

    internal string Summary => _profile is null ? "unavailable" :
        $"swim={_profile.Swimming.Enabled};climb={_profile.Climbing.Enabled};radial={_profile.RadialMenu.Actions.Count};" +
        $"time={_profile.TimeControl.PlanningEnabled};combat={_profile.CombatModes.DefaultProviderId}";

    public void Initialize(Func<ProjectState?> state, Func<ModuleGameplayControlProfile, bool> apply,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Contrôles & combat · OpenRPG Forge";
        MinSize = new Vector2I(1180, 780);
        Size = new Vector2I(1260, 820);
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

    internal bool ConfigureForSmokeTest(ModuleGameplayControlProfile profile)
    {
        _profile = profile;
        Populate();
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "CONTRÔLES DE JEU, ACTIONS & COMBAT", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Configurez les capacités de déplacement, la roue d’actions, le temps tactique et le fournisseur de combat. " +
                   "Toutes les modifications passent par une transaction canonique annulable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new Label();
        _summary.AddThemeColorOverride("font_color", new Color("c7b3dc"));
        root.AddChild(_summary);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(scroll);
        var columns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(columns);

        var movement = Card(columns, "DÉPLACEMENTS ÉTENDUS");
        _swimEnabled = Check("Autoriser nage et plongée", true); movement.AddChild(_swimEnabled);
        _swimRequired = Check("Extension obligatoire au runtime", false); movement.AddChild(_swimRequired);
        _buoyancy = Spin(0, 50, 8, .25); movement.AddChild(Row("Flottabilité", _buoyancy));
        _swimSpeed = Spin(.1, 30, 4.5, .1); movement.AddChild(Row("Vitesse surface", _swimSpeed));
        _diveSpeed = Spin(.1, 30, 3, .1); movement.AddChild(Row("Vitesse plongée", _diveSpeed));
        _breath = Spin(1, 600, 45, 1); movement.AddChild(Row("Souffle (s)", _breath));
        movement.AddChild(new HSeparator());
        _climbEnabled = Check("Autoriser l’escalade", true); movement.AddChild(_climbEnabled);
        _climbRequired = Check("Extension obligatoire au runtime", false); movement.AddChild(_climbRequired);
        _climbIk = Check("IK mains/pieds", true); movement.AddChild(_climbIk);
        _authoredLinks = Check("Exiger des liens auteur sûrs", true); movement.AddChild(_authoredLinks);
        _climbSpeed = Spin(.1, 20, 3, .1); movement.AddChild(Row("Vitesse", _climbSpeed));
        _stamina = Spin(1, 1_000, 100, 1); movement.AddChild(Row("Endurance", _stamina));
        _staminaDrain = Spin(.1, 100, 12, .1); movement.AddChild(Row("Coût / seconde", _staminaDrain));
        _ledgeReach = Spin(.1, 5, 1.5, .1); movement.AddChild(Row("Portée rebord", _ledgeReach));

        var actions = Card(columns, "ROUE D’ACTIONS");
        _radialEnabled = Check("Activer la roue", true); actions.AddChild(_radialEnabled);
        _gamepad = Check("Navigation manette", true); actions.AddChild(_gamepad);
        _keyboard = Check("Navigation clavier", true); actions.AddChild(_keyboard);
        _listFallback = Check("Vue liste accessible", true); actions.AddChild(_listFallback);
        _radialDepth = Spin(1, 8, 3, 1); actions.AddChild(Row("Profondeur max.", _radialDepth));
        actions.AddChild(new Label { Text = "Actions · clé | libellé | commande | parent", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _radialActions = new TextEdit { CustomMinimumSize = new Vector2(350, 270), WrapMode = TextEdit.LineWrappingMode.None };
        actions.AddChild(_radialActions);
        actions.AddChild(new Label
        {
            Text = "Une ligne sans parent crée un segment racine. Une commande open:clé ouvre un sous-menu.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var tactical = Card(columns, "TEMPS & COMBAT");
        _pause = Check("Pause tactique", true); tactical.AddChild(_pause);
        _slowMotion = Check("Ralenti", true); tactical.AddChild(_slowMotion);
        _slowScale = Spin(.05, .95, .2, .05); tactical.AddChild(Row("Échelle ralenti", _slowScale));
        _planning = Check("Planification d’actions", true); tactical.AddChild(_planning);
        _plannedActions = Spin(1, 64, 8, 1); tactical.AddChild(Row("Actions planifiées", _plannedActions));
        _turnMode = Check("Mode tour par tour", true); tactical.AddChild(_turnMode);
        _multiplayer = new OptionButton();
        foreach (var value in Enum.GetValues<MultiplayerPausePolicy>()) _multiplayer.AddItem(MultiplayerLabel(value));
        tactical.AddChild(Row("Multijoueur", _multiplayer));
        _vote = Spin(.05, 1, .5, .05); tactical.AddChild(Row("Seuil de vote", _vote));
        tactical.AddChild(new HSeparator());
        _combatProvider = new OptionButton();
        tactical.AddChild(Row("Mode de combat", _combatProvider));
        tactical.AddChild(new Label
        {
            Text = "Les fournisseurs temps réel, pause et tours partagent les mêmes événements de combat et restent autoritaires côté serveur.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var footer = new HBoxContainer();
        _status = new Label { Text = "Prêt · aucune mutation.", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd"));
        footer.AddChild(_status);
        var apply = new Button { Text = "Valider et appliquer", CustomMinimumSize = new Vector2(190, 40) };
        apply.Pressed += () => Save(); footer.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide; footer.AddChild(close);
        root.AddChild(footer);
    }

    private void Refresh()
    {
        _profile = _state?.Invoke()?.Project.Module.RuntimePolicies.Gameplay;
        if (_profile is null) { _status!.Text = "Aucun projet chargé."; return; }
        Populate();
    }

    private void Populate()
    {
        if (_profile is null) return;
        var swim = _profile.Swimming;
        _swimEnabled!.ButtonPressed = swim.Enabled; _swimRequired!.ButtonPressed = swim.Required;
        _buoyancy!.Value = swim.Buoyancy; _swimSpeed!.Value = swim.SurfaceSpeed;
        _diveSpeed!.Value = swim.DiveSpeed; _breath!.Value = swim.BreathSeconds;
        var climb = _profile.Climbing;
        _climbEnabled!.ButtonPressed = climb.Enabled; _climbRequired!.ButtonPressed = climb.Required;
        _climbIk!.ButtonPressed = climb.InverseKinematics; _authoredLinks!.ButtonPressed = climb.RequireAuthoredLinks;
        _climbSpeed!.Value = climb.ClimbSpeed; _stamina!.Value = climb.StaminaCapacity;
        _staminaDrain!.Value = climb.StaminaDrainPerSecond; _ledgeReach!.Value = climb.LedgeReach;
        var radial = _profile.RadialMenu;
        _radialEnabled!.ButtonPressed = radial.Enabled; _gamepad!.ButtonPressed = radial.GamepadEnabled;
        _keyboard!.ButtonPressed = radial.KeyboardNavigation; _listFallback!.ButtonPressed = radial.ListFallback;
        _radialDepth!.Value = radial.MaximumDepth;
        _radialActions!.Text = string.Join("\n", radial.Actions.OrderBy(item => item.ParentKey).ThenBy(item => item.Order)
            .Select(item => $"{item.Key} | {item.Label} | {item.Command} | {item.ParentKey}"));
        var time = _profile.TimeControl;
        _pause!.ButtonPressed = time.PauseEnabled; _slowMotion!.ButtonPressed = time.SlowMotionEnabled;
        _slowScale!.Value = time.SlowMotionScale; _planning!.ButtonPressed = time.PlanningEnabled;
        _plannedActions!.Value = time.MaximumPlannedActions; _turnMode!.ButtonPressed = time.TurnModeEnabled;
        _multiplayer!.Selected = (int)time.MultiplayerPolicy; _vote!.Value = time.RequiredVoteFraction;
        _combatProvider!.Clear();
        foreach (var provider in _profile.CombatModes.Providers) _combatProvider.AddItem(provider.DisplayName);
        _combatProvider.Selected = _profile.CombatModes.Providers.ToList().FindIndex(item =>
            item.ProviderId == _profile.CombatModes.DefaultProviderId);
        _summary!.Text = $"Schéma v{_profile.SchemaVersion} · {_profile.RadialMenu.Actions.Count} action(s) · " +
                         $"{_profile.CombatModes.Providers.Count} fournisseur(s) · provenance {_profile.Provenance}";
        _status!.Text = "Prêt · configuration canonique chargée.";
    }

    private bool Save()
    {
        if (_profile is null) return false;
        try
        {
            var swim = _profile.Swimming;
            swim = new SwimmingProfile(swim.Id, 1, _swimEnabled!.ButtonPressed, _swimRequired!.ButtonPressed,
                (float)_buoyancy!.Value, swim.SurfaceSnapDistance, (float)_swimSpeed!.Value,
                (float)_diveSpeed!.Value, swim.CurrentInfluence, (float)_breath!.Value, swim.WaterVolumeTags,
                swim.SurfaceAnimation, swim.DiveAnimation, swim.IdleAnimation, "forge-ui");
            var climb = _profile.Climbing;
            climb = new ClimbingProfile(climb.Id, 1, _climbEnabled!.ButtonPressed, _climbRequired!.ButtonPressed,
                (float)_climbSpeed!.Value, (float)_stamina!.Value, (float)_staminaDrain!.Value,
                (float)_ledgeReach!.Value, _climbIk!.ButtonPressed, _authoredLinks!.ButtonPressed,
                climb.PreventUnsafeDrop, climb.SurfaceTags, climb.ClimbAnimation, climb.LedgeAnimation, "forge-ui");
            var radial = _profile.RadialMenu;
            var parsed = ParseActions(radial);
            radial = new RadialMenuProfile(radial.Id, 1, _radialEnabled!.ButtonPressed, radial.InputAction,
                radial.DeadZone, (int)_radialDepth!.Value, _gamepad!.ButtonPressed, _keyboard!.ButtonPressed,
                _listFallback!.ButtonPressed, true, parsed, "forge-ui");
            var time = _profile.TimeControl;
            time = new TimeControlPolicy(time.Id, 1, _pause!.ButtonPressed, _slowMotion!.ButtonPressed,
                (float)_slowScale!.Value, _planning!.ButtonPressed, (int)_plannedActions!.Value,
                _turnMode!.ButtonPressed, (MultiplayerPausePolicy)_multiplayer!.Selected, (float)_vote!.Value,
                time.PausePermissions, "forge-ui");
            var providers = _profile.CombatModes.Providers;
            var selected = Math.Clamp(_combatProvider!.Selected, 0, providers.Count - 1);
            var combat = new CombatModeProfile(_profile.CombatModes.Id, 1, providers[selected].ProviderId,
                providers, "forge-ui");
            var candidate = new ModuleGameplayControlProfile(_profile.Id, 1, swim, climb, radial, time, combat,
                "forge-ui");
            var succeeded = _apply?.Invoke(candidate) == true;
            _status!.Text = succeeded
                ? "✓ Contrôles validés, persistants et annulables."
                : "✕ Application refusée · consultez les diagnostics.";
            if (succeeded) { _profile = candidate; Populate(); }
            return succeeded;
        }
        catch (ArgumentException exception)
        {
            _status!.Text = $"✕ {exception.Message}";
            return false;
        }
    }

    private IReadOnlyList<RadialActionDefinition> ParseActions(RadialMenuProfile source)
    {
        var ids = source.Actions.ToDictionary(action => action.Key, action => action.Id, StringComparer.Ordinal);
        var actions = new List<RadialActionDefinition>();
        var orderByParent = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var raw in _radialActions!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|').Select(value => value.Trim()).ToArray();
            if (parts.Length < 3) throw new ArgumentException($"Action invalide : '{raw}'.");
            var key = parts[0].ToLowerInvariant();
            var command = parts[2].ToLowerInvariant();
            var parent = parts.Length > 3 ? parts[3].ToLowerInvariant() : string.Empty;
            var order = orderByParent.GetValueOrDefault(parent);
            orderByParent[parent] = order + 1;
            var kind = command.StartsWith("open:", StringComparison.Ordinal) ? RadialActionKind.Navigation :
                command == "attack" ? RadialActionKind.Ability :
                command == "quick_save" ? RadialActionKind.System : RadialActionKind.Verb;
            actions.Add(new RadialActionDefinition(ids.GetValueOrDefault(key, Guid.NewGuid()), key, parts[1],
                parts[1], kind, command, parent, order, command is "attack" or "interact", []));
        }

        if (actions.Count == 0) throw new ArgumentException("La roue doit contenir au moins une action.");
        return actions;
    }

    private static VBoxContainer Card(Container parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Style("171221", "513a6d")); parent.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(350, 0) };
        box.AddThemeConstantOverride("separation", 7); panel.AddChild(box);
        var heading = new Label { Text = title }; heading.AddThemeColorOverride("font_color", new Color("ffb06f"));
        box.AddChild(heading); return box;
    }

    private static StyleBoxFlat Style(string background, string border)
    {
        var style = new StyleBoxFlat { BgColor = new Color(background), BorderColor = new Color(border) };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = style.ContentMarginRight = 14;
        style.ContentMarginTop = style.ContentMarginBottom = 12; return style;
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(145, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(control); return row;
    }

    private static CheckBox Check(string text, bool pressed) => new() { Text = text, ButtonPressed = pressed };
    private static SpinBox Spin(double min, double max, double value, double step) => new()
    { MinValue = min, MaxValue = max, Value = value, Step = step, AllowGreater = false, AllowLesser = false };
    private static string MultiplayerLabel(MultiplayerPausePolicy value) => value switch
    {
        MultiplayerPausePolicy.Disabled => "Désactivée",
        MultiplayerPausePolicy.HostOnly => "Hôte uniquement",
        MultiplayerPausePolicy.Vote => "Vote",
        MultiplayerPausePolicy.Everyone => "Tous les joueurs",
        _ => value.ToString(),
    };
}
#endif
