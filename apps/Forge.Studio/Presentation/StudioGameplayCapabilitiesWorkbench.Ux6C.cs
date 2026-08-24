using Forge.Domain.Gameplay;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioGameplayCapabilitiesWorkbenchUx6C : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6CEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profile;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly CheckBox _swimEnabled;
    private readonly CheckBox _swimRequired;
    private readonly SpinBox _buoyancy;
    private readonly SpinBox _surfaceSnap;
    private readonly SpinBox _surfaceSpeed;
    private readonly SpinBox _diveSpeed;
    private readonly SpinBox _currentInfluence;
    private readonly SpinBox _breath;
    private readonly TextEdit _waterTags;
    private readonly LineEdit _surfaceAnimation;
    private readonly LineEdit _diveAnimation;
    private readonly LineEdit _idleAnimation;

    private readonly CheckBox _climbEnabled;
    private readonly CheckBox _climbRequired;
    private readonly SpinBox _climbSpeed;
    private readonly SpinBox _stamina;
    private readonly SpinBox _staminaDrain;
    private readonly SpinBox _ledgeReach;
    private readonly CheckBox _inverseKinematics;
    private readonly CheckBox _authoredLinks;
    private readonly CheckBox _safeDrop;
    private readonly TextEdit _surfaceTags;
    private readonly LineEdit _climbAnimation;
    private readonly LineEdit _ledgeAnimation;

    private readonly CheckBox _radialEnabled;
    private readonly LineEdit _radialInput;
    private readonly SpinBox _deadZone;
    private readonly SpinBox _maximumDepth;
    private readonly CheckBox _gamepad;
    private readonly CheckBox _keyboard;
    private readonly CheckBox _listFallback;
    private readonly CheckBox _screenReader;
    private readonly OptionButton _radialActionChoice;
    private readonly LineEdit _actionKey;
    private readonly LineEdit _actionLabel;
    private readonly LineEdit _actionAccessible;
    private readonly OptionButton _actionKind;
    private readonly LineEdit _actionCommand;
    private readonly LineEdit _actionParent;
    private readonly SpinBox _actionOrder;
    private readonly CheckBox _actionTarget;
    private readonly TextEdit _actionTags;

    private readonly CheckBox _pauseEnabled;
    private readonly CheckBox _slowEnabled;
    private readonly SpinBox _slowScale;
    private readonly CheckBox _planningEnabled;
    private readonly SpinBox _plannedActions;
    private readonly CheckBox _turnEnabled;
    private readonly OptionButton _multiplayerPolicy;
    private readonly SpinBox _voteFraction;
    private readonly TextEdit _pausePermissions;

    private readonly OptionButton _defaultProvider;
    private readonly OptionButton _providerChoice;
    private readonly LineEdit _providerKey;
    private readonly LineEdit _providerName;
    private readonly OptionButton _providerKind;
    private readonly LineEdit _customProvider;
    private readonly TextEdit _eventContracts;
    private readonly CheckBox _serverAuthority;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx6CProfileChoice> _choices = [];
    private StudioUx6CDraft? _draft;
    private List<StudioUx6CRadialActionDraft> _actions = [];
    private List<StudioUx6CCombatProviderDraft> _providers = [];
    private int _actionIndex = -1;
    private int _providerIndex = -1;
    private bool _updating;
    private bool _previewReady;
    private string? _previewFingerprint;
    private StudioUx6CPreview? _previewResult;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? GameplayCapabilitiesChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _draft is not null && _profile.Selected > 0;
    internal bool PreviewReady => _previewReady && _previewResult is
    { Succeeded: true, CanonicalDryRun: true, RuntimeClaimsBounded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6C();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsFiveWorkflows => _tabs.GetTabCount() == 5 &&
        Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle).SequenceEqual(
            new[] { "Nage", "Escalade", "Menu radial", "Temps tactique", "Combat" });
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6C(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Capacités gameplay", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   text.Contains("Nage", StringComparison.Ordinal) &&
                   text.Contains("Escalade", StringComparison.Ordinal) &&
                   text.Contains("Menu radial", StringComparison.Ordinal) &&
                   text.Contains("Temps tactique", StringComparison.Ordinal) &&
                   text.Contains("Combat", StringComparison.Ordinal) && !ContainsUuidUx6C(text);
        }
    }

    internal StudioGameplayCapabilitiesWorkbenchUx6C(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6CGameplayCapabilitiesDialog", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("Capacités gameplay", 18));
        headingText.AddChild(StudioStyle.Label(
            "Nage, escalade, roue d’actions, temps tactique et combat · aperçu sec canonique · aucune exécution moteur.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _profile = StudioUx2EnvBControls.Options();
        _profile.ItemSelected += SelectProfileUx6C;
        root.AddChild(FieldUx6C("Module et zone de contexte · sélection explicite", _profile, 620));
        _selection = StudioStyle.Label("Choisissez le module et sa zone de contexte.", 10,
            StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 320),
            CustomMaximumSize = new Vector2(-1, 600),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var swimTab = ScrollTabUx6C("Nage");
        var swimFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _swimEnabled = StudioUx2EnvBControls.Check("Nage activée");
        _swimRequired = StudioUx2EnvBControls.Check("Profil requis au runtime");
        swimFlags.AddChild(_swimEnabled); swimFlags.AddChild(_swimRequired); swimTab.AddChild(swimFlags);
        var swimNumbers = GridUx6C(4);
        _buoyancy = NumberUx6C(0, 50, .05); _surfaceSnap = NumberUx6C(0, 10, .05);
        _surfaceSpeed = NumberUx6C(.001, 200, .05); _diveSpeed = NumberUx6C(.001, 200, .05);
        _currentInfluence = NumberUx6C(0, 5, .05); _breath = NumberUx6C(.001, 3600, .1);
        swimNumbers.AddChild(FieldUx6C("Flottabilité", _buoyancy));
        swimNumbers.AddChild(FieldUx6C("Accroche surface (m)", _surfaceSnap));
        swimNumbers.AddChild(FieldUx6C("Vitesse surface", _surfaceSpeed));
        swimNumbers.AddChild(FieldUx6C("Vitesse plongée", _diveSpeed));
        swimNumbers.AddChild(FieldUx6C("Influence du courant", _currentInfluence));
        swimNumbers.AddChild(FieldUx6C("Souffle (s)", _breath));
        swimTab.AddChild(StudioUx2EnvBControls.Card("Ux6CSwimMotion", "Mouvement aquatique",
            "Le résolveur calcule vitesse, flottabilité et souffle; aucun volume d’eau n’est instancié.", swimNumbers));
        _waterTags = StudioUx2EnvBControls.Lines("Un tag de volume par ligne", 74);
        swimTab.AddChild(FieldUx6C("Volumes reconnus", _waterTags));
        var swimAnimations = GridUx6C(3);
        _surfaceAnimation = StudioUx2EnvBControls.Text("swim.surface");
        _diveAnimation = StudioUx2EnvBControls.Text("swim.dive");
        _idleAnimation = StudioUx2EnvBControls.Text("swim.idle");
        swimAnimations.AddChild(FieldUx6C("Animation surface", _surfaceAnimation));
        swimAnimations.AddChild(FieldUx6C("Animation plongée", _diveAnimation));
        swimAnimations.AddChild(FieldUx6C("Animation repos", _idleAnimation));
        swimTab.AddChild(swimAnimations);

        var climbTab = ScrollTabUx6C("Escalade");
        var climbFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _climbEnabled = StudioUx2EnvBControls.Check("Escalade activée");
        _climbRequired = StudioUx2EnvBControls.Check("Profil requis");
        _inverseKinematics = StudioUx2EnvBControls.Check("Cinématique inverse configurée");
        _authoredLinks = StudioUx2EnvBControls.Check("Liens authorés requis");
        _safeDrop = StudioUx2EnvBControls.Check("Bloquer les chutes dangereuses");
        climbFlags.AddChild(_climbEnabled); climbFlags.AddChild(_climbRequired);
        climbFlags.AddChild(_inverseKinematics); climbFlags.AddChild(_authoredLinks);
        climbFlags.AddChild(_safeDrop); climbTab.AddChild(climbFlags);
        var climbNumbers = GridUx6C(4);
        _climbSpeed = NumberUx6C(.001, 200, .05); _stamina = NumberUx6C(.001, 10000, .1);
        _staminaDrain = NumberUx6C(.001, 10000, .1); _ledgeReach = NumberUx6C(.001, 5, .05);
        climbNumbers.AddChild(FieldUx6C("Vitesse", _climbSpeed));
        climbNumbers.AddChild(FieldUx6C("Endurance", _stamina));
        climbNumbers.AddChild(FieldUx6C("Drain par seconde", _staminaDrain));
        climbNumbers.AddChild(FieldUx6C("Portée de rebord", _ledgeReach));
        climbTab.AddChild(StudioUx2EnvBControls.Card("Ux6CClimbMotion", "Mouvement vertical",
            "Le résolveur consomme l’endurance; aucun lien, IK ou acteur n’est exécuté dans Studio.", climbNumbers));
        _surfaceTags = StudioUx2EnvBControls.Lines("Un tag de surface par ligne", 74);
        climbTab.AddChild(FieldUx6C("Surfaces autorisées", _surfaceTags));
        var climbAnimations = GridUx6C(2);
        _climbAnimation = StudioUx2EnvBControls.Text("climb.loop");
        _ledgeAnimation = StudioUx2EnvBControls.Text("climb.ledge");
        climbAnimations.AddChild(FieldUx6C("Animation escalade", _climbAnimation));
        climbAnimations.AddChild(FieldUx6C("Animation rebord", _ledgeAnimation));
        climbTab.AddChild(climbAnimations);

        var radialTab = ScrollTabUx6C("Menu radial");
        var radialFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _radialEnabled = StudioUx2EnvBControls.Check("Menu radial activé");
        _gamepad = StudioUx2EnvBControls.Check("Manette");
        _keyboard = StudioUx2EnvBControls.Check("Navigation clavier");
        _listFallback = StudioUx2EnvBControls.Check("Liste de repli");
        _screenReader = StudioUx2EnvBControls.Check("Libellés lecteur d’écran");
        radialFlags.AddChild(_radialEnabled); radialFlags.AddChild(_gamepad); radialFlags.AddChild(_keyboard);
        radialFlags.AddChild(_listFallback); radialFlags.AddChild(_screenReader); radialTab.AddChild(radialFlags);
        var radialConfig = GridUx6C(3);
        _radialInput = StudioUx2EnvBControls.Text("action_wheel");
        _deadZone = NumberUx6C(0, .999, .01); _maximumDepth = NumberUx6C(1, 8, 1);
        radialConfig.AddChild(FieldUx6C("Action d’entrée", _radialInput));
        radialConfig.AddChild(FieldUx6C("Zone morte", _deadZone));
        radialConfig.AddChild(FieldUx6C("Profondeur maximale", _maximumDepth));
        radialTab.AddChild(radialConfig);
        var actionHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _radialActionChoice = StudioUx2EnvBControls.Options();
        _radialActionChoice.ItemSelected += SelectActionUx6C;
        actionHeader.AddChild(FieldUx6C("Action sélectionnée", _radialActionChoice, 330));
        var addAction = StudioStyle.Button("Ajouter", compact: true); addAction.Pressed += AddActionUx6C;
        var removeAction = StudioStyle.Button("Retirer", compact: true); removeAction.Pressed += RemoveActionUx6C;
        var upAction = StudioStyle.Button("Monter", compact: true); upAction.Pressed += () => MoveActionUx6C(-1);
        var downAction = StudioStyle.Button("Descendre", compact: true); downAction.Pressed += () => MoveActionUx6C(1);
        actionHeader.AddChild(addAction); actionHeader.AddChild(removeAction);
        actionHeader.AddChild(upAction); actionHeader.AddChild(downAction); radialTab.AddChild(actionHeader);
        var actionIdentity = GridUx6C(4);
        _actionKey = StudioUx2EnvBControls.Text("inspect");
        _actionLabel = StudioUx2EnvBControls.Text("Examiner");
        _actionAccessible = StudioUx2EnvBControls.Text("Examiner la cible");
        _actionKind = StudioUx2EnvBControls.Options("Verbe", "Capacité", "Objet", "Navigation", "Système");
        actionIdentity.AddChild(FieldUx6C("Clé", _actionKey));
        actionIdentity.AddChild(FieldUx6C("Libellé", _actionLabel));
        actionIdentity.AddChild(FieldUx6C("Libellé accessible", _actionAccessible));
        actionIdentity.AddChild(FieldUx6C("Type", _actionKind));
        radialTab.AddChild(actionIdentity);
        var actionBehavior = GridUx6C(3);
        _actionCommand = StudioUx2EnvBControls.Text("inspect");
        _actionParent = StudioUx2EnvBControls.Text(string.Empty);
        _actionOrder = NumberUx6C(0, 10000, 1);
        actionBehavior.AddChild(FieldUx6C("Commande", _actionCommand));
        actionBehavior.AddChild(FieldUx6C("Clé du parent", _actionParent));
        actionBehavior.AddChild(FieldUx6C("Ordre", _actionOrder));
        radialTab.AddChild(actionBehavior);
        _actionTarget = StudioUx2EnvBControls.Check("Cible requise");
        _actionTags = StudioUx2EnvBControls.Lines("Un tag de disponibilité par ligne", 70);
        radialTab.AddChild(_actionTarget); radialTab.AddChild(FieldUx6C("Tags", _actionTags));

        var timeTab = ScrollTabUx6C("Temps tactique");
        var timeFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pauseEnabled = StudioUx2EnvBControls.Check("Pause");
        _slowEnabled = StudioUx2EnvBControls.Check("Ralenti");
        _planningEnabled = StudioUx2EnvBControls.Check("Planification");
        _turnEnabled = StudioUx2EnvBControls.Check("Tour par tour");
        timeFlags.AddChild(_pauseEnabled); timeFlags.AddChild(_slowEnabled);
        timeFlags.AddChild(_planningEnabled); timeFlags.AddChild(_turnEnabled); timeTab.AddChild(timeFlags);
        var timeConfig = GridUx6C(4);
        _slowScale = NumberUx6C(.001, .999, .01); _plannedActions = NumberUx6C(1, 64, 1);
        _multiplayerPolicy = StudioUx2EnvBControls.Options("Désactivée", "Hôte seulement", "Vote", "Tout le monde");
        _voteFraction = NumberUx6C(.001, 1, .01);
        timeConfig.AddChild(FieldUx6C("Échelle du ralenti", _slowScale));
        timeConfig.AddChild(FieldUx6C("Actions planifiées max.", _plannedActions));
        timeConfig.AddChild(FieldUx6C("Politique multijoueur", _multiplayerPolicy));
        timeConfig.AddChild(FieldUx6C("Fraction de vote", _voteFraction));
        timeTab.AddChild(StudioUx2EnvBControls.Card("Ux6CTimeControl", "Décision temporelle",
            "L’aperçu évalue une demande locale; aucune horloge moteur ni session réseau n’est modifiée.", timeConfig));
        _pausePermissions = StudioUx2EnvBControls.Lines("Une permission humaine par ligne", 90);
        timeTab.AddChild(FieldUx6C("Permissions de pause", _pausePermissions));

        var combatTab = ScrollTabUx6C("Combat");
        var providerHeader = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _providerChoice = StudioUx2EnvBControls.Options(); _providerChoice.ItemSelected += SelectProviderUx6C;
        _defaultProvider = StudioUx2EnvBControls.Options();
        providerHeader.AddChild(FieldUx6C("Fournisseur sélectionné", _providerChoice, 300));
        providerHeader.AddChild(FieldUx6C("Mode par défaut", _defaultProvider, 300));
        var addProvider = StudioStyle.Button("Ajouter", compact: true); addProvider.Pressed += AddProviderUx6C;
        var removeProvider = StudioStyle.Button("Retirer", compact: true); removeProvider.Pressed += RemoveProviderUx6C;
        providerHeader.AddChild(addProvider); providerHeader.AddChild(removeProvider); combatTab.AddChild(providerHeader);
        var providerIdentity = GridUx6C(4);
        _providerKey = StudioUx2EnvBControls.Text("custom-mode");
        _providerName = StudioUx2EnvBControls.Text("Mode personnalisé");
        _providerKind = StudioUx2EnvBControls.Options("Temps réel", "Temps réel avec pause", "Tour par tour", "Personnalisé");
        _customProvider = StudioUx2EnvBControls.Text(string.Empty);
        providerIdentity.AddChild(FieldUx6C("Clé", _providerKey));
        providerIdentity.AddChild(FieldUx6C("Nom humain", _providerName));
        providerIdentity.AddChild(FieldUx6C("Type", _providerKind));
        providerIdentity.AddChild(FieldUx6C("Fournisseur personnalisé", _customProvider));
        combatTab.AddChild(providerIdentity);
        _eventContracts = StudioUx2EnvBControls.Lines("Un contrat d’événement par ligne", 110);
        _serverAuthority = StudioUx2EnvBControls.Check("Autorité serveur");
        combatTab.AddChild(FieldUx6C("Contrats d’événement partagés", _eventContracts));
        combatTab.AddChild(_serverAuthority);
        combatTab.AddChild(StudioStyle.Label(
            "Tous les fournisseurs doivent exposer le même ensemble d’événements; le dry-run canonique le vérifie.",
            10, StudioStyle.Muted, wrap: true));

        var reviewHost = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 132),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _diff = ReviewColumnUx6C(reviewHost, "Diff sémantique");
        _runtime = ReviewColumnUx6C(reviewHost, "Résolveurs déterministes");
        _diagnostics = ReviewColumnUx6C(reviewHost, "Diagnostics");
        root.AddChild(reviewHost);
        _summary = StudioStyle.Label("Aucun aperçu sec frais.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Aperçu sec", compact: true); _preview.Pressed += PreviewUx6C;
        _apply = StudioStyle.Button("Appliquer", compact: true); _apply.Pressed += ApplyUx6C;
        _undo = StudioStyle.Button("Annuler", compact: true); _undo.Pressed += UndoUx6C;
        _redo = StudioStyle.Button("Rétablir", compact: true); _redo.Pressed += RedoUx6C;
        _save = StudioStyle.Button("Enregistrer", compact: true); _save.Pressed += SaveUx6C;
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(118, 34);
            actions.AddChild(button);
        }
        actions.AddChild(StudioStyle.Label(
            "Aperçu en mémoire · application unique · historique · sauvegarde et réouverture canoniques.",
            10, StudioStyle.Muted, wrap: true));
        root.AddChild(actions);

        RenderPreviewUx6C(null);
        UpdateButtonsUx6C();
    }

    internal void Open() => OpenBoundUx6C(_sourceSession, false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6C(_sourceSession, false, cancelApproval: false);

    private void OpenBoundUx6C(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6CApproval();
        _choices = _session.GameplayCapabilityChoicesUx6C;
        ResetControlsUx6C();
        _profile.Clear();
        _profile.AddItem("Choisir explicitement un contexte…");
        foreach (var choice in _choices) _profile.AddItem(choice.Label);
        _profile.Select(0);
        _selection.Text = evidence
            ? "Copie temporaire isolée · la source reste strictement inchangée."
            : "Choisissez explicitement le module et une zone de contexte.";
        Visible = true;
        MoveToFront();
        UpdateBoundsUx6C();
        UpdateButtonsUx6C();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null)
        {
            ReleaseEvidenceUx6C();
        }
        else
        {
            _session.CancelUx6CApproval();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControlsUx6C();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidenceUx6C();
        Visible = false;
        ResetControlsUx6C();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        _sourceSnapshot = CaptureSourceUx6C();
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux6-c-{Guid.NewGuid():N}");
        _evidenceSession = new StudioSessionController();
        _evidenceReleased = false;
        var created = ProjectSession.Create(_evidenceRoot, _sourceSession.Project);
        if (!created.Succeeded || !_evidenceSession.Open(_evidenceRoot).Succeeded)
        {
            ReleaseEvidenceUx6C();
            return false;
        }
        OpenBoundUx6C(_evidenceSession, true);
        if (_choices.Count == 0)
        {
            CloseEvidence();
            return false;
        }
        _profile.Select(1);
        SelectProfileUx6C(1);
        ConfigureEvidenceUx6C();
        PreviewUx6C();
        var ready = PreviewReady && UsesIsolatedEvidence && HasExplicitSelection &&
                    ShowsFiveWorkflows && ShowsHumanSurface && SourceMatchesUx6C();
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceUx6C();
        var prepared = false;
        try
        {
            prepared = PrepareEvidence("ux6-c-gameplay-capabilities");
            if (!prepared || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady) return false;
            var before = _evidenceSession.Project!;
            var history = _evidenceSession.History!;
            var journalCount = history.Journal.Count;
            ApplyUx6C();
            var applied = history.Journal.Count == journalCount + 1 &&
                          history.Journal.Last().CommandName == "set_gameplay_control_profile" &&
                          !ReferenceEquals(before, _evidenceSession.Project);
            var after = _evidenceSession.Project!;
            UndoUx6C();
            var undone = ReferenceEquals(before, _evidenceSession.Project);
            RedoUx6C();
            var redone = ReferenceEquals(after, _evidenceSession.Project);
            var json = ProjectJsonSerializer.Serialize(after);
            SaveUx6C();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded && reopened.Project is not null &&
                            string.Equals(json, ProjectJsonSerializer.Serialize(reopened.Project),
                                StringComparison.Ordinal);
            reopened.Close();
            var sourceMatches = SourceMatchesUx6C(source);
            if (!(applied && undone && redone && persisted && sourceMatches))
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_C_UI_TRANSACTION_FAILED " +
                            $"applied={applied.ToString().ToLowerInvariant()} " +
                            $"undone={undone.ToString().ToLowerInvariant()} " +
                            $"redone={redone.ToString().ToLowerInvariant()} " +
                            $"persisted={persisted.ToString().ToLowerInvariant()} " +
                            $"source={sourceMatches.ToString().ToLowerInvariant()}");
            return applied && undone && redone && persisted && sourceMatches;
        }
        finally
        {
            if (prepared || _evidenceSession is not null || _evidenceRoot is not null) CloseEvidence();
            _sourceSession.RestoreGameplayCapabilityApprovalUx6C(source.Approval);
        }
    }

    private void SelectProfileUx6C(long selected)
    {
        if (_updating) return;
        _session.CancelUx6CApproval();
        _previewReady = false; _previewFingerprint = null; _previewResult = null;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ResetControlsUx6C();
            _selection.Text = "Sélection explicite requise.";
            return;
        }
        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateGameplayCapabilityDraftUx6C(choice.ContextAreaId, true);
        if (_draft is null)
        {
            ResetControlsUx6C();
            _selection.Text = "Le profil gameplay n’est pas disponible.";
            return;
        }
        PopulateUx6C(_draft);
        _selection.Text = $"Profil chargé · {choice.Label} · identités internes protégées.";
        SetStatusUx6C("Profil chargé. Modifiez puis lancez l’aperçu sec.", false);
    }

    private void PopulateUx6C(StudioUx6CDraft draft)
    {
        _updating = true;
        _swimEnabled.ButtonPressed = draft.Swimming.Enabled;
        _swimRequired.ButtonPressed = draft.Swimming.Required;
        _buoyancy.Value = DisplayUx6C(draft.Swimming.Buoyancy);
        _surfaceSnap.Value = DisplayUx6C(draft.Swimming.SurfaceSnapDistance);
        _surfaceSpeed.Value = DisplayUx6C(draft.Swimming.SurfaceSpeed);
        _diveSpeed.Value = DisplayUx6C(draft.Swimming.DiveSpeed);
        _currentInfluence.Value = DisplayUx6C(draft.Swimming.CurrentInfluence);
        _breath.Value = DisplayUx6C(draft.Swimming.BreathSeconds);
        _waterTags.Text = string.Join('\n', draft.Swimming.WaterVolumeTags);
        _surfaceAnimation.Text = draft.Swimming.SurfaceAnimation;
        _diveAnimation.Text = draft.Swimming.DiveAnimation;
        _idleAnimation.Text = draft.Swimming.IdleAnimation;

        _climbEnabled.ButtonPressed = draft.Climbing.Enabled;
        _climbRequired.ButtonPressed = draft.Climbing.Required;
        _climbSpeed.Value = DisplayUx6C(draft.Climbing.ClimbSpeed);
        _stamina.Value = DisplayUx6C(draft.Climbing.StaminaCapacity);
        _staminaDrain.Value = DisplayUx6C(draft.Climbing.StaminaDrainPerSecond);
        _ledgeReach.Value = DisplayUx6C(draft.Climbing.LedgeReach);
        _inverseKinematics.ButtonPressed = draft.Climbing.InverseKinematics;
        _authoredLinks.ButtonPressed = draft.Climbing.RequireAuthoredLinks;
        _safeDrop.ButtonPressed = draft.Climbing.PreventUnsafeDrop;
        _surfaceTags.Text = string.Join('\n', draft.Climbing.SurfaceTags);
        _climbAnimation.Text = draft.Climbing.ClimbAnimation;
        _ledgeAnimation.Text = draft.Climbing.LedgeAnimation;

        _radialEnabled.ButtonPressed = draft.RadialMenu.Enabled;
        _radialInput.Text = draft.RadialMenu.InputAction;
        _deadZone.Value = DisplayUx6C(draft.RadialMenu.DeadZone);
        _maximumDepth.Value = draft.RadialMenu.MaximumDepth;
        _gamepad.ButtonPressed = draft.RadialMenu.GamepadEnabled;
        _keyboard.ButtonPressed = draft.RadialMenu.KeyboardNavigation;
        _listFallback.ButtonPressed = draft.RadialMenu.ListFallback;
        _screenReader.ButtonPressed = draft.RadialMenu.ScreenReaderLabels;
        _actions = draft.RadialMenu.Actions.ToList();

        _pauseEnabled.ButtonPressed = draft.TimeControl.PauseEnabled;
        _slowEnabled.ButtonPressed = draft.TimeControl.SlowMotionEnabled;
        _slowScale.Value = DisplayUx6C(draft.TimeControl.SlowMotionScale);
        _planningEnabled.ButtonPressed = draft.TimeControl.PlanningEnabled;
        _plannedActions.Value = draft.TimeControl.MaximumPlannedActions;
        _turnEnabled.ButtonPressed = draft.TimeControl.TurnModeEnabled;
        _multiplayerPolicy.Select((int)draft.TimeControl.MultiplayerPolicy);
        _voteFraction.Value = DisplayUx6C(draft.TimeControl.RequiredVoteFraction);
        _pausePermissions.Text = string.Join('\n', draft.TimeControl.PausePermissions);

        _providers = draft.CombatModes.Providers.ToList();
        RebuildCollectionsUx6C(draft.CombatModes.DefaultProviderId);
        _updating = false;
        PopulateActionUx6C(); PopulateProviderUx6C();
        InvalidatePreviewUx6C();
    }

    private void RebuildCollectionsUx6C(string defaultProviderId)
    {
        _radialActionChoice.Clear();
        foreach (var item in _actions)
            _radialActionChoice.AddItem($"{HumanValueUx6C(item.Label)} · {HumanValueUx6C(item.Key)}");
        _actionIndex = _actions.Count == 0 ? -1 : Math.Clamp(_actionIndex, 0, _actions.Count - 1);
        if (_actionIndex >= 0) _radialActionChoice.Select(_actionIndex);

        _providerChoice.Clear(); _defaultProvider.Clear();
        foreach (var item in _providers)
        {
            var label = $"{HumanValueUx6C(item.DisplayName)} · {HumanValueUx6C(item.ProviderId)}";
            _providerChoice.AddItem(label); _defaultProvider.AddItem(label);
        }
        _providerIndex = _providers.Count == 0 ? -1 : Math.Clamp(_providerIndex, 0, _providers.Count - 1);
        if (_providerIndex >= 0) _providerChoice.Select(_providerIndex);
        var defaultIndex = _providers.FindIndex(item => item.ProviderId == defaultProviderId);
        if (defaultIndex < 0 && _providers.Count > 0) defaultIndex = 0;
        if (defaultIndex >= 0) _defaultProvider.Select(defaultIndex);
    }

    private StudioUx6CDraft? ReadDraftUx6C()
    {
        if (_draft is null) return null;
        FlushActionUx6C(); FlushProviderUx6C();
        var defaultProvider = _defaultProvider.Selected >= 0 && _defaultProvider.Selected < _providers.Count
            ? _providers[_defaultProvider.Selected].ProviderId : string.Empty;
        return _draft with
        {
            Swimming = new(_swimEnabled.ButtonPressed, _swimRequired.ButtonPressed,
                (float)_buoyancy.Value, (float)_surfaceSnap.Value, (float)_surfaceSpeed.Value,
                (float)_diveSpeed.Value, (float)_currentInfluence.Value, (float)_breath.Value,
                LinesUx6C(_waterTags.Text), _surfaceAnimation.Text, _diveAnimation.Text, _idleAnimation.Text),
            Climbing = new(_climbEnabled.ButtonPressed, _climbRequired.ButtonPressed,
                (float)_climbSpeed.Value, (float)_stamina.Value, (float)_staminaDrain.Value,
                (float)_ledgeReach.Value, _inverseKinematics.ButtonPressed, _authoredLinks.ButtonPressed,
                _safeDrop.ButtonPressed, LinesUx6C(_surfaceTags.Text), _climbAnimation.Text,
                _ledgeAnimation.Text),
            RadialMenu = new(_radialEnabled.ButtonPressed, _radialInput.Text,
                (float)_deadZone.Value, (int)_maximumDepth.Value, _gamepad.ButtonPressed,
                _keyboard.ButtonPressed, _listFallback.ButtonPressed, _screenReader.ButtonPressed,
                _actions.ToArray()),
            TimeControl = new(_pauseEnabled.ButtonPressed, _slowEnabled.ButtonPressed,
                (float)_slowScale.Value, _planningEnabled.ButtonPressed, (int)_plannedActions.Value,
                _turnEnabled.ButtonPressed, (MultiplayerPausePolicy)_multiplayerPolicy.Selected,
                (float)_voteFraction.Value, LinesUx6C(_pausePermissions.Text)),
            CombatModes = new(defaultProvider, _providers.ToArray()),
        };
    }

    private void PreviewUx6C()
    {
        var draft = ReadDraftUx6C();
        if (draft is null) { SetStatusUx6C("Choisissez explicitement un profil.", true); return; }
        _draft = draft;
        _previewResult = _session.PreviewGameplayCapabilitiesUx6C(draft);
        _previewReady = _previewResult.Succeeded;
        _previewFingerprint = _session.GameplayCapabilityDraftFingerprintUx6C(draft);
        RenderPreviewUx6C(_previewResult);
        SetStatusUx6C(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtonsUx6C();
    }

    private void ApplyUx6C()
    {
        var current = ReadDraftUx6C();
        if (current is null || !_previewReady ||
            !string.Equals(_previewFingerprint,
                _session.GameplayCapabilityDraftFingerprintUx6C(current), StringComparison.Ordinal))
        {
            _session.CancelUx6CApproval();
            _previewReady = false;
            SetStatusUx6C("Le brouillon a changé. Relancez l’aperçu sec.", true);
            UpdateButtonsUx6C();
            return;
        }
        var result = _session.ApplyApprovedGameplayCapabilitiesUx6C();
        SetStatusUx6C(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            GameplayCapabilitiesChanged?.Invoke();
            ReloadSelectedUx6C();
        }
        UpdateButtonsUx6C();
    }

    private void UndoUx6C()
    {
        var result = _session.Undo();
        SetStatusUx6C(result.Message, !result.Succeeded);
        if (result.Succeeded) { GameplayCapabilitiesChanged?.Invoke(); ReloadSelectedUx6C(); }
    }

    private void RedoUx6C()
    {
        var result = _session.Redo();
        SetStatusUx6C(result.Message, !result.Succeeded);
        if (result.Succeeded) { GameplayCapabilitiesChanged?.Invoke(); ReloadSelectedUx6C(); }
    }

    private void SaveUx6C()
    {
        var result = _session.Save();
        SetStatusUx6C(result.Message, !result.Succeeded);
        UpdateButtonsUx6C();
    }

    private void ReloadSelectedUx6C()
    {
        var selected = _profile.Selected;
        _session.CancelUx6CApproval();
        _previewReady = false; _previewFingerprint = null;
        if (selected > 0) SelectProfileUx6C(selected);
    }

    private void ConfigureEvidenceUx6C()
    {
        _surfaceSpeed.Value = Math.Min(199, _surfaceSpeed.Value + .125);
        _climbSpeed.Value = Math.Min(199, _climbSpeed.Value + .125);
        _plannedActions.Value = Math.Min(64, _plannedActions.Value + 1);
        if (_actionIndex >= 0)
        {
            _actionLabel.Text += " · Studio";
            _actionAccessible.Text += " · Studio";
        }
        if (_providerIndex >= 0) _providerName.Text += " · Studio";
    }

    private void SelectActionUx6C(long selected)
    {
        if (_updating) return;
        FlushActionUx6C(); _actionIndex = (int)selected; PopulateActionUx6C(); InvalidatePreviewUx6C();
    }

    private void SelectProviderUx6C(long selected)
    {
        if (_updating) return;
        FlushProviderUx6C(); _providerIndex = (int)selected; PopulateProviderUx6C(); InvalidatePreviewUx6C();
    }

    private void PopulateActionUx6C()
    {
        if (_actionIndex < 0 || _actionIndex >= _actions.Count) return;
        _updating = true;
        var item = _actions[_actionIndex];
        _actionKey.Text = item.Key; _actionLabel.Text = item.Label;
        _actionAccessible.Text = item.AccessibleLabel; _actionKind.Select((int)item.Kind);
        _actionCommand.Text = item.Command; _actionParent.Text = item.ParentKey;
        _actionOrder.Value = item.Order; _actionTarget.ButtonPressed = item.RequiresTarget;
        _actionTags.Text = string.Join('\n', item.Tags);
        _updating = false;
    }

    private void FlushActionUx6C()
    {
        if (_updating || _actionIndex < 0 || _actionIndex >= _actions.Count) return;
        var oldKey = _actions[_actionIndex].Key;
        var item = _actions[_actionIndex] with
        {
            Key = _actionKey.Text,
            Label = _actionLabel.Text,
            AccessibleLabel = _actionAccessible.Text,
            Kind = (RadialActionKind)_actionKind.Selected,
            Command = _actionCommand.Text,
            ParentKey = _actionParent.Text,
            Order = (int)_actionOrder.Value,
            RequiresTarget = _actionTarget.ButtonPressed,
            Tags = LinesUx6C(_actionTags.Text),
        };
        _actions[_actionIndex] = item;
        if (!string.Equals(oldKey, item.Key, StringComparison.Ordinal))
            _actions = _actions.Select(action => action.ParentKey == oldKey
                ? action with { ParentKey = item.Key } : action).ToList();
    }

    private void PopulateProviderUx6C()
    {
        if (_providerIndex < 0 || _providerIndex >= _providers.Count) return;
        _updating = true;
        var item = _providers[_providerIndex];
        _providerKey.Text = item.ProviderId; _providerName.Text = item.DisplayName;
        _providerKind.Select((int)item.Kind); _customProvider.Text = item.CustomProvider;
        _eventContracts.Text = string.Join('\n', item.EventContracts);
        _serverAuthority.ButtonPressed = item.ServerAuthoritative;
        _updating = false;
    }

    private void FlushProviderUx6C()
    {
        if (_updating || _providerIndex < 0 || _providerIndex >= _providers.Count) return;
        var oldKey = _providers[_providerIndex].ProviderId;
        var item = _providers[_providerIndex] with
        {
            ProviderId = _providerKey.Text,
            DisplayName = _providerName.Text,
            Kind = (CombatModeKind)_providerKind.Selected,
            CustomProvider = _customProvider.Text,
            EventContracts = LinesUx6C(_eventContracts.Text),
            ServerAuthoritative = _serverAuthority.ButtonPressed,
        };
        _providers[_providerIndex] = item;
        var defaultKey = _defaultProvider.Selected >= 0 && _defaultProvider.Selected < _providers.Count
            ? _providers[_defaultProvider.Selected].ProviderId : oldKey;
        if (defaultKey == oldKey) defaultKey = item.ProviderId;
        RebuildCollectionsUx6C(defaultKey);
    }

    private void AddActionUx6C()
    {
        FlushActionUx6C();
        var n = _actions.Count + 1;
        _actions.Add(new(Guid.NewGuid(), $"action-{n}", $"Nouvelle action {n}",
            $"Nouvelle action {n}", RadialActionKind.Verb, $"action-{n}", string.Empty,
            _actions.Count, false, ["world"]));
        _actionIndex = _actions.Count - 1;
        RebuildCollectionsUx6C(CurrentDefaultProviderUx6C());
        _radialActionChoice.Select(_actionIndex); PopulateActionUx6C(); InvalidatePreviewUx6C();
    }

    private void RemoveActionUx6C()
    {
        if (_actionIndex < 0) return;
        var key = _actions[_actionIndex].Key;
        _actions.RemoveAt(_actionIndex);
        _actions = _actions.Select(item => item.ParentKey == key ? item with { ParentKey = string.Empty } : item)
            .Select((item, index) => item with { Order = index }).ToList();
        _actionIndex = _actions.Count == 0 ? -1 : Math.Min(_actionIndex, _actions.Count - 1);
        RebuildCollectionsUx6C(CurrentDefaultProviderUx6C());
        PopulateActionUx6C(); InvalidatePreviewUx6C();
    }

    private void MoveActionUx6C(int delta)
    {
        FlushActionUx6C();
        var target = _actionIndex + delta;
        if (_actionIndex < 0 || target < 0 || target >= _actions.Count) return;
        (_actions[_actionIndex], _actions[target]) = (_actions[target], _actions[_actionIndex]);
        _actions = _actions.Select((item, index) => item with { Order = index }).ToList();
        _actionIndex = target;
        RebuildCollectionsUx6C(CurrentDefaultProviderUx6C());
        _radialActionChoice.Select(_actionIndex); PopulateActionUx6C(); InvalidatePreviewUx6C();
    }

    private void AddProviderUx6C()
    {
        FlushProviderUx6C();
        var n = _providers.Count + 1;
        var events = _providers.FirstOrDefault()?.EventContracts ??
                     ["combat.started", "combat.action.requested", "combat.action.resolved", "combat.ended"];
        _providers.Add(new(Guid.NewGuid(), $"mode-{n}", $"Mode {n}", CombatModeKind.Custom,
            $"provider.mode-{n}", events.ToArray(), true));
        _providerIndex = _providers.Count - 1;
        RebuildCollectionsUx6C(CurrentDefaultProviderUx6C());
        _providerChoice.Select(_providerIndex); PopulateProviderUx6C(); InvalidatePreviewUx6C();
    }

    private void RemoveProviderUx6C()
    {
        if (_providerIndex < 0 || _providers.Count <= 1)
        {
            SetStatusUx6C("Au moins un fournisseur de combat doit rester disponible.", true);
            return;
        }
        var defaultKey = CurrentDefaultProviderUx6C();
        var removedKey = _providers[_providerIndex].ProviderId;
        _providers.RemoveAt(_providerIndex);
        if (defaultKey == removedKey) defaultKey = _providers[0].ProviderId;
        _providerIndex = Math.Min(_providerIndex, _providers.Count - 1);
        RebuildCollectionsUx6C(defaultKey);
        PopulateProviderUx6C(); InvalidatePreviewUx6C();
    }

    private string CurrentDefaultProviderUx6C() =>
        _defaultProvider.Selected >= 0 && _defaultProvider.Selected < _providers.Count
            ? _providers[_defaultProvider.Selected].ProviderId
            : _providers.FirstOrDefault()?.ProviderId ?? string.Empty;

    private void InvalidatePreviewUx6C()
    {
        if (_updating) return;
        _previewReady = false; _previewFingerprint = null;
        _session.CancelUx6CApproval(); UpdateButtonsUx6C();
    }

    private void ResetControlsUx6C()
    {
        _updating = true;
        _draft = null; _actions = []; _providers = [];
        _radialActionChoice.Clear(); _providerChoice.Clear(); _defaultProvider.Clear();
        _actionIndex = _providerIndex = -1;
        _previewReady = false; _previewFingerprint = null; _previewResult = null;
        RenderPreviewUx6C(null); _updating = false; UpdateButtonsUx6C();
    }

    private void UpdateButtonsUx6C()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !PreviewReady || _previewResult is not { CanApply: true };
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
        _save.Disabled = !_session.IsDirty;
    }

    private void RenderPreviewUx6C(StudioUx6CPreview? preview)
    {
        ClearChildrenUx6C(_diff); ClearChildrenUx6C(_runtime); ClearChildrenUx6C(_diagnostics);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Le diff sémantique apparaîtra après l’aperçu sec.", 10,
                StudioStyle.Muted, wrap: true));
            _runtime.AddChild(StudioStyle.Label("Les cinq résolutions pures apparaîtront ici.", 10,
                StudioStyle.Muted, wrap: true));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic frais.", 10, StudioStyle.Muted));
            _summary.Text = "Aucun aperçu sec frais.";
            return;
        }
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement sémantique.", 10, StudioStyle.Muted));
        foreach (var item in preview.Diff)
            _diff.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        foreach (var item in preview.RuntimePreview)
            _runtime.AddChild(StudioStyle.Label("• " + item, 10, StudioStyle.Text, wrap: true));
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Conforme · dry-run canonique", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10,
                item.Blocking ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
        _summary.Text = preview.Message + "\n" + preview.PatchSummary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
    }

    private StudioUx6CEvidenceSourceSnapshot CaptureSourceUx6C()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureGameplayCapabilityApprovalUx6C());
    }

    private bool SourceMatchesUx6C(StudioUx6CEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureGameplayCapabilityApprovalUx6C() == snapshot.Approval;
    }

    private bool ReleaseEvidenceUx6C()
    {
        var isolated = _evidenceSession; var root = _evidenceRoot; var sessionReleased = true;
        try { isolated?.Close(); }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_C_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = StudioSessionController.DeleteUx6CTemporaryWorkspace(root);
        _evidenceSession = null; _evidenceRoot = rootReleased ? null : root; _sourceSnapshot = null;
        _session = _sourceSession; _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void UpdateBoundsUx6C()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.68f : viewport.X >= 2_300 ? 1.22f : 1f;
        var width = Math.Clamp((viewport.X - 48f) / scale, 1_180f, 1_650f);
        var height = Math.Clamp((viewport.Y - 38f) / scale, 760f, 1_060f);
        _dialog.Scale = Vector2.One * scale; _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width * scale) / 2f,
            Math.Max(16f, (viewport.Y - height * scale) * .46f));
    }

    private VBoxContainer ScrollTabUx6C(string title)
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
        scroll.AddChild(content); _tabs.AddChild(scroll);
        _tabs.SetTabTitle(_tabs.GetTabCount() - 1, title);
        return content;
    }

    private static GridContainer GridUx6C(int columns)
    {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 9);
        grid.AddThemeConstantOverride("v_separation", 7);
        return grid;
    }

    private static SpinBox NumberUx6C(double min, double max, double step, bool allowGreater = false,
        bool allowLesser = false) => StudioUx2EnvBControls.Number(min, max, step, allowGreater, allowLesser);

    private static VBoxContainer FieldUx6C(string label, Control control, int minimum = 118) =>
        StudioUx2EnvBControls.Field(label, control, minimum);

    private static VBoxContainer ReviewColumnUx6C(HBoxContainer host, string title)
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

    private static IReadOnlyList<string> LinesUx6C(string text) => (text ?? string.Empty)
        .Replace("\r", string.Empty).Split('\n').Select(item => item.Trim())
        .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    private static string HumanValueUx6C(string value) => string.IsNullOrWhiteSpace(value) ? "Sans nom" :
        string.Join(' ', value.Trim().Replace('_', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static double DisplayUx6C(float value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static void ClearChildrenUx6C(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private void SetStatusUx6C(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static IEnumerable<string> DescendantTextUx6C(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var nested in DescendantTextUx6C(child)) yield return nested;
        }
    }

    private static bool ContainsUuidUx6C(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b");

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6C();
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
