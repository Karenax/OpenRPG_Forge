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
    private CheckBox? _groundEnabled;
    private CheckBox? _groundRequired;
    private SpinBox? _walkSpeed;
    private SpinBox? _runSpeed;
    private SpinBox? _acceleration;
    private SpinBox? _deceleration;
    private SpinBox? _slope;
    private SpinBox? _stepHeight;
    private SpinBox? _jumpVelocity;
    private SpinBox? _crouchMultiplier;
    private CheckBox? _rootMotion;
    private CheckBox? _prediction;
    private CheckBox? _interactionEnabled;
    private CheckBox? _interactionRequired;
    private LineEdit? _interactionAction;
    private OptionButton? _targeting;
    private SpinBox? _focusDistance;
    private SpinBox? _viewAngle;
    private CheckBox? _showPrompt;
    private TextEdit? _verbs;
    private OptionButton? _defaultCamera;
    private SpinBox? _cameraFov;
    private SpinBox? _cameraDistance;
    private SpinBox? _cameraPitch;
    private CheckBox? _cameraCollision;
    private CheckBox? _allowRebinding;
    private CheckBox? _perPlayerInput;
    private TextEdit? _inputBindings;
    private CheckBox? _saveRequired;
    private SpinBox? _chunkKilobytes;
    private SpinBox? _autosaveSeconds;
    private SpinBox? _rollbackSlots;
    private CheckBox? _saveMigrations;
    private CheckBox? _saveDependencies;
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
    private TabContainer? _tabs;
    private GameplaySimulationPreview? _simulationPreview;
    private CheckBox? _flightEnabled;
    private CheckBox? _flightRequired;
    private SpinBox? _flightSpeed;
    private SpinBox? _flightAcceleration;
    private SpinBox? _flightAltitude;
    private SpinBox? _flightBanking;
    private SpinBox? _flightLift;
    private CheckBox? _flightLanding;
    private CheckBox? _flightVolumetric;
    private OptionButton? _projectileMode;
    private SpinBox? _projectileSpeed;
    private SpinBox? _projectileGravity;
    private SpinBox? _projectileLifetime;
    private CheckBox? _projectilePrediction;
    private CheckBox? _projectileAuthority;
    private LineEdit? _projectileVfx;
    private SpinBox? _physicsStep;
    private CheckBox? _physicsRequired;
    private OptionButton? _targetMode;
    private SpinBox? _targetRange;
    private SpinBox? _targetRadius;
    private SpinBox? _targetAngle;
    private SpinBox? _targetCount;
    private CheckBox? _targetLos;
    private SpinBox? _worldScale;
    private CheckBox? _worldPaused;
    private LineEdit? _worldPauseDomain;
    private GameplaySessionPreview? _sessionPreview;
    private OptionButton? _saveScope;
    private OptionButton? _saveAuthority;
    private OptionButton? _saveConflict;
    private CheckBox? _saveCloud;
    private LineEdit? _saveCloudAdapter;
    private CheckBox? _savePersonal;
    private CheckBox? _saveEncrypted;
    private SpinBox? _saveSnapshots;
    private LineEdit? _checkpointWaypoint;
    private SpinBox? _checkpointCost;
    private OptionButton? _checkpointParty;
    private OptionButton? _checkpointCorpse;
    private SpinBox? _checkpointRadius;
    private SpinBox? _respawnDelay;
    private CheckBox? _safeSpawn;
    private LineEdit? _shellKit;
    private LineEdit? _shellSkin;
    private SpinBox? _shellScale;
    private CheckBox? _shellMap;
    private CheckBox? _shellJournal;
    private CheckBox? _shellHud;
    private TextEdit? _shellInputs;
    private TextEdit? _controlContexts;
    private CheckBox? _replayHeadless;
    private CheckBox? _replayReport;
    private CheckBox? _replayPackage;
    private SpinBox? _replayFrames;
    private LineEdit? _replayDirectory;
    private Label? _summary;
    private Label? _status;
    private string? _capturePath;
    private double _captureDelay;
    private bool _captureOpened;

    internal string Summary => _profile is null ? "unavailable" :
        $"ground={_profile.Foundation.GroundedMovement.RunSpeed};cameras={_profile.Foundation.Cameras.Profiles.Count};" +
        $"bindings={_profile.Foundation.Input.Bindings.Count};save={_profile.Foundation.Save.ChecksumAlgorithm};" +
        $"swim={_profile.Swimming.Enabled};climb={_profile.Climbing.Enabled};radial={_profile.RadialMenu.Actions.Count};" +
        $"time={_profile.TimeControl.PlanningEnabled};combat={_profile.CombatModes.DefaultProviderId}";

    public void Initialize(Func<ProjectState?> state, Func<ModuleGameplayControlProfile, bool> apply,
        Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Fondations gameplay & contrôles · OpenRPG Forge";
        MinSize = new Vector2I(1180, 780);
        Size = new Vector2I(1440, 900);
        Transient = true;
        Visible = false;
        Build();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--gameplay-foundation-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _capturePath = capture["--gameplay-foundation-ui-capture=".Length..];
            SetProcess(true);
        }
        var r066Capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r066-gameplay-simulation-ui-capture=", StringComparison.Ordinal));
        if (r066Capture is not null)
        {
            _capturePath = r066Capture["--r066-gameplay-simulation-ui-capture=".Length..];
            SetProcess(true);
        }
        var r067Capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r067-gameplay-session-ui-capture=", StringComparison.Ordinal));
        if (r067Capture is not null)
        {
            _capturePath = r067Capture["--r067-gameplay-session-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureDelay += delta;
        if (!_captureOpened && _captureDelay >= 1)
        {
            Open();
            if (OS.GetCmdlineUserArgs().Any(value => value.StartsWith(
                    "--r067-gameplay-session-ui-capture=", StringComparison.Ordinal)))
                _tabs!.CurrentTab = 3;
            else if (OS.GetCmdlineUserArgs().Any(value => value.StartsWith(
                         "--r066-gameplay-simulation-ui-capture=", StringComparison.Ordinal)))
                _tabs!.CurrentTab = 2;
            _captureOpened = true;
            return;
        }
        if (!_captureOpened || _captureDelay < 2.5) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var image = GetViewport().GetTexture().GetImage();
        var error = image.SavePng(destination);
        GD.Print($"OPENRPG_FORGE_GAMEPLAY_FOUNDATION_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
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

    internal bool ConfigureForSmokeTest(ModuleGameplayControlProfile profile)
    {
        _profile = profile;
        Populate();
        return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    internal bool ConfigureForR066SmokeTest(ModuleGameplayControlProfile profile)
    {
        _profile = profile;
        Populate();
        _tabs!.CurrentTab = 2;
        _flightAltitude!.Value = 96;
        _projectileMode!.Selected = (int)ProjectileMode.Homing;
        _targetMode!.Selected = (int)TargetingMode.Chain;
        _worldScale!.Value = 1.5;
        return true;
    }

    internal bool ConfigureForR067SmokeTest(ModuleGameplayControlProfile profile)
    {
        _profile = profile;
        Populate();
        _tabs!.CurrentTab = 3;
        _saveScope!.Selected = (int)PersistenceScope.Server;
        _saveAuthority!.Selected = (int)SaveAuthority.Server;
        _saveConflict!.Selected = (int)SaveConflictPolicy.ServerWins;
        _checkpointCost!.Value = 25;
        _shellScale!.Value = 1.25;
        _replayFrames!.Value = 120000;
        return true;
    }

    private void Build()
    {
        var background = new ColorRect { Color = new Color("0d0915") };
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
        var title = new Label { Text = "FONDATIONS GAMEPLAY, CONTRÔLES & SAUVEGARDES", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Configurez mouvement, interactions, caméras, périphériques et sauvegardes, puis les capacités étendues. " +
                   "Toutes les modifications passent par une transaction canonique annulable.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new Label();
        _summary.AddThemeColorOverride("font_color", new Color("c7b3dc"));
        root.AddChild(_summary);

        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(_tabs);
        var foundationScroll = new ScrollContainer { Name = "Fondations P0", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _tabs.AddChild(foundationScroll);
        var foundationRows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foundationRows.AddThemeConstantOverride("separation", 12); foundationScroll.AddChild(foundationRows);
        var firstRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        firstRow.AddThemeConstantOverride("separation", 12); foundationRows.AddChild(firstRow);
        var secondRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        secondRow.AddThemeConstantOverride("separation", 12); foundationRows.AddChild(secondRow);

        var ground = Card(firstRow, "DÉPLACEMENT AU SOL");
        _groundEnabled = Check("Activer marche/course", true); ground.AddChild(_groundEnabled);
        _groundRequired = Check("Obligatoire au runtime", true); ground.AddChild(_groundRequired);
        _walkSpeed = Spin(.1, 50, 2.5, .1); ground.AddChild(Row("Marche (m/s)", _walkSpeed));
        _runSpeed = Spin(.1, 100, 5.5, .1); ground.AddChild(Row("Course (m/s)", _runSpeed));
        _acceleration = Spin(.1, 500, 24, .1); ground.AddChild(Row("Accélération", _acceleration));
        _deceleration = Spin(.1, 500, 30, .1); ground.AddChild(Row("Décélération", _deceleration));
        _slope = Spin(0, 89, 48, 1); ground.AddChild(Row("Pente max. (°)", _slope));
        _stepHeight = Spin(0, 5, .45, .05); ground.AddChild(Row("Hauteur marche", _stepHeight));
        _jumpVelocity = Spin(0, 100, 9, .25); ground.AddChild(Row("Impulsion saut", _jumpVelocity));
        _crouchMultiplier = Spin(.05, 1, .45, .05); ground.AddChild(Row("Vitesse accroupie", _crouchMultiplier));
        _rootMotion = Check("Root motion", false); ground.AddChild(_rootMotion);
        _prediction = Check("Prédiction réseau", true); ground.AddChild(_prediction);

        var interaction = Card(firstRow, "INTERACTIONS CONTEXTUELLES");
        _interactionEnabled = Check("Activer focus et verbes", true); interaction.AddChild(_interactionEnabled);
        _interactionRequired = Check("Obligatoire au runtime", true); interaction.AddChild(_interactionRequired);
        _interactionAction = new LineEdit { PlaceholderText = "interact" };
        interaction.AddChild(Row("Action d’entrée", _interactionAction));
        _targeting = new OptionButton();
        foreach (var value in Enum.GetValues<InteractionTargetingMode>()) _targeting.AddItem(value.ToString());
        interaction.AddChild(Row("Ciblage", _targeting));
        _focusDistance = Spin(.1, 100, 3, .1); interaction.AddChild(Row("Portée focus", _focusDistance));
        _viewAngle = Spin(1, 360, 100, 1); interaction.AddChild(Row("Cône de vue (°)", _viewAngle));
        _showPrompt = Check("Afficher le prompt", true); interaction.AddChild(_showPrompt);
        interaction.AddChild(new Label { Text = "Verbes · clé | libellé | commande | maintien | tags", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _verbs = new TextEdit { CustomMinimumSize = new Vector2(350, 150), WrapMode = TextEdit.LineWrappingMode.None };
        interaction.AddChild(_verbs);

        var cameras = Card(firstRow, "CAMÉRAS CONFIGURABLES");
        _defaultCamera = new OptionButton();
        _defaultCamera.ItemSelected += _ => PopulateSelectedCamera();
        cameras.AddChild(Row("Profil actif", _defaultCamera));
        _cameraFov = Spin(20, 140, 60, 1); cameras.AddChild(Row("Champ de vue", _cameraFov));
        _cameraDistance = Spin(0, 40, 14, .25); cameras.AddChild(Row("Distance", _cameraDistance));
        _cameraPitch = Spin(-89, 89, 24, 1); cameras.AddChild(Row("Inclinaison (°)", _cameraPitch));
        _cameraCollision = Check("Évitement collision", true); cameras.AddChild(_cameraCollision);
        cameras.AddChild(new Label
        {
            Text = "8 profils livrés : isométrique, orbite, épaule, première personne, dessus, libre, véhicule et cinématique.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var input = Card(secondRow, "CONTRÔLES & PÉRIPHÉRIQUES");
        _allowRebinding = Check("Autoriser le rebinding", true); input.AddChild(_allowRebinding);
        _perPlayerInput = Check("Profil local par joueur", true); input.AddChild(_perPlayerInput);
        input.AddChild(new Label { Text = "Bindings · action | périphérique | touche | glyphe", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _inputBindings = new TextEdit { CustomMinimumSize = new Vector2(520, 190), WrapMode = TextEdit.LineWrappingMode.None };
        input.AddChild(_inputBindings);
        input.AddChild(new Label { Text = "Les contextes et actions accessibles restent typés dans le document canonique.", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("bcaacb") });

        var saves = Card(secondRow, "SAUVEGARDE & CHARGEMENT");
        _saveRequired = Check("Sauvegarde requise", true); saves.AddChild(_saveRequired);
        _chunkKilobytes = Spin(4, 4096, 64, 4); saves.AddChild(Row("Chunk (Kio)", _chunkKilobytes));
        _autosaveSeconds = Spin(0, 86400, 60, 5); saves.AddChild(Row("Autosave (s)", _autosaveSeconds));
        _rollbackSlots = Spin(0, 100, 3, 1); saves.AddChild(Row("Retours conservés", _rollbackSlots));
        _saveMigrations = Check("Autoriser migrations", true); saves.AddChild(_saveMigrations);
        _saveDependencies = Check("Vérifier dépendances module", true); saves.AddChild(_saveDependencies);
        saves.AddChild(new Label { Text = "SHA-256 · chunks réutilisés · rotation atomique · rollback vérifié", AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("bcaacb") });

        var scroll = new ScrollContainer { Name = "Gameplay étendu", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _tabs.AddChild(scroll);
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

        BuildSimulationTab();
        BuildSessionTab();

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
        var foundation = _profile.Foundation;
        var ground = foundation.GroundedMovement;
        _groundEnabled!.ButtonPressed = ground.Enabled; _groundRequired!.ButtonPressed = ground.Required;
        _walkSpeed!.Value = ground.WalkSpeed; _runSpeed!.Value = ground.RunSpeed;
        _acceleration!.Value = ground.Acceleration; _deceleration!.Value = ground.Deceleration;
        _slope!.Value = ground.MaximumSlopeDegrees; _stepHeight!.Value = ground.StepHeight;
        _jumpVelocity!.Value = ground.JumpVelocity; _crouchMultiplier!.Value = ground.CrouchSpeedMultiplier;
        _rootMotion!.ButtonPressed = ground.RootMotion; _prediction!.ButtonPressed = ground.NetworkPrediction;
        var interaction = foundation.Interaction;
        _interactionEnabled!.ButtonPressed = interaction.Enabled; _interactionRequired!.ButtonPressed = interaction.Required;
        _interactionAction!.Text = interaction.InputAction; _targeting!.Selected = (int)interaction.Targeting;
        _focusDistance!.Value = interaction.FocusDistance; _viewAngle!.Value = interaction.ViewAngleDegrees;
        _showPrompt!.ButtonPressed = interaction.ShowPrompt;
        _verbs!.Text = string.Join("\n", interaction.Verbs.Select(item =>
            $"{item.Key} | {item.Label} | {item.Command} | " +
            $"{item.HoldSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} | " +
            $"{string.Join(',', item.TargetTags)}"));
        _defaultCamera!.Clear();
        foreach (var camera in foundation.Cameras.Profiles) _defaultCamera.AddItem(camera.DisplayName);
        _defaultCamera.Selected = Math.Max(0, foundation.Cameras.Profiles.ToList().FindIndex(item =>
            item.Key == foundation.Cameras.DefaultProfileKey));
        PopulateSelectedCamera();
        _allowRebinding!.ButtonPressed = foundation.Input.AllowRebinding;
        _perPlayerInput!.ButtonPressed = foundation.Input.PerPlayerLocal;
        _inputBindings!.Text = string.Join("\n", foundation.Input.Bindings.Select(item =>
            $"{item.ActionKey} | {item.Device} | {item.Binding} | {item.Glyph}"));
        _saveRequired!.ButtonPressed = foundation.Save.Required;
        _chunkKilobytes!.Value = foundation.Save.ChunkBytes / 1024;
        _autosaveSeconds!.Value = foundation.Save.AutosaveIntervalSeconds;
        _rollbackSlots!.Value = foundation.Save.RollbackSlots;
        _saveMigrations!.ButtonPressed = foundation.Save.AllowMigrations;
        _saveDependencies!.ButtonPressed = foundation.Save.RequireModuleDependencies;
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
        PopulateSimulation();
        PopulateSession();
        _summary!.Text = $"P0 v{foundation.SchemaVersion} · {foundation.Cameras.Profiles.Count} caméras · " +
                         $"{foundation.Input.Bindings.Count} bindings · autosave {foundation.Save.AutosaveIntervalSeconds}s · " +
                         $"{_profile.RadialMenu.Actions.Count} actions avancées · provenance {_profile.Provenance}";
        _status!.Text = "Prêt · configuration canonique chargée.";
    }

    private bool Save()
    {
        if (_profile is null) return false;
        try
        {
            var sourceFoundation = _profile.Foundation;
            var ground = sourceFoundation.GroundedMovement with
            {
                Enabled = _groundEnabled!.ButtonPressed,
                Required = _groundRequired!.ButtonPressed,
                WalkSpeed = (float)_walkSpeed!.Value,
                RunSpeed = (float)_runSpeed!.Value,
                Acceleration = (float)_acceleration!.Value,
                Deceleration = (float)_deceleration!.Value,
                MaximumSlopeDegrees = (float)_slope!.Value,
                StepHeight = (float)_stepHeight!.Value,
                JumpVelocity = (float)_jumpVelocity!.Value,
                CrouchSpeedMultiplier = (float)_crouchMultiplier!.Value,
                RootMotion = _rootMotion!.ButtonPressed,
                NetworkPrediction = _prediction!.ButtonPressed,
                Provenance = "forge-ui:r010",
            };
            var interaction = sourceFoundation.Interaction with
            {
                Enabled = _interactionEnabled!.ButtonPressed,
                Required = _interactionRequired!.ButtonPressed,
                InputAction = _interactionAction!.Text.Trim().ToLowerInvariant(),
                Targeting = (InteractionTargetingMode)_targeting!.Selected,
                FocusDistance = (float)_focusDistance!.Value,
                ViewAngleDegrees = (float)_viewAngle!.Value,
                ShowPrompt = _showPrompt!.ButtonPressed,
                Verbs = ParseVerbs(sourceFoundation.Interaction),
                Provenance = "forge-ui:r010",
            };
            var cameraIndex = Math.Clamp(_defaultCamera!.Selected, 0, sourceFoundation.Cameras.Profiles.Count - 1);
            var selectedCamera = sourceFoundation.Cameras.Profiles[cameraIndex] with
            {
                FieldOfViewDegrees = (float)_cameraFov!.Value,
                Distance = (float)_cameraDistance!.Value,
                PitchDegrees = (float)_cameraPitch!.Value,
                CollisionAvoidance = _cameraCollision!.ButtonPressed,
            };
            var cameraProfiles = sourceFoundation.Cameras.Profiles.Select((item, index) =>
                index == cameraIndex ? selectedCamera : item).ToArray();
            var cameras = sourceFoundation.Cameras with
            {
                DefaultProfileKey = selectedCamera.Key,
                Profiles = cameraProfiles,
                Provenance = "forge-ui:r010",
            };
            var inputProfile = sourceFoundation.Input with
            {
                AllowRebinding = _allowRebinding!.ButtonPressed,
                PerPlayerLocal = _perPlayerInput!.ButtonPressed,
                Bindings = ParseBindings(sourceFoundation.Input),
                Provenance = "forge-ui:r010",
            };
            var savePolicy = sourceFoundation.Save with
            {
                Required = _saveRequired!.ButtonPressed,
                ChunkBytes = (int)_chunkKilobytes!.Value * 1024,
                AutosaveIntervalSeconds = (int)_autosaveSeconds!.Value,
                RollbackSlots = (int)_rollbackSlots!.Value,
                AllowMigrations = _saveMigrations!.ButtonPressed,
                RequireModuleDependencies = _saveDependencies!.ButtonPressed,
                Provenance = "forge-ui:r010",
            };
            var foundation = sourceFoundation with
            {
                GroundedMovement = ground,
                Interaction = interaction,
                Cameras = cameras,
                Input = inputProfile,
                Save = savePolicy,
                Provenance = "forge-ui:r010",
            };
            var foundationDiagnostic = GameplayFoundationValidator.Validate(foundation).FirstOrDefault();
            if (foundationDiagnostic is not null)
            {
                _status!.Text = $"✕ {foundationDiagnostic.Code} · {foundationDiagnostic.Path}";
                return false;
            }
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
            var simulation = BuildSimulationProfile(_profile.Simulation);
            var simulationDiagnostic = GameplaySimulationProfileValidator.Validate(simulation).FirstOrDefault();
            if (simulationDiagnostic is not null)
            {
                _status!.Text = $"âœ• {simulationDiagnostic.Code} Â· {simulationDiagnostic.Path}";
                return false;
            }
            var session = BuildSessionProfile(_profile.Session);
            var sessionDiagnostic = GameplaySessionProfileValidator.Validate(session).FirstOrDefault();
            if (sessionDiagnostic is not null)
            {
                _status!.Text = $"âœ• {sessionDiagnostic.Code} Â· {sessionDiagnostic.Path}";
                return false;
            }
            var candidate = new ModuleGameplayControlProfile(_profile.Id, 1, swim, climb, radial, time, combat,
                "forge-ui", foundation, simulation, session);
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

    private void BuildSimulationTab()
    {
        var simulationScroll = new ScrollContainer
        {
            Name = "Simulation 3D P1",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _tabs!.AddChild(simulationScroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        simulationScroll.AddChild(content);
        _simulationPreview = new GameplaySimulationPreview();
        content.AddChild(_simulationPreview);
        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        content.AddChild(grid);

        var flight = Card(grid, "VOL LIBRE 3D");
        _flightEnabled = Check("Activer le vol", true); flight.AddChild(_flightEnabled);
        _flightRequired = Check("Requis au runtime", false); flight.AddChild(_flightRequired);
        _flightSpeed = Spin(0, 250, 12, .5); flight.AddChild(Row("Vitesse max.", _flightSpeed));
        _flightAcceleration = Spin(0, 500, 18, .5); flight.AddChild(Row("Accélération", _flightAcceleration));
        _flightAltitude = Spin(1, 10000, 80, 1); flight.AddChild(Row("Altitude max.", _flightAltitude));
        _flightBanking = Spin(0, 89, 35, 1); flight.AddChild(Row("Inclinaison", _flightBanking));
        _flightLift = Spin(0, 100, 2, .1); flight.AddChild(Row("Portance", _flightLift));
        _flightLanding = Check("Atterrissage sur surface", true); flight.AddChild(_flightLanding);
        _flightVolumetric = Check("Navigation volumétrique", true); flight.AddChild(_flightVolumetric);

        var projectile = Card(grid, "PROJECTILES");
        _projectileMode = new OptionButton();
        foreach (var value in Enum.GetValues<ProjectileMode>()) _projectileMode.AddItem(value.ToString());
        projectile.AddChild(Row("Mode", _projectileMode));
        _projectileSpeed = Spin(0, 1000, 24, 1); projectile.AddChild(Row("Vitesse", _projectileSpeed));
        _projectileGravity = Spin(0, 100, 9.81, .01); projectile.AddChild(Row("Gravité", _projectileGravity));
        _projectileLifetime = Spin(0, 300, 8, .25); projectile.AddChild(Row("Durée (s)", _projectileLifetime));
        _projectilePrediction = Check("Prédiction client", true); projectile.AddChild(_projectilePrediction);
        _projectileAuthority = Check("Impact serveur autoritaire", true); projectile.AddChild(_projectileAuthority);
        _projectileVfx = new LineEdit { PlaceholderText = "vfx:projectile-trail" };
        projectile.AddChild(Row("Traînée VFX", _projectileVfx));
        projectile.AddChild(new Label
        {
            Text = "Collision, impact VFX, révision et audit restent dans le profil canonique.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var physics = Card(grid, "PHYSIQUE 3D");
        _physicsRequired = Check("Scène requise au runtime", false); physics.AddChild(_physicsRequired);
        _physicsStep = Spin(.001, .1, 1.0 / 60, .001); physics.AddChild(Row("Pas fixe (s)", _physicsStep));
        physics.AddChild(new Label
        {
            Text = "Corps configurés : statique, cinématique, rigide et trigger.\n" +
                   "Liaisons : fixe, charnière, glissière et ressort.\n" +
                   "Bornes déterministes, personnages et politique réseau serveur.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var targeting = Card(grid, "CIBLAGE & APERÇU");
        _targetMode = new OptionButton();
        foreach (var value in Enum.GetValues<TargetingMode>()) _targetMode.AddItem(value.ToString());
        targeting.AddChild(Row("Forme", _targetMode));
        _targetRange = Spin(.1, 1000, 18, .5); targeting.AddChild(Row("Portée", _targetRange));
        _targetRadius = Spin(0, 500, 4, .25); targeting.AddChild(Row("Rayon", _targetRadius));
        _targetAngle = Spin(1, 360, 65, 1); targeting.AddChild(Row("Angle", _targetAngle));
        _targetCount = Spin(1, 256, 6, 1); targeting.AddChild(Row("Cibles max.", _targetCount));
        _targetLos = Check("Exiger ligne de vue", true); targeting.AddChild(_targetLos);
        targeting.AddChild(new Label
        {
            Text = "Self · Entity · Point · Direction · Area · Cone · Line · Chain",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var world = Card(grid, "TEMPS DU MONDE");
        _worldScale = Spin(.01, 100, 1, .01); world.AddChild(Row("Échelle globale", _worldScale));
        _worldPaused = Check("Horloge en pause", false); world.AddChild(_worldPaused);
        _worldPauseDomain = new LineEdit { PlaceholderText = "gameplay" };
        world.AddChild(Row("Domaine pause", _worldPauseDomain));
        world.AddChild(new Label
        {
            Text = "Calendrier, échelles locales, domaines de pause, planifications autoritaires et sauvegarde de révision.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var governance = Card(grid, "GOUVERNANCE");
        governance.AddChild(new Label
        {
            Text = "✓ UUID et schéma stables\n✓ Provenance par ressource\n✓ Diagnostic champ par champ\n" +
                   "✓ Commande dry-run / diff\n✓ Annuler / rétablir atomique\n✓ Handles ForgeScript typés\n" +
                   "✓ Lecture MCP filtrée\n✓ Autorité serveur explicite",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("aee8d2"),
        });
    }

    private void PopulateSimulation()
    {
        if (_profile is null || _flightEnabled is null) return;
        var simulation = _profile.Simulation;
        var flight = simulation.Flight;
        _flightEnabled.ButtonPressed = flight.Enabled;
        _flightRequired!.ButtonPressed = flight.Required;
        _flightSpeed!.Value = flight.MaximumSpeed;
        _flightAcceleration!.Value = flight.Acceleration;
        _flightAltitude!.Value = flight.MaximumAltitude;
        _flightBanking!.Value = flight.BankingDegrees;
        _flightLift!.Value = flight.Lift;
        _flightLanding!.ButtonPressed = flight.RequiresLandingSurface;
        _flightVolumetric!.ButtonPressed = flight.VolumetricNavigation;
        _projectileMode!.Selected = (int)simulation.Projectiles.Mode;
        _projectileSpeed!.Value = simulation.Projectiles.Speed;
        _projectileGravity!.Value = simulation.Projectiles.Gravity;
        _projectileLifetime!.Value = simulation.Projectiles.LifetimeSeconds;
        _projectilePrediction!.ButtonPressed = simulation.Projectiles.Prediction;
        _projectileAuthority!.ButtonPressed = simulation.Projectiles.ServerAuthoritative;
        _projectileVfx!.Text = simulation.Projectiles.VfxAssetId;
        _physicsRequired!.ButtonPressed = simulation.Physics.Required;
        _physicsStep!.Value = simulation.Physics.FixedStepSeconds;
        _targetMode!.Selected = (int)simulation.Targeting.Mode;
        _targetRange!.Value = simulation.Targeting.MaximumRange;
        _targetRadius!.Value = simulation.Targeting.Radius;
        _targetAngle!.Value = simulation.Targeting.AngleDegrees;
        _targetCount!.Value = simulation.Targeting.MaximumTargets;
        _targetLos!.ButtonPressed = simulation.Targeting.RequiresLineOfSight;
        _worldScale!.Value = simulation.WorldTime.InitialClock.TimeScale;
        _worldPaused!.ButtonPressed = simulation.WorldTime.InitialClock.Paused;
        _worldPauseDomain!.Text = simulation.WorldTime.InitialClock.PauseDomain;
        _simulationPreview!.ShowProfile(simulation);
    }

    private GameplaySimulationProfile BuildSimulationProfile(GameplaySimulationProfile source)
    {
        var flight = source.Flight with
        {
            Enabled = _flightEnabled!.ButtonPressed,
            Required = _flightRequired!.ButtonPressed,
            MaximumSpeed = (float)_flightSpeed!.Value,
            Acceleration = (float)_flightAcceleration!.Value,
            MaximumAltitude = (float)_flightAltitude!.Value,
            BankingDegrees = (float)_flightBanking!.Value,
            Lift = (float)_flightLift!.Value,
            RequiresLandingSurface = _flightLanding!.ButtonPressed,
            VolumetricNavigation = _flightVolumetric!.ButtonPressed,
            Provenance = "forge-ui:r066",
        };
        var projectile = source.Projectiles with
        {
            Mode = (ProjectileMode)_projectileMode!.Selected,
            Speed = (float)_projectileSpeed!.Value,
            Gravity = (float)_projectileGravity!.Value,
            LifetimeSeconds = (float)_projectileLifetime!.Value,
            Prediction = _projectilePrediction!.ButtonPressed,
            ServerAuthoritative = _projectileAuthority!.ButtonPressed,
            VfxAssetId = _projectileVfx!.Text.Trim(),
            Provenance = "forge-ui:r066",
        };
        var physics = source.Physics with
        {
            Required = _physicsRequired!.ButtonPressed,
            FixedStepSeconds = (float)_physicsStep!.Value,
            Provenance = "forge-ui:r066",
        };
        var targeting = source.Targeting with
        {
            Mode = (TargetingMode)_targetMode!.Selected,
            MaximumRange = (float)_targetRange!.Value,
            Radius = (float)_targetRadius!.Value,
            AngleDegrees = (float)_targetAngle!.Value,
            MaximumTargets = (int)_targetCount!.Value,
            RequiresLineOfSight = _targetLos!.ButtonPressed,
            Provenance = "forge-ui:r066",
        };
        var world = source.WorldTime with
        {
            InitialClock = source.WorldTime.InitialClock with
            {
                TimeScale = (float)_worldScale!.Value,
                Paused = _worldPaused!.ButtonPressed,
                PauseDomain = _worldPauseDomain!.Text.Trim().ToLowerInvariant(),
            },
            Provenance = "forge-ui:r066",
        };
        return source with
        {
            Flight = flight,
            Projectiles = projectile,
            Physics = physics,
            Targeting = targeting,
            WorldTime = world,
            Provenance = "forge-ui:r066",
        };
    }

    private void BuildSessionTab()
    {
        var sessionScroll = new ScrollContainer
        {
            Name = "Session & replay P1",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _tabs!.AddChild(sessionScroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        sessionScroll.AddChild(content);
        _sessionPreview = new GameplaySessionPreview();
        content.AddChild(_sessionPreview);
        var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        content.AddChild(grid);

        var persistence = Card(grid, "SAUVEGARDES & PORTÉES", 245);
        _saveScope = EnumChoice<PersistenceScope>(); persistence.AddChild(CompactRow("Portée", _saveScope));
        _saveAuthority = EnumChoice<SaveAuthority>(); persistence.AddChild(CompactRow("Autorité", _saveAuthority));
        _saveConflict = EnumChoice<SaveConflictPolicy>(); persistence.AddChild(CompactRow("Conflit", _saveConflict));
        _saveCloud = Check("Adaptateur cloud optionnel", false); persistence.AddChild(_saveCloud);
        _saveCloudAdapter = new LineEdit { PlaceholderText = "none" };
        persistence.AddChild(CompactRow("Adaptateur", _saveCloudAdapter));
        _savePersonal = Check("Contient des données personnelles", true); persistence.AddChild(_savePersonal);
        _saveEncrypted = Check("Chiffrer au repos", true); persistence.AddChild(_saveEncrypted);
        _saveSnapshots = Spin(1, 10000, 8, 1); persistence.AddChild(CompactRow("Snapshots", _saveSnapshots));

        var checkpoint = Card(grid, "CHECKPOINT & RESPAWN", 245);
        _checkpointWaypoint = new LineEdit { PlaceholderText = "waypoint:start" };
        checkpoint.AddChild(CompactRow("Waypoint", _checkpointWaypoint));
        _checkpointCost = Spin(0, 1000000, 0, 1); checkpoint.AddChild(CompactRow("Coût", _checkpointCost));
        _checkpointParty = EnumChoice<PartyRespawnPolicy>(); checkpoint.AddChild(CompactRow("Groupe", _checkpointParty));
        _checkpointCorpse = EnumChoice<CorpsePolicy>(); checkpoint.AddChild(CompactRow("Cadavre", _checkpointCorpse));
        _checkpointRadius = Spin(.1, 1000, 3, .1); checkpoint.AddChild(CompactRow("Rayon sûr", _checkpointRadius));
        _respawnDelay = Spin(0, 600, 3, .25); checkpoint.AddChild(CompactRow("Délai", _respawnDelay));
        _safeSpawn = Check("Exiger un spawn sûr", true); checkpoint.AddChild(_safeSpawn);
        checkpoint.AddChild(new Label
        {
            Text = "Hooks avant/après et politique d’inventaire sont persistés dans le profil.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var shell = Card(grid, "CARTE · JOURNAL · HUD", 245);
        _shellKit = new LineEdit(); shell.AddChild(CompactRow("Kit UI", _shellKit));
        _shellSkin = new LineEdit(); shell.AddChild(CompactRow("Skin", _shellSkin));
        _shellScale = Spin(.5, 3, 1, .05); shell.AddChild(CompactRow("Échelle", _shellScale));
        _shellMap = Check("Carte visible", true); shell.AddChild(_shellMap);
        _shellJournal = Check("Journal visible", true); shell.AddChild(_shellJournal);
        _shellHud = Check("HUD visible", true); shell.AddChild(_shellHud);
        shell.AddChild(new Label { Text = "Actions d’entrée · une par ligne" });
        _shellInputs = new TextEdit { CustomMinimumSize = new Vector2(210, 92) };
        shell.AddChild(_shellInputs);

        var controls = Card(grid, "VERROUS DE CONTRÔLE", 245);
        controls.AddChild(new Label
        {
            Text = "Contexte · propriétaire | curseur | caméra | actions | priorité",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _controlContexts = new TextEdit
        {
            CustomMinimumSize = new Vector2(210, 170),
            WrapMode = TextEdit.LineWrappingMode.None,
        };
        controls.AddChild(_controlContexts);
        controls.AddChild(new Label
        {
            Text = "Dialogue et cinématique empilent leur propriétaire puis restaurent toujours le contexte précédent.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var replay = Card(grid, "REPLAY DÉTERMINISTE", 245);
        _replayHeadless = Check("Exécuter headless", true); replay.AddChild(_replayHeadless);
        _replayReport = Check("Rapport de divergence", true); replay.AddChild(_replayReport);
        _replayPackage = Check("Reproduction minimale", true); replay.AddChild(_replayPackage);
        _replayFrames = Spin(0, 10000000, 100000, 1000); replay.AddChild(CompactRow("Frames", _replayFrames));
        _replayDirectory = new LineEdit(); replay.AddChild(CompactRow("Dossier", _replayDirectory));
        replay.AddChild(new Label
        {
            Text = "Entrées · événements · seeds · hash d’état · données privées expurgées",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

    }

    private void PopulateSession()
    {
        if (_profile is null || _saveScope is null) return;
        var session = _profile.Session;
        _saveScope.Selected = (int)session.Persistence.Scope;
        _saveAuthority!.Selected = (int)session.Persistence.Authority;
        _saveConflict!.Selected = (int)session.Persistence.ConflictPolicy;
        _saveCloud!.ButtonPressed = session.Persistence.CloudAdapterEnabled;
        _saveCloudAdapter!.Text = session.Persistence.CloudAdapter;
        _savePersonal!.ButtonPressed = session.Persistence.ContainsPersonalData;
        _saveEncrypted!.ButtonPressed = session.Persistence.EncryptAtRest;
        _saveSnapshots!.Value = session.Persistence.MaximumSnapshots;
        var checkpoint = session.Checkpoints.FirstOrDefault();
        _checkpointWaypoint!.Text = checkpoint?.WaypointId ?? string.Empty;
        _checkpointCost!.Value = checkpoint?.CurrencyCost ?? 0;
        _checkpointParty!.Selected = (int)(checkpoint?.PartyPolicy ?? PartyRespawnPolicy.Individual);
        _checkpointCorpse!.Selected = (int)(checkpoint?.CorpsePolicy ?? CorpsePolicy.Preserve);
        _checkpointRadius!.Value = checkpoint?.SafeRadius ?? 3;
        _respawnDelay!.Value = checkpoint?.RespawnDelaySeconds ?? 3;
        _safeSpawn!.ButtonPressed = checkpoint?.RequireSafeSpawn ?? true;
        _shellKit!.Text = session.Shell.KitId;
        _shellSkin!.Text = session.Shell.SkinId;
        _shellScale!.Value = session.Shell.Scale;
        _shellMap!.ButtonPressed = session.Shell.MapVisible;
        _shellJournal!.ButtonPressed = session.Shell.JournalVisible;
        _shellHud!.ButtonPressed = session.Shell.HudVisible;
        _shellInputs!.Text = string.Join("\n", session.Shell.InputActions);
        _controlContexts!.Text = string.Join("\n", session.ControlLocks.Contexts.OrderBy(item => item.Priority)
            .Select(item => $"{item.OwnerId} | {item.CursorVisible} | {item.CameraMode} | " +
                            $"{string.Join(',', item.AllowedInputActions.Order(StringComparer.Ordinal))} | {item.Priority}"));
        _replayHeadless!.ButtonPressed = session.Replay.HeadlessReplay;
        _replayReport!.ButtonPressed = session.Replay.EmitDivergenceReport;
        _replayPackage!.ButtonPressed = session.Replay.EmitMinimalPackage;
        _replayFrames!.Value = session.Replay.MaximumFrames;
        _replayDirectory!.Text = session.Replay.PackageDirectory;
        _sessionPreview!.ShowProfile(session);
    }

    private GameplaySessionProfile BuildSessionProfile(GameplaySessionProfile source)
    {
        var persistence = source.Persistence with
        {
            Scope = (PersistenceScope)_saveScope!.Selected,
            Authority = (SaveAuthority)_saveAuthority!.Selected,
            ConflictPolicy = (SaveConflictPolicy)_saveConflict!.Selected,
            CloudAdapterEnabled = _saveCloud!.ButtonPressed,
            CloudAdapter = _saveCloudAdapter!.Text.Trim(),
            ContainsPersonalData = _savePersonal!.ButtonPressed,
            PrivacyClassification = _savePersonal.ButtonPressed ? "personal" : "internal",
            EncryptAtRest = _saveEncrypted!.ButtonPressed,
            MaximumSnapshots = (int)_saveSnapshots!.Value,
            Provenance = "forge-ui:r067",
        };
        var checkpointSource = source.Checkpoints.FirstOrDefault() ?? GameplaySessionProfile
            .CreateDefault(Forge.Domain.Modules.ModuleId.From(_profile!.Id)).Checkpoints[0];
        var checkpoint = checkpointSource with
        {
            WaypointId = _checkpointWaypoint!.Text.Trim(),
            CurrencyCost = (int)_checkpointCost!.Value,
            PartyPolicy = (PartyRespawnPolicy)_checkpointParty!.Selected,
            CorpsePolicy = (CorpsePolicy)_checkpointCorpse!.Selected,
            SafeRadius = _checkpointRadius!.Value,
            RespawnDelaySeconds = _respawnDelay!.Value,
            RequireSafeSpawn = _safeSpawn!.ButtonPressed,
            Provenance = "forge-ui:r067",
        };
        var inputs = _shellInputs!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries).Select(item => item.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal).ToArray();
        var shell = source.Shell with
        {
            KitId = _shellKit!.Text.Trim(),
            SkinId = _shellSkin!.Text.Trim(),
            Scale = _shellScale!.Value,
            MapVisible = _shellMap!.ButtonPressed,
            JournalVisible = _shellJournal!.ButtonPressed,
            HudVisible = _shellHud!.ButtonPressed,
            InputActions = inputs,
            Provenance = "forge-ui:r067",
        };
        var contextIds = source.ControlLocks.Contexts.ToDictionary(item => item.OwnerId, item => item.Id,
            StringComparer.Ordinal);
        var contexts = new List<ControlContext>();
        foreach (var raw in _controlContexts!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|').Select(item => item.Trim()).ToArray();
            if (parts.Length != 5 || !bool.TryParse(parts[1], out var cursor) ||
                !int.TryParse(parts[4], out var priority))
                throw new ArgumentException($"Contexte de contrôle invalide : '{raw}'.");
            contexts.Add(new(contextIds.GetValueOrDefault(parts[0], Guid.NewGuid()), 1, "forge-ui:r067",
                parts[0], cursor, parts[2], new HashSet<string>(parts[3].Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.Ordinal), true, priority));
        }
        var locks = source.ControlLocks with { Contexts = contexts, Provenance = "forge-ui:r067" };
        var replay = source.Replay with
        {
            HeadlessReplay = _replayHeadless!.ButtonPressed,
            EmitDivergenceReport = _replayReport!.ButtonPressed,
            EmitMinimalPackage = _replayPackage!.ButtonPressed,
            MaximumFrames = (int)_replayFrames!.Value,
            PackageDirectory = _replayDirectory!.Text.Trim(),
            Provenance = "forge-ui:r067",
        };
        return source with
        {
            Persistence = persistence,
            Checkpoints = [checkpoint],
            Shell = shell,
            ControlLocks = locks,
            Replay = replay,
            Provenance = "forge-ui:r067",
        };
    }

    private void PopulateSelectedCamera()
    {
        if (_profile is null || _defaultCamera is null || _defaultCamera.Selected < 0) return;
        var index = Math.Clamp(_defaultCamera.Selected, 0, _profile.Foundation.Cameras.Profiles.Count - 1);
        var camera = _profile.Foundation.Cameras.Profiles[index];
        _cameraFov!.Value = camera.FieldOfViewDegrees;
        _cameraDistance!.Value = camera.Distance;
        _cameraPitch!.Value = camera.PitchDegrees;
        _cameraCollision!.ButtonPressed = camera.CollisionAvoidance;
    }

    private IReadOnlyList<InteractionVerbDefinition> ParseVerbs(InteractionProfile source)
    {
        var ids = source.Verbs.ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);
        var verbs = new List<InteractionVerbDefinition>();
        foreach (var raw in _verbs!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|').Select(value => value.Trim()).ToArray();
            if (parts.Length < 5 || !float.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var hold))
                throw new ArgumentException($"Verbe invalide : '{raw}'.");
            var key = parts[0].ToLowerInvariant();
            var tags = parts[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.ToLowerInvariant()).ToArray();
            verbs.Add(new(ids.GetValueOrDefault(key, Guid.NewGuid()), key, parts[1], parts[2], hold, tags, true));
        }
        return verbs;
    }

    private IReadOnlyList<RuntimeInputBindingDefinition> ParseBindings(RuntimeInputProfile source)
    {
        var ids = source.Bindings.ToDictionary(item => $"{item.ActionKey}:{item.Device}", item => item.Id,
            StringComparer.Ordinal);
        var bindings = new List<RuntimeInputBindingDefinition>();
        foreach (var raw in _inputBindings!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|').Select(value => value.Trim()).ToArray();
            if (parts.Length < 4 || !Enum.TryParse<RuntimeInputDevice>(parts[1], true, out var device))
                throw new ArgumentException($"Binding invalide : '{raw}'.");
            var action = parts[0].ToLowerInvariant();
            var id = ids.GetValueOrDefault($"{action}:{device}", Guid.NewGuid());
            bindings.Add(new(id, action, device, parts[2], parts[3], true));
        }
        return bindings;
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

    private static VBoxContainer Card(Container parent, string title, float minimumWidth = 350)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Style("171221", "513a6d")); parent.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(minimumWidth, 0) };
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

    private static HBoxContainer CompactRow(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(78, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private static CheckBox Check(string text, bool pressed) => new() { Text = text, ButtonPressed = pressed };
    private static OptionButton EnumChoice<T>() where T : struct, Enum
    {
        var choice = new OptionButton { FitToLongestItem = false, ClipText = true };
        foreach (var value in Enum.GetValues<T>()) choice.AddItem(value.ToString());
        return choice;
    }
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
