#if TOOLS
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Rules;
using Godot;

namespace Forge.Toolset.Editor;

public partial class RulesConfigurationWindow : Window
{
    private Func<ProjectState?>? _state;
    private Func<AreaId?>? _activeArea;
    private Func<ModuleRulesProfile, AreaId, AreaRulesProfile, bool>? _apply;
    private Action<Window>? _brand;
    private ModuleRulesProfile? _module;
    private AreaRulesProfile? _area;
    private AreaId? _areaId;
    private SpinBox? _groupMultiplier;
    private SpinBox? _progressionDifficulty;
    private CheckBox? _shareParty;
    private TextEdit? _sourceMultipliers;
    private SpinBox? _playerDamage;
    private SpinBox? _incomingDamage;
    private SpinBox? _aiIntensity;
    private SpinBox? _economy;
    private SpinBox? _restRecovery;
    private OptionButton? _saveRestrictions;
    private OptionButton? _difficultyFriendlyFire;
    private TextEdit? _mutators;
    private TextEdit? _perception;
    private OptionButton? _pvpMode;
    private OptionButton? _areaFriendlyFire;
    private CheckBox? _crime;
    private CheckBox? _aggro;
    private CheckBox? _environmentDamage;
    private CheckBox? _serverAuthority;
    private Label? _summary;
    private Label? _status;

    internal string Summary => _module is null || _area is null ? "unavailable" :
        $"difficulty={_module.ActiveDifficultyKey};mutators={_module.Mutators.Mutators.Count(item => item.Enabled)};" +
        $"perception={_area.Perception.Modifiers.Count};pvp={_area.Combat.PvpMode}";

    public void Initialize(Func<ProjectState?> state, Func<AreaId?> activeArea,
        Func<ModuleRulesProfile, AreaId, AreaRulesProfile, bool> apply, Action<Window> brand)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _activeArea = activeArea ?? throw new ArgumentNullException(nameof(activeArea));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _brand = brand ?? throw new ArgumentNullException(nameof(brand));
        Title = "Règles & difficulté · OpenRPG Forge";
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

    internal bool ConfigureForSmokeTest(ModuleRulesProfile module, AreaId areaId, AreaRulesProfile area)
    {
        _module = module; _areaId = areaId; _area = area; Populate(); return true;
    }

    internal bool ApplyForSmokeTest() => Save();

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);
        var title = new Label { Text = "PROGRESSION, DIFFICULTÉ & RÈGLES DE ZONE", ThemeTypeVariation = "HeaderSmall" };
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Composez des règles génériques pilotées par les données. Les modifications sont prévisualisées, validées, persistantes et annulables.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _summary = new Label(); _summary.AddThemeColorOverride("font_color", new Color("c7b3dc")); root.AddChild(_summary);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; root.AddChild(scroll);
        var columns = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12); scroll.AddChild(columns);

        var progression = Card(columns, "PROGRESSION DATA-DRIVEN");
        _groupMultiplier = Spin(.05, 20, 1, .05); progression.AddChild(Row("Multiplicateur groupe", _groupMultiplier));
        _progressionDifficulty = Spin(.05, 20, 1, .05); progression.AddChild(Row("Multiplicateur difficulté", _progressionDifficulty));
        _shareParty = Check("Partager entre les membres du groupe", true); progression.AddChild(_shareParty);
        progression.AddChild(new Label { Text = "Sources · identifiant | multiplicateur" });
        _sourceMultipliers = new TextEdit { CustomMinimumSize = new Vector2(350, 145) }; progression.AddChild(_sourceMultipliers);
        progression.AddChild(new Label { Text = "Mutateurs · clé | actif", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _mutators = new TextEdit { CustomMinimumSize = new Vector2(350, 115) }; progression.AddChild(_mutators);

        var difficulty = Card(columns, "DIFFICULTÉ COMPOSABLE");
        _playerDamage = Spin(.05, 20, 1, .05); difficulty.AddChild(Row("Dégâts joueur", _playerDamage));
        _incomingDamage = Spin(.05, 20, 1, .05); difficulty.AddChild(Row("Dégâts reçus", _incomingDamage));
        _aiIntensity = Spin(.05, 20, 1, .05); difficulty.AddChild(Row("Intensité IA", _aiIntensity));
        _economy = Spin(.05, 20, 1, .05); difficulty.AddChild(Row("Économie", _economy));
        _restRecovery = Spin(.05, 20, 1, .05); difficulty.AddChild(Row("Récupération repos", _restRecovery));
        _saveRestrictions = EnumOptions<SaveRestrictionPolicy>(); difficulty.AddChild(Row("Sauvegardes", _saveRestrictions));
        _difficultyFriendlyFire = EnumOptions<FriendlyFireMode>(); difficulty.AddChild(Row("Tirs alliés globaux", _difficultyFriendlyFire));
        difficulty.AddChild(new HSeparator());
        difficulty.AddChild(new Label { Text = "Perception · sens | multiplicateur | addition | condition", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _perception = new TextEdit { CustomMinimumSize = new Vector2(350, 170) }; difficulty.AddChild(_perception);

        var area = Card(columns, "COMBAT DE ZONE");
        _pvpMode = EnumOptions<AreaPvpMode>(); area.AddChild(Row("Mode PvP", _pvpMode));
        _areaFriendlyFire = EnumOptions<FriendlyFireMode>(); area.AddChild(Row("Tirs alliés zone", _areaFriendlyFire));
        _crime = Check("Activer les actes criminels", true); area.AddChild(_crime);
        _aggro = Check("Activer l’hostilité et l’aggro", true); area.AddChild(_aggro);
        _environmentDamage = Check("Autoriser les dégâts environnementaux", true); area.AddChild(_environmentDamage);
        _serverAuthority = Check("Exiger l’autorité serveur", true); area.AddChild(_serverAuthority);
        area.AddChild(new Label
        {
            Text = "Les duels, factions hostiles, crimes, tirs alliés et dégâts du décor sont arbitrés par la politique de la zone.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("bcaacb"),
        });

        var footer = new HBoxContainer();
        _status = new Label { Text = "Prêt · aucune mutation.", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _status.AddThemeColorOverride("font_color", new Color("9bd9bd")); footer.AddChild(_status);
        var apply = new Button { Text = "Prévisualiser et appliquer", CustomMinimumSize = new Vector2(220, 40) };
        apply.Pressed += () => Save(); footer.AddChild(apply);
        var close = new Button { Text = "Fermer", CustomMinimumSize = new Vector2(110, 40) };
        close.Pressed += Hide; footer.AddChild(close); root.AddChild(footer);
    }

    private void Refresh()
    {
        var state = _state?.Invoke(); _areaId = _activeArea?.Invoke();
        if (state is null || _areaId is null) { _status!.Text = "Aucun projet ou zone active."; return; }
        var area = state.Project.Areas.SingleOrDefault(item => item.Id == _areaId);
        if (area is null) { _status!.Text = "Zone active introuvable."; return; }
        _module = state.Project.Module.RuntimePolicies.Rules; _area = area.RuntimePolicies.Rules; Populate();
    }

    private void Populate()
    {
        if (_module is null || _area is null) return;
        var progression = _module.Progression;
        _groupMultiplier!.Value = progression.GroupMultiplier;
        _progressionDifficulty!.Value = progression.DifficultyMultiplier;
        _shareParty!.ButtonPressed = progression.ShareWithParty;
        _sourceMultipliers!.Text = string.Join("\n", progression.SourceMultipliers.Select(item =>
            $"{item.Key} | {item.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}"));
        _mutators!.Text = string.Join("\n", _module.Mutators.Mutators.Select(item => $"{item.Key} | {(item.Enabled ? "oui" : "non")}"));
        var difficulty = _module.ActiveDifficulty;
        _playerDamage!.Value = difficulty.PlayerDamageMultiplier; _incomingDamage!.Value = difficulty.IncomingDamageMultiplier;
        _aiIntensity!.Value = difficulty.AiIntensityMultiplier; _economy!.Value = difficulty.EconomyMultiplier;
        _restRecovery!.Value = difficulty.RestRecoveryMultiplier; _saveRestrictions!.Selected = (int)difficulty.SaveRestrictions;
        _difficultyFriendlyFire!.Selected = (int)difficulty.FriendlyFire;
        _perception!.Text = string.Join("\n", _area.Perception.Modifiers.Select(item =>
            $"{item.SenseId} | {item.Multiplier.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} | " +
            $"{item.AdditiveModifier.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} | {item.ConditionTag}"));
        var combat = _area.Combat; _pvpMode!.Selected = (int)combat.PvpMode;
        _areaFriendlyFire!.Selected = (int)combat.FriendlyFire; _crime!.ButtonPressed = combat.CrimeEnabled;
        _aggro!.ButtonPressed = combat.AggroEnabled; _environmentDamage!.ButtonPressed = combat.EnvironmentalDamageEnabled;
        _serverAuthority!.ButtonPressed = combat.ServerAuthoritative;
        _summary!.Text = $"Schéma v{_module.SchemaVersion} · règles {progression.RulesetId} · difficulté {difficulty.DisplayName} · zone {_areaId}";
        _status!.Text = "Prêt · profils canoniques chargés.";
    }

    private bool Save()
    {
        if (_module is null || _area is null || _areaId is null) return false;
        try
        {
            var sourceMultipliers = ParseSources();
            var progression = new ProgressionPolicy(_module.Progression.Id, 1, _module.Progression.RulesetId,
                _module.Progression.Curve, _groupMultiplier!.Value, _progressionDifficulty!.Value,
                sourceMultipliers, _shareParty!.ButtonPressed, "forge-ui");
            var current = _module.ActiveDifficulty;
            var difficulty = new DifficultyProfile(current.Id, 1, current.Key, current.DisplayName,
                _playerDamage!.Value, _incomingDamage!.Value, _aiIntensity!.Value, _economy!.Value,
                _restRecovery!.Value, (SaveRestrictionPolicy)_saveRestrictions!.Selected,
                (FriendlyFireMode)_difficultyFriendlyFire!.Selected, current.Accessibility, "forge-ui");
            var difficulties = _module.Difficulties.Select(item => item.Key == current.Key ? difficulty : item).ToArray();
            var mutators = new MutatorSet(_module.Mutators.Id, 1, ParseMutators(_module.Mutators), "forge-ui");
            var module = new ModuleRulesProfile(_module.Id, 1, progression, difficulties, current.Key, mutators, "forge-ui");
            var perception = new EnvironmentPerceptionProfile(_area.Perception.Id, 1,
                _area.Perception.RulesetId, ParsePerception(_area.Perception), "forge-ui");
            var sourceCombat = _area.Combat;
            var combat = new AreaCombatPolicy(sourceCombat.Id, 1, (AreaPvpMode)_pvpMode!.Selected,
                (FriendlyFireMode)_areaFriendlyFire!.Selected, _crime!.ButtonPressed, _aggro!.ButtonPressed,
                _environmentDamage!.ButtonPressed, _serverAuthority!.ButtonPressed, "forge-ui");
            var area = new AreaRulesProfile(_area.Id, 1, perception, combat, "forge-ui");
            var succeeded = _apply?.Invoke(module, _areaId, area) == true;
            _status!.Text = succeeded ? "✓ Règles validées, persistantes et annulables." :
                "✕ Application refusée · consultez les diagnostics.";
            if (succeeded) { _module = module; _area = area; Populate(); }
            return succeeded;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            _status!.Text = $"✕ {exception.Message}"; return false;
        }
    }

    private Dictionary<string, double> ParseSources()
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var raw in _sourceMultipliers!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|').Select(item => item.Trim()).ToArray();
            if (parts.Length != 2 || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                throw new FormatException($"Source invalide : {raw}");
            result.Add(parts[0].ToLowerInvariant(), value);
        }
        return result;
    }

    private IReadOnlyList<GameplayMutatorDefinition> ParseMutators(MutatorSet source)
    {
        var enabled = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var raw in _mutators!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|').Select(item => item.Trim().ToLowerInvariant()).ToArray();
            if (parts.Length != 2 || parts[1] is not ("oui" or "non" or "on" or "off" or "true" or "false"))
                throw new FormatException($"Mutateur invalide : {raw}");
            enabled[parts[0]] = parts[1] is "oui" or "on" or "true";
        }
        return source.Mutators.Select(item => item with { Enabled = enabled.GetValueOrDefault(item.Key, item.Enabled) }).ToArray();
    }

    private IReadOnlyList<EnvironmentPerceptionModifier> ParsePerception(EnvironmentPerceptionProfile source)
    {
        var ids = source.Modifiers.ToDictionary(item => $"{item.SenseId}:{item.ConditionTag}", item => item.Id,
            StringComparer.Ordinal);
        var result = new List<EnvironmentPerceptionModifier>();
        foreach (var raw in _perception!.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|').Select(item => item.Trim()).ToArray();
            if (parts.Length < 3 || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var multiplier) ||
                !double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var additive))
                throw new FormatException($"Modificateur de perception invalide : {raw}");
            var condition = parts.Length > 3 ? parts[3].ToLowerInvariant() : string.Empty;
            var sense = parts[0].ToLowerInvariant();
            result.Add(new EnvironmentPerceptionModifier(ids.GetValueOrDefault($"{sense}:{condition}", Guid.NewGuid()),
                sense, multiplier, additive, condition));
        }
        return result;
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
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(10); style.ContentMarginLeft = style.ContentMarginRight = 14;
        style.ContentMarginTop = style.ContentMarginBottom = 12; return style;
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer(); row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(155, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row.AddChild(control); return row;
    }

    private static CheckBox Check(string text, bool value) => new() { Text = text, ButtonPressed = value };
    private static SpinBox Spin(double min, double max, double value, double step) => new()
    { MinValue = min, MaxValue = max, Value = value, Step = step, AllowGreater = false, AllowLesser = false };
    private static OptionButton EnumOptions<T>() where T : struct, Enum
    {
        var options = new OptionButton(); foreach (var value in Enum.GetValues<T>()) options.AddItem(value.ToString());
        return options;
    }
}
#endif
