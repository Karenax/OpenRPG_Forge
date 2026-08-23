using System.Globalization;
using Forge.Domain.Rules;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioRulesWorldWorkbenchUx6A : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6AEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profile;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly OptionButton _difficultyChoice;
    private readonly CheckBox _difficultyActive;
    private readonly LineEdit _difficultyKey;
    private readonly LineEdit _difficultyName;
    private readonly SpinBox[] _difficultyMultipliers;
    private readonly OptionButton _saveRestrictions;
    private readonly OptionButton _friendlyFire;
    private readonly CheckBox _pauseAssist;
    private readonly CheckBox _telegraphAssist;
    private readonly SpinBox _timingAssist;
    private readonly SpinBox _perceptionAssist;
    private readonly Button _addDifficulty;
    private readonly Button _removeDifficulty;

    private readonly OptionButton _mutatorChoice;
    private readonly LineEdit _mutatorKey;
    private readonly LineEdit _mutatorName;
    private readonly CheckBox _mutatorEnabled;
    private readonly TextEdit _mutatorScalars;
    private readonly TextEdit _mutatorTags;
    private readonly Button _addMutator;
    private readonly Button _removeMutator;

    private readonly CheckBox _pluginsEnabled;
    private readonly CheckBox _pluginsRequired;
    private readonly OptionButton _pluginChoice;
    private readonly CheckBox _pluginActive;
    private readonly LineEdit _pluginKey;
    private readonly LineEdit _pluginVersion;
    private readonly LineEdit _pluginContent;
    private readonly TextEdit[] _pluginRegistries;
    private readonly Button _addPlugin;
    private readonly Button _removePlugin;

    private readonly CheckBox _worldEnabled;
    private readonly CheckBox _worldRequired;
    private readonly SpinBox _daysPerMonth;
    private readonly SpinBox _monthsPerYear;
    private readonly SpinBox _hoursPerDay;
    private readonly SpinBox _minutesPerHour;
    private readonly LineEdit _totalMinutes;
    private readonly SpinBox _timeScale;
    private readonly CheckBox _clockPaused;
    private readonly LineEdit _pauseDomain;
    private readonly VBoxContainer _areaScales;
    private readonly TextEdit _pauseDomains;
    private readonly OptionButton _scheduleChoice;
    private readonly LineEdit _scheduleKey;
    private readonly SpinBox _scheduleMinute;
    private readonly LineEdit _scheduleDays;
    private readonly LineEdit _scheduleEvent;
    private readonly LineEdit _scheduleDomain;
    private readonly CheckBox _scheduleAuthority;
    private readonly Button _addSchedule;
    private readonly Button _removeSchedule;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _previewRules;
    private readonly Button _applyRules;
    private readonly Button _previewWorld;
    private readonly Button _applyWorld;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx6AProfileChoice> _choices = [];
    private StudioUx6ADraft? _draft;
    private List<StudioUx6ADifficultyDraft> _difficulties = [];
    private List<StudioUx6AMutatorDraft> _mutators = [];
    private List<StudioUx6APluginPackageDraft> _plugins = [];
    private List<StudioUx6AScheduleDraft> _schedules = [];
    private readonly List<(StudioUx6AAreaScaleDraft Source, CheckBox Enabled, SpinBox Scale)> _scaleEditors = [];
    private string _activeDifficulty = string.Empty;
    private string _activePlugin = string.Empty;
    private int _difficultyIndex = -1;
    private int _mutatorIndex = -1;
    private int _pluginIndex = -1;
    private int _scheduleIndex = -1;
    private bool _updating;
    private bool _rulesReady;
    private bool _worldReady;
    private bool _applied;
    private StudioUx6ABlockPreview? _rulesResult;
    private StudioUx6ABlockPreview? _worldResult;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? RulesWorldChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _draft is not null && _profile.Selected > 0;
    internal bool RulesPreviewReady => _rulesReady && _rulesResult is
    { Succeeded: true, CanonicalDryRun: true, RuntimeClaimsBounded: true };
    internal bool WorldPreviewReady => _worldReady && _worldResult is
    { Succeeded: true, CanonicalDryRun: true, RuntimeClaimsBounded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6A();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null && _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsFourWorkflows => _tabs.GetTabCount() == 4 &&
                                        Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)
                                            .SequenceEqual(new[]
                                            {
                                                "Difficultés", "Mutateurs", "Plugins de règles", "Temps du monde",
                                            });
    internal bool ShowsTwoTransactions => DescendantTextUx6A(this).Any(item =>
                                                item.Contains("Aperçu règles", StringComparison.Ordinal)) &&
                                            DescendantTextUx6A(this).Any(item =>
                                                item.Contains("Aperçu temps", StringComparison.Ordinal));
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6A(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Règles & temps du monde", StringComparison.Ordinal) &&
                   text.Contains("Difficultés", StringComparison.Ordinal) &&
                   text.Contains("Mutateurs", StringComparison.Ordinal) &&
                   text.Contains("Plugins de règles", StringComparison.Ordinal) &&
                   text.Contains("Temps du monde", StringComparison.Ordinal) &&
                   text.Contains("deux transactions", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx6A(text);
        }
    }

    internal StudioRulesWorldWorkbenchUx6A(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6ARulesWorldDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Règles & temps du monde", 18));
        headingText.AddChild(StudioStyle.Label(
            "Quatre workflows humains · deux transactions canoniques séparées · aucun package ni événement exécuté dans Studio.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _profile = StudioUx2EnvBControls.Options();
        _profile.ItemSelected += SelectProfileUx6A;
        root.AddChild(StudioUx2EnvBControls.Field("Module et zone de contexte · sélection explicite", _profile, 620));
        _selection = StudioStyle.Label("Choisissez le module et sa zone de contexte.", 10, StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 610),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var difficultyTab = ScrollTabUx6A("Difficultés");
        var difficultyHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _difficultyChoice = StudioUx2EnvBControls.Options();
        _difficultyChoice.ItemSelected += SelectDifficultyUx6A;
        difficultyHeader.AddChild(StudioUx2EnvBControls.Field("Profil sélectionné", _difficultyChoice, 360));
        _addDifficulty = StudioStyle.Button("Ajouter un profil", compact: true);
        _addDifficulty.Pressed += AddDifficultyUx6A;
        difficultyHeader.AddChild(_addDifficulty);
        _removeDifficulty = StudioStyle.Button("Retirer", compact: true);
        _removeDifficulty.Pressed += RemoveDifficultyUx6A;
        difficultyHeader.AddChild(_removeDifficulty);
        difficultyTab.AddChild(difficultyHeader);
        _difficultyActive = StudioUx2EnvBControls.Check("Profil actif du module");
        difficultyTab.AddChild(_difficultyActive);
        var difficultyIdentity = GridUx6A(2);
        _difficultyKey = StudioUx2EnvBControls.Text("standard");
        _difficultyName = StudioUx2EnvBControls.Text("Standard");
        difficultyIdentity.AddChild(StudioUx2EnvBControls.Field("Clé canonique", _difficultyKey, 260));
        difficultyIdentity.AddChild(StudioUx2EnvBControls.Field("Nom humain", _difficultyName, 320));
        difficultyTab.AddChild(StudioUx2EnvBControls.Card("Ux6ADifficultyIdentity", "Identité du profil",
            "La clé reste indépendante d’un ruleset historique; l’identité stable est conservée sans être affichée.",
            difficultyIdentity));
        var multiplierGrid = GridUx6A(5);
        _difficultyMultipliers = new[]
        {
            NumberUx6A(.05, 20, .05), NumberUx6A(.05, 20, .05), NumberUx6A(.05, 20, .05),
            NumberUx6A(.05, 20, .05), NumberUx6A(.05, 20, .05),
        };
        var multiplierLabels = new[] { "Dégâts joueur", "Dégâts reçus", "Intensité IA", "Économie", "Récupération" };
        for (var index = 0; index < _difficultyMultipliers.Length; index++)
            multiplierGrid.AddChild(StudioUx2EnvBControls.Field(multiplierLabels[index],
                _difficultyMultipliers[index], 150));
        difficultyTab.AddChild(StudioUx2EnvBControls.Card("Ux6ADifficultyScalars", "Multiplicateurs composables",
            "Valeurs data-driven de 0,05× à 20×; aucune formule de ruleset n’est codée dans le Studio.", multiplierGrid));
        var policyGrid = GridUx6A(4);
        _saveRestrictions = StudioUx2EnvBControls.Options("Sans restriction", "Points de contrôle", "Désactivée");
        _friendlyFire = StudioUx2EnvBControls.Options("Désactivé", "Groupe seulement", "Activé");
        _pauseAssist = StudioUx2EnvBControls.Check("Assistance pause");
        _telegraphAssist = StudioUx2EnvBControls.Check("Télégraphie renforcée");
        _timingAssist = NumberUx6A(.25, 4, .05);
        _perceptionAssist = NumberUx6A(.25, 4, .05);
        policyGrid.AddChild(StudioUx2EnvBControls.Field("Sauvegarde", _saveRestrictions));
        policyGrid.AddChild(StudioUx2EnvBControls.Field("Tir allié", _friendlyFire));
        policyGrid.AddChild(_pauseAssist);
        policyGrid.AddChild(_telegraphAssist);
        policyGrid.AddChild(StudioUx2EnvBControls.Field("Fenêtre temporelle", _timingAssist));
        policyGrid.AddChild(StudioUx2EnvBControls.Field("Indices de perception", _perceptionAssist));
        difficultyTab.AddChild(StudioUx2EnvBControls.Card("Ux6ADifficultyPolicies", "Sauvegarde & accessibilité",
            "Les aides restent explicites et indépendantes des multiplicateurs de combat.", policyGrid));

        var mutatorTab = ScrollTabUx6A("Mutateurs");
        var mutatorHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _mutatorChoice = StudioUx2EnvBControls.Options();
        _mutatorChoice.ItemSelected += SelectMutatorUx6A;
        mutatorHeader.AddChild(StudioUx2EnvBControls.Field("Mutateur sélectionné", _mutatorChoice, 360));
        _addMutator = StudioStyle.Button("Ajouter un mutateur", compact: true);
        _addMutator.Pressed += AddMutatorUx6A;
        mutatorHeader.AddChild(_addMutator);
        _removeMutator = StudioStyle.Button("Retirer", compact: true);
        _removeMutator.Pressed += RemoveMutatorUx6A;
        mutatorHeader.AddChild(_removeMutator);
        mutatorTab.AddChild(mutatorHeader);
        _mutatorEnabled = StudioUx2EnvBControls.Check("Mutateur activé");
        mutatorTab.AddChild(_mutatorEnabled);
        var mutatorIdentity = GridUx6A(2);
        _mutatorKey = StudioUx2EnvBControls.Text("story-support");
        _mutatorName = StudioUx2EnvBControls.Text("Soutien narratif");
        mutatorIdentity.AddChild(StudioUx2EnvBControls.Field("Clé", _mutatorKey, 260));
        mutatorIdentity.AddChild(StudioUx2EnvBControls.Field("Nom humain", _mutatorName, 320));
        mutatorTab.AddChild(StudioUx2EnvBControls.Card("Ux6AMutatorIdentity", "Identité & activation",
            "Chaque mutateur compose seulement les scalaires déclarés par le contrat générique.", mutatorIdentity));
        _mutatorScalars = StudioUx2EnvBControls.Lines(
            "Une valeur par ligne : incomingDamage=0.65\nprogression=1.25", 120);
        _mutatorTags = StudioUx2EnvBControls.Lines("Un tag humain par ligne", 78);
        mutatorTab.AddChild(StudioUx2EnvBControls.Card("Ux6AMutatorValues", "Scalaires & tags",
            "Scalaires autorisés : playerDamage, incomingDamage, aiIntensity, economy, restRecovery, progression.",
            StudioUx2EnvBControls.Field("Compositions", _mutatorScalars)));
        mutatorTab.AddChild(StudioUx2EnvBControls.Field("Tags", _mutatorTags));

        var pluginTab = ScrollTabUx6A("Plugins de règles");
        var pluginPolicy = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pluginsEnabled = StudioUx2EnvBControls.Check("Catalogue activé");
        _pluginsRequired = StudioUx2EnvBControls.Check("Extension requise au runtime");
        pluginPolicy.AddChild(_pluginsEnabled);
        pluginPolicy.AddChild(_pluginsRequired);
        pluginTab.AddChild(StudioUx2EnvBControls.Card("Ux6APluginPolicy", "Politique d’extension",
            "Un package optionnel absent est ignoré; un package requis absent produit un diagnostic stable.",
            pluginPolicy));
        var pluginHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pluginChoice = StudioUx2EnvBControls.Options();
        _pluginChoice.ItemSelected += SelectPluginUx6A;
        pluginHeader.AddChild(StudioUx2EnvBControls.Field("Package sélectionné", _pluginChoice, 360));
        _addPlugin = StudioStyle.Button("Ajouter un package", compact: true);
        _addPlugin.Pressed += AddPluginUx6A;
        pluginHeader.AddChild(_addPlugin);
        _removePlugin = StudioStyle.Button("Retirer", compact: true);
        _removePlugin.Pressed += RemovePluginUx6A;
        pluginHeader.AddChild(_removePlugin);
        pluginTab.AddChild(pluginHeader);
        _pluginActive = StudioUx2EnvBControls.Check("Package actif");
        pluginTab.AddChild(_pluginActive);
        var pluginIdentity = GridUx6A(3);
        _pluginKey = StudioUx2EnvBControls.Text("forge.generic");
        _pluginVersion = StudioUx2EnvBControls.Text("1.0.0");
        _pluginContent = StudioUx2EnvBControls.Text("content.core");
        pluginIdentity.AddChild(StudioUx2EnvBControls.Field("Clé", _pluginKey, 240));
        pluginIdentity.AddChild(StudioUx2EnvBControls.Field("Version", _pluginVersion, 160));
        pluginIdentity.AddChild(StudioUx2EnvBControls.Field("Package de contenu", _pluginContent, 240));
        pluginTab.AddChild(StudioUx2EnvBControls.Card("Ux6APluginIdentity", "Package de règles",
            "Les registres étendent Core sans DLL ou code natif provenant du module.", pluginIdentity));
        var registryGrid = GridUx6A(3);
        _pluginRegistries = new TextEdit[6];
        var registryNames = new[] { "Attributs", "Effets", "Capacités", "Inventaires", "Progression", "Panneaux UI" };
        for (var index = 0; index < _pluginRegistries.Length; index++)
        {
            _pluginRegistries[index] = StudioUx2EnvBControls.Lines("Une clé par ligne", 82);
            registryGrid.AddChild(StudioUx2EnvBControls.Field(registryNames[index], _pluginRegistries[index], 220));
        }
        pluginTab.AddChild(StudioUx2EnvBControls.Card("Ux6APluginRegistries", "Six registres obligatoires",
            "L’aperçu teste le package déclaré présent puis absent, sans charger ni exécuter l’extension.", registryGrid));

        var worldTab = ScrollTabUx6A("Temps du monde");
        var worldPolicy = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _worldEnabled = StudioUx2EnvBControls.Check("Horloge activée");
        _worldRequired = StudioUx2EnvBControls.Check("Temps du monde requis");
        worldPolicy.AddChild(_worldEnabled);
        worldPolicy.AddChild(_worldRequired);
        worldTab.AddChild(worldPolicy);
        var calendarGrid = GridUx6A(4);
        _daysPerMonth = NumberUx6A(1, 366, 1);
        _monthsPerYear = NumberUx6A(1, 100, 1);
        _hoursPerDay = NumberUx6A(1, 100, 1);
        _minutesPerHour = NumberUx6A(1, 1000, 1);
        calendarGrid.AddChild(StudioUx2EnvBControls.Field("Jours / mois", _daysPerMonth));
        calendarGrid.AddChild(StudioUx2EnvBControls.Field("Mois / année", _monthsPerYear));
        calendarGrid.AddChild(StudioUx2EnvBControls.Field("Heures / jour", _hoursPerDay));
        calendarGrid.AddChild(StudioUx2EnvBControls.Field("Minutes / heure", _minutesPerHour));
        worldTab.AddChild(StudioUx2EnvBControls.Card("Ux6ACalendar", "Calendrier",
            "Le calendrier reste générique et entièrement data-driven.", calendarGrid));
        var clockGrid = GridUx6A(4);
        _totalMinutes = StudioUx2EnvBControls.Text("0");
        _timeScale = NumberUx6A(0, 1000, .05);
        _clockPaused = StudioUx2EnvBControls.Check("Horloge initialement en pause");
        _pauseDomain = StudioUx2EnvBControls.Text("gameplay");
        clockGrid.AddChild(StudioUx2EnvBControls.Field("Minute totale initiale", _totalMinutes, 190));
        clockGrid.AddChild(StudioUx2EnvBControls.Field("Échelle initiale", _timeScale));
        clockGrid.AddChild(_clockPaused);
        clockGrid.AddChild(StudioUx2EnvBControls.Field("Domaine de pause", _pauseDomain, 180));
        worldTab.AddChild(StudioUx2EnvBControls.Card("Ux6AClock", "Horloge initiale",
            "La révision autoritaire cachée est préservée; Studio ne modifie aucune sauvegarde live.", clockGrid));
        _areaScales = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        worldTab.AddChild(StudioUx2EnvBControls.Card("Ux6AAreaScales", "Échelles locales par zone",
            "Activez une zone par son nom; toute clé héritée inconnue reste protégée sans UUID affiché.", _areaScales));
        _pauseDomains = StudioUx2EnvBControls.Lines("Un domaine par ligne : gameplay\ncinematic\nworld", 82);
        worldTab.AddChild(StudioUx2EnvBControls.Field("Domaines de pause", _pauseDomains));
        var scheduleHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scheduleChoice = StudioUx2EnvBControls.Options();
        _scheduleChoice.ItemSelected += SelectScheduleUx6A;
        scheduleHeader.AddChild(StudioUx2EnvBControls.Field("Horaire sélectionné", _scheduleChoice, 340));
        _addSchedule = StudioStyle.Button("Ajouter un horaire", compact: true);
        _addSchedule.Pressed += AddScheduleUx6A;
        scheduleHeader.AddChild(_addSchedule);
        _removeSchedule = StudioStyle.Button("Retirer", compact: true);
        _removeSchedule.Pressed += RemoveScheduleUx6A;
        scheduleHeader.AddChild(_removeSchedule);
        worldTab.AddChild(scheduleHeader);
        var scheduleGrid = GridUx6A(3);
        _scheduleKey = StudioUx2EnvBControls.Text("dawn-bell");
        _scheduleMinute = NumberUx6A(0, 10_000_000, 1, allowGreater: true);
        _scheduleDays = StudioUx2EnvBControls.Text("1,2,3,4,5,6,7");
        _scheduleEvent = StudioUx2EnvBControls.Text("world.dawn");
        _scheduleDomain = StudioUx2EnvBControls.Text("world");
        _scheduleAuthority = StudioUx2EnvBControls.Check("Autorité serveur");
        scheduleGrid.AddChild(StudioUx2EnvBControls.Field("Clé", _scheduleKey, 220));
        scheduleGrid.AddChild(StudioUx2EnvBControls.Field("Minute du jour", _scheduleMinute));
        scheduleGrid.AddChild(StudioUx2EnvBControls.Field("Jours du mois", _scheduleDays, 230));
        scheduleGrid.AddChild(StudioUx2EnvBControls.Field("Événement", _scheduleEvent, 240));
        scheduleGrid.AddChild(StudioUx2EnvBControls.Field("Domaine", _scheduleDomain, 180));
        scheduleGrid.AddChild(_scheduleAuthority);
        worldTab.AddChild(StudioUx2EnvBControls.Card("Ux6ASchedule", "Horaire sélectionné",
            "L’aperçu calcule l’éligibilité mais ne distribue aucun événement et n’exécute aucun ForgeScript.",
            scheduleGrid));

        var review = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 126),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _diff = ReviewColumnUx6A(review, "Diff sémantique");
        _runtime = ReviewColumnUx6A(review, "Aperçu déterministe");
        _diagnostics = ReviewColumnUx6A(review, "Diagnostics localisés");
        root.AddChild(review);
        _summary = StudioStyle.Label("Sélectionnez un profil puis lancez l’un des deux aperçus secs.",
            10, StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Deux transactions explicites : appliquer l’une invalide l’approbation de l’autre; rechargez puis prévisualisez de nouveau.",
            9, StudioStyle.Warning, wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        _previewRules = StudioStyle.Button("Aperçu règles", primary: true, compact: true);
        _previewRules.Pressed += PreviewRulesUx6A;
        _applyRules = StudioStyle.Button("Appliquer règles", compact: true);
        _applyRules.Pressed += ApplyRulesUx6A;
        _previewWorld = StudioStyle.Button("Aperçu temps", primary: true, compact: true);
        _previewWorld.Pressed += PreviewWorldUx6A;
        _applyWorld = StudioStyle.Button("Appliquer temps", compact: true);
        _applyWorld.Pressed += ApplyWorldUx6A;
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += UndoUx6A;
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += RedoUx6A;
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += SaveUx6A;
        foreach (var button in new[] { _previewRules, _applyRules, _previewWorld, _applyWorld, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(button == _previewRules || button == _previewWorld ? 132 : 116, 34);
            actions.AddChild(button);
        }
        root.AddChild(actions);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        WireChangesUx6A();
        SetEditingEnabledUx6A(false);
        RenderPreviewUx6A(null, "Aucun aperçu frais.");
        UpdateActionsUx6A();
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidenceUx6A();
        _session = _sourceSession;
        Visible = true;
        UpdateBoundsUx6A();
        RefreshChoicesUx6A();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (Visible || !ReferenceEquals(_session, _sourceSession)) _session.CancelUx6AApprovals();
        Visible = false;
        _draft = null;
        _rulesResult = null;
        _worldResult = null;
        _rulesReady = false;
        _worldReady = false;
        _applied = false;
        ReleaseEvidenceUx6A();
        _session = _sourceSession;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        Visible = false;
        _session.CancelUx6AApprovals();
        _draft = null;
        ReleaseEvidenceUx6A();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CaptureRulesWorldApprovalUx6A();
        _session = _sourceSession;
        Visible = true;
        UpdateBoundsUx6A();
        RefreshChoicesUx6A();
        _sourceSession.RestoreRulesWorldApprovalUx6A(approval);
    }

    internal bool PrepareEvidence(string mode)
    {
        var sourceApproval = _sourceSession.CaptureRulesWorldApprovalUx6A();
        Close();
        _sourceSession.RestoreRulesWorldApprovalUx6A(sourceApproval);
        _evidenceReleased = true;
        var source = _sourceSession.Project;
        if (source is null || _sourceSession.History is null || source.Areas.Count == 0) return false;
        _sourceSnapshot = CaptureSourceUx6A();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-a-{Guid.NewGuid():N}");
        var isolated = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(root, source);
            if (!created.Succeeded || !isolated.Open(root).Succeeded)
            {
                isolated.Close();
                _evidenceReleased = StudioSessionController.DeleteUx6ATemporaryWorkspace(root);
                _sourceSnapshot = null;
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _session = isolated;
            Visible = true;
            UpdateBoundsUx6A();
            RefreshChoicesUx6A();
            if (_choices.Count == 0) { CloseEvidence(); return false; }
            _profile.Select(1);
            LoadProfileUx6A();
            ConfigureRulesEvidenceUx6A();
            ConfigureWorldEvidenceUx6A();
            PreviewRulesUx6A();
            PreviewWorldUx6A();
            _tabs.CurrentTab = 2;
            var ready = RulesPreviewReady && WorldPreviewReady && HasExplicitSelection && ShowsFourWorkflows &&
                        ShowsTwoTransactions && ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched;
            if (!ready) { CloseEvidence(); return false; }
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_A_EVIDENCE_FAILED mode={mode} " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            isolated.Close();
            _evidenceReleased = StudioSessionController.DeleteUx6ATemporaryWorkspace(root);
            _evidenceSession = null;
            _evidenceRoot = _evidenceReleased ? null : root;
            _sourceSnapshot = null;
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceUx6A();
        var prepared = PrepareEvidence("ux6-a-rules");
        if (!prepared || !RulesPreviewReady || !WorldPreviewReady || !UsesIsolatedEvidence)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_A_UI_FAILED stage=prepare prepared={prepared} " +
                        $"rules={RulesPreviewReady} world={WorldPreviewReady} isolated={UsesIsolatedEvidence}");
            CloseEvidence();
            return false;
        }
        var isolated = _session;
        var rulesJournalCount = isolated.History?.Journal.Count ?? -1;
        ApplyRulesUx6A();
        var rulesApplied = isolated.History?.Journal.Count == rulesJournalCount + 1 &&
                           isolated.History.Journal.LastOrDefault()?.CommandName == "set_rules_profiles";
        if (!rulesApplied)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_A_UI_FAILED stage=rules_apply applied={_applied} " +
                        $"command={isolated.History?.Journal.LastOrDefault()?.CommandName ?? "none"} " +
                        $"status={_status.Text}");
            CloseEvidence();
            return false;
        }
        ConfigureWorldEvidenceUx6A();
        PreviewWorldUx6A();
        var worldJournalCount = isolated.History?.Journal.Count ?? -1;
        ApplyWorldUx6A();
        var worldApplied = isolated.History?.Journal.Count == worldJournalCount + 1 &&
                           isolated.History.Journal.LastOrDefault()?.CommandName == "set_gameplay_control_profile";
        if (!worldApplied || isolated.Project is null)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_A_UI_FAILED stage=world_apply applied={_applied} " +
                        $"command={isolated.History?.Journal.LastOrDefault()?.CommandName ?? "none"} " +
                        $"project={isolated.Project is not null} status={_status.Text}");
            CloseEvidence();
            return false;
        }
        var finalJson = ProjectJsonSerializer.Serialize(isolated.Project);
        UndoUx6A();
        UndoUx6A();
        var undone = isolated.History?.CanRedo == true;
        RedoUx6A();
        RedoUx6A();
        var redone = isolated.Project is not null && string.Equals(finalJson,
            ProjectJsonSerializer.Serialize(isolated.Project), StringComparison.Ordinal);
        SaveUx6A();
        var reopen = new StudioSessionController();
        var roundTrip = false;
        try
        {
            roundTrip = _evidenceRoot is not null && reopen.Open(_evidenceRoot).Succeeded &&
                        reopen.Project is not null && string.Equals(finalJson,
                            ProjectJsonSerializer.Serialize(reopen.Project), StringComparison.Ordinal);
        }
        finally { reopen.Close(); }
        CloseEvidence();
        var sourceMatches = SourceMatchesUx6A(source);
        var resourcesReleased = EvidenceResourcesReleased;
        var sourceRestored = SourceSessionRestored;
        var succeeded = rulesApplied && worldApplied && undone && redone && roundTrip && sourceMatches &&
                        resourcesReleased && sourceRestored;
        if (!succeeded)
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_A_UI_FAILED stage=final undone={undone} redone={redone} " +
                        $"round_trip={roundTrip} source={sourceMatches} released={resourcesReleased} " +
                        $"restored={sourceRestored}");
        return succeeded;
    }

    private void RefreshChoicesUx6A()
    {
        _updating = true;
        _choices = _session.RulesWorldChoicesUx6A;
        _profile.Clear();
        _profile.AddItem("Choisir le module et la zone…");
        foreach (var choice in _choices) _profile.AddItem(choice.Label);
        _profile.Select(0);
        _updating = false;
        ClearSelectionUx6A();
    }

    private void SelectProfileUx6A(long index)
    {
        if (_updating) return;
        if (index <= 0 || index > _choices.Count) { ClearSelectionUx6A(); return; }
        LoadProfileUx6A();
    }

    private void LoadProfileUx6A()
    {
        if (_profile.Selected <= 0 || _profile.Selected > _choices.Count) return;
        _draft = _session.CreateRulesWorldDraftUx6A(_choices[_profile.Selected - 1].ContextAreaId,
            explicitlySelected: true);
        if (_draft is null) { ClearSelectionUx6A(); return; }
        PopulateUx6A(_draft);
        _selection.Text = "Profil chargé · difficultés, mutateurs, plugins et temps du monde restent indépendants des règles de zone UX2.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetEditingEnabledUx6A(true);
        InvalidateUx6A();
    }

    private void ClearSelectionUx6A()
    {
        _session.CancelUx6AApprovals();
        _draft = null;
        _difficulties.Clear(); _mutators.Clear(); _plugins.Clear(); _schedules.Clear();
        _difficultyIndex = _mutatorIndex = _pluginIndex = _scheduleIndex = -1;
        _selection.Text = "Choisissez le module et sa zone de contexte.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditingEnabledUx6A(false);
        RenderPreviewUx6A(null, "Aucun aperçu frais.");
        UpdateActionsUx6A();
    }

    private void PopulateUx6A(StudioUx6ADraft draft)
    {
        _updating = true;
        _difficulties = draft.Difficulties.ToList();
        _mutators = draft.Mutators.ToList();
        _plugins = draft.PluginPackages.ToList();
        _schedules = draft.Schedules.ToList();
        _activeDifficulty = draft.ActiveDifficultyKey;
        _activePlugin = draft.ActivePluginKey;
        RefreshDifficultyChoicesUx6A(0);
        RefreshMutatorChoicesUx6A(_mutators.Count > 0 ? 0 : -1);
        _pluginsEnabled.ButtonPressed = draft.PluginsEnabled;
        _pluginsRequired.ButtonPressed = draft.PluginsRequiredAtRuntime;
        RefreshPluginChoicesUx6A(_plugins.Count > 0 ? 0 : -1);
        _worldEnabled.ButtonPressed = draft.WorldTimeEnabled;
        _worldRequired.ButtonPressed = draft.WorldTimeRequired;
        _daysPerMonth.Value = draft.DaysPerMonth;
        _monthsPerYear.Value = draft.MonthsPerYear;
        _hoursPerDay.Value = draft.HoursPerDay;
        _minutesPerHour.Value = draft.MinutesPerHour;
        _totalMinutes.Text = draft.InitialTotalMinutes.ToString(CultureInfo.InvariantCulture);
        _timeScale.Value = draft.InitialTimeScale;
        _clockPaused.ButtonPressed = draft.InitiallyPaused;
        _pauseDomain.Text = draft.InitialPauseDomain;
        _pauseDomains.Text = string.Join('\n', draft.PauseDomains);
        BuildAreaScaleEditorsUx6A(draft.AreaScales);
        RefreshScheduleChoicesUx6A(_schedules.Count > 0 ? 0 : -1);
        _updating = false;
        _rulesReady = _worldReady = false;
        _rulesResult = _worldResult = null;
        RenderPreviewUx6A(null, "Profil chargé · choisissez le bloc à prévisualiser.");
        UpdateActionsUx6A();
    }

    private StudioUx6ADraft ReadDraftUx6A()
    {
        if (_draft is null) throw new InvalidOperationException("Aucun profil sélectionné.");
        CommitDifficultyUx6A(); CommitMutatorUx6A(); CommitPluginUx6A(); CommitScheduleUx6A();
        if (!long.TryParse(_totalMinutes.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var totalMinutes) || totalMinutes < 0)
            throw new ArgumentException("La minute totale initiale doit être un entier positif.");
        var scales = _scaleEditors.Select(item => item.Source with
        {
            Enabled = item.Enabled.ButtonPressed,
            Scale = (float)item.Scale.Value,
        }).ToArray();
        return _draft with
        {
            Difficulties = _difficulties.ToArray(),
            ActiveDifficultyKey = _activeDifficulty,
            Mutators = _mutators.ToArray(),
            PluginsEnabled = _pluginsEnabled.ButtonPressed,
            PluginsRequiredAtRuntime = _pluginsRequired.ButtonPressed,
            ActivePluginKey = _activePlugin,
            PluginPackages = _plugins.ToArray(),
            WorldTimeEnabled = _worldEnabled.ButtonPressed,
            WorldTimeRequired = _worldRequired.ButtonPressed,
            DaysPerMonth = (int)_daysPerMonth.Value,
            MonthsPerYear = (int)_monthsPerYear.Value,
            HoursPerDay = (int)_hoursPerDay.Value,
            MinutesPerHour = (int)_minutesPerHour.Value,
            InitialTotalMinutes = totalMinutes,
            InitialTimeScale = (float)_timeScale.Value,
            InitiallyPaused = _clockPaused.ButtonPressed,
            InitialPauseDomain = _pauseDomain.Text,
            AreaScales = scales,
            PauseDomains = LinesUx6A(_pauseDomains.Text),
            Schedules = _schedules.ToArray(),
        };
    }

    private void PreviewRulesUx6A()
    {
        if (!TryReadDraftUx6A(out var draft)) return;
        var result = _session.PreviewRulesUx6A(draft!);
        _rulesResult = result;
        _rulesReady = result.Succeeded && result.CanonicalDryRun;
        RenderPreviewUx6A(result, "Aperçu règles");
        SetStatusUx6A(result.Message, !result.Succeeded);
        UpdateActionsUx6A();
    }

    private void PreviewWorldUx6A()
    {
        if (!TryReadDraftUx6A(out var draft)) return;
        var result = _session.PreviewWorldTimeUx6A(draft!);
        _worldResult = result;
        _worldReady = result.Succeeded && result.CanonicalDryRun;
        RenderPreviewUx6A(result, "Aperçu temps du monde");
        SetStatusUx6A(result.Message, !result.Succeeded);
        UpdateActionsUx6A();
    }

    private bool TryReadDraftUx6A(out StudioUx6ADraft? draft)
    {
        try { draft = ReadDraftUx6A(); return true; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                              FormatException)
        {
            draft = null;
            _rulesReady = _worldReady = false;
            _session.CancelUx6AApprovals();
            var local = new StudioUx6ABlockPreview(false, false, exception.Message, string.Empty, [],
                [new StudioUx6ADiagnostic("studio.rules.form_invalid", "rules", exception.Message)], [], false, true);
            RenderPreviewUx6A(local, "Brouillon invalide");
            SetStatusUx6A(exception.Message, true);
            UpdateActionsUx6A();
            return false;
        }
    }

    private void ApplyRulesUx6A()
    {
        var result = _session.ApplyApprovedRulesUx6A();
        SetStatusUx6A(result.Message, !result.Succeeded);
        if (!result.Succeeded) { InvalidateUx6A(); return; }
        _applied = true;
        ReloadSelectedUx6A();
        RulesWorldChanged?.Invoke();
    }

    private void ApplyWorldUx6A()
    {
        var result = _session.ApplyApprovedWorldTimeUx6A();
        SetStatusUx6A(result.Message, !result.Succeeded);
        if (!result.Succeeded) { InvalidateUx6A(); return; }
        _applied = true;
        ReloadSelectedUx6A();
        RulesWorldChanged?.Invoke();
    }

    private void UndoUx6A()
    {
        var result = _session.Undo();
        SetStatusUx6A(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _applied = false;
        ReloadSelectedUx6A();
        RulesWorldChanged?.Invoke();
    }

    private void RedoUx6A()
    {
        var result = _session.Redo();
        SetStatusUx6A(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _applied = true;
        ReloadSelectedUx6A();
        RulesWorldChanged?.Invoke();
    }

    private void SaveUx6A()
    {
        var result = _session.Save();
        SetStatusUx6A(result.Message, !result.Succeeded);
        if (result.Succeeded) _summary.Text += "\nProjet enregistré · la réouverture relit les deux agrégats canoniques.";
        UpdateActionsUx6A();
    }

    private void ReloadSelectedUx6A()
    {
        if (_draft is null) return;
        var context = _draft.ContextAreaId;
        _draft = _session.CreateRulesWorldDraftUx6A(context, explicitlySelected: true);
        if (_draft is not null) PopulateUx6A(_draft);
    }

    private void ConfigureRulesEvidenceUx6A()
    {
        if (_difficulties.Count > 0)
        {
            var item = _difficulties[0];
            _difficulties[0] = item with
            {
                DisplayName = item.DisplayName + " · preuve",
                PlayerDamageMultiplier = item.PlayerDamageMultiplier >= 19.9
                    ? item.PlayerDamageMultiplier - .1 : item.PlayerDamageMultiplier + .1,
            };
            RefreshDifficultyChoicesUx6A(0);
        }
        if (_mutators.Count > 0)
        {
            _mutators[0] = _mutators[0] with { Enabled = !_mutators[0].Enabled };
            RefreshMutatorChoicesUx6A(0);
        }
        if (_plugins.Count > 0)
        {
            _plugins[0] = _plugins[0] with { Version = _plugins[0].Version + "-proof" };
            RefreshPluginChoicesUx6A(0);
        }
        InvalidateUx6A();
    }

    private void ConfigureWorldEvidenceUx6A()
    {
        if (_draft is null) return;
        _timeScale.Value = Math.Min(999, _timeScale.Value + .25);
        if (_scaleEditors.Count > 0)
        {
            _scaleEditors[0].Enabled.ButtonPressed = true;
            _scaleEditors[0].Scale.Value = Math.Min(999, _scaleEditors[0].Scale.Value + .5);
        }
        InvalidateUx6A();
    }

    private void CommitDifficultyUx6A()
    {
        if (_updating || _difficultyIndex < 0 || _difficultyIndex >= _difficulties.Count) return;
        var source = _difficulties[_difficultyIndex];
        _difficulties[_difficultyIndex] = source with
        {
            Key = _difficultyKey.Text,
            DisplayName = _difficultyName.Text,
            PlayerDamageMultiplier = _difficultyMultipliers[0].Value,
            IncomingDamageMultiplier = _difficultyMultipliers[1].Value,
            AiIntensityMultiplier = _difficultyMultipliers[2].Value,
            EconomyMultiplier = _difficultyMultipliers[3].Value,
            RestRecoveryMultiplier = _difficultyMultipliers[4].Value,
            SaveRestrictions = (SaveRestrictionPolicy)_saveRestrictions.Selected,
            FriendlyFire = (FriendlyFireMode)_friendlyFire.Selected,
            PauseAssist = _pauseAssist.ButtonPressed,
            TelegraphAssist = _telegraphAssist.ButtonPressed,
            TimingWindowMultiplier = _timingAssist.Value,
            PerceptionCueMultiplier = _perceptionAssist.Value,
        };
        if (_difficultyActive.ButtonPressed) _activeDifficulty = _difficultyKey.Text.Trim().ToLowerInvariant();
    }

    private void SelectDifficultyUx6A(long index)
    {
        if (_updating) return;
        CommitDifficultyUx6A();
        LoadDifficultyUx6A((int)index);
        InvalidateUx6A();
    }

    private void RefreshDifficultyChoicesUx6A(int selected)
    {
        _difficultyChoice.Clear();
        foreach (var item in _difficulties) _difficultyChoice.AddItem(HumanValueUx6A(item.DisplayName));
        if (_difficulties.Count == 0) { _difficultyIndex = -1; return; }
        _difficultyChoice.Select(Math.Clamp(selected, 0, _difficulties.Count - 1));
        LoadDifficultyUx6A(_difficultyChoice.Selected);
    }

    private void LoadDifficultyUx6A(int index)
    {
        if (index < 0 || index >= _difficulties.Count) return;
        _updating = true;
        _difficultyIndex = index;
        var item = _difficulties[index];
        _difficultyKey.Text = item.Key;
        _difficultyName.Text = item.DisplayName;
        _difficultyMultipliers[0].Value = item.PlayerDamageMultiplier;
        _difficultyMultipliers[1].Value = item.IncomingDamageMultiplier;
        _difficultyMultipliers[2].Value = item.AiIntensityMultiplier;
        _difficultyMultipliers[3].Value = item.EconomyMultiplier;
        _difficultyMultipliers[4].Value = item.RestRecoveryMultiplier;
        _saveRestrictions.Select((int)item.SaveRestrictions);
        _friendlyFire.Select((int)item.FriendlyFire);
        _pauseAssist.ButtonPressed = item.PauseAssist;
        _telegraphAssist.ButtonPressed = item.TelegraphAssist;
        _timingAssist.Value = item.TimingWindowMultiplier;
        _perceptionAssist.Value = item.PerceptionCueMultiplier;
        _difficultyActive.ButtonPressed = string.Equals(item.Key, _activeDifficulty, StringComparison.Ordinal);
        _removeDifficulty.Disabled = _difficulties.Count <= 1;
        _updating = false;
    }

    private void AddDifficultyUx6A()
    {
        CommitDifficultyUx6A();
        var count = _difficulties.Count + 1;
        _difficulties.Add(new StudioUx6ADifficultyDraft(Guid.NewGuid(), 1, $"profile-{count}",
            $"Profil {count}", 1, 1, 1, 1, 1, SaveRestrictionPolicy.Unrestricted,
            FriendlyFireMode.Disabled, true, true, 1, 1, "studio:ux6-a"));
        RefreshDifficultyChoicesUx6A(_difficulties.Count - 1);
        InvalidateUx6A();
    }

    private void RemoveDifficultyUx6A()
    {
        if (_difficulties.Count <= 1 || _difficultyIndex < 0) return;
        var removed = _difficulties[_difficultyIndex];
        _difficulties.RemoveAt(_difficultyIndex);
        if (string.Equals(_activeDifficulty, removed.Key, StringComparison.Ordinal))
            _activeDifficulty = _difficulties[0].Key;
        RefreshDifficultyChoicesUx6A(Math.Min(_difficultyIndex, _difficulties.Count - 1));
        InvalidateUx6A();
    }

    private void CommitMutatorUx6A()
    {
        if (_updating || _mutatorIndex < 0 || _mutatorIndex >= _mutators.Count) return;
        _mutators[_mutatorIndex] = _mutators[_mutatorIndex] with
        {
            Key = _mutatorKey.Text,
            DisplayName = _mutatorName.Text,
            Enabled = _mutatorEnabled.ButtonPressed,
            ScalarOverrides = ParseScalarsUx6A(_mutatorScalars.Text),
            Tags = LinesUx6A(_mutatorTags.Text),
        };
    }

    private void SelectMutatorUx6A(long index)
    {
        if (_updating) return;
        CommitMutatorUx6A(); LoadMutatorUx6A((int)index); InvalidateUx6A();
    }

    private void RefreshMutatorChoicesUx6A(int selected)
    {
        _mutatorChoice.Clear();
        foreach (var item in _mutators) _mutatorChoice.AddItem(HumanValueUx6A(item.DisplayName));
        if (_mutators.Count == 0) { _mutatorIndex = -1; ClearMutatorUx6A(); return; }
        _mutatorChoice.Select(Math.Clamp(selected, 0, _mutators.Count - 1));
        LoadMutatorUx6A(_mutatorChoice.Selected);
    }

    private void LoadMutatorUx6A(int index)
    {
        if (index < 0 || index >= _mutators.Count) return;
        _updating = true;
        _mutatorIndex = index;
        var item = _mutators[index];
        _mutatorKey.Text = item.Key; _mutatorName.Text = item.DisplayName;
        _mutatorEnabled.ButtonPressed = item.Enabled;
        _mutatorScalars.Text = string.Join('\n', item.ScalarOverrides.OrderBy(entry => entry.Key)
            .Select(entry => $"{entry.Key}={entry.Value.ToString("R", CultureInfo.InvariantCulture)}"));
        _mutatorTags.Text = string.Join('\n', item.Tags);
        _removeMutator.Disabled = false;
        _updating = false;
    }

    private void ClearMutatorUx6A()
    {
        _updating = true;
        _mutatorKey.Text = _mutatorName.Text = _mutatorScalars.Text = _mutatorTags.Text = string.Empty;
        _mutatorEnabled.ButtonPressed = false; _removeMutator.Disabled = true;
        _updating = false;
    }

    private void AddMutatorUx6A()
    {
        CommitMutatorUx6A();
        var count = _mutators.Count + 1;
        _mutators.Add(new StudioUx6AMutatorDraft(Guid.NewGuid(), $"mutator-{count}", $"Mutateur {count}",
            false, new Dictionary<string, double> { ["incomingDamage"] = 1 }, []));
        RefreshMutatorChoicesUx6A(_mutators.Count - 1); InvalidateUx6A();
    }

    private void RemoveMutatorUx6A()
    {
        if (_mutatorIndex < 0) return;
        _mutators.RemoveAt(_mutatorIndex);
        RefreshMutatorChoicesUx6A(Math.Min(_mutatorIndex, _mutators.Count - 1)); InvalidateUx6A();
    }

    private void CommitPluginUx6A()
    {
        if (_updating || _pluginIndex < 0 || _pluginIndex >= _plugins.Count) return;
        _plugins[_pluginIndex] = _plugins[_pluginIndex] with
        {
            Key = _pluginKey.Text,
            Version = _pluginVersion.Text,
            ContentPackageId = _pluginContent.Text,
            Attributes = LinesUx6A(_pluginRegistries[0].Text),
            Effects = LinesUx6A(_pluginRegistries[1].Text),
            Abilities = LinesUx6A(_pluginRegistries[2].Text),
            Inventories = LinesUx6A(_pluginRegistries[3].Text),
            ProgressionTracks = LinesUx6A(_pluginRegistries[4].Text),
            UiPanels = LinesUx6A(_pluginRegistries[5].Text),
        };
        if (_pluginActive.ButtonPressed) _activePlugin = _pluginKey.Text.Trim().ToLowerInvariant();
    }

    private void SelectPluginUx6A(long index)
    {
        if (_updating) return;
        CommitPluginUx6A(); LoadPluginUx6A((int)index); InvalidateUx6A();
    }

    private void RefreshPluginChoicesUx6A(int selected)
    {
        _pluginChoice.Clear();
        foreach (var item in _plugins) _pluginChoice.AddItem(HumanValueUx6A(item.Key));
        if (_plugins.Count == 0) { _pluginIndex = -1; ClearPluginUx6A(); return; }
        _pluginChoice.Select(Math.Clamp(selected, 0, _plugins.Count - 1));
        LoadPluginUx6A(_pluginChoice.Selected);
    }

    private void LoadPluginUx6A(int index)
    {
        if (index < 0 || index >= _plugins.Count) return;
        _updating = true;
        _pluginIndex = index;
        var item = _plugins[index];
        _pluginKey.Text = item.Key; _pluginVersion.Text = item.Version; _pluginContent.Text = item.ContentPackageId;
        _pluginActive.ButtonPressed = string.Equals(item.Key, _activePlugin, StringComparison.Ordinal);
        var values = new[] { item.Attributes, item.Effects, item.Abilities, item.Inventories,
            item.ProgressionTracks, item.UiPanels };
        for (var i = 0; i < values.Length; i++) _pluginRegistries[i].Text = string.Join('\n', values[i]);
        _removePlugin.Disabled = false;
        _updating = false;
    }

    private void ClearPluginUx6A()
    {
        _updating = true;
        _pluginKey.Text = _pluginVersion.Text = _pluginContent.Text = string.Empty;
        _pluginActive.ButtonPressed = false;
        foreach (var registry in _pluginRegistries) registry.Text = string.Empty;
        _removePlugin.Disabled = true;
        _updating = false;
    }

    private void AddPluginUx6A()
    {
        CommitPluginUx6A();
        var count = _plugins.Count + 1;
        var key = $"forge.package-{count}";
        _plugins.Add(new StudioUx6APluginPackageDraft(Guid.NewGuid(), 1, key, "1.0.0",
            $"content.package-{count}", ["attribute"], ["effect"], ["ability"], ["inventory"],
            ["progression"], ["panel"], "studio:ux6-a"));
        if (_plugins.Count == 1) _activePlugin = key;
        RefreshPluginChoicesUx6A(_plugins.Count - 1); InvalidateUx6A();
    }

    private void RemovePluginUx6A()
    {
        if (_pluginIndex < 0) return;
        var removed = _plugins[_pluginIndex];
        _plugins.RemoveAt(_pluginIndex);
        if (string.Equals(_activePlugin, removed.Key, StringComparison.Ordinal))
            _activePlugin = _plugins.FirstOrDefault()?.Key ?? string.Empty;
        RefreshPluginChoicesUx6A(Math.Min(_pluginIndex, _plugins.Count - 1)); InvalidateUx6A();
    }

    private void CommitScheduleUx6A()
    {
        if (_updating || _scheduleIndex < 0 || _scheduleIndex >= _schedules.Count) return;
        _schedules[_scheduleIndex] = _schedules[_scheduleIndex] with
        {
            Key = _scheduleKey.Text,
            MinuteOfDay = (int)_scheduleMinute.Value,
            DaysOfMonth = ParseDaysUx6A(_scheduleDays.Text),
            EventName = _scheduleEvent.Text,
            PauseDomain = _scheduleDomain.Text,
            ServerAuthoritative = _scheduleAuthority.ButtonPressed,
        };
    }

    private void SelectScheduleUx6A(long index)
    {
        if (_updating) return;
        CommitScheduleUx6A(); LoadScheduleUx6A((int)index); InvalidateUx6A();
    }

    private void RefreshScheduleChoicesUx6A(int selected)
    {
        _scheduleChoice.Clear();
        foreach (var item in _schedules) _scheduleChoice.AddItem(HumanValueUx6A(item.Key));
        if (_schedules.Count == 0) { _scheduleIndex = -1; ClearScheduleUx6A(); return; }
        _scheduleChoice.Select(Math.Clamp(selected, 0, _schedules.Count - 1));
        LoadScheduleUx6A(_scheduleChoice.Selected);
    }

    private void LoadScheduleUx6A(int index)
    {
        if (index < 0 || index >= _schedules.Count) return;
        _updating = true;
        _scheduleIndex = index;
        var item = _schedules[index];
        _scheduleKey.Text = item.Key; _scheduleMinute.Value = item.MinuteOfDay;
        _scheduleDays.Text = string.Join(',', item.DaysOfMonth);
        _scheduleEvent.Text = item.EventName; _scheduleDomain.Text = item.PauseDomain;
        _scheduleAuthority.ButtonPressed = item.ServerAuthoritative;
        _removeSchedule.Disabled = false;
        _updating = false;
    }

    private void ClearScheduleUx6A()
    {
        _updating = true;
        _scheduleKey.Text = _scheduleDays.Text = _scheduleEvent.Text = _scheduleDomain.Text = string.Empty;
        _scheduleMinute.Value = 0; _scheduleAuthority.ButtonPressed = false; _removeSchedule.Disabled = true;
        _updating = false;
    }

    private void AddScheduleUx6A()
    {
        CommitScheduleUx6A();
        var count = _schedules.Count + 1;
        _schedules.Add(new StudioUx6AScheduleDraft(Guid.NewGuid(), 1, "studio:ux6-a", $"schedule-{count}",
            0, [1], $"world.schedule-{count}", LinesUx6A(_pauseDomains.Text).FirstOrDefault() ?? "world", true));
        RefreshScheduleChoicesUx6A(_schedules.Count - 1); InvalidateUx6A();
    }

    private void RemoveScheduleUx6A()
    {
        if (_scheduleIndex < 0) return;
        _schedules.RemoveAt(_scheduleIndex);
        RefreshScheduleChoicesUx6A(Math.Min(_scheduleIndex, _schedules.Count - 1)); InvalidateUx6A();
    }

    private void BuildAreaScaleEditorsUx6A(IReadOnlyList<StudioUx6AAreaScaleDraft> values)
    {
        foreach (var child in _areaScales.GetChildren()) child.QueueFree();
        _scaleEditors.Clear();
        foreach (var value in values)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var enabled = StudioUx2EnvBControls.Check(value.Protected
                ? $"{value.Label} · conservation obligatoire" : value.Label);
            enabled.ButtonPressed = value.Enabled;
            enabled.Disabled = value.Protected;
            var scale = NumberUx6A(0, 1000, .05);
            scale.Value = value.Scale;
            scale.Editable = !value.Protected;
            row.AddChild(enabled);
            var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(spacer);
            row.AddChild(StudioUx2EnvBControls.Field("Échelle", scale, 150));
            _areaScales.AddChild(row);
            enabled.Toggled += _ => InvalidateUx6A();
            scale.ValueChanged += _ => InvalidateUx6A();
            _scaleEditors.Add((value, enabled, scale));
        }
    }

    private void WireChangesUx6A()
    {
        foreach (var control in new[] { _difficultyKey, _difficultyName, _mutatorKey, _mutatorName,
                     _pluginKey, _pluginVersion, _pluginContent, _totalMinutes, _pauseDomain,
                     _scheduleKey, _scheduleDays, _scheduleEvent, _scheduleDomain })
            control.TextChanged += _ => InvalidateUx6A();
        foreach (var control in _difficultyMultipliers.Concat(new[] { _timingAssist, _perceptionAssist,
                     _daysPerMonth, _monthsPerYear, _hoursPerDay, _minutesPerHour, _timeScale, _scheduleMinute }))
            control.ValueChanged += _ => InvalidateUx6A();
        foreach (var control in new[] { _difficultyActive, _pauseAssist, _telegraphAssist, _mutatorEnabled,
                     _pluginsEnabled, _pluginsRequired, _pluginActive, _worldEnabled, _worldRequired,
                     _clockPaused, _scheduleAuthority })
            control.Toggled += _ => InvalidateUx6A();
        _saveRestrictions.ItemSelected += _ => InvalidateUx6A();
        _friendlyFire.ItemSelected += _ => InvalidateUx6A();
        _mutatorScalars.TextChanged += InvalidateUx6A;
        _mutatorTags.TextChanged += InvalidateUx6A;
        foreach (var registry in _pluginRegistries) registry.TextChanged += InvalidateUx6A;
        _pauseDomains.TextChanged += InvalidateUx6A;
    }

    private void InvalidateUx6A()
    {
        if (_updating || _draft is null) return;
        _session.CancelUx6AApprovals();
        _rulesReady = _worldReady = false;
        _rulesResult = _worldResult = null;
        _applied = false;
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec du bloc concerné.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Warning);
        UpdateActionsUx6A();
    }

    private void SetEditingEnabledUx6A(bool enabled)
    {
        foreach (var edit in new[] { _difficultyKey, _difficultyName, _mutatorKey, _mutatorName,
                     _pluginKey, _pluginVersion, _pluginContent, _totalMinutes, _pauseDomain,
                     _scheduleKey, _scheduleDays, _scheduleEvent, _scheduleDomain }) edit.Editable = enabled;
        foreach (var edit in new[] { _mutatorScalars, _mutatorTags, _pauseDomains }.Concat(_pluginRegistries))
            edit.Editable = enabled;
        foreach (var spin in _difficultyMultipliers.Concat(new[] { _timingAssist, _perceptionAssist,
                     _daysPerMonth, _monthsPerYear, _hoursPerDay, _minutesPerHour, _timeScale, _scheduleMinute }))
            spin.Editable = enabled;
        foreach (var option in new[] { _difficultyChoice, _saveRestrictions, _friendlyFire, _mutatorChoice,
                     _pluginChoice, _scheduleChoice }) option.Disabled = !enabled;
        foreach (var check in new[] { _difficultyActive, _pauseAssist, _telegraphAssist, _mutatorEnabled,
                     _pluginsEnabled, _pluginsRequired, _pluginActive, _worldEnabled, _worldRequired,
                     _clockPaused, _scheduleAuthority }) check.Disabled = !enabled;
        foreach (var button in new[] { _addDifficulty, _removeDifficulty, _addMutator, _removeMutator,
                     _addPlugin, _removePlugin, _addSchedule, _removeSchedule }) button.Disabled = !enabled;
        _previewRules.Disabled = !enabled; _previewWorld.Disabled = !enabled;
    }

    private void UpdateActionsUx6A()
    {
        _previewRules.Disabled = _draft is null;
        _previewWorld.Disabled = _draft is null;
        _applyRules.Disabled = !_rulesReady || _rulesResult is not { Succeeded: true, CanApply: true };
        _applyWorld.Disabled = !_worldReady || _worldResult is not { Succeeded: true, CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
    }

    private void RenderPreviewUx6A(StudioUx6ABlockPreview? preview, string title)
    {
        ClearChildrenUx6A(_diff); ClearChildrenUx6A(_runtime); ClearChildrenUx6A(_diagnostics);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label(title, 10, StudioStyle.Muted, wrap: true));
            _runtime.AddChild(StudioStyle.Label("Les calculs déterministes apparaîtront ici.", 10,
                StudioStyle.Muted, wrap: true));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic frais.", 10, StudioStyle.Muted));
            return;
        }
        if (preview.Diff.Count == 0) _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10,
            StudioStyle.Muted, wrap: true));
        foreach (var item in preview.Diff) _diff.AddChild(StudioStyle.Label("• " + item, 10,
            StudioStyle.Text, wrap: true));
        foreach (var item in preview.RuntimePreview) _runtime.AddChild(StudioStyle.Label("• " + item, 10,
            StudioStyle.Text, wrap: true));
        if (preview.Diagnostics.Count == 0) _diagnostics.AddChild(StudioStyle.Label("Conforme · dry-run canonique",
            10, StudioStyle.Success, wrap: true));
        foreach (var item in preview.Diagnostics) _diagnostics.AddChild(StudioStyle.Label("• " + item.Message,
            10, item.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        _summary.Text = title + " · " + preview.Message + "\n" + preview.PatchSummary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
    }

    private StudioUx6AEvidenceSourceSnapshot CaptureSourceUx6A()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new StudioUx6AEvidenceSourceSnapshot(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.CaptureRulesWorldApprovalUx6A());
    }

    private bool SourceMatchesUx6A(StudioUx6AEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureRulesWorldApprovalUx6A() == snapshot.Approval;
    }

    private bool ReleaseEvidenceUx6A()
    {
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sessionReleased = true;
        try { isolated?.Close(); }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_A_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx6ATemporaryWorkspace(root);
        _evidenceSession = null;
        _evidenceRoot = rootReleased ? null : root;
        _sourceSnapshot = null;
        _session = _sourceSession;
        _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void UpdateBoundsUx6A()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.68f : viewport.X >= 2_300 ? 1.22f : 1f;
        var width = Math.Clamp((viewport.X - 48f) / scale, 1_180f, 1_650f);
        var height = Math.Clamp((viewport.Y - 38f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(16f, (viewport.Y - height * scale) * .46f));
    }

    private VBoxContainer ScrollTabUx6A(string title)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(1_060, 0),
        };
        content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(content);
        _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private static GridContainer GridUx6A(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 9);
        grid.AddThemeConstantOverride("v_separation", 7);
        return grid;
    }

    private static SpinBox NumberUx6A(double minimum, double maximum, double step, bool allowGreater = false) =>
        StudioUx2EnvBControls.Number(minimum, maximum, step, allowGreater: allowGreater);

    private static VBoxContainer ReviewColumnUx6A(HBoxContainer host, string title)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 0),
        };
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Accent));
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body); column.AddChild(scroll); host.AddChild(column);
        return body;
    }

    private static IReadOnlyDictionary<string, double> ParseScalarsUx6A(string text)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var line in LinesUx6A(text))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0 || !double.TryParse(line[(separator + 1)..].Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var value) || !values.TryAdd(line[..separator].Trim(), value))
                throw new ArgumentException("Chaque scalaire exige une clé unique et une valeur décimale avec un point.");
        }
        return values;
    }

    private static IReadOnlyList<int> ParseDaysUx6A(string text)
    {
        var values = text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)
                ? day : throw new ArgumentException("Les jours d’un horaire doivent être des entiers séparés par des virgules."))
            .Distinct().Order().ToArray();
        if (values.Length == 0) throw new ArgumentException("Un horaire exige au moins un jour du mois.");
        return values;
    }

    private static IReadOnlyList<string> LinesUx6A(string text) => (text ?? string.Empty)
        .Replace("\r", string.Empty).Split('\n').Select(item => item.Trim())
        .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    private static string HumanValueUx6A(string value) => string.IsNullOrWhiteSpace(value)
        ? "Sans nom"
        : string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static void ClearChildrenUx6A(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private void SetStatusUx6A(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static IEnumerable<string> DescendantTextUx6A(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var nested in DescendantTextUx6A(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx6A(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6A();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close(); GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) Close();
    }
}
